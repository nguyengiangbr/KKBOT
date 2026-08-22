// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KKBOT.PluginMarketplace;

public interface IPluginRegistry
{
    Task<PluginRegistryEntry?> GetPluginAsync(string pluginId, string? version = null, CancellationToken ct = default);
    Task<IReadOnlyList<PluginRegistryEntry>> SearchAsync(PluginSearchQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<PluginRegistryEntry>> GetAllAsync(CancellationToken ct = default);
    Task<PluginRegistryEntry> PublishAsync(PluginPublishRequest request, CancellationToken ct = default);
    Task<bool> UnpublishAsync(string pluginId, string version, CancellationToken ct = default);
    Task<PluginDownloadInfo> GetDownloadInfoAsync(string pluginId, string version, CancellationToken ct = default);
}

public interface IPluginInstaller
{
    Task<InstallResult> InstallAsync(string pluginId, string? version = null, CancellationToken ct = default);
    Task<InstallResult> UpdateAsync(string pluginId, CancellationToken ct = default);
    Task<bool> UninstallAsync(string pluginId, CancellationToken ct = default);
    Task<IReadOnlyList<InstalledPluginInfo>> GetInstalledAsync(CancellationToken ct = default);
    Task<DependencyResolutionResult> ResolveDependenciesAsync(string pluginId, string version, CancellationToken ct = default);
}

public interface IPluginRepository
{
    Task<PluginPackage> DownloadAsync(string downloadUrl, CancellationToken ct = default);
    Task<bool> VerifySignatureAsync(PluginPackage package, CancellationToken ct = default);
    Task<PluginManifest> ExtractManifestAsync(PluginPackage package, CancellationToken ct = default);
}

// Models

public sealed record PluginRegistryEntry(
    string PluginId,
    string Name,
    string Version,
    string Author,
    string Description,
    string[] Tags,
    string RepositoryUrl,
    string DownloadUrl,
    string IconUrl,
    PluginManifest Manifest,
    DateTime PublishedAt,
    long DownloadCount,
    double Rating,
    int RatingCount,
    bool Verified,
    string[] SupportedPlatforms
);

public sealed record PluginSearchQuery(
    string? Keyword = null,
    string[]? Tags = null,
    string? Author = null,
    bool? VerifiedOnly = null,
    int Page = 1,
    int PageSize = 20,
    string SortBy = "downloads", // downloads, rating, updated, name
    bool SortDescending = true
);

public sealed record PluginPublishRequest(
    string PluginId,
    string Version,
    string Author,
    string Description,
    string[] Tags,
    string RepositoryUrl,
    string DownloadUrl,
    string IconUrl,
    PluginManifest Manifest,
    string? Changelog = null
);

public sealed record PluginDownloadInfo(
    string DownloadUrl,
    long FileSize,
    string Sha256Hash,
    string Signature,
    PluginManifest Manifest
);

public sealed record InstallResult(
    bool Success,
    string? PluginId = null,
    string? Version = null,
    string? ErrorMessage = null,
    IReadOnlyList<string>? InstalledDependencies = null
);

public sealed record InstalledPluginInfo(
    string PluginId,
    string Name,
    string Version,
    string InstallPath,
    DateTime InstalledAt,
    bool IsEnabled,
    string? UpdateAvailableVersion = null
);

public sealed record DependencyResolutionResult(
    bool Success,
    IReadOnlyList<PluginDependency> ResolvedDependencies,
    IReadOnlyList<DependencyConflict> Conflicts
);

public sealed record PluginDependency(
    string PluginId,
    string VersionRange, // e.g., ">=1.0.0 <2.0.0"
    bool IsOptional
);

public sealed record DependencyConflict(
    string PluginId,
    string RequiredVersion,
    string InstalledVersion,
    string ConflictType // "version_mismatch", "circular", "missing"
);

public sealed record PluginPackage(
    string PluginId,
    string Version,
    byte[] Data,
    PluginManifest Manifest
);

public sealed record PluginManifest(
    string Id,
    string Name,
    string Version,
    string Author,
    string Description,
    string EntryAssembly,
    string[] Assets,
    PluginDependency[] Dependencies,
    string ConfigSchema,
    string DefaultConfig,
    Resolution[] SupportedResolutions,
    string[] RequiredPermissions
);

public sealed record Resolution(int Width, int Height);