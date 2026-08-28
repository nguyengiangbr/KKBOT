// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KKBOT.Plugins.Abstractions;

/// <summary>
/// A point in the game's logical/world coordinate space.
/// </summary>
public sealed record WorldPoint(double X, double Y, double Z = 0);

/// <summary>
/// A named navigation node on a game map.
/// </summary>
public sealed record Waypoint(string Id, WorldPoint Position);

/// <summary>
/// A game map that can answer reachability and path queries.
/// Implemented per-game by plugins (or by the built-in WaypointGraph).
/// </summary>
public interface IGameMap
{
    /// <summary>All navigation nodes known to this map.</summary>
    IReadOnlyList<Waypoint> Waypoints { get; }

    /// <summary>
    /// Computes a path of waypoints from <paramref name="from"/> to <paramref name="to"/>.
    /// Returns an empty list when no path exists.
    /// </summary>
    IReadOnlyList<Waypoint> FindPath(Waypoint from, Waypoint to);
}

/// <summary>
/// Service that drives an instance along a route to a target location.
/// Exposed on <see cref="GameContext"/> so every plugin can navigate without re-implementing pathfinding.
/// </summary>
public interface INavigationService
{
    /// <summary>Maps the currently loaded game map (per instance).</summary>
    void SetMap(IGameMap map);

    /// <summary>Gets the currently loaded map, or <c>null</c> when none is set.</summary>
    IGameMap? CurrentMap { get; }

    /// <summary>True when the bot has not made progress for longer than the stuck threshold.</summary>
    bool IsStuck { get; }

    /// <summary>Raised when movement appears stuck during navigation.</summary>
    event EventHandler<NavigationStuckEventArgs>? Stuck;

    /// <summary>Navigates to a world point, walking the map path and issuing movement input.</summary>
    Task NavigateToAsync(WorldPoint target, CancellationToken cancellationToken = default);

    /// <summary>Navigates to a named waypoint already present on the loaded map.</summary>
    Task NavigateToAsync(string waypointId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Event data emitted when navigation detects the bot is stuck.
/// </summary>
public sealed class NavigationStuckEventArgs : EventArgs
{
    public NavigationStuckEventArgs(WorldPoint lastPosition, WorldPoint target)
    {
        LastPosition = lastPosition;
        Target = target;
    }

    public WorldPoint LastPosition { get; }
    public WorldPoint Target { get; }
}

/// <summary>
/// Outcome of a plugin signature validation.
/// </summary>
public sealed record PluginValidationOutcome(bool IsValid, string? Error = null);
