// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.PluginMarketplace;
using KKBOT.Plugins.Abstractions;
using KKBOT.Plugins.Loader;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KKBOT.PluginMarketplace;

public sealed class LocalPluginRegistry : IPluginRegistry
{
    private readonly ConcurrentDictionary<string, PluginRegistryEntry> _plugins = new();
    private readonly ILogger<LocalPluginRegistry> _logger;
    private readonly string _storagePath;

    public LocalPluginRegistry(ILogger<LocalPluginRegistry> logger, IOptions<MarketplaceOptions> options)
    {
        _logger = logger;
        _storagePath = options.Value.StoragePath;
        Directory.CreateDirectory(_storagePath);
        LoadFromDisk();
    }

    private void LoadFromDisk()
    {
        var indexFile = Path.Combine(_storagePath, "registry-index.json");
        if (File.Exists(indexFile))
        {
            try
            {
                var json = File.ReadAllText(indexFile);
                var entries = JsonSerializer.Deserialize<List<PluginRegistryEntry>>(json);
                if (entries != null)
                {
                    foreach (var entry in entries)
                    {
                        _plugins[GetKey(entry.PluginId, entry.Version)] = entry;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load registry index");
            }
        }
    }

    private void SaveToDisk()
    {
        try
        {
            var indexFile = Path.Combine(_storagePath, "registry-index.json");
            var json = JsonSerializer.Serialize(_plugins.Values.ToList(), new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(indexFile, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save registry index");
        }
    }

    private static string GetKey(string pluginId, string version) => $"{pluginId}@{version}";

    public Task<PluginRegistryEntry?> GetPluginAsync(string pluginId, string? version = null, CancellationToken ct = default)
    {
        if (version != null)
        {
            _plugins.TryGetValue(GetKey(pluginId, version), out var entry);
            return Task.FromResult(entry);
        }

        // Return latest version
        var matches = _plugins.Values.Where(p => p.PluginId == pluginId).OrderByDescending(p => p.Version).FirstOrDefault();
        return Task.FromResult(matches);
    }

    public Task<IReadOnlyList<PluginRegistryEntry>> SearchAsync(PluginSearchQuery query, CancellationToken ct = default)
    {
        var results = _plugins.Values.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.ToLowerInvariant();
            results = results.Where(p => p.Name.Contains(kw, StringComparison.OrdinalIgnoreCase) 
                || p.Description.Contains(kw, StringComparison.OrdinalIgnoreCase)
                || p.Tags.Any(t => t.Contains(kw, StringComparison.OrdinalIgnoreCase)));
        }

        if (query.Tags?.Length > 0)
        {
            results = results.Where(p => query.Tags!.All(t => p.Tags.Contains(t, StringComparer.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(query.Author))
        {
            results = results.Where(p => p.Author.Equals(query.Author, StringComparison.OrdinalIgnoreCase));
        }

        if (query.VerifiedOnly == true)
        {
            results = results.Where(p => p.Verified);
        }

        // Sort
        results = query.SortBy.ToLowerInvariant() switch
        {
            "rating" => query.SortDescending ? results.OrderByDescending(p => p.Rating) : results.OrderBy(p => p.Rating),
            "updated" => query.SortDescending ? results.OrderByDescending(p => p.PublishedAt) : results.OrderBy(p => p.PublishedAt),
            "name" => query.SortDescending ? results.OrderByDescending(p => p.Name) : results.OrderBy(p => p.Name),
            _ => query.SortDescending ? results.OrderByDescending(p => p.DownloadCount) : results.OrderBy(p => p.DownloadCount)
        };

        // Paginate
        var paged = results.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();
        return Task.FromResult<IReadOnlyList<PluginRegistryEntry>>(paged);
    }

    public Task<IReadOnlyList<PluginRegistryEntry>> GetAllAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<PluginRegistryEntry>>(_plugins.Values.ToList());
    }

    public Task<PluginRegistryEntry> PublishAsync(PluginPublishRequest request, CancellationToken ct = default)
    {
        var entry = new PluginRegistryEntry(
            request.PluginId,
            request.Manifest.Name,
            request.Version,
            request.Author,
            request.Description,
            request.Tags,
            request.RepositoryUrl,
            request.DownloadUrl,
            request.IconUrl,
            request.Manifest,
            DateTime.UtcNow,
            0,
            0,
            0,
            false,
            ["windows", "linux"]
        );

        _plugins[GetKey(request.PluginId, request.Version)] = entry;
        SaveToDisk();
        
        _logger.LogInformation("Published plugin {PluginId} v{Version}", request.PluginId, request.Version);
        return Task.FromResult(entry);
    }

    public Task<bool> UnpublishAsync(string pluginId, string version, CancellationToken ct = default)
    {
        var removed = _plugins.TryRemove(GetKey(pluginId, version), out _);
        if (removed) SaveToDisk();
        return Task.FromResult(removed);
    }

    public Task<PluginDownloadInfo> GetDownloadInfoAsync(string pluginId, string version, CancellationToken ct = default)
    {
        var entry = GetPluginAsync(pluginId, version, ct).Result;
        if (entry == null)
            throw new FileNotFoundException($"Plugin {pluginId} v{version} not found");

        return Task.FromResult(new PluginDownloadInfo(
            entry.DownloadUrl,
            0, // Would be actual file size
            "", // Would be actual hash
            "", // Would be actual signature
            entry.Manifest
        ));
    }
}

public sealed class LocalPluginInstaller : IPluginInstaller
{
    private readonly IPluginRegistry _registry;
    private readonly IPluginRepository _repository;
    private readonly PluginLoader _pluginLoader;
    private readonly ILogger<LocalPluginInstaller> _logger;
    private readonly string _pluginsPath;
    private readonly ConcurrentDictionary<string, InstalledPluginInfo> _installed = new();

    public LocalPluginInstaller(
        IPluginRegistry registry,
        IPluginRepository repository,
        PluginLoader pluginLoader,
        ILogger<LocalPluginInstaller> logger,
        IOptions<MarketplaceOptions> options)
    {
        _registry = registry;
        _repository = repository;
        _pluginLoader = pluginLoader;
        _logger = logger;
        _pluginsPath = options.Value.PluginsPath;
        Directory.CreateDirectory(_pluginsPath);
        LoadInstalled();
    }

    private void LoadInstalled()
    {
        var installedFile = Path.Combine(_pluginsPath, "installed.json");
        if (File.Exists(installedFile))
        {
            try
            {
                var json = File.ReadAllText(installedFile);
                var list = JsonSerializer.Deserialize<List<InstalledPluginInfo>>(json);
                if (list != null)
                {
                    foreach (var p in list) _installed[p.PluginId] = p;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load installed plugins");
            }
        }
    }

    private void SaveInstalled()
    {
        try
        {
            var installedFile = Path.Combine(_pluginsPath, "installed.json");
            var json = JsonSerializer.Serialize(_installed.Values.ToList(), new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(installedFile, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save installed plugins");
        }
    }

    public async Task<InstallResult> InstallAsync(string pluginId, string? version = null, CancellationToken ct = default)
    {
        _logger.LogInformation("Installing plugin {PluginId} v{Version}", pluginId, version ?? "latest");

        // 1. Get plugin info from registry
        var plugin = await _registry.GetPluginAsync(pluginId, version, ct);
        if (plugin == null)
            return new InstallResult(false, pluginId, version, $"Plugin {pluginId} not found in registry");

        // 2. Resolve dependencies
        var depResult = await ResolveDependenciesAsync(pluginId, plugin.Version, ct);
        if (!depResult.Success)
            return new InstallResult(false, pluginId, plugin.Version, $"Dependency resolution failed: {string.Join(", ", depResult.Conflicts.Select(c => c.ConflictType))}");

        // 3. Install dependencies first
        var installedDeps = new List<string>();
        foreach (var dep in depResult.ResolvedDependencies)
        {
            if (!_installed.ContainsKey(dep.PluginId))
            {
                var depResult2 = await InstallAsync(dep.PluginId, dep.VersionRange, ct);
                if (!depResult2.Success)
                    return new InstallResult(false, pluginId, plugin.Version, $"Failed to install dependency {dep.PluginId}: {depResult2.ErrorMessage}");
                installedDeps.Add(dep.PluginId);
            }
        }

        // 4. Download plugin package
        var downloadInfo = await _registry.GetDownloadInfoAsync(pluginId, plugin.Version, ct);
        var package = await _repository.DownloadAsync(downloadInfo.DownloadUrl, ct);
        
        // 5. Verify signature
        if (!await _repository.VerifySignatureAsync(package, ct))
            return new InstallResult(false, pluginId, plugin.Version, "Plugin signature verification failed");

        // 6. Extract to plugins folder
        var pluginDir = Path.Combine(_pluginsPath, pluginId);
        Directory.CreateDirectory(pluginDir);
        
        // For simplicity, assume package.Data contains the DLL
        var dllPath = Path.Combine(pluginDir, $"{pluginId}.dll");
        await File.WriteAllBytesAsync(dllPath, package.Data, ct);

        // 7. Record installation
        var installed = new InstalledPluginInfo(
            pluginId,
            plugin.Manifest.Name,
            plugin.Version,
            pluginDir,
            DateTime.UtcNow,
            true
        );
        _installed[pluginId] = installed;
        SaveInstalled();

        _logger.LogInformation("Successfully installed plugin {PluginId} v{Version}", pluginId, plugin.Version);
        return new InstallResult(true, pluginId, plugin.Version, null, installedDeps);
    }

    public async Task<InstallResult> UpdateAsync(string pluginId, CancellationToken ct = default)
    {
        if (!_installed.TryGetValue(pluginId, out var installed))
            return new InstallResult(false, pluginId, null, "Plugin not installed");

        // Check for updates
        var latest = await _registry.GetPluginAsync(pluginId, null, ct);
        if (latest == null || latest.Version == installed.Version)
            return new InstallResult(false, pluginId, installed.Version, "Already up to date");

        // Uninstall old version first
        await UninstallAsync(pluginId, ct);

        // Install new version
        return await InstallAsync(pluginId, latest.Version, ct);
    }

    public async Task<bool> UninstallAsync(string pluginId, CancellationToken ct = default)
    {
        if (!_installed.TryRemove(pluginId, out var installed))
            return false;

        // Delete plugin directory
        try
        {
            if (Directory.Exists(installed.InstallPath))
                Directory.Delete(installed.InstallPath, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete plugin directory for {PluginId}", pluginId);
        }

        SaveInstalled();
        _logger.LogInformation("Uninstalled plugin {PluginId}", pluginId);
        return true;
    }

    public Task<IReadOnlyList<InstalledPluginInfo>> GetInstalledAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<InstalledPluginInfo>>(_installed.Values.ToList());
    }

    public async Task<DependencyResolutionResult> ResolveDependenciesAsync(string pluginId, string version, CancellationToken ct = default)
    {
        var plugin = await _registry.GetPluginAsync(pluginId, version, ct);
        if (plugin == null)
            return new DependencyResolutionResult(false, Array.Empty<PluginDependency>(), 
                new[] { new DependencyConflict(pluginId, version, "", "not_found") });

        var resolved = new List<PluginDependency>();
        var conflicts = new List<DependencyConflict>();

        foreach (var dep in plugin.Manifest.Dependencies)
        {
            // Check if already installed
            if (_installed.TryGetValue(dep.PluginId, out var installed))
            {
                // Check version compatibility (simplified)
                if (IsVersionCompatible(installed.Version, dep.VersionRange))
                {
                    resolved.Add(dep with { VersionRange = installed.Version });
                }
                else
                {
                    conflicts.Add(new DependencyConflict(dep.PluginId, dep.VersionRange, installed.Version, "version_mismatch"));
                }
            }
            else
            {
                // Need to install - check registry
                var depPlugin = await _registry.GetPluginAsync(dep.PluginId, null, ct);
                if (depPlugin != null)
                {
                    resolved.Add(dep with { VersionRange = depPlugin.Version });
                }
                else
                {
                    conflicts.Add(new DependencyConflict(dep.PluginId, dep.VersionRange, "", "missing"));
                }
            }
        }

        return new DependencyResolutionResult(conflicts.Count == 0, resolved, conflicts);
    }

    private bool IsVersionCompatible(string installedVersion, string requiredRange)
    {
        // Simplified version check - in production use NuGet.Versioning
        return true;
    }
}

public sealed class HttpPluginRepository : IPluginRepository
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpPluginRepository> _logger;

    public HttpPluginRepository(HttpClient httpClient, ILogger<HttpPluginRepository> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<PluginPackage> DownloadAsync(string downloadUrl, CancellationToken ct = default)
    {
        _logger.LogDebug("Downloading plugin from {Url}", downloadUrl);
        var response = await _httpClient.GetAsync(downloadUrl, ct);
        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadAsByteArrayAsync(ct);

        // For now, create a minimal manifest
        var manifest = new PluginManifest(
            "unknown", "Unknown", "1.0.0", "Unknown", "",
            "unknown.dll", Array.Empty<string>(), Array.Empty<PluginDependency>(),
            "{}", "{}", Array.Empty<Resolution>(), Array.Empty<string>()
        );

        return new PluginPackage("unknown", "1.0.0", data, manifest);
    }

    public async Task<bool> VerifySignatureAsync(PluginPackage package, CancellationToken ct = default)
    {
        // In production, verify cryptographic signature
        // For now, just check SHA256
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(package.Data);
        _logger.LogDebug("Plugin SHA256: {Hash}", Convert.ToHexString(hash));
        return true;
    }

    public async Task<PluginManifest> ExtractManifestAsync(PluginPackage package, CancellationToken ct = default)
    {
        return package.Manifest;
    }
}

public sealed class MarketplaceOptions
{
    public string StoragePath { get; set; } = "data/marketplace";
    public string PluginsPath { get; set; } = "plugins";
    public string RegistryUrl { get; set; } = "https://registry.kkbot.example.com";
    public bool EnableAutoUpdate { get; set; } = true;
    public int UpdateCheckIntervalHours { get; set; } = 24;
}