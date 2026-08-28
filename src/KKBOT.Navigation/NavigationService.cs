// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KKBOT.Plugins.Abstractions;

namespace KKBOT.Navigation;

/// <summary>
/// Options controlling navigation behavior.
/// </summary>
public sealed class NavigationOptions
{
    /// <summary>Distance (world units) considered "arrived" at a waypoint.</summary>
    public double ArrivalEpsilon { get; set; } = 1.5;

    /// <summary>Maximum time spent moving to a single waypoint before giving up.</summary>
    public TimeSpan WaypointTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Time window used to detect a stuck state.</summary>
    public TimeSpan StuckDetectionWindow { get; set; } = TimeSpan.FromSeconds(8);

    /// <summary>Minimum world distance that must be covered within <see cref="StuckDetectionWindow"/>.</summary>
    public double StuckMinProgress { get; set; } = 0.5;
}

/// <summary>
/// Default <see cref="INavigationService"/> implementation: plans a path across the loaded
/// <see cref="IGameMap"/>, delegates actual movement to a game-supplied <see cref="INavigationMover"/>,
/// and raises <see cref="INavigationService.Stuck"/> when progress stalls.
/// </summary>
public sealed class NavigationService : INavigationService
{
    private readonly ILogger<NavigationService> _logger;
    private readonly NavigationOptions _options;
    private readonly object _gate = new();
    private IGameMap? _map;
    private INavigationMover? _mover;
    private WorldPoint _lastPosition;
    private DateTimeOffset _lastProgressTime = DateTimeOffset.UtcNow;
    private bool _isStuck;

    public NavigationService(ILogger<NavigationService> logger, IOptions<NavigationOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>Sets the game-specific mover. Must be called before navigating.</summary>
    public void SetMover(INavigationMover mover)
    {
        lock (_gate)
        {
            _mover = mover ?? throw new ArgumentNullException(nameof(mover));
        }
    }

    public void SetMap(IGameMap map)
    {
        lock (_gate)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
        }
    }

    public IGameMap? CurrentMap
    {
        get
        {
            lock (_gate)
            {
                return _map;
            }
        }
    }

    public bool IsStuck
    {
        get
        {
            lock (_gate)
            {
                return _isStuck;
            }
        }
    }

    public event EventHandler<NavigationStuckEventArgs>? Stuck;

    public async Task NavigateToAsync(WorldPoint target, CancellationToken cancellationToken = default)
    {
        INavigationMover? mover;
        IGameMap? map;
        lock (_gate)
        {
            mover = _mover;
            map = _map;
        }

        if (mover is null)
        {
            throw new InvalidOperationException("No INavigationMover configured. Call SetMover first.");
        }

        if (map is null)
        {
            throw new InvalidOperationException("No IGameMap loaded. Call SetMap first.");
        }

        var start = NearestWaypoint(map, mover.GetPosition());
        var end = NearestWaypoint(map, target);
        var path = map.FindPath(start, end);

        if (path.Count == 0)
        {
            _logger.LogWarning("No path found from {Start} to target {Target}", start.Id, target);
            return;
        }

        ResetProgress(mover.GetPosition());

        foreach (var node in path)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await MoveAlongAsync(mover, node.Position, cancellationToken).ConfigureAwait(false);
        }

        await MoveAlongAsync(mover, target, cancellationToken).ConfigureAwait(false);
    }

    public Task NavigateToAsync(string waypointId, CancellationToken cancellationToken = default)
    {
        IGameMap? map;
        lock (_gate)
        {
            map = _map;
        }

        if (map is null)
        {
            throw new InvalidOperationException("No IGameMap loaded. Call SetMap first.");
        }

        var target = map.Waypoints.FirstOrDefault(w => string.Equals(w.Id, waypointId, StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            throw new ArgumentException($"Waypoint '{waypointId}' not found on the loaded map.", nameof(waypointId));
        }

        return NavigateToAsync(target.Position, cancellationToken);
    }

    private async Task MoveAlongAsync(INavigationMover mover, WorldPoint destination, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_options.WaypointTimeout);

        _logger.LogDebug("Navigating toward {Destination}", destination);

        while (!cts.IsCancellationRequested)
        {
            var current = mover.GetPosition();
            if (Distance(current, destination) <= _options.ArrivalEpsilon)
            {
                ClearStuck();
                return;
            }

            await mover.MoveToAsync(destination, cts.Token).ConfigureAwait(false);

            if (UpdateProgress(current))
            {
                ClearStuck();
            }
            else
            {
                MarkStuck(current, destination);
                if (_isStuck)
                {
                    _logger.LogWarning("Stuck while navigating to {Destination}", destination);
                    return;
                }
            }
        }
    }

    private void ResetProgress(WorldPoint position)
    {
        lock (_gate)
        {
            _lastPosition = position;
            _lastProgressTime = DateTimeOffset.UtcNow;
            _isStuck = false;
        }
    }

    private bool UpdateProgress(WorldPoint current)
    {
        lock (_gate)
        {
            var moved = Distance(_lastPosition, current);
            var elapsed = DateTimeOffset.UtcNow - _lastProgressTime;
            if (moved >= _options.StuckMinProgress && elapsed <= _options.StuckDetectionWindow)
            {
                _lastPosition = current;
                _lastProgressTime = DateTimeOffset.UtcNow;
                return true;
            }

            if (elapsed > _options.StuckDetectionWindow)
            {
                _lastPosition = current;
                _lastProgressTime = DateTimeOffset.UtcNow;
            }

            return false;
        }
    }

    private void MarkStuck(WorldPoint current, WorldPoint target)
    {
        bool raise;
        lock (_gate)
        {
            raise = !_isStuck;
            _isStuck = true;
        }

        if (raise)
        {
            Stuck?.Invoke(this, new NavigationStuckEventArgs(current, target));
        }
    }

    private void ClearStuck()
    {
        lock (_gate)
        {
            _isStuck = false;
        }
    }

    private static Waypoint NearestWaypoint(IGameMap map, WorldPoint position)
    {
        return map.Waypoints
            .OrderBy(w => Distance(w.Position, position))
            .First();
    }

    private static double Distance(WorldPoint a, WorldPoint b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        var dz = a.Z - b.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }
}
