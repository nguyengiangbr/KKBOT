// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace KKBOT.PluginHost.Navigation;

/// <summary>
/// Adjacency-list graph over <see cref="Waypoint"/> nodes with A* path planning.
/// </summary>
public sealed class WaypointGraph
{
    private readonly Dictionary<string, Waypoint> _nodes = new();
    private readonly Dictionary<string, List<(string To, double Cost)>> _edges = new();

    public void AddWaypoint(Waypoint wp)
    {
        var id = wp.Id ?? Guid.NewGuid().ToString();
        _nodes[id] = wp with { Id = id };
        if (!_edges.ContainsKey(id))
        {
            _edges[id] = new List<(string, double)>();
        }
    }

    public void Connect(string from, string to, double cost)
    {
        if (!_nodes.ContainsKey(from) || !_nodes.ContainsKey(to))
        {
            throw new InvalidOperationException("Both endpoints must be registered waypoints.");
        }

        _edges[from].Add((to, cost));
        _edges[to].Add((from, cost));
    }

    public IReadOnlyDictionary<string, Waypoint> Nodes => _nodes;

    public IReadOnlyList<WorldPoint> FindPath(WorldPoint start, WorldPoint goal)
    {
        var startIndex = Nearest(start);
        var goalIndex = Nearest(goal);
        if (startIndex is null || goalIndex is null)
        {
            return Array.Empty<WorldPoint>();
        }

        return GridPathfinder.Search(startIndex, goalIndex, _nodes, _edges);
    }

    private string? Nearest(WorldPoint p)
    {
        string? best = null;
        var bestDist = double.MaxValue;
        foreach (var (id, wp) in _nodes)
        {
            var d = wp.Position.DistanceTo(p);
            if (d < bestDist)
            {
                bestDist = d;
                best = id;
            }
        }

        return best;
    }
}
