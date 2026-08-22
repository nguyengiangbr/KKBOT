// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Threading.Channels;
using KKBOT.Domain.Enums;
using KKBOT.Domain.Models;
using KKBOT.Persistence;
using Microsoft.Extensions.Logging;

namespace KKBOT.Orchestration;

/// <summary>
/// Manages the lifecycle of all automation instances.
/// </summary>
public interface IInstanceManager
{
    /// <summary>
    /// Gets all registered instances.
    /// </summary>
    IReadOnlyList<Instance> GetAllInstances();

    /// <summary>
    /// Gets an instance by ID.
    /// </summary>
    Instance? GetInstance(Guid instanceId);

    /// <summary>
    /// Registers a new instance.
    /// </summary>
    Task<Instance> RegisterAsync(Instance instance, CancellationToken cancellationToken);

    /// <summary>
    /// Unregisters an instance.
    /// </summary>
    Task<bool> UnregisterAsync(Guid instanceId, CancellationToken cancellationToken);

    /// <summary>
    /// Starts an instance.
    /// </summary>
    Task<bool> StartAsync(Guid instanceId, CancellationToken cancellationToken);

    /// <summary>
    /// Stops an instance.
    /// </summary>
    Task<bool> StopAsync(Guid instanceId, string? reason, CancellationToken cancellationToken);

    /// <summary>
    /// Pauses an instance.
    /// </summary>
    Task<bool> PauseAsync(Guid instanceId, string? reason, CancellationToken cancellationToken);

    /// <summary>
    /// Resumes a paused instance.
    /// </summary>
    Task<bool> ResumeAsync(Guid instanceId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the count of instances in each state.
    /// </summary>
    InstanceCounts GetCounts();

    /// <summary>
    /// Gets the current resource budget status.
    /// </summary>
    ResourceBudget GetResourceBudget();

    /// <summary>
    /// Triggers emergency stop on all instances.
    /// </summary>
    Task EmergencyStopAsync(string? reason, CancellationToken cancellationToken);

    /// <summary>
    /// Event raised when an instance state changes.
    /// </summary>
    event EventHandler<InstanceStateChangedEventArgs>? InstanceStateChanged;
}

/// <summary>
/// Event args for instance state changes.
/// </summary>
public class InstanceStateChangedEventArgs : EventArgs
{
    public Guid InstanceId { get; init; }
    public InstanceState PreviousState { get; init; }
    public InstanceState NewState { get; init; }
    public string? Reason { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Counts of instances grouped by state.
/// </summary>
public record InstanceCounts
{
    public int Total { get; init; }
    public int Running { get; init; }
    public int Paused { get; init; }
    public int Recovering { get; init; }
    public int Failed { get; init; }
    public int ManualReview { get; init; }
    public int Ready { get; init; }
    public int Stopped { get; init; }
    public int Disposed { get; init; }
}

/// <summary>
/// Resource budget information.
/// </summary>
public record ResourceBudget
{
    public int MaxInstances { get; init; } = 20;
    public int CurrentInstanceCount { get; init; }
    public double CpuUsagePercent { get; init; }
    public double MemoryUsageMb { get; init; }
    public double AvailableCpuPercent { get; init; }
    public double AvailableMemoryMb { get; init; }
    public bool CanAcceptMoreInstances { get; init; }
}

/// <summary>
/// Manages the lifecycle of automation instances.
/// </summary>
public sealed class InstanceManager : IInstanceManager, IDisposable
{
    private readonly ILogger<InstanceManager> _logger;
    private readonly ConcurrentDictionary<Guid, Instance> _instances = new();
    private readonly Channel<InstanceStateChangedEventArgs> _eventChannel;
    private readonly CancellationTokenSource _shutdownCts;
    private readonly Task _eventProcessorTask;
    private bool _disposed;

    public event EventHandler<InstanceStateChangedEventArgs>? InstanceStateChanged;

    public InstanceManager(ILogger<InstanceManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _eventChannel = Channel.CreateUnbounded<InstanceStateChangedEventArgs>(
            new UnboundedChannelOptions { SingleReader = true });
        _shutdownCts = new CancellationTokenSource();
        _eventProcessorTask = ProcessEventsAsync(_shutdownCts.Token);
    }

    public IReadOnlyList<Instance> GetAllInstances()
        => _instances.Values.ToList().AsReadOnly();

    public Instance? GetInstance(Guid instanceId)
        => _instances.TryGetValue(instanceId, out var instance) ? instance : null;

    public Task<Instance> RegisterAsync(Instance instance, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instance);

        if (instance.Id == Guid.Empty)
            throw new ArgumentException("Instance ID cannot be empty.", nameof(instance));

        if (!_instances.TryAdd(instance.Id, instance))
            throw new InvalidOperationException($"Instance '{instance.Id}' is already registered.");

        _logger.LogInformation("Instance '{InstanceId}' ({Key}) registered", instance.Id, instance.Key);
        return Task.FromResult(instance);
    }

    public async Task<bool> UnregisterAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        if (_instances.TryRemove(instanceId, out var instance))
        {
            await PublishEventAsync(instanceId, instance.State, InstanceState.Disposed, "Unregistered");
            _logger.LogInformation("Instance '{InstanceId}' unregistered", instanceId);
            return true;
        }
        return false;
    }

