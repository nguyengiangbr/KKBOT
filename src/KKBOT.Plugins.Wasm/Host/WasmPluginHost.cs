// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wasmtime;
using KKBOT.Plugins.Abstractions;
using KKBOT.Device.Abstractions;
using KKBOT.Perception.Abstractions;
using KKBOT.AI.Abstractions;
using KKBOT.Observability;
using KKBOT.Observability.Tracing;

namespace KKBOT.Plugins.Wasm;

/// <summary>
/// WASM plugin capabilities - deny by default.
/// </summary>
[Flags]
public enum PluginCapability : ulong
{
    None = 0,
    
    // Device capabilities
    DeviceRead = 1UL << 0,      // Capture screen, get device info
    DeviceWrite = 1UL << 1,     // Send input commands
    DeviceControl = 1UL << 2,   // Launch/stop apps, install packages
    
    // Perception capabilities
    PerceptionBasic = 1UL << 3, // Template matching, color analysis
    PerceptionAdvanced = 1UL << 4, // Custom detectors, OCR
    PerceptionTrain = 1UL << 5, // Train detectors
    
    // AI capabilities
    AiInference = 1UL << 6,     // Text generation
    AiVision = 1UL << 7,        // Vision models
    AiTrain = 1UL << 8,         // Model training
    
    // Storage capabilities
    StorageRead = 1UL << 9,     // Read plugin assets, config
    StorageWrite = 1UL << 10,   // Write logs, cache, temp files
    StoragePersistent = 1UL << 11, // Persistent storage across runs
    
    // Network capabilities
    NetworkHttp = 1UL << 12,    // Outbound HTTP requests
    NetworkWebsocket = 1UL << 13, // WebSocket connections
    NetworkLocal = 1UL << 14,   // Localhost only
    
    // System capabilities
    SystemTime = 1UL << 15,     // Access to time/date
    SystemRandom = 1UL << 16,   // Cryptographic random
    SystemLogging = 1UL << 17,  // Structured logging
    
    // Plugin capabilities
    PluginSpawn = 1UL << 18,    // Spawn child plugins
    PluginCommunicate = 1UL << 19, // Inter-plugin communication
    
    // Preset combinations
    Standard = DeviceRead | DeviceWrite | PerceptionBasic | AiInference | StorageRead | SystemTime | SystemLogging,
    Full = ~0UL
}

/// <summary>
/// Resource limits for WASM plugins.
/// </summary>
public sealed record PluginResourceLimits
{
    public ulong MaxMemoryBytes { get; init; } = 64 * 1024 * 1024; // 64 MB
    public ulong MaxFuel { get; init; } = 10_000_000; // ~10M instructions
    public TimeSpan MaxExecutionTime { get; init; } = TimeSpan.FromSeconds(30);
    public int MaxStackSize { get; init; } = 512 * 1024; // 512 KB
    public int MaxTableEntries { get; init; } = 1000;
    public int MaxConcurrentCalls { get; init; } = 10;
}

/// <summary>
/// WASM plugin manifest with capabilities and limits.
/// </summary>
public sealed record WasmPluginManifest
{
    public string PluginId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string EntryPoint { get; init; } = "plugin_init"; // WASM export name
    public PluginCapability RequiredCapabilities { get; init; } = PluginCapability.Standard;
    public PluginResourceLimits ResourceLimits { get; init; } = new();
    public Dictionary<string, string> Metadata { get; init; } = new();
    public string[] RequiredHostFunctions { get; init; } = Array.Empty<string>();
    public string Sha256Hash { get; init; } = string.Empty; // For integrity verification
}

/// <summary>
/// Host function registry for WASM imports.
/// </summary>
public sealed class HostFunctionRegistry
{
    private readonly Dictionary<string, HostFunction> _functions = new();
    private readonly ILogger<HostFunctionRegistry> _logger;

    public HostFunctionRegistry(ILogger<HostFunctionRegistry> logger)
    {
        _logger = logger;
    }

    public void Register(HostFunction function)
    {
        _functions[function.Name] = function;
        _logger.LogDebug("Registered host function: {Name} ({Namespace})", function.Name, function.Namespace);
    }

