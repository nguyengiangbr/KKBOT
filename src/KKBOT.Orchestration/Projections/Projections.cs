// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Orchestration;
using Microsoft.Extensions.Logging;

namespace KKBOT.Orchestration.Projections;

/// <summary>
/// Instance state projection - tracks current state of all instances.
/// </summary>
public sealed class InstanceStateProjection : IEventProjection
{
    public string Name => "instance-state";
    public int Version => 1;

    private readonly ConcurrentDictionary<Guid, InstanceProjectionState> _instances = new();
    private readonly ILogger<InstanceStateProjection> _logger;

    public InstanceStateProjection(ILogger<InstanceStateProjection> logger)
    {
        _logger = logger;
    }

    public async Task ProjectAsync(StoredEvent @event, CancellationToken ct)
    {
        switch (@event.EventType)
        {
            case nameof(InstanceCreatedEvent):
                var created = JsonSerializer.Deserialize<InstanceCreatedEvent>(@event.EventData);
                if (created != null)
                {
                    _instances[created.InstanceId] = new InstanceProjectionState
                    {
                        InstanceId = created.InstanceId,
                        State = InstanceState.Created,
                        CreatedAt = @event.Timestamp,
                        LastUpdated = @event.Timestamp
                    };
                }
                break;

            case nameof(StateEnteredEvent):
                var stateEvent = JsonSerializer.Deserialize<StateEnteredEvent>(@event.EventData);
                if (stateEvent != null && _instances.TryGetValue(stateEvent.InstanceId, out var instance))
                {
                    if (Enum.TryParse<InstanceState>(stateEvent.StateId, true, out var parsedState))
                    {
                        instance.State = parsedState;
                    }
                    instance.LastUpdated = @event.Timestamp;
                    instance.CurrentStateId = stateEvent.StateId;
                }
                break;

            case nameof(JobStartedEvent):
                var jobStarted = JsonSerializer.Deserialize<JobStartedEvent>(@event.EventData);
                if (jobStarted != null && _instances.TryGetValue(jobStarted.InstanceId, out var jobInstance))
                {
                    jobInstance.ActiveJobs.Add(jobStarted.JobId);
                    jobInstance.LastUpdated = @event.Timestamp;
                }
                break;

            case nameof(JobCompletedEvent):
                var jobCompleted = JsonSerializer.Deserialize<JobCompletedEvent>(@event.EventData);
                if (jobCompleted != null && _instances.TryGetValue(jobCompleted.InstanceId, out var completeInstance))
                {
                    completeInstance.ActiveJobs.Remove(jobCompleted.JobId);
                    completeInstance.CompletedJobs.Add(jobCompleted.JobId);
                    completeInstance.LastUpdated = @event.Timestamp;
                    
                    if (!jobCompleted.Success)
                    {
                        completeInstance.FailedJobs.Add(jobCompleted.JobId);
                    }
                }
                break;
        }
    }

    public async Task<object?> GetStateAsync(CancellationToken ct)
    {
        return _instances.Values.ToList().AsReadOnly();
    }

    public async Task<InstanceProjectionState?> GetInstanceStateAsync(Guid instanceId, CancellationToken ct)
    {
        _instances.TryGetValue(instanceId, out var state);
        return state;
    }
}

/// <summary>
/// Instance projection state.
/// </summary>
public sealed class InstanceProjectionState
{
    public Guid InstanceId { get; set; }
    public InstanceState State { get; set; } = InstanceState.Created;
    public string? CurrentStateId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastUpdated { get; set; }
    public HashSet<Guid> ActiveJobs { get; set; } = new();
    public HashSet<Guid> CompletedJobs { get; set; } = new();
    public HashSet<Guid> FailedJobs { get; set; } = new();
}

/// <summary>
/// Job execution projection - tracks job execution metrics.
/// </summary>
public sealed class JobExecutionProjection : IEventProjection
{
    public string Name => "job-execution";
    public int Version => 1;

    private readonly ConcurrentDictionary<Guid, JobProjectionState> _jobs = new();
    private readonly ILogger<JobExecutionProjection> _logger;

    public JobExecutionProjection(ILogger<JobExecutionProjection> logger)
    {
        _logger = logger;
    }