    public Task<bool> StartAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        if (!_instances.TryGetValue(instanceId, out var instance))
            return Task.FromResult(false);

        lock (instance)
        {
            if (!instance.CanStart())
                return Task.FromResult(false);

            var previousState = instance.State;
            instance.SetState(InstanceState.Connecting);
            instance.SetState(InstanceState.Ready);

            _ = PublishEventAsync(instanceId, previousState, InstanceState.Ready, "Started");
            _logger.LogInformation("Instance '{InstanceId}' started", instanceId);

            return Task.FromResult(true);
        }
    }

    public Task<bool> StopAsync(Guid instanceId, string? reason, CancellationToken cancellationToken)
    {
        if (!_instances.TryGetValue(instanceId, out var instance))
            return Task.FromResult(false);

        lock (instance)
        {
            if (!instance.CanStop())
                return Task.FromResult(false);

            var previousState = instance.State;
            instance.SetState(InstanceState.Stopped, reason);

            _ = PublishEventAsync(instanceId, previousState, InstanceState.Stopped, reason);
            _logger.LogInformation("Instance '{InstanceId}' stopped: {Reason}", instanceId, reason);

            return Task.FromResult(true);
        }
    }

    public Task<bool> PauseAsync(Guid instanceId, string? reason, CancellationToken cancellationToken)
    {
        if (!_instances.TryGetValue(instanceId, out var instance))
            return Task.FromResult(false);

        lock (instance)
        {
            if (instance.State != InstanceState.Running && instance.State != InstanceState.Ready)
                return Task.FromResult(false);

            var previousState = instance.State;
            instance.SetState(InstanceState.Paused, reason);

            _ = PublishEventAsync(instanceId, previousState, InstanceState.Paused, reason);
            _logger.LogInformation("Instance '{InstanceId}' paused: {Reason}", instanceId, reason);

            return Task.FromResult(true);
        }
    }

    public Task<bool> ResumeAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        if (!_instances.TryGetValue(instanceId, out var instance))
            return Task.FromResult(false);