    public void RegisterMany(IEnumerable<HostFunction> functions)
    {
        foreach (var f in functions) Register(f);
    }

    public HostFunction? Get(string namespace_, string name)
    {
        var key = $"{namespace_}::{name}";
        return _functions.TryGetValue(key, out var fn) ? fn : null;
    }

    public IReadOnlyList<HostFunction> GetAll() => _functions.Values.ToList().AsReadOnly();
}

/// <summary>
/// Definition of a host function available to WASM plugins.
/// </summary>
public sealed record HostFunction
{
    public string Namespace { get; init; } = "kkbot";
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public ValType[] ParameterTypes { get; init; } = Array.Empty<ValType>();
    public ValType[] ReturnTypes { get; init; } = Array.Empty<ValType>();
    public PluginCapability RequiredCapability { get; init; } = PluginCapability.None;
    public Func<Caller, Val[], ValueTask<Val[]>> Implementation { get; init; } = null!;
}

/// <summary>
/// WASM plugin host - manages Wasmtime engine and plugin instances.
/// </summary>
public sealed class WasmPluginHost : IAsyncDisposable
{
    private readonly ILogger<WasmPluginHost> _logger;
    private readonly WasmPluginHostOptions _options;
    private readonly Engine _engine;
    private readonly HostFunctionRegistry _hostFunctions;
    private readonly ConcurrentDictionary<string, WasmPluginInstance> _instances = new();
    private readonly IMetricsRegistry _metrics;
    private bool _disposed;

    public WasmPluginHost(
        ILogger<WasmPluginHost> logger,
        IOptions<WasmPluginHostOptions> options,
        IMetricsRegistry metrics,
        HostFunctionRegistry hostFunctions)
    {
        _logger = logger;
        _options = options.Value;
        _metrics = metrics;
        _hostFunctions = hostFunctions;

        // Configure Wasmtime engine
        var config = new Config();
        config.ConsumeFuel = true; // Enable fuel metering
        config.EpochInterruption = true; // Enable epoch-based interruption
        config.WithHostRegistration<HostFunctionRegistry>("kkbot", registry => registry);
        
        if (_options.EnableSimd) config.Simd = true;
        if (_options.EnableThreads) config.Threads = true;
        if (_options.EnableRelaxedSimd) config.RelaxedSimd = true;
        if (_options.EnableMemory64) config.Memory64 = true;

        // Set resource limits
        config.MaxWasmStack = _options.DefaultLimits.MaxStackSize;
        
        _engine = new Engine(config);
        
        _logger.LogInformation("WASM Plugin Host initialized with engine version {Version}", _engine.Version);
    }

    /// <summary>
    /// Loads and instantiates a WASM plugin from a .wasm file.
    /// </summary>
    public async Task<WasmPluginInstance> LoadPluginAsync(
        string pluginPath, 
        WasmPluginManifest manifest,
        CancellationToken cancellationToken = default)
    {
        using var activity = KkbotActivitySources.Plugins.StartActivity("wasm.load_plugin", ActivityKind.Internal);
        activity?.SetTag("plugin.id", manifest.PluginId);
        
        var startTime = Stopwatch.GetTimestamp();
        
        try
        {
            _logger.LogInformation("Loading WASM plugin: {PluginId} v{Version} from {Path}", 
                manifest.PluginId, manifest.Version, pluginPath);

            // Verify plugin integrity
            if (!string.IsNullOrEmpty(manifest.Sha256Hash))
            {
                await VerifyPluginHashAsync(pluginPath, manifest.Sha256Hash, cancellationToken);
            }

            // Load module
            var moduleBytes = await File.ReadAllBytesAsync(pluginPath, cancellationToken);
            var module = new Module(_engine, moduleBytes);

            // Create linker with host functions
            var linker = new Linker(_engine);
            RegisterHostFunctions(linker);

            // Create store with resource limits
            var store = new Store(_engine);
            store.Fuel = manifest.ResourceLimits.MaxFuel;
            
            // Set epoch deadline for time-based interruption
            _ = Task.Run(async () =>
            {
                await Task.Delay(manifest.ResourceLimits.MaxExecutionTime, cancellationToken);
                if (!cancellationToken.IsCancellationRequested)
                {
                    store.IncrementEpoch(); // Force interruption
                }
            }, cancellationToken);

            // Instantiate plugin
            var instance = linker.Instantiate(store, module);
            
            // Get exports
            var exports = instance.GetExports(store).ToDictionary(e => e.Name, e => e);
            
            // Verify required exports
            var initFunc = exports.GetValueOrDefault(manifest.EntryPoint) as Func;
            if (initFunc == null)
            {
                throw new InvalidOperationException($"Plugin missing entry point: {manifest.EntryPoint}");
            }

            // Create plugin instance
            var pluginInstance = new WasmPluginInstance(
                manifest.PluginId,
                manifest,
                store,
                instance,
                initFunc,
                exports,
                _hostFunctions,
                _logger,
                _metrics);

            // Initialize plugin
            await pluginInstance.InitializeAsync(cancellationToken);

            _instances[manifest.PluginId] = pluginInstance;
            
            var elapsedMs = (Stopwatch.GetTimestamp() - startTime) * 1000.0 / Stopwatch.Frequency;
            _metrics.RecordPluginLoadDuration(elapsedMs);
            
            activity?.SetStatus(ActivityStatusCode.Ok);
            _logger.LogInformation("Loaded WASM plugin {PluginId} in {ElapsedMs:F2}ms", manifest.PluginId, elapsedMs);
            
            return pluginInstance;
        }
        catch (Exception ex)
        {
            _metrics.IncrementPluginLoadsFailed();
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Failed to load WASM plugin: {PluginId}", manifest.PluginId);
            throw;
        }
    }

