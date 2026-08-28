// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KKBOT.PluginHost;

/// <summary>
/// A position in the game world's coordinate space (logical units, not screen pixels).
/// </summary>
public readonly struct WorldPoint : IEquatable<WorldPoint>
{
    public WorldPoint(double x, double y, double? z = null)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public double X { get; }
    public double Y { get; }
    public double? Z { get; }

    public double DistanceTo(WorldPoint other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        var dz = (Z - other.Z) ?? 0;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    public bool Equals(WorldPoint other) =>
        X.Equals(other.X) && Y.Equals(other.Y) && Nullable.Equals(Z, other.Z);

    public override bool Equals(object? obj) => obj is WorldPoint other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(X, Y, Z);

    public static bool operator ==(WorldPoint left, WorldPoint right) => left.Equals(right);

    public static bool operator !=(WorldPoint left, WorldPoint right) => !left.Equals(right);
}

/// <summary>
/// A node in a navigation graph.
/// </summary>
public sealed record Waypoint(WorldPoint Position, string? Id = null, double Cost = 1.0);

/// <summary>
/// Static map information supplied by a game plugin (graph + obstacles).
/// </summary>
public interface IGameMap
{
    IReadOnlyList<Waypoint> Waypoints { get; }

    IReadOnlyList<(string From, string To)> Edges { get; }

    bool IsWalkable(WorldPoint from, WorldPoint toWaypoint) => true;
}

/// <summary>
/// Result of a navigation request.
/// </summary>
public sealed record NavigationResult(bool Success, IReadOnlyList<WorldPoint> Path, string? Error = null);

/// <summary>
/// Raised when the mover appears stuck during navigation.
/// </summary>
public sealed class NavigationStuckEventArgs : EventArgs
{
    public NavigationStuckEventArgs(WorldPoint lastPosition, int stuckTicks)
    {
        LastPosition = lastPosition;
        StuckTicks = stuckTicks;
    }

    public WorldPoint LastPosition { get; }
    public int StuckTicks { get; }
}

/// <summary>
/// Core navigation service. Plans a path on the plugin-supplied map and drives the
/// plugin-supplied mover along it, with stuck detection.
/// </summary>
public interface INavigationService
{
    void SetMap(IGameMap map);

    void SetMover(INavigationMover mover);

    Task<NavigationResult> NavigateToAsync(WorldPoint target, CancellationToken cancellationToken = default);

    event EventHandler<NavigationStuckEventArgs>? Stuck;
}