    public async Task ProjectAsync(StoredEvent @event, CancellationToken ct)
    {
        switch (@event.EventType)
        {
            case nameof(JobScheduledEvent):
                var scheduled = JsonSerializer.Deserialize<JobScheduledEvent>(@event.EventData);
                if (scheduled != null)
                {
                    _jobs[scheduled.JobId] = new JobProjectionState
                    {
                        JobId = scheduled.JobId,
                        InstanceId = scheduled.InstanceId,
                        Status = JobStatus.Scheduled,
                        ScheduledAt = @event.Timestamp,
                        Dependencies = scheduled.Dependencies?.ToList() ?? new()
                    };
                }
                break;

            case nameof(JobStartedEvent):
                var started = JsonSerializer.Deserialize<JobStartedEvent>(@event.EventData);
                if (started != null && _jobs.TryGetValue(started.JobId, out var job))
                {
                    job.Status = JobStatus.Running;
                    job.StartedAt = @event.Timestamp;
                }
                break;

            case nameof(JobCompletedEvent):
                var completed = JsonSerializer.Deserialize<JobCompletedEvent>(@event.EventData);
                if (completed != null && _jobs.TryGetValue(completed.JobId, out var completeJob))
                {
                    completeJob.Status = completed.Success ? JobStatus.Completed : JobStatus.Failed;
                    completeJob.CompletedAt = @event.Timestamp;
                    completeJob.Success = completed.Success;
                    completeJob.DurationMs = (completeJob.CompletedAt - completeJob.StartedAt).TotalMilliseconds;
                }
                break;
        }
    }

    public async Task<object?> GetStateAsync(CancellationToken ct)
    {
        return _jobs.Values.ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<JobProjectionState>> GetJobsByInstanceAsync(Guid instanceId, CancellationToken ct)
    {
        return _jobs.Values.Where(j => j.InstanceId == instanceId).ToList().AsReadOnly();
    }

    public async Task<JobMetrics> GetMetricsAsync(CancellationToken ct)
    {
        var jobs = _jobs.Values.ToList();
        return new JobMetrics
        {
            TotalJobs = jobs.Count,
            RunningJobs = jobs.Count(j => j.Status == JobStatus.Running),
            CompletedJobs = jobs.Count(j => j.Status == JobStatus.Completed),
            FailedJobs = jobs.Count(j => j.Status == JobStatus.Failed),
            AvgDurationMs = jobs.Where(j => j.DurationMs > 0).DefaultIfEmpty(0).Average(j => j.DurationMs),
            SuccessRate = jobs.Count > 0 ? (double)jobs.Count(j => j.Success) / jobs.Count : 0
        };
    }
}

public enum JobStatus
{
    Scheduled,
    Running,
    Completed,
    Failed,
    Cancelled
}

public sealed class JobProjectionState
{
    public Guid JobId { get; set; }
    public Guid InstanceId { get; set; }
    public JobStatus Status { get; set; }
    public DateTimeOffset ScheduledAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public double DurationMs { get; set; }
    public bool Success { get; set; }
    public List<Guid> Dependencies { get; set; } = new();
}

public sealed record JobMetrics
{
    public int TotalJobs { get; init; }
    public int RunningJobs { get; init; }
    public int CompletedJobs { get; init; }
    public int FailedJobs { get; init; }
    public double AvgDurationMs { get; init; }
    public double SuccessRate { get; init; }
}

/// <summary>
/// Recovery projection - tracks recovery attempts and effectiveness.
/// </summary>
public sealed class RecoveryProjection : IEventProjection
{
    public string Name => "recovery";
    public int Version => 1;

    private readonly ConcurrentDictionary<Guid, InstanceRecoveryState> _recoveries = new();
    private readonly ILogger<RecoveryProjection> _logger;

    public RecoveryProjection(ILogger<RecoveryProjection> logger)
    {
        _logger = logger;
    }

    public async Task ProjectAsync(StoredEvent @event, CancellationToken ct)
    {
        // In real implementation, listen for RecoveryAttempt events
        // For now, track state changes that indicate recovery
        if (@event.EventType == nameof(StateEnteredEvent))
        {
            var stateEvent = JsonSerializer.Deserialize<StateEnteredEvent>(@event.EventData);
            if (stateEvent != null && stateEvent.StateId == "recovering")
            {
                _recoveries.AddOrUpdate(stateEvent.InstanceId, 
                    new InstanceRecoveryState { InstanceId = stateEvent.InstanceId, RecoveryCount = 1, LastRecoveryAt = @event.Timestamp },
                    (_, existing) => { existing.RecoveryCount++; existing.LastRecoveryAt = @event.Timestamp; return existing; });
            }
        }
    }

    public async Task<object?> GetStateAsync(CancellationToken ct)
    {
        return _recoveries.Values.ToList().AsReadOnly();
    }
}

public sealed class InstanceRecoveryState
{
    public Guid InstanceId { get; set; }
    public int RecoveryCount { get; set; }
    public DateTimeOffset LastRecoveryAt { get; set; }
    public Dictionary<string, int> RecoveryByLevel { get; set; } = new();
}

/// <summary>
/// Metrics projection - aggregates system metrics from events.
/// </summary>
public sealed class MetricsProjection : IEventProjection
{
    public string Name => "metrics";
    public int Version => 1;

    private readonly ConcurrentDictionary<string, MetricAggregator> _metrics = new();
    private readonly ILogger<MetricsProjection> _logger;