    /// <summary>
    /// Unloads a plugin and releases resources.
    /// </summary>
    public async Task UnloadPluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        if (_instances.TryRemove(pluginId, out var instance))
        {
            await instance.DisposeAsync();
            _logger.LogInformation("Unloaded WASM plugin: {PluginId}", pluginId);
        }
    }

    /// <summary>
    /// Gets a loaded plugin instance.
    /// </summary>
    public WasmPluginInstance? GetPlugin(string pluginId)
    {
        _instances.TryGetValue(pluginId, out var instance);
        return instance;
    }

    /// <summary>
    /// Gets all loaded plugin instances.
    /// </summary>
    public IReadOnlyList<WasmPluginInstance> GetAllPlugins()
    {
        return _instances.Values.ToList().AsReadOnly();
    }

    private void RegisterHostFunctions(Linker linker)
    {
        foreach (var fn in _hostFunctions.GetAll())
        {
            try
            {
                var funcType = new FuncType(fn.ParameterTypes, fn.ReturnTypes);
                var hostFunc = HostFunction.Create(fn.Namespace, fn.Name, funcType, fn.Implementation);
                linker.Define(fn.Namespace, fn.Name, hostFunc);
                _logger.LogDebug("Defined host function: {Namespace}::{Name}", fn.Namespace, fn.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register host function: {Namespace}::{Name}", fn.Namespace, fn.Name);
            }
        }
    }

    private async Task VerifyPluginHashAsync(string path, string expectedHash, CancellationToken ct)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        await using var stream = File.OpenRead(path);
        var hash = await sha256.ComputeHashAsync(stream, ct);
        var actualHash = Convert.ToHexString(hash).ToLowerInvariant();
        
        if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Plugin hash mismatch. Expected: {expectedHash}, Got: {actualHash}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        var unloadTasks = _instances.Values.Select(i => i.DisposeAsync().AsTask());
        await Task.WhenAll(unloadTasks);
        _instances.Clear();

        _engine.Dispose();
        _logger.LogInformation("WASM Plugin Host disposed");
    }
}

/// <summary>
/// Options for WASM plugin host.
/// </summary>
public sealed class WasmPluginHostOptions
{
    public PluginResourceLimits DefaultLimits { get; set; } = new();
    public bool EnableSimd { get; set; } = true;
    public bool EnableThreads { get; set; } = false; // Requires careful handling
    public bool EnableRelaxedSimd { get; set; } = false;
    public bool EnableMemory64 { get; set; } = false;
    public string PluginDirectory { get; set; } = "plugins/wasm";
    public bool AutoLoadOnStartup { get; set; } = false;
}