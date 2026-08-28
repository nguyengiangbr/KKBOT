// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.PluginHost;
using Microsoft.Extensions.DependencyInjection;

namespace KKBOT.PluginHost.Navigation;

/// <summary>
/// Default <see cref="INavigationService"/> implementation. Plans a path on the active map
/// and drives the registered <see cref="INavigationMover"/>, detecting stuck states.
/// </summary>
public sealed class NavigationService : INavigationService
{
    private IGameMap? _map;
    private INavigationMover? _mover;

    public void SetMap(IGameMap map) => _map = map ?? throw new ArgumentNullException(nameof(map));

    public void SetMover(INavigationMover mover) => _mover = mover ?? throw new ArgumentNullException(nameof(mover));

    public event EventHandler<NavigationStuckEventArgs>? Stuck;

    public async Task<NavigationResult> NavigateToAsync(WorldPoint target, CancellationToken cancellationToken = default)
    {
        if (_mover is null)
        {
            return new NavigationResult(false, Array.Empty<WorldPoint>(), "No mover registered.");
        }

        if (_map is null)
        {
            // Map-less fallback: move straight toward the target.
            return await MoveDirectAsync(target, cancellationToken);
        }

        var graph = new WaypointGraph();
        foreach (var wp in _map.Waypoints)
        {
            graph.AddWaypoint(wp);
        }

        foreach (var (from, to) in _map.Edges)
        {
            graph.Connect(from, to, _map.Waypoints[0].Cost);
        }

        var path = graph.FindPath(_mover.CurrentPosition, target);
        if (path.Count == 0)
        {
            return new NavigationResult(false, Array.Empty<WorldPoint>(), "No path found.");
        }

        foreach (var step in path)
        {
            await StepTowardAsync(step, cancellationToken);
        }

        return new NavigationResult(true, path);
    }

    private async Task<NavigationResult> MoveDirectAsync(WorldPoint target, CancellationToken cancellationToken)
    {
        await StepTowardAsync(target, cancellationToken);
        return new NavigationResult(true, new[] { target });
    }

    private async Task StepTowardAsync(WorldPoint step, CancellationToken cancellationToken)
    {
        var last = _mover!.CurrentPosition;
        var stuckTicks = 0;

        while (_mover.CurrentPosition.DistanceTo(step) > 1.0 && !cancellationToken.IsCancellationRequested)
        {
            await _mover.MoveTowardAsync(step, cancellationToken);

            if (_mover.CurrentPosition.DistanceTo(last) < 0.1)
            {
                if (await _mover.IsStuckAsync(cancellationToken))
                {
                    stuckTicks++;
                    Stuck?.Invoke(this, new NavigationStuckEventArgs(last, stuckTicks));
                    if (stuckTicks > 10)
                    {
                        return;
                    }
                }
            }
            else
            {
                stuckTicks = 0;
            }

            last = _mover.CurrentPosition;
        }
    }
}

/// <summary>
/// DI registration helper for the navigation service.
/// </summary>
public static class NavigationExtensions
{
    public static IServiceCollection AddKkbotNavigation(this IServiceCollection services)
    {
        services.AddSingleton<INavigationService, NavigationService>();
        return services;
    }
}
