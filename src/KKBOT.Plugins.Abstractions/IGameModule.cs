// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

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
