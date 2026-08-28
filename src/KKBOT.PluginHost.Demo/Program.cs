// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.PluginHost;
using KKBOT.PluginHost.Loader;
using KKBOT.PluginHost.Navigation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KKBOT.PluginHost.Demo;

public sealed class FakeMover : INavigationMover
{
    public WorldPoint CurrentPosition { get; private set; } = new(0, 0);

    public Task MoveTowardAsync(WorldPoint direction, CancellationToken cancellationToken)
    {
        // Step toward the desired point at a fixed speed.
        var dx = direction.X - CurrentPosition.X;
        var dy = direction.Y - CurrentPosition.Y;
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-6)
        {
            CurrentPosition = direction;
            return Task.CompletedTask;
        }

        var step = Math.Min(5.0, len);
        CurrentPosition = new WorldPoint(
            CurrentPosition.X + (dx / len) * step,
            CurrentPosition.Y + (dy / len) * step);
        return Task.CompletedTask;
    }

    public Task<bool> IsStuckAsync(CancellationToken cancellationToken) => Task.FromResult(false);
}

public sealed class DemoMap : IGameMap
{
    public IReadOnlyList<Waypoint> Waypoints { get; init; } = Array.Empty<Waypoint>();
    public IReadOnlyList<(string From, string To)> Edges { get; init; } = Array.Empty<(string, string)>();
}

public static class Program
{
    public static async Task Main()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole());
        services.AddKkbotNavigation();
        var provider = services.BuildServiceProvider();

        var nav = provider.GetRequiredService<INavigationService>();
        var mover = new FakeMover();
        nav.SetMover(mover);

        // Build a simple grid map: (0,0)-(10,0)-(10,10) and (0,0)-(0,10)-(10,10)
        var graph = new WaypointGraph();
        graph.AddWaypoint(new Waypoint(new WorldPoint(0, 0), "a"));
        graph.AddWaypoint(new Waypoint(new WorldPoint(10, 0), "b"));
        graph.AddWaypoint(new Waypoint(new WorldPoint(10, 10), "c"));
        graph.AddWaypoint(new Waypoint(new WorldPoint(0, 10), "d"));
        graph.Connect("a", "b", 10);
        graph.Connect("b", "c", 10);
        graph.Connect("a", "d", 10);
        graph.Connect("d", "c", 10);

        var path = graph.FindPath(new WorldPoint(0, 0), new WorldPoint(10, 10));
        Console.WriteLine($"Path nodes: {path.Count}");
        foreach (var p in path)
        {
            Console.WriteLine($"  ({p.X}, {p.Y})");
        }

        nav.SetMap(new DemoMap
        {
            Waypoints = new List<Waypoint>
            {
                new Waypoint(new WorldPoint(0, 0), "a"),
                new Waypoint(new WorldPoint(10, 0), "b"),
                new Waypoint(new WorldPoint(10, 10), "c"),
                new Waypoint(new WorldPoint(0, 10), "d"),
            },
            Edges = new List<(string, string)> { ("a", "b"), ("b", "c"), ("a", "d"), ("d", "c") },
        });

        var result = await nav.NavigateToAsync(new WorldPoint(10, 10), CancellationToken.None);
        Console.WriteLine($"NavigateToAsync success={result.Success}, finalPos=({mover.CurrentPosition.X},{mover.CurrentPosition.Y})");
        var navOk = result.Success && Math.Abs(mover.CurrentPosition.X - 10) < 0.5 && Math.Abs(mover.CurrentPosition.Y - 10) < 0.5;

        // Prove the isolated plugin loader loads an external assembly.
        var pluginDir = System.IO.Path.Combine(AppContext.BaseDirectory, "plugins");
        var loader = new PluginLoader(
            provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PluginLoader>>(),
            provider,
            pluginDir);
        var modules = loader.LoadAll();
        Console.WriteLine($"Loaded external plugins: {modules.Count}");
        foreach (var m in modules)
        {
            Console.WriteLine($"  -> {m.GetManifest().Name} v{m.Version}");
        }

        var loadOk = modules.Count > 0;

        // Phase 3: prove the farm orchestration layer can run many clone accounts per game.
        var farm = new FarmManager();
        var firstPlugin = modules.Count > 0 ? modules[0] : null;
        var farmOk = false;
        if (firstPlugin is not null)
        {
            var acc1 = new AccountProfile("acc-1", firstPlugin.Id, "clone_user_1", "VN-1");
            var acc2 = new AccountProfile("acc-2", firstPlugin.Id, "clone_user_2", "VN-2");
            var i1 = farm.CreateInstance(acc1, firstPlugin, new NullDeviceSession("dev-1"));
            var i2 = farm.CreateInstance(acc2, firstPlugin, new NullDeviceSession("dev-2"));
            await farm.StartAsync(i1.InstanceId);
            await farm.StartAsync(i2.InstanceId);
            Console.WriteLine($"Farm instances: {farm.Instances.Count}, statuses: " +
                string.Join(", ", farm.Instances.Select(x => x.Status.ToString())));
            farmOk = farm.Instances.Count == 2 &&
                     farm.Instances.All(x => x.Status == InstanceStatus.Running);
            await farm.StopAsync(i1.InstanceId);
            await farm.StopAsync(i2.InstanceId);
        }

        Console.WriteLine(navOk && loadOk && farmOk ? "DEMO PASSED" : "DEMO FAILED");
    }
}

/// <summary>
/// Stub device session; a real implementation would drive Win32 input / screen capture.
/// </summary>
public sealed class NullDeviceSession : IDeviceSession
{
    public NullDeviceSession(string id) => DeviceId = id;

    public string DeviceId { get; }

    public Task SendKeyAsync(string key, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ClickAtAsync(int x, int y, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<byte[]> CaptureFrameAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Array.Empty<byte>());
}
