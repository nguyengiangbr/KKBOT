// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Wasmtime;
using KKBOT.Plugins.Abstractions;
using KKBOT.Device.Abstractions;
using KKBOT.Perception.Abstractions;
using KKBOT.AI.Abstractions;
using KKBOT.Observability;
using KKBOT.Observability.Tracing;

namespace KKBOT.Plugins.Wasm;

/// <summary>
/// WASM plugin instance with capability enforcement and resource monitoring.
/// </summary>
public sealed class WasmPluginInstance : IAsyncDisposable
{
    private readonly string _pluginId;
    private readonly WasmPluginManifest _manifest;
    private readonly Store _store;
    private readonly Instance _instance;
    private readonly Func _initFunction;
    private readonly IReadOnlyDictionary<string, Export> _exports;
    private readonly HostFunctionRegistry _hostFunctions;
    private readonly ILogger<WasmPluginInstance> _logger;
    private readonly IMetricsRegistry _metrics;
    private readonly ConcurrentDictionary<string, object> _pluginState = new();
    private bool _initialized;
    private bool _disposed;
    private ulong _fuelConsumed;

    public string PluginId => _pluginId;
    public WasmPluginManifest Manifest => _manifest;
    public PluginCapability Capabilities => _manifest.RequiredCapabilities;
    public PluginResourceLimits ResourceLimits => _manifest.ResourceLimits;
    public bool IsInitialized => _initialized;
    public ulong FuelConsumed => _fuelConsumed;
    public ulong FuelRemaining => _store.Fuel ?? 0;

    public WasmPluginInstance(
        string pluginId,
        WasmPluginManifest manifest,
        Store store,
        Instance instance,
        Func initFunction,
        IReadOnlyDictionary<string, Export> exports,
        HostFunctionRegistry hostFunctions,
        ILogger<WasmPluginInstance> logger,
        IMetricsRegistry metrics)
    {
        _pluginId = pluginId;
        _manifest = manifest;
        _store = store;
        _instance = instance;
        _initFunction = initFunction;
        _exports = exports;
        _hostFunctions = hostFunctions;
        _logger = logger;
        _metrics = metrics;
    }

    /// <summary>
    /// Initializes the plugin by calling its entry point.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;

        try
        {
            _logger.LogDebug("Initializing plugin {PluginId}", _pluginId);
            
            // Call plugin entry point with manifest JSON
            var manifestJson = JsonSerializer.Serialize(_manifest);
            var manifestPtr = WriteStringToMemory(_store, manifestJson);
            
            var result = await _initFunction.InvokeAsync(_store, manifestPtr, manifestJson.Length);
            
            // Read initialization result
            var resultPtr = (int)result[0].GetI32();
            var resultLen = (int)result[1].GetI32();
            var resultJson = ReadStringFromMemory(_store, resultPtr, resultLen);
            
            var initResult = JsonSerializer.Deserialize<PluginInitResult>(resultJson);
            
            if (!initResult.Success)
            {
                throw new InvalidOperationException($"Plugin initialization failed: {initResult.Error}");
            }

            _initialized = true;
            _logger.LogInformation("Plugin {PluginId} initialized successfully", _pluginId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Plugin {PluginId} initialization failed", _pluginId);
            throw;
        }
    }

