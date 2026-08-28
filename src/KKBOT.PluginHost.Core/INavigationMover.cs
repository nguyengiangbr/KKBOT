// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;

namespace KKBOT.PluginHost;

/// <summary>
/// Game-specific movement primitive. The plugin translates logical world movement into
/// real input (keyboard/mouse via the device layer).
/// </summary>
public interface INavigationMover
{
    WorldPoint CurrentPosition { get; }

    Task MoveTowardAsync(WorldPoint direction, CancellationToken cancellationToken);

    Task<bool> IsStuckAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Outcome of plugin signature validation at load time.
/// </summary>
public readonly record struct PluginValidationOutcome(bool IsValid, string? Error = null)
{
    public static readonly PluginValidationOutcome Valid = new(true);
}
