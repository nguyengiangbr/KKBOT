// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.PluginHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KKBOT.PluginHost.Loader;

/// <summary>
/// Loads plugin assemblies in isolated <see cref="AssemblyLoadContext"/> instances so the host
/// and shared contracts keep a single type identity.
/// </summary>
public sealed class PluginLoader
{
    private readonly ILogger _logger;
    private readonly IServiceProvider _services;
    private readonly string _pluginDirectory;
    private readonly ConcurrentDictionary<string, PluginLoadContext> _contexts = new();
    private readonly ConcurrentDictionary<string, IGameModule> _plugins = new();

    public PluginLoader(ILogger<PluginLoader> logger, IServiceProvider services, string pluginDirectory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _pluginDirectory = pluginDirectory;
        Directory.CreateDirectory(_pluginDirectory);
    }

    public IReadOnlyList<IGameModule> LoadAll(CancellationToken cancellationToken = default)
    {
        foreach (var dll in Directory.GetFiles(_pluginDirectory, "*.dll", SearchOption.AllDirectories))
        {
            try
            {
                LoadOne(dll, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load plugin {Path}", dll);
            }
        }

        return _plugins.Values.ToList();
    }

    private IGameModule? LoadOne(string assemblyPath, CancellationToken cancellationToken)
    {
        var name = Path.GetFileNameWithoutExtension(assemblyPath);
        if (_plugins.ContainsKey(name))
        {
            return _plugins[name];
        }

        var alc = new PluginLoadContext(assemblyPath, _pluginDirectory);
        var assembly = alc.LoadFromAssemblyPath(assemblyPath);

        var type = assembly.GetTypes()
            .FirstOrDefault(t => typeof(IGameModule).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
        if (type is null)
        {
            _logger.LogError("No IGameModule in {Assembly}", assembly.FullName);
            return null;
        }

        var logger = _services.GetService<ILoggerFactory>()?.CreateLogger(type) ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        var ctor = type.GetConstructor(new[] { typeof(ILogger), typeof(IServiceProvider) });
        var instance = ctor is not null
            ? (IGameModule)ctor.Invoke(new object[] { logger, _services })
            : (IGameModule)Activator.CreateInstance(type)!;

        instance.InitializeAsync(_services, cancellationToken).GetAwaiter().GetResult();

        _contexts[name] = alc;
        _plugins[name] = instance;
        _logger.LogInformation("Loaded plugin {Name} v{Version}", instance.GetManifest().Name, instance.Version);
        return instance;
    }

    public IReadOnlyList<IGameModule> Plugins => _plugins.Values.ToList();

    public void UnloadAll()
    {
        foreach (var (name, plugin) in _plugins)
        {
            try
            {
                plugin.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
                plugin.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unloading {Name}", name);
            }

            if (_contexts.TryRemove(name, out var alc))
            {
                alc.Unload();
            }
        }

        _plugins.Clear();
    }
}

/// <summary>
/// Isolated ALC that serves shared contract assemblies (KKBOT.PluginHost.*) from the default
/// context so interface casts across the boundary succeed.
/// </summary>
public sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly string _directory;

    public PluginLoadContext(string pluginPath, string directory)
        : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
        _directory = directory;
    }

    protected override Assembly? Load(AssemblyName name)
    {
        if (name.Name is not null && name.Name.StartsWith("KKBOT.PluginHost", StringComparison.OrdinalIgnoreCase))
        {
            return null; // shared contract: load from default ALC
        }

        var path = _resolver.ResolveAssemblyToPath(name);
        if (path is not null)
        {
            return LoadFromAssemblyPath(path);
        }

        var local = Path.Combine(_directory, $"{name.Name}.dll");
        return File.Exists(local) ? LoadFromAssemblyPath(local) : null;
    }
}
