// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KKBOT.PluginHost;

/// <summary>
/// Unified contract that every game plugin must implement. Combines module metadata
/// (manifest/detectors/tasks/states) with the lifecycle used by the host.
/// </summary>
public interface IGameModule : IDisposable
{
    string Id { get; }

    string Version { get; }

    string Name => GetManifest().Name;

    ModuleManifest GetManifest();

    Task<ModuleValidationResult> ValidateAsync(CancellationToken cancellationToken = default);

    Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default);

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    Task<ModuleHealth> GetHealthAsync(CancellationToken cancellationToken = default);
}

public sealed record ModuleManifest
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ConfigSchema { get; init; } = "{}";
    public string DefaultConfig { get; init; } = "{}";
}

public sealed record ModuleValidationResult(bool IsValid, string? Error = null);

public sealed record ModuleHealth(bool IsHealthy, string Status = "Healthy");
