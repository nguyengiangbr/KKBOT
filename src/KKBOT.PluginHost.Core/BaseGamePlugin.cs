// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KKBOT.PluginHost;

/// <summary>
/// Template base for game plugins. Implements lifecycle and exposes a <see cref="GameContext"/>.
/// </summary>
public abstract class BaseGamePlugin : IGameModule
{
    private readonly ILogger _logger;
    private readonly IServiceProvider _services;
    private readonly GameContext _context;
    private bool _started;

    protected BaseGamePlugin(ILogger logger, IServiceProvider services)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        _services = services ?? new ServiceCollection().BuildServiceProvider();
        _context = new GameContext(_services);
    }

    public abstract string Id { get; }
    public abstract string Version { get; }
    public virtual string Name => Id;

    public abstract ModuleManifest GetManifest();

    protected GameContext GameContext => _context;
    protected IServiceProvider Services => _services;
    protected ILogger Logger => _logger;
    protected IDeviceSession? Device => GameContext.Device;

    public virtual Task<ModuleValidationResult> ValidateAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new ModuleValidationResult(true));

    public virtual Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        // Host may pass a fresh provider; the derived plugin can override to bind configuration.
        return OnInitializeAsync(cancellationToken);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await OnStartAsync(cancellationToken);
        _started = true;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await OnStopAsync(cancellationToken);
        _started = false;
    }

    public Task<ModuleHealth> GetHealthAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_started
            ? new ModuleHealth(true)
            : new ModuleHealth(false, "Stopped"));

    protected virtual Task OnInitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    protected virtual Task OnStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    protected virtual Task OnStopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public virtual void Dispose()
    {
        if (_started)
        {
            StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}
