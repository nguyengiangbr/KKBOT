// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KKBOT.Plugins.Abstractions;

namespace KKBOT.Plugins.Loader;

/// <summary>
/// Plugin loader with AssemblyLoadContext isolation for hot-reload and security.
/// </summary>
public sealed class PluginLoader : IDisposable
{
    private readonly ILogger<PluginLoader> _logger;
    private readonly PluginLoaderOptions _options;
    private readonly ConcurrentDictionary<string, PluginLoadContext> _loadContexts = new();
    private readonly ConcurrentDictionary<string, IGameModule> _loadedPlugins = new();
    private readonly PluginSigningValidator _signingValidator;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILoggerFactory _loggerFactory;
    private bool _disposed;

    public PluginLoader(
        ILogger<PluginLoader> logger,
        IOptions<PluginLoaderOptions> options,
        PluginSigningValidator signingValidator,
        IServiceProvider serviceProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _signingValidator = signingValidator ?? throw new ArgumentNullException(nameof(signingValidator));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

        // Ensure plugin directory exists
        Directory.CreateDirectory(_options.PluginDirectory);
    }

    /// <summary>
    /// Loads all plugins from the plugin directory.
    /// </summary>
    public async Task<IReadOnlyList<IGameModule>> LoadAllPluginsAsync(CancellationToken cancellationToken = default)
    {
        var pluginFiles = Directory.GetFiles(_options.PluginDirectory, "*.dll", SearchOption.AllDirectories);
        var loadedPlugins = new List<IGameModule>();

        foreach (var pluginPath in pluginFiles)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                var plugin = await LoadPluginAsync(pluginPath, cancellationToken);
                if (plugin != null)
                {
                    loadedPlugins.Add(plugin);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load plugin from {PluginPath}", pluginPath);
            }
        }

