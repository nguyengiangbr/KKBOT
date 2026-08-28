// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using KKBOT.Plugins.Abstractions;

namespace KKBOT.Navigation;

/// <summary>
/// Game-specific movement contract. A game plugin supplies an implementation that knows
/// how to read the bot position and how to move the character toward a world point
/// (e.g. click the minimap, send W/A/S/D, or use the game's pathfind command).
/// </summary>
public interface INavigationMover
{
    /// <summary>Returns the bot's current world position.</summary>
    WorldPoint GetPosition();

    /// <summary>Moves the character toward <paramref name="target"/> and returns when arrived (or after a timeout).</summary>
    Task MoveToAsync(WorldPoint target, CancellationToken cancellationToken);
}