    public MetricsProjection(ILogger<MetricsProjection> logger)
    {
        _logger = logger;
    }

    public async Task ProjectAsync(StoredEvent @event, CancellationToken ct)
    {
        var key = $"{@event.EventType}:{@event.CorrelationId}";
        _metrics.AddOrUpdate(key, 
            new MetricAggregator { Count = 1, FirstSeen = @event.Timestamp, LastSeen = @event.Timestamp },
            (_, existing) => { existing.Count++; existing.LastSeen = @event.Timestamp; return existing; });
    }

    public async Task<object?> GetStateAsync(CancellationToken ct)
    {
        return _metrics.ToDictionary(
            kvp => kvp.Key, 
            kvp => new { kvp.Value.Count, kvp.Value.FirstSeen, kvp.Value.LastSeen });
    }
}

public sealed class MetricAggregator
{
    public long Count { get; set; }
    public DateTimeOffset FirstSeen { get; set; }
    public DateTimeOffset LastSeen { get; set; }
    public Dictionary<string, long> ByCorrelation { get; set; } = new();
}

/// <summary>
/// Orchestration health projection - overall system health.
/// </summary>
public sealed class OrchestrationHealthProjection : IEventProjection
{
    public string Name => "orchestration-health";
    public int Version => 1;

    private readonly ILogger<OrchestrationHealthProjection> _logger;
    private HealthSnapshot _currentHealth = new();

    public OrchestrationHealthProjection(ILogger<OrchestrationHealthProjection> logger)
    {
        _logger = logger;
    }

    public async Task ProjectAsync(StoredEvent @event, CancellationToken ct)
    {
        switch (@event.EventType)
        {
            case nameof(InstanceCreatedEvent):
                _currentHealth.TotalInstances++;
                break;
            case nameof(StateEnteredEvent):
                var stateEvent = JsonSerializer.Deserialize<StateEnteredEvent>(@event.EventData);
                if (stateEvent != null)
                {
                    UpdateStateCount(stateEvent.StateId, +1);
                }
                break;
            case nameof(JobCompletedEvent):
                var jobCompleted = JsonSerializer.Deserialize<JobCompletedEvent>(@event.EventData);
                if (jobCompleted != null)
                {
                    _currentHealth.TotalJobs++;
                    if (jobCompleted.Success) _currentHealth.SuccessfulJobs++;
                    else _currentHealth.FailedJobs++;
                }
                break;
        }

        // Calculate health score
        _currentHealth.HealthScore = CalculateHealthScore();
        _currentHealth.LastUpdated = DateTimeOffset.UtcNow;
    }

    public async Task<object?> GetStateAsync(CancellationToken ct)
    {
        return _currentHealth;
    }

    private void UpdateStateCount(string stateId, int delta)
    {
        switch (stateId.ToLowerInvariant())
        {
            case "running": _currentHealth.RunningInstances += delta; break;
            case "paused": _currentHealth.PausedInstances += delta; break;
            case "failed": _currentHealth.FailedInstances += delta; break;
            case "stopped": _currentHealth.StoppedInstances += delta; break;
            case "recovering": _currentHealth.RecoveringInstances += delta; break;
        }
    }

    private double CalculateHealthScore()
    {
        if (_currentHealth.TotalInstances == 0) return 1.0;
        
        var healthy = _currentHealth.RunningInstances + _currentHealth.PausedInstances;
        var unhealthy = _currentHealth.FailedInstances + _currentHealth.RecoveringInstances;
        
        return (double)healthy / (_currentHealth.TotalInstances + unhealthy);
    }
}

public sealed class HealthSnapshot
{
    public int TotalInstances { get; set; }
    public int RunningInstances { get; set; }
    public int PausedInstances { get; set; }
    public int FailedInstances { get; set; }
    public int StoppedInstances { get; set; }
    public int RecoveringInstances { get; set; }
    public int TotalJobs { get; set; }
    public int SuccessfulJobs { get; set; }
    public int FailedJobs { get; set; }
    public double HealthScore { get; set; } = 1.0;
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Registers all built-in projections.
/// </summary>
public static class ProjectionRegistry
{
    public static async Task RegisterAllAsync(IEventStore eventStore, IServiceProvider serviceProvider, CancellationToken ct = default)
    {
        var projections = new IEventProjection[]
        {
            serviceProvider.GetRequiredService<InstanceStateProjection>(),
            serviceProvider.GetRequiredService<JobExecutionProjection>(),
            serviceProvider.GetRequiredService<RecoveryProjection>(),
            serviceProvider.GetRequiredService<MetricsProjection>(),
            serviceProvider.GetRequiredService<OrchestrationHealthProjection>()
        };

        foreach (var projection in projections)
        {
            await eventStore.RegisterProjectionAsync(projection.Name, projection, ct);
        }
    }
}