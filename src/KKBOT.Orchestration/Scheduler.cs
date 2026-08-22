// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using KKBOT.Domain.Enums;
using KKBOT.Domain.Models;
using Microsoft.Extensions.Logging;

namespace KKBOT.Orchestration;

/// <summary>
/// Schedules job runs and manages the execution queue.
/// </summary>
public sealed class Scheduler : IScheduler, IDisposable
{
    private readonly ILogger<Scheduler> _logger;
    private readonly ConcurrentDictionary<Guid, JobRun> _pendingRuns = new();
    private readonly ConcurrentDictionary<Guid, JobRun> _activeRuns = new();
    private readonly ConcurrentDictionary<Guid, Job> _jobs = new();
    private bool _disposed;

    public Scheduler(ILogger<Scheduler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<JobRun> ScheduleJobRunAsync(Guid jobId, Guid instanceId,
        Dictionary<string, object?>? parameters, CancellationToken cancellationToken)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
        {
            var jobRun = new JobRun
            {
                JobId = jobId,
                InstanceId = instanceId,
                State = TaskState.Pending,
                CorrelationId = Guid.NewGuid().ToString()
            };

            _pendingRuns.TryAdd(jobRun.Id, jobRun);
            _logger.LogInformation("Job run '{JobRunId}' scheduled for instance '{InstanceId}'", jobRun.Id, instanceId);
            return Task.FromResult(jobRun);
        }

        // Check concurrency limit
        var activeForJob = _activeRuns.Values.Count(r => r.JobId == jobId);
        if (job.MaxConcurrency > 0 && activeForJob >= job.MaxConcurrency)
        {
            var jobRun = new JobRun
            {
                JobId = jobId,
                InstanceId = instanceId,
                State = TaskState.Blocked,
                ErrorMessage = "Concurrency limit exceeded"
            };

            _pendingRuns.TryAdd(jobRun.Id, jobRun);
            return Task.FromResult(jobRun);
        }

        // Check approval requirement
        if (job.RequiresApproval)
        {
            var jobRun = new JobRun
            {
                JobId = jobId,
                InstanceId = instanceId,
                State = TaskState.RequiresApproval
            };

            _pendingRuns.TryAdd(jobRun.Id, jobRun);
            return Task.FromResult(jobRun);
        }

        var run = new JobRun
        {
            JobId = jobId,
            InstanceId = instanceId,
            State = TaskState.Pending,
            CorrelationId = Guid.NewGuid().ToString()
        };

        _pendingRuns.TryAdd(run.Id, run);
        _logger.LogInformation("Job run '{JobRunId}' scheduled for job '{JobId}' on instance '{InstanceId}'",
            run.Id, jobId, instanceId);

        return Task.FromResult(run);
    }

    public Task<bool> CancelJobRunAsync(Guid jobRunId, string? reason, CancellationToken cancellationToken)
    {
        if (_pendingRuns.TryRemove(jobRunId, out var pendingRun))
        {
            pendingRun.MarkCancelled(reason ?? "Cancelled by operator");
            _logger.LogInformation("Pending job run '{JobRunId}' cancelled: {Reason}", jobRunId, reason);
            return Task.FromResult(true);
        }

        if (_activeRuns.TryGetValue(jobRunId, out var activeRun))
        {
            activeRun.MarkCancelled(reason ?? "Cancelled by operator");
            _activeRuns.TryRemove(jobRunId, out _);
            _logger.LogInformation("Active job run '{JobRunId}' cancelled: {Reason}", jobRunId, reason);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task<IReadOnlyList<JobRun>> GetPendingJobsAsync(CancellationToken cancellationToken)
    {
        var pending = _pendingRuns.Values
            .OrderBy(r => r.CreatedAt)
            .ToList()
            .AsReadOnly();

        return Task.FromResult<IReadOnlyList<JobRun>>(pending);
    }

    public int GetQueueDepth() => _pendingRuns.Count;

    public void RegisterJob(Job job)
    {
        ArgumentNullException.ThrowIfNull(job);
        _jobs.TryAdd(job.Id, job);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pendingRuns.Clear();
        _activeRuns.Clear();
        _jobs.Clear();
    }
}