        _logger.LogInformation("Loaded {Count} plugins from {Directory}", loadedPlugins.Count, _options.PluginDirectory);
        return loadedPlugins;
    }

    /// <summary>
    /// Loads a single plugin from the specified assembly path.
    /// </summary>
    public async Task<IGameModule?> LoadPluginAsync(string assemblyPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(assemblyPath))
        {
            _logger.LogError("Plugin assembly not found: {Path}", assemblyPath);
            return null;
        }

        var assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
        
        // Check if already loaded
        if (_loadedPlugins.TryGetValue(assemblyName, out var existingPlugin))
        {
            _logger.LogDebug("Plugin {AssemblyName} already loaded", assemblyName);
            return existingPlugin;
        }

        _logger.LogInformation("Loading plugin from {Path}", assemblyPath);

        try
        {
            // Validate plugin signature if enabled
            if (_options.RequireSignature)
            {
                var validationResult = await _signingValidator.ValidateAsync(assemblyPath, cancellationToken);
                if (!validationResult.IsValid)
                {
                    _logger.LogError("Plugin signature validation failed for {Path}: {Error}", assemblyPath, validationResult.Error);
                    return null;
                }
            }

            // Create isolated load context
            var loadContext = new PluginLoadContext(assemblyPath, _options.PluginDirectory);
            var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);

            // Find and instantiate IGameModule implementation
            var pluginType = assembly.GetTypes()
                .FirstOrDefault(t => typeof(IGameModule).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            if (pluginType == null)
            {
                _logger.LogError("No IGameModule implementation found in {Assembly}", assembly.FullName);
                return null;
            }

            // Check if it's a BaseGamePlugin derived class
            var isBaseGamePlugin = typeof(BaseGamePlugin).IsAssignableFrom(pluginType);

            // Create plugin instance
            IGameModule pluginInstance;
            
            if (isBaseGamePlugin)
            {
                // For BaseGamePlugin derived classes, use DI constructor
                var constructor = pluginType.GetConstructor(new[] { typeof(ILogger), typeof(IServiceProvider) });
                if (constructor != null)
                {
                    var logger = _loggerFactory.CreateLogger(pluginType);
                    pluginInstance = (IGameModule)constructor.Invoke(new object[] { logger, _serviceProvider });
                }
                else
                {
                    // Fallback to parameterless constructor
                    pluginInstance = (IGameModule)Activator.CreateInstance(pluginType);
                }
            }
            else
            {
                // For legacy IGameModule implementations
                pluginInstance = (IGameModule)Activator.CreateInstance(pluginType);
            }

            // Initialize the plugin
            await pluginInstance.InitializeAsync(_serviceProvider, CancellationToken.None);

            // Store the load context and plugin
            _loadContexts.TryAdd(assemblyName, loadContext);
            _loadedPlugins.TryAdd(assemblyName, pluginInstance);

            _logger.LogInformation("Successfully loaded plugin {PluginName} ({GameId}) v{Version}", 
                pluginInstance.GameName, pluginInstance.GameId, pluginInstance.Version);

            return pluginInstance;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load plugin from {Path}", assemblyPath);
            return null;
        }
    }

    /// <summary>
    /// Unloads a plugin by name.
    /// </summary>
    public bool UnloadPlugin(string pluginName)
    {
        if (_loadedPlugins.TryRemove(pluginName, out var plugin))
        {
            _logger.LogInformation("Unloading plugin {PluginName}", pluginName);
            
            try
            {
                plugin.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
                plugin.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping plugin {PluginName}", pluginName);
            }

            if (_loadContexts.TryRemove(pluginName, out var loadContext))
            {
                loadContext.Unload();
            }

            _logger.LogInformation("Plugin {PluginName} unloaded successfully", pluginName);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Reloads a plugin (hot-reload).
    /// </summary>
    public async Task<IGameModule?> ReloadPluginAsync(string pluginName, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Reloading plugin {PluginName}", pluginName);

        // Unload existing
        UnloadPlugin(pluginName);

        // Find plugin file
        var pluginFiles = Directory.GetFiles(_options.PluginDirectory, $"{pluginName}.dll", SearchOption.AllDirectories);
        if (pluginFiles.Length == 0)
        {
            _logger.LogError("Plugin file not found for {PluginName}", pluginName);
            return null;
        }

        // Load new version
        return await LoadPluginAsync(pluginFiles[0], cancellationToken);
    }

    /// <summary>
    /// Gets information about all loaded plugins.
    /// </summary>
    public IReadOnlyList<PluginInfo> GetLoadedPlugins()
    {
        return _loadedPlugins.Values.Select(p => new PluginInfo
        {
            Name = p.GameName,
            GameId = p.GameId,
            Version = p.Version,
            IsInitialized = true,
            IsStarted = true,
        }).ToList().AsReadOnly();
    }

    /// <summary>
    /// Gets a loaded plugin by game ID.
    /// </summary>
    public IGameModule? GetPlugin(string gameId)
    {
        _loadedPlugins.TryGetValue(gameId, out var plugin);
        return plugin;
    }

    /// <summary>
    /// Validates all loaded plugins for compatibility.
    /// </summary>
    public async Task<PluginValidationResult> ValidatePluginsAsync(CancellationToken cancellationToken = default)
    {
        var result = new PluginValidationResult { IsValid = true };

        foreach (var plugin in _loadedPlugins.Values)
        {
            var health = await plugin.GetHealthAsync(cancellationToken);
            if (!health.IsHealthy)
            {
                result.IsValid = false;
                result.Issues.Add($"{plugin.GameName}: {string.Join(", ", health.Issues)}");
            }
        }

        return result;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Unload all plugins
        foreach (var pluginName in _loadedPlugins.Keys.ToList())
        {
            UnloadPlugin(pluginName);
        }

        // Unload all contexts
        foreach (var context in _loadContexts.Values)
        {
            context.Unload();
        }

        _loadContexts.Clear();
        _loadedPlugins.Clear();
    }
}

/// <summary>
/// AssemblyLoadContext for plugin isolation.
/// </summary>
public sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly string _pluginDirectory;

    public PluginLoadContext(string pluginPath, string pluginDirectory) 
        : base(isCollectible: true)
    {
        _pluginDirectory = pluginDirectory;
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        // Try to load from plugin directory
        var assemblyPath2 = Path.Combine(_pluginDirectory, $"{assemblyName.Name}.dll");
        if (File.Exists(assemblyPath2))
        {
            return LoadFromAssemblyPath(assemblyPath2);
        }

        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }
        return base.LoadUnmanagedDll(unmanagedDllName);
    }
}

/// <summary>
/// Options for plugin loader.
/// </summary>
public sealed class PluginLoaderOptions
{
    public string PluginDirectory { get; set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
    public bool RequireSignature { get; set; } = true;
    public bool EnableHotReload { get; set; } = true;
    public TimeSpan LoadTimeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Plugin information.
/// </summary>
public sealed class PluginInfo
{
    public string Name { get; init; } = string.Empty;
    public string GameId { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public bool IsInitialized { get; init; }
    public bool IsStarted { get; init; }
}

/// <summary>
/// Plugin validation result.
/// </summary>
public sealed class PluginValidationResult
{
    public bool IsValid { get; set; } = true;
    public List<string> Issues { get; init; } = new();
}

/// <summary>
/// Extension methods for registering plugin loader.
/// </summary>
public static class PluginLoaderExtensions
{
    public static IServiceCollection AddPluginLoader(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PluginLoaderOptions>(configuration.GetSection("PluginLoader"));
        services.AddSingleton<PluginLoader>();
        services.AddHostedService(sp => sp.GetRequiredService<PluginLoader>());
        return services;
    }
}