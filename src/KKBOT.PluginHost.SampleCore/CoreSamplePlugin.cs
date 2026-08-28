// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.PluginHost;
using Microsoft.Extensions.Logging;

namespace KKBOT.PluginHost.SampleCore;

/// <summary>
/// Minimal (non-WPF) plugin used to prove the isolated loader pipeline end-to-end.
/// </summary>
public sealed class CoreSamplePlugin : BaseGamePlugin
{
    private readonly CoreMover _mover = new();

    public CoreSamplePlugin(ILogger logger, IServiceProvider services)
        : base(logger, services)
    {
    }

    public override string Id => "samplecore";

    public override string Version => "1.0.0";

    public override ModuleManifest GetManifest() => new()
    {
        Id = Id,
        Name = "Core Sample Plugin",
        Version = Version,
        Author = "KKBOT",
        Description = "Headless plugin proving ALC-isolated loading.",
    };

    protected override Task OnInitializeAsync(CancellationToken cancellationToken)
    {
        GameContext.Navigation.SetMover(_mover);
        return Task.CompletedTask;
    }
}

public sealed class CoreMover : INavigationMover
{
    public WorldPoint CurrentPosition { get; private set; } = new(0, 0);

    public Task MoveTowardAsync(WorldPoint direction, CancellationToken cancellationToken)
    {
        CurrentPosition = direction;
        return Task.CompletedTask;
    }

    public Task<bool> IsStuckAsync(CancellationToken cancellationToken) => Task.FromResult(false);
}
