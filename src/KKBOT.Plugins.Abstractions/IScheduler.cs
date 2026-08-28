// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KKBOT.Plugins.Abstractions;

/// <summary>
/// Schedules jobs and manages queues.
/// </summary>
public interface IScheduler
{
    /// <summary>
    /// Schedules a job run.
    /// </summary>
    Task<JobRun> ScheduleJobRunAsync(Guid jobId, Guid instanceId, Dictionary<string, object?>? parameters, CancellationToken cancellationToken);

    /// <summary>
    /// Cancels a job run.
    /// </summary>
    Task<bool> CancelJobRunAsync(Guid jobRunId, string? reason, CancellationToken cancellationToken);

    /// <summary>
    /// Gets pending jobs.
    /// </summary>
    Task<IReadOnlyList<JobRun>> GetPendingJobsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets the queue depth.
    /// </summary>
    int GetQueueDepth();
}

/// <summary>
/// Represents a scheduled job run.
/// </summary>
public sealed record JobRun
{
    public Guid JobRunId { get; init; } = Guid.NewGuid();
    public Guid JobId { get; init; }
    public Guid InstanceId { get; init; }
    public Dictionary<string, object?>? Parameters { get; init; }
    public DateTimeOffset ScheduledAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public JobRunStatus Status { get; init; } = JobRunStatus.Pending;
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Job run status.
/// </summary>
public enum JobRunStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}