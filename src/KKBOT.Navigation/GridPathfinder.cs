// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using KKBOT.Plugins.Abstractions;

namespace KKBOT.Navigation;

/// <summary>
/// A* pathfinding over a weighted graph identified by node ids.
/// Edge cost is the Euclidean distance between node world positions.
/// </summary>
public static class GridPathfinder
{
    /// <summary>
    /// Returns a map of nodeId -&gt; previous nodeId along the shortest path, or an empty
    /// dictionary when no path exists. The destination key maps to <c>null</c>.
    /// </summary>
    public static Dictionary<string, string?> AStar(
        string startId,
        string goalId,
        Dictionary<string, List<string>> edges,
        Func<string, WorldPoint> positionOf)
    {
        var open = new PriorityQueue<string, double>();
        open.Enqueue(startId, 0);
        var cameFrom = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var gScore = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            [startId] = 0,
        };

        while (open.TryDequeue(out var current, out _))
        {
            if (string.Equals(current, goalId, StringComparison.OrdinalIgnoreCase))
            {
                cameFrom[goalId] = cameFrom.TryGetValue(goalId, out var p) ? p : null;
                return cameFrom;
            }

            foreach (var neighbor in edges.GetValueOrDefault(current, new List<string>()))
            {
                var tentative = gScore[current] + Distance(positionOf(current), positionOf(neighbor));
                if (tentative < gScore.GetValueOrDefault(neighbor, double.PositiveInfinity))
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentative;
                    open.Enqueue(neighbor, tentative + Distance(positionOf(neighbor), positionOf(goalId)));
                }
            }
        }

        return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    }

    private static double Distance(WorldPoint a, WorldPoint b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        var dz = a.Z - b.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }
}
