// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Perception.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace KKBOT.Plugins.Abstractions;

/// <summary>
/// Contract that every game plugin must implement.
/// </summary>
public interface IGameModule
{
    string Id { get; }
    string Version { get; }
    ModuleManifest GetManifest();
    IReadOnlyCollection<IGameDetector> GetDetectors();
    IReadOnlyCollection<IGameTaskDefinition> GetTasks();
    IReadOnlyCollection<IStateDefinition> GetStates();
    Task<ModuleValidationResult> ValidateAsync(ModuleContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Display name of the game. Defaults to <see cref="Id"/> for modules that don't override it.
    /// </summary>
    string GameName => Id;

    /// <summary>
    /// Called once before the plugin starts. Default is a no-op.
    /// </summary>
    Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// Starts the plugin. Default is a no-op.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// Stops the plugin. Default is a no-op.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// Returns the current health of the plugin. Default reports healthy.
    /// </summary>
    Task<ModuleHealth> GetHealthAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new ModuleHealth { IsHealthy = true, Status = HealthStatus.Healthy });
}

public sealed record ModuleManifest
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public Resolution[] SupportedResolutions { get; init; } = System.Array.Empty<Resolution>();
    public string[] RequiredPermissions { get; init; } = System.Array.Empty<string>();
    public string ConfigSchema { get; init; } = string.Empty;
    public string DefaultConfig { get; init; } = string.Empty;
    public string EntryAssembly { get; init; } = string.Empty;
    public string[] Assets { get; init; } = System.Array.Empty<string>();
}

public sealed record Resolution(int Width, int Height);

public sealed record ModuleContext(Guid InstanceId, Guid DeviceId, Guid ModuleId);

public sealed record ModuleValidationResult(bool IsValid, IReadOnlyCollection<ModuleValidationError> Errors);
public sealed record ModuleValidationError(string Code, string Message);