    /// <summary>
    /// Calls a plugin export function.
    /// </summary>
    public async Task<PluginCallResult> CallAsync(
        string functionName, 
        string inputJson, 
        CancellationToken cancellationToken = default)
    {
        if (!_initialized)
            throw new InvalidOperationException("Plugin not initialized");

        if (!_exports.TryGetValue(functionName, out var export) || export is not Func func)
        {
            return PluginCallResult.Failed($"Function '{functionName}' not found or not a function");
        }

        // Check capability
        var requiredCap = GetRequiredCapability(functionName);
        if (requiredCap != PluginCapability.None && !_manifest.RequiredCapabilities.HasFlag(requiredCap))
        {
            return PluginCallResult.Failed($"Insufficient capability for '{functionName}': requires {requiredCap}");
        }

        using var activity = KkbotActivitySources.Plugins.StartActivity($"wasm.call.{functionName}", ActivityKind.Internal);
        activity?.SetTag("plugin.id", _pluginId);
        activity?.SetTag("plugin.function", functionName);

        var stopwatch = Stopwatch.StartNew();
        var fuelBefore = _store.Fuel ?? 0;

        try
        {
            // Write input to plugin memory
            var inputPtr = WriteStringToMemory(_store, inputJson);
            
            // Call function
            var result = await func.InvokeAsync(_store, inputPtr, inputJson.Length);
            
            // Read output
            var outputPtr = (int)result[0].GetI32();
            var outputLen = (int)result[1].GetI32();
            var outputJson = ReadStringFromMemory(_store, outputPtr, outputLen);

            var fuelAfter = _store.Fuel ?? 0;
            _fuelConsumed += fuelBefore - fuelAfter;
            stopwatch.Stop();

            _metrics.RecordPluginExecutionLatency(stopwatch.Elapsed.TotalMilliseconds);

            var callResult = JsonSerializer.Deserialize<PluginCallResult>(outputJson);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return callResult;
        }
        catch (WasmtimeException ex) when (ex.Message.Contains("fuel"))
        {
            stopwatch.Stop();
            _metrics.IncrementPluginExecutionsFailed();
            activity?.SetStatus(ActivityStatusCode.Error, "Out of fuel");
            return PluginCallResult.Failed("Plugin exceeded fuel limit (CPU quota)");
        }
        catch (WasmtimeException ex) when (ex.Message.Contains("epoch"))
        {
            stopwatch.Stop();
            _metrics.IncrementPluginExecutionsFailed();
            activity?.SetStatus(ActivityStatusCode.Error, "Execution timeout");
            return PluginCallResult.Failed("Plugin exceeded time limit");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metrics.IncrementPluginExecutionsFailed();
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Plugin {PluginId} call to {Function} failed", _pluginId, functionName);
            return PluginCallResult.Failed($"Execution error: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets a plugin export (memory, global, etc.).
    /// </summary>
    public Export? GetExport(string name)
    {
        _exports.TryGetValue(name, out var export);
        return export;
    }

    /// <summary>
    /// Reads plugin memory.
    /// </summary>
    public byte[] ReadMemory(int offset, int length)
    {
        var memory = _exports.GetValueOrDefault("memory") as Memory;
        if (memory == null) throw new InvalidOperationException("Plugin has no memory export");
        
        var span = memory.GetData(_store);
        if (offset + length > span.Length) throw new ArgumentOutOfRangeException();
        
        return span.Slice(offset, length).ToArray();
    }

    /// <summary>
    /// Writes to plugin memory.
    /// </summary>
    public int WriteMemory(byte[] data)
    {
        var memory = _exports.GetValueOrDefault("memory") as Memory;
        if (memory == null) throw new InvalidOperationException("Plugin has no memory export");
        
        var span = memory.GetData(_store);
        var offset = FindFreeMemoryRegion(span, data.Length);
        
        if (offset < 0) throw new OutOfMemoryException("Plugin memory full");
        
        data.CopyTo(span.Slice(offset, data.Length));
        return offset;
    }

    /// <summary>
    /// Sets plugin state (persisted across calls).
    /// </summary>
    public void SetState(string key, object value)
    {
        _pluginState[key] = value;
    }

    /// <summary>
    /// Gets plugin state.
    /// </summary>
    public T? GetState<T>(string key)
    {
        if (_pluginState.TryGetValue(key, out var value) && value is T typed)
            return typed;
        return default;
    }

    /// <summary>
    /// Checks if plugin has a capability.
    /// </summary>
    public bool HasCapability(PluginCapability capability)
    {
        return _manifest.RequiredCapabilities.HasFlag(capability);
    }

    /// <summary>
    /// Gets current resource usage.
    /// </summary>
    public PluginResourceUsage GetResourceUsage()
    {
        var memory = _exports.GetValueOrDefault("memory") as Memory;
        var memorySize = memory?.GetSize(_store) ?? 0;
        
        return new PluginResourceUsage
        {
            PluginId = _pluginId,
            MemoryUsedBytes = (ulong)memorySize,
            MemoryLimitBytes = _manifest.ResourceLimits.MaxMemoryBytes,
            FuelConsumed = _fuelConsumed,
            FuelLimit = _manifest.ResourceLimits.MaxFuel,
            IsHealthy = _store.Fuel > 0
        };
    }

    private int WriteStringToMemory(Store store, string str)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(str);
        return WriteMemory(bytes);
    }

    private string ReadStringFromMemory(Store store, int ptr, int length)
    {
        var memory = _exports.GetValueOrDefault("memory") as Memory;
        if (memory == null) throw new InvalidOperationException("Plugin has no memory export");
        
        var span = memory.GetData(store);
        var bytes = span.Slice(ptr, length).ToArray();
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    private int FindFreeMemoryRegion(MemoryData span, int size)
    {
        // Simple bump allocator - in production use a proper allocator
        // For now, assume plugin exports a malloc function
        var mallocExport = _exports.GetValueOrDefault("malloc") as Func;
        if (mallocExport != null)
        {
            var result = mallocExport.Invoke(_store, size);
            return (int)result[0].GetI32();
        }
        
        // Fallback: find first zero region (very naive)
        for (int i = 0; i <= span.Length - size; i += 64)
        {
            bool free = true;
            for (int j = 0; j < size; j++)
            {
                if (span[i + j] != 0) { free = false; break; }
            }
            if (free) return i;
        }
        return -1;
    }

    private PluginCapability GetRequiredCapability(string functionName)
    {
        return functionName switch
        {
            var f when f.StartsWith("device_") => PluginCapability.DeviceRead | PluginCapability.DeviceWrite,
            var f when f.StartsWith("perception_") => PluginCapability.PerceptionBasic,
            var f when f.StartsWith("ai_") => PluginCapability.AiInference,
            var f when f.StartsWith("storage_") => PluginCapability.StorageRead | PluginCapability.StorageWrite,
            var f when f.StartsWith("network_") => PluginCapability.NetworkHttp,
            _ => PluginCapability.None
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            // Call plugin cleanup if exported
            if (_exports.TryGetValue("plugin_cleanup", out var cleanup) && cleanup is Func func)
            {
                await func.InvokeAsync(_store);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Plugin {PluginId} cleanup failed", _pluginId);
        }

        _store.Dispose();
        _logger.LogInformation("Plugin {PluginId} disposed. Fuel consumed: {Fuel}", _pluginId, _fuelConsumed);
    }
}

/// <summary>
/// Plugin initialization result.
/// </summary>
public sealed record PluginInitResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public string[] ExportedFunctions { get; init; } = Array.Empty<string>();
    public PluginCapability DeclaredCapabilities { get; init; }
    public PluginResourceLimits DeclaredLimits { get; init; } = new();
}

/// <summary>
/// Result of a plugin function call.
/// </summary>
public sealed record PluginCallResult
{
    public bool Success { get; init; }
    public string? OutputJson { get; init; }
    public string? Error { get; init; }
    public double DurationMs { get; init; }

    public static PluginCallResult Failed(string error) => new() { Success = false, Error = error };
    public static PluginCallResult Success(string outputJson, double durationMs) => new() { Success = true, OutputJson = outputJson, DurationMs = durationMs };
}

/// <summary>
/// Plugin resource usage.
/// </summary>
public sealed record PluginResourceUsage
{
    public string PluginId { get; init; } = string.Empty;
    public ulong MemoryUsedBytes { get; init; }
    public ulong MemoryLimitBytes { get; init; }
    public ulong FuelConsumed { get; init; }
    public ulong FuelLimit { get; init; }
    public bool IsHealthy { get; init; }
    public double MemoryUtilization => MemoryLimitBytes > 0 ? (double)MemoryUsedBytes / MemoryLimitBytes : 0;
    public double FuelUtilization => FuelLimit > 0 ? (double)FuelConsumed / FuelLimit : 0;
}