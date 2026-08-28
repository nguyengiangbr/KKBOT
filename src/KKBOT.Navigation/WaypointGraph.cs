// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using KKBOT.Plugins.Abstractions;

namespace KKBOT.Navigation;

/// <summary>
/// A static or dynamically built map of waypoints with adjacency information.
/// Uses A* over the waypoint graph for <see cref="IGameMap.FindPath"/>.
/// </summary>
public sealed class WaypointGraph : IGameMap
{
    private readonly Dictionary<string, Waypoint> _nodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _edges = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public IReadOnlyList<Waypoint> Waypoints => _nodes.Values.ToList();

    /// <summary>Adds a node to the graph (idempotent).</summary>
    public WaypointGraph AddNode(Waypoint waypoint)
    {
        _nodes[waypoint.Id] = waypoint;
        if (!_edges.ContainsKey(waypoint.Id))
        {
            _edges[waypoint.Id] = new List<string>();
        }

        return this;
    }

    /// <summary>Adds a bidirectional edge between two existing nodes.</summary>
    public WaypointGraph Connect(string fromId, string toId)
    {
        if (!_nodes.ContainsKey(fromId) || !_nodes.ContainsKey(toId))
        {
            throw new ArgumentException("Both endpoints must be registered nodes.");
        }

        if (!_edges[fromId].Contains(toId, StringComparer.OrdinalIgnoreCase))
        {
            _edges[fromId].Add(toId);
        }

        if (!_edges[toId].Contains(fromId, StringComparer.OrdinalIgnoreCase))
        {
            _edges[toId].Add(fromId);
        }

        return this;
    }

    /// <inheritdoc />
    public IReadOnlyList<Waypoint> FindPath(Waypoint from, Waypoint to)
    {
        if (!_nodes.TryGetValue(from.Id, out _) || !_nodes.TryGetValue(to.Id, out _))
        {
            return Array.Empty<Waypoint>();
        }

        if (string.Equals(from.Id, to.Id, StringComparison.OrdinalIgnoreCase))
        {
            return new[] { to };
        }

        var cameFrom = GridPathfinder.AStar(from.Id, to.Id, _edges, id => _nodes[id].Position);
        if (cameFrom.Count == 0)
        {
            return Array.Empty<Waypoint>();
        }

        var path = new List<Waypoint>();
        var current = to.Id;
        while (current != null)
        {
            path.Insert(0, _nodes[current]);
            cameFrom.TryGetValue(current, out current);
        }

        return path;
    }
}
