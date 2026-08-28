// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace KKBOT.PluginHost.Navigation;

/// <summary>
/// A* search over a <see cref="WaypointGraph"/>.
/// </summary>
public static class GridPathfinder
{
    public static IReadOnlyList<WorldPoint> Search(
        string start,
        string goal,
        IReadOnlyDictionary<string, Waypoint> nodes,
        IReadOnlyDictionary<string, List<(string To, double Cost)>> edges)
    {
        var open = new PriorityQueue<string, double>();
        var cameFrom = new Dictionary<string, string>();
        var g = new Dictionary<string, double> { [start] = 0 };
        var f = new Dictionary<string, double> { [start] = Heuristic(start, goal, nodes) };

        open.Enqueue(start, f[start]);

        while (open.TryDequeue(out var current, out _))
        {
            if (current == goal)
            {
                return Reconstruct(cameFrom, current, nodes);
            }

            foreach (var (next, cost) in edges.GetValueOrDefault(current, new List<(string, double)>()))
            {
                var tentative = g[current] + cost;
                if (tentative < g.GetValueOrDefault(next, double.MaxValue))
                {
                    cameFrom[next] = current;
                    g[next] = tentative;
                    f[next] = tentative + Heuristic(next, goal, nodes);
                    open.Enqueue(next, f[next]);
                }
            }
        }

        return Array.Empty<WorldPoint>();
    }

    private static double Heuristic(string a, string b, IReadOnlyDictionary<string, Waypoint> nodes)
        => nodes[a].Position.DistanceTo(nodes[b].Position);

    private static IReadOnlyList<WorldPoint> Reconstruct(
        Dictionary<string, string> cameFrom,
        string current,
        IReadOnlyDictionary<string, Waypoint> nodes)
    {
        var path = new List<WorldPoint> { nodes[current].Position };
        while (cameFrom.TryGetValue(current, out var prev))
        {
            current = prev;
            path.Insert(0, nodes[current].Position);
        }

        return path;
    }
}