        lock (instance)
        {
            if (instance.State != InstanceState.Paused && instance.State != InstanceState.Waiting)
                return Task.FromResult(false);

            var previousState = instance.State;
            instance.SetState(InstanceState.Ready);

            _ = PublishEventAsync(instanceId, previousState, InstanceState.Ready, "Resumed");
            _logger.LogInformation("Instance '{InstanceId}' resumed", instanceId);

            return Task.FromResult(true);
        }
    }

    public InstanceCounts GetCounts()
    {
        var all = _instances.Values.ToList();
        return new InstanceCounts
        {
            Total = all.Count,
            Running = all.Count(i => i.State == InstanceState.Running),
            Paused = all.Count(i => i.State == InstanceState.Paused),
            Recovering = all.Count(i => i.State == InstanceState.Recovering),
            Failed = all.Count(i => i.State == InstanceState.Failed),
            ManualReview = all.Count(i => i.State == InstanceState.ManualReview),
            Ready = all.Count(i => i.State == InstanceState.Ready),
            Stopped = all.Count(i => i.State == InstanceState.Stopped),
            Disposed = all.Count(i => i.State == InstanceState.Disposed)
        };
    }

    public ResourceBudget GetResourceBudget()
    {
        var all = _instances.Values.ToList();
        var running = all.Where(i => i.State is InstanceState.Running or InstanceState.Ready).ToList();

        return new ResourceBudget
        {
            MaxInstances = 20,
            CurrentInstanceCount = running.Count,
            CpuUsagePercent = running.Sum(i => i.CpuUsagePercent ?? 0),
            MemoryUsageMb = running.Sum(i => i.MemoryUsageMb ?? 0),
            AvailableCpuPercent = 100 - running.Sum(i => i.CpuUsagePercent ?? 0),
            AvailableMemoryMb = Math.Max(0, 16000 - running.Sum(i => i.MemoryUsageMb ?? 0)),
            CanAcceptMoreInstances = running.Count < 20
        };
    }

    public async Task EmergencyStopAsync(string? reason, CancellationToken cancellationToken)
    {
        _logger.LogCritical("EMERGENCY STOP triggered: {Reason}", reason);

        var tasks = new List<Task>();
        foreach (var (id, instance) in _instances)
        {
            lock (instance)
            {
                if (!instance.State.IsTerminal())
                {
                    var previousState = instance.State;
                    instance.SetState(InstanceState.Stopped, $"Emergency stop: {reason}");
                    tasks.Add(PublishEventAsync(id, previousState, InstanceState.Stopped, $"Emergency stop: {reason}"));
                }
            }
        }

        await Task.WhenAll(tasks);
        _logger.LogInformation("Emergency stop completed for {Count} instances", tasks.Count);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _shutdownCts.Cancel();
        _eventProcessorTask.GetAwaiter().GetResult();
        _shutdownCts.Dispose();
    }

    private async Task PublishEventAsync(Guid instanceId, InstanceState previous, InstanceState current, string? reason)
    {
        var eventArgs = new InstanceStateChangedEventArgs
        {
            InstanceId = instanceId,
            PreviousState = previous,
            NewState = current,
            Reason = reason,
            Timestamp = DateTimeOffset.UtcNow
        };

        await _eventChannel.Writer.WriteAsync(eventArgs, _shutdownCts.Token);
    }

    private async Task ProcessEventsAsync(CancellationToken cancellationToken)
    {
        await foreach (var eventArgs in _eventChannel.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                InstanceStateChanged?.Invoke(this, eventArgs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing instance state change event");
            }
        }
    }
}

/// <summary>
/// Schedules jobs and manages queues.
/// </summary>
public interface IScheduler
{
    Task<JobRun> ScheduleJobRunAsync(Guid jobId, Guid instanceId, Dictionary<string, object?>? parameters, CancellationToken cancellationToken);
    Task<bool> CancelJobRunAsync(Guid jobRunId, string? reason, CancellationToken cancellationToken);
    Task<IReadOnlyList<JobRun>> GetPendingJobsAsync(CancellationToken cancellationToken);
    int GetQueueDepth();
}

/// <summary>
/// Coordinates recovery attempts for failed operations.
/// </summary>
public interface IRecoveryCoordinator
{
    Task<RecoveryLevel> DetermineNextLevelAsync(Guid instanceId, string errorCode, CancellationToken cancellationToken);
    Task<bool> ExecuteRecoveryAsync(Guid instanceId, RecoveryLevel level, CancellationToken cancellationToken);
    Task ResetRecoveryCounterAsync(Guid instanceId, CancellationToken cancellationToken);
}