// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KKBOT.PluginHost;

public enum InstanceStatus
{
    Created,
    Running,
    Stopped,
    Faulted
}

/// <summary>
/// Runtime binding of one plugin instance to one clone account and one device session.
/// </summary>
public sealed class GameInstance
{
    private InstanceStatus _status = InstanceStatus.Created;

    public GameInstance(IGameModule plugin, AccountProfile account, IDeviceSession device)
    {
        Plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        Account = account ?? throw new ArgumentNullException(nameof(account));
        Device = device ?? throw new ArgumentNullException(nameof(device));
    }

    public Guid InstanceId { get; } = Guid.NewGuid();

    public IGameModule Plugin { get; }

    public AccountProfile Account { get; }

    public IDeviceSession Device { get; }

    public InstanceStatus Status => _status;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await Plugin.StartAsync(cancellationToken);
        _status = InstanceStatus.Running;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await Plugin.StopAsync(cancellationToken);
        _status = InstanceStatus.Stopped;
    }

    internal void MarkFaulted() => _status = InstanceStatus.Faulted;
}

/// <summary>
/// Coordinates many clone-account instances across loaded game plugins.
/// </summary>
public interface IFarmManager
{
    GameInstance CreateInstance(AccountProfile account, IGameModule plugin, IDeviceSession device);

    Task StartAsync(Guid instanceId, CancellationToken cancellationToken = default);

    Task StopAsync(Guid instanceId, CancellationToken cancellationToken = default);

    IReadOnlyList<GameInstance> Instances { get; }
}

public sealed class FarmManager : IFarmManager
{
    private readonly Dictionary<Guid, GameInstance> _instances = new();

    public IReadOnlyList<GameInstance> Instances => _instances.Values.ToList();

    public GameInstance CreateInstance(AccountProfile account, IGameModule plugin, IDeviceSession device)
    {
        var instance = new GameInstance(plugin, account, device);
        lock (_instances)
        {
            _instances[instance.InstanceId] = instance;
        }

        return instance;
    }

    public async Task StartAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        var instance = Get(instanceId);
        try
        {
            await instance.StartAsync(cancellationToken);
        }
        catch (Exception)
        {
            instance.MarkFaulted();
            throw;
        }
    }

    public async Task StopAsync(Guid instanceId, CancellationToken cancellationToken = default)
        => await Get(instanceId).StopAsync(cancellationToken);

    private GameInstance Get(Guid id)
    {
        lock (_instances)
        {
            if (!_instances.TryGetValue(id, out var instance))
            {
                throw new InvalidOperationException($"Instance {id} not found.");
            }

            return instance;
        }
    }
}
