// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using KKBOT.Plugins.Abstractions;
using KKBOT.Plugins.Wasm.HostFunctions;
using KKBOT.Observability;

namespace KKBOT.Plugins.Wasm;

public static class WasmPluginExtensions
{
    public static IServiceCollection AddKkbotWasmPlugins(this IServiceCollection services, IConfiguration configuration)
    {
        // Options
        services.Configure<WasmPluginHostOptions>(configuration.GetSection("WasmPlugins"));

        // Core services
        services.AddSingleton<HostFunctionRegistry>();
        services.AddSingleton<WasmPluginHost>();
        services.AddSingleton<IWasmPluginManager, WasmPluginManager>();

        // Register host functions
        services.AddTransient<IHostFunctionRegistrar, KkbotHostFunctionRegistrar>();

        return services;
    }
}

/// <summary>
/// Interface for registering host functions.
/// </summary>
public interface IHostFunctionRegistrar
{
    void RegisterAll(HostFunctionRegistry registry, IServiceProvider serviceProvider);
}

/// <summary>
/// Registrar for KKBOT built-in host functions.
/// </summary>
public sealed class KkbotHostFunctionRegistrar : IHostFunctionRegistrar
{
    public void RegisterAll(HostFunctionRegistry registry, IServiceProvider serviceProvider)
    {
        KkbotHostFunctions.RegisterAll(registry, serviceProvider);
    }
}

/// <summary>
/// High-level plugin manager for WASM plugins.
/// </summary>
public interface IWasmPluginManager
{
    Task<WasmPluginInstance> LoadPluginAsync(string pluginPath, WasmPluginManifest manifest, CancellationToken ct = default);
    Task UnloadPluginAsync(string pluginId, CancellationToken ct = default);
    WasmPluginInstance? GetPlugin(string pluginId);
    IReadOnlyList<WasmPluginInstance> GetAllPlugins();
    Task<PluginCallResult> CallPluginAsync(string pluginId, string functionName, string inputJson, CancellationToken ct = default);
    Task<WasmPluginInstance> LoadFromDirectoryAsync(string directory, CancellationToken ct = default);
}

/// <summary>
/// WASM plugin manager implementation.
/// </summary>
public sealed class WasmPluginManager : IWasmPluginManager
{
    private readonly WasmPluginHost _host;
    private readonly ILogger<WasmPluginManager> _logger;

    public WasmPluginManager(WasmPluginHost host, ILogger<WasmPluginManager> logger)
    {
        _host = host;
        _logger = logger;
    }

    public async Task<WasmPluginInstance> LoadPluginAsync(string pluginPath, WasmPluginManifest manifest, CancellationToken ct = default)
    {
        return await _host.LoadPluginAsync(pluginPath, manifest, ct);
    }

    public async Task UnloadPluginAsync(string pluginId, CancellationToken ct = default)
    {
        await _host.UnloadPluginAsync(pluginId, ct);
    }

    public WasmPluginInstance? GetPlugin(string pluginId)
    {
        return _host.GetPlugin(pluginId);
    }

    public IReadOnlyList<WasmPluginInstance> GetAllPlugins()
    {
        return _host.GetAllPlugins();
    }

    public async Task<PluginCallResult> CallPluginAsync(string pluginId, string functionName, string inputJson, CancellationToken ct = default)
    {
        var plugin = _host.GetPlugin(pluginId);
        if (plugin == null)
            return PluginCallResult.Failed($"Plugin '{pluginId}' not found");

        return await plugin.CallAsync(functionName, inputJson, ct);
    }

    public async Task<WasmPluginInstance> LoadFromDirectoryAsync(string directory, CancellationToken ct = default)
    {
        var manifestPath = Path.Combine(directory, "manifest.json");
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException($"Manifest not found: {manifestPath}");

        var manifestJson = await File.ReadAllTextAsync(manifestPath, ct);
        var manifest = JsonSerializer.Deserialize<WasmPluginManifest>(manifestJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        }) ?? throw new InvalidOperationException("Invalid manifest");

        var wasmPath = Path.Combine(directory, "plugin.wasm");
        if (!File.Exists(wasmPath))
        {
            // Try to find any .wasm file
            var wasmFiles = Directory.GetFiles(directory, "*.wasm");
            if (wasmFiles.Length == 0)
                throw new FileNotFoundException($"No .wasm file found in {directory}");
            wasmPath = wasmFiles[0];
        }

        // Verify hash if provided
        if (!string.IsNullOrEmpty(manifest.Sha256Hash))
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            await using var stream = File.OpenRead(wasmPath);
            var hash = await sha256.ComputeHashAsync(stream, ct);
            var actualHash = Convert.ToHexString(hash).ToLowerInvariant();
            if (!actualHash.Equals(manifest.Sha256Hash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Plugin hash mismatch: expected {manifest.Sha256Hash}, got {actualHash}");
            }
        }

        return await LoadPluginAsync(wasmPath, manifest, ct);
    }
}

/// <summary>
/// Plugin manifest for WASM plugins (serializable).
/// </summary>
public sealed record WasmPluginManifest
{
    public string PluginId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string EntryPoint { get; init; } = "plugin_init";
    public PluginCapability RequiredCapabilities { get; init; } = PluginCapability.Standard;
    public PluginResourceLimits ResourceLimits { get; init; } = new();
    public Dictionary<string, string> Metadata { get; init; } = new();
    public string[] RequiredHostFunctions { get; init; } = Array.Empty<string>();
    public string Sha256Hash { get; init; } = string.Empty;
}