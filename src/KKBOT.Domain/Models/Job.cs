// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KKBOT.Domain.Enums;

namespace KKBOT.Domain.Models;

/// <summary>
/// Represents a job definition that can be scheduled or triggered manually.
/// </summary>
public class Job : Entity
{
    /// <summary>
    /// Unique key for the job.
    /// </summary>
    [MaxLength(100)]
    [Required]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Display name.
    /// </summary>
    [MaxLength(200)]
    [Required]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Description.
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Foreign key to the module this job belongs to.
    /// </summary>
    [Required]
    public Guid ModuleId { get; set; }

    /// <summary>
    /// Navigation property to the module.
    /// </summary>
    [ForeignKey(nameof(ModuleId))]
    public virtual Module Module { get; set; } = null!;

    /// <summary>
    /// Task ID to execute (from module definition).
    /// </summary>
    [MaxLength(100)]
    [Required]
    public string TaskId { get; set; } = string.Empty;

    /// <summary>
    /// Task parameters (JSON).
    /// </summary>
    [Column(TypeName = "json")]
    public string? Parameters { get; set; }

    /// <summary>
    /// Job priority (higher = more priority).
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Maximum concurrent runs.
    /// </summary>
    public int MaxConcurrency { get; set; } = 1;

    /// <summary>
    /// Timeout for the job.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Retry policy (JSON).
    /// </summary>
    [Column(TypeName = "json")]
    public string? RetryPolicy { get; set; }

    /// <summary>
    /// Whether the job is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Whether the job requires manual approval to start.
    /// </summary>
    public bool RequiresApproval { get; set; } = false;

    /// <summary>
    /// Required permissions to run this job.
    /// </summary>
    [Column(TypeName = "json")]
    public string? RequiredPermissions { get; set; }

    /// <summary>
    /// Resource requirements (CPU, memory, etc.).
    /// </summary>
    [Column(TypeName = "json")]
    public string? ResourceRequirements { get; set; }

    /// <summary>
    /// Tags for categorization.
    /// </summary>
    [MaxLength(500)]
    public string? Tags { get; set; }

    /// <summary>
    /// Navigation property for job runs.
    /// </summary>
    public virtual ICollection<JobRun> Runs { get; set; } = new List<JobRun>();

    /// <summary>
    /// Navigation property for schedules.
    /// </summary>
    public virtual ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
}

/// <summary>
/// Represents an execution of a job.
/// </summary>
public class JobRun : Entity
{
    /// <summary>
    /// Foreign key to the job definition.
    /// </summary>
    [Required]
    public Guid JobId { get; set; }

    /// <summary>
    /// Navigation property to the job.
    /// </summary>
    [ForeignKey(nameof(JobId))]
    public virtual Job Job { get; set; } = null!;

    /// <summary>
    /// Foreign key to the instance executing this job.
    /// </summary>
    public Guid? InstanceId { get; set; }

    /// <summary>
    /// Navigation property to the instance.
    /// </summary>
    [ForeignKey(nameof(InstanceId))]
    public virtual Instance? Instance { get; set; }

    /// <summary>
    /// Current state of the job run.
    /// </summary>
    [Required]
    public TaskState State { get; set; } = TaskState.Pending;

    /// <summary>
    /// How the job was triggered.
    /// </summary>
    [MaxLength(50)]
    [Required]
    public string TriggerType { get; set; } = "Manual"; // Manual, Scheduled, Api, Retry

    /// <summary>
    /// Who/what triggered the job.
    /// </summary>
    [MaxLength(100)]
    public string? TriggeredBy { get; set; }

    /// <summary>
    /// Correlation ID for tracking.
    /// </summary>
    [MaxLength(100)]
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Parameters used for this run (resolved from job + overrides).
    /// </summary>
    [Column(TypeName = "json")]
    public string? ResolvedParameters { get; set; }

    /// <summary>
    /// Start time.
    /// </summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>
    /// End time.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// Duration.
    /// </summary>
    public TimeSpan? Duration => CompletedAt.HasValue && StartedAt.HasValue
        ? CompletedAt.Value - StartedAt.Value
        : null;

    /// <summary>
    /// Error message if failed.
    /// </summary>
    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Error code if failed.
    /// </summary>
    [MaxLength(100)]
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Number of retry attempts.
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Maximum retries allowed.
    /// </summary>
    public int MaxRetries { get; set; } = 0;

    /// <summary>
    /// Whether this run was cancelled.
    /// </summary>
    public bool WasCancelled { get; set; } = false;

    /// <summary>
    /// Cancellation reason.
    /// </summary>
    [MaxLength(500)]
    public string? CancellationReason { get; set; }

    /// <summary>
    /// Navigation property for task runs.
    /// </summary>
    public virtual ICollection<TaskRun> TaskRuns { get; set; } = new List<TaskRun>();

    /// <summary>
    /// Marks the job run as started.
    /// </summary>
    public void MarkStarted(Guid instanceId, string? correlationId = null)
    {
        InstanceId = instanceId;
        State = TaskState.Running;
        StartedAt = DateTimeOffset.UtcNow;
        CorrelationId = correlationId;
        Touch();
    }

    /// <summary>
    /// Marks the job run as completed successfully.
    /// </summary>
    public void MarkSucceeded()
    {
        State = TaskState.Succeeded;
        CompletedAt = DateTimeOffset.UtcNow;
        Touch();
    }

    /// <summary>
    /// Marks the job run as failed.
    /// </summary>
    public void MarkFailed(string errorCode, string errorMessage)
    {
        State = TaskState.Failed;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        CompletedAt = DateTimeOffset.UtcNow;
        Touch();
    }

    /// <summary>
    /// Marks the job run as timed out.
    /// </summary>
    public void MarkTimedOut()
    {
        State = TaskState.TimedOut;
        ErrorCode = "TASK_TIMEOUT";
        ErrorMessage = "Job run timed out";
        CompletedAt = DateTimeOffset.UtcNow;
        Touch();
    }

    /// <summary>
    /// Marks the job run as cancelled.
    /// </summary>
    public void MarkCancelled(string reason)
    {
        State = TaskState.Cancelled;
        WasCancelled = true;
        CancellationReason = reason;
        CompletedAt = DateTimeOffset.UtcNow;
        Touch();
    }

    /// <summary>
    /// Increments retry count.
    /// </summary>
    public void IncrementRetry()
    {
        RetryCount++;
        State = TaskState.Pending;
        ErrorCode = null;
        ErrorMessage = null;
        Touch();
    }

    /// <summary>
    /// Checks if the job run can be retried.
    /// </summary>
    public bool CanRetry()
        => State.IsFailed() && RetryCount < MaxRetries;
}