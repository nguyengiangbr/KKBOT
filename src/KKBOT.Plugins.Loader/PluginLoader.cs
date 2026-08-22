// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Plugins.Abstractions;
using Microsoft.Extensions.Logging;

namespace KKBOT.Plugins.Loader;

/// <summary>
/// Loads plugin assemblies from a directory, discovers types implementing <see cref="IGameModule"/>,
/// validates their manifests and returns instantiated modules.
/// </summary>
public sealed class PluginLoader
{
    private readonly ILogger<PluginLoader> _logger;

    public PluginLoader(ILogger<PluginLoader> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Loads all plugin DLLs from <paramref name="pluginsPath"/>.
    /// Each DLL is loaded into its own <see cref="AssemblyLoadContext"/> to enable isolation.
    /// </summary>
    public async Task<IReadOnlyCollection<IGameModule>> LoadPluginsAsync(string pluginsPath, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(pluginsPath))
        {
            _logger.LogWarning("Plugin directory '{Path}' does not exist.", pluginsPath);
            return Array.Empty<IGameModule>();
        }

        var modules = new List<IGameModule>();
        var dllFiles = Directory.GetFiles(pluginsPath, "*.dll", SearchOption.TopDirectoryOnly);
        foreach (var dll in dllFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var alc = new PluginLoadContext(dll);
                var assembly = alc.LoadFromAssemblyPath(dll);
                var moduleTypes = assembly.GetTypes().Where(t => typeof(IGameModule).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass);
                foreach (var type in moduleTypes)
                {
                    // Create instance via parameterless constructor
                    if (Activator.CreateInstance(type) is IGameModule module)
                    {
                        // Basic manifest validation (non‑empty Id/Version)
                        var manifest = module.GetManifest();
                        if (string.IsNullOrWhiteSpace(manifest.Id) || string.IsNullOrWhiteSpace(manifest.Version))
                        {
                            _logger.LogWarning("Plugin '{Dll}' contains module with invalid manifest – skipping.", dll);
                            continue;
                        }
                        // Optional deeper validation via module.ValidateAsync (use dummy context)
                        var dummyContext = new ModuleContext(Guid.Empty, Guid.Empty, Guid.Empty);
                        var validation = await module.ValidateAsync(dummyContext, cancellationToken);
                        if (!validation.IsValid)
                        {
                            _logger.LogWarning("Plugin '{Dll}' module validation failed: {Errors}", dll, string.Join(", ", validation.Errors.Select(e => e.Message)));
                            continue;
                        }
                        modules.Add(module);
                        _logger.LogInformation("Loaded plugin module '{Id}' version {Version} from {Dll}", manifest.Id, manifest.Version, dll);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load plugin assembly '{Dll}'.", dll);
            }
        }
        return modules;
    }
}

/// <summary>
/// Custom <see cref="AssemblyLoadContext"/> that isolates plugin assemblies.
/// It resolves dependencies from the plugin folder first, then falls back to the default context.
/// </summary>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly string _pluginPath;

    public PluginLoadContext(string pluginPath) : base(isCollectible: true)
    {
        _pluginPath = Path.GetDirectoryName(pluginPath)!;
    }

    protected override Assembly Load(AssemblyName assemblyName)
    {
        // Look for the assembly in the plugin folder.
        var candidate = Path.Combine(_pluginPath, $"{assemblyName.Name}.dll");
        if (File.Exists(candidate))
            return LoadFromAssemblyPath(candidate);
        return null;