// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KKBOT.Domain.Enums;

namespace KKBOT.Domain.Models;

/// <summary>
/// Represents an execution of a task within a job run.
/// </summary>
public class TaskRun : Entity
{
    /// <summary>
    /// Foreign key to the job run.
    /// </summary>
    [Required]
    public Guid JobRunId { get; set; }

    /// <summary>
    /// Navigation property to the job run.
    /// </summary>
    [ForeignKey(nameof(JobRunId))]
    public virtual JobRun JobRun { get; set; } = null!;

    /// <summary>
    /// Foreign key to the instance.
    /// </summary>
    [Required]
    public Guid InstanceId { get; set; }

    /// <summary>
    /// Navigation property to the instance.
    /// </summary>
    [ForeignKey(nameof(InstanceId))]
    public virtual Instance Instance { get; set; } = null!;

    /// <summary>
    /// Task ID (from module definition).
    /// </summary>
    [MaxLength(100)]
    [Required]
    public string TaskId { get; set; } = string.Empty;

    /// <summary>
    /// Task name (human-readable).
    /// </summary>
    [MaxLength(200)]
    public string? TaskName { get; set; }

    /// <summary>
    /// Current state of the task run.
    /// </summary>
    [Required]
    public TaskState State { get; set; } = TaskState.Pending;

    /// <summary>
    /// Task parameters (JSON).
    /// </summary>
    [Column(TypeName = "json")]
    public string? Parameters { get; set; }

    /// <summary>
    /// Task result data (JSON).
    /// </summary>
    [Column(TypeName = "json")]
    public string? Result { get; set; }

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
    /// Timeout for this task.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

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
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Current step in the task (for multi-step tasks).
    /// </summary>
    [MaxLength(100)]
    public string? CurrentStep { get; set; }

    /// <summary>
    /// Total steps in the task.
    /// </summary>
    public int TotalSteps { get; set; } = 1;

    /// <summary>
    /// Completed steps.
    /// </summary>
    public int CompletedSteps { get; set; } = 0;

    /// <summary>
    /// Progress percentage (0-100).
    /// </summary>
    public int ProgressPercent { get; set; } = 0;

    /// <summary>
    /// Navigation property for state transitions.
    /// </summary>
    public virtual ICollection<StateTransition> StateTransitions { get; set; } = new List<StateTransition>();

    /// <summary>
    /// Marks the task run as started.
    /// </summary>
    public void MarkStarted()
    {
        State = TaskState.Running;
        StartedAt = DateTimeOffset.UtcNow;
        ProgressPercent = 0;
        Touch();
    }

    /// <summary>
    /// Updates task progress.
    /// </summary>
    public void UpdateProgress(int completedSteps, int totalSteps, string? currentStep = null)
    {
        CompletedSteps = completedSteps;
        TotalSteps = totalSteps;
        if (totalSteps > 0)
            ProgressPercent = (int)Math.Round((double)completedSteps / totalSteps * 100);
        if (currentStep != null)
            CurrentStep = currentStep;
        Touch();
    }

    /// <summary>
    /// Marks the task run as completed successfully.
    /// </summary>
    public void MarkSucceeded(string? result = null)
    {
        State = TaskState.Succeeded;
        CompletedAt = DateTimeOffset.UtcNow;
        CompletedSteps = TotalSteps;
        ProgressPercent = 100;
        if (result != null)
            Result = result;
        Touch();
    }

    /// <summary>
    /// Marks the task run as failed.
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
    /// Marks the task run as timed out.
    /// </summary>
    public void MarkTimedOut()
    {
        State = TaskState.TimedOut;
        ErrorCode = "TASK_TIMEOUT";
        ErrorMessage = $"Task '{TaskId}' timed out";
        CompletedAt = DateTimeOffset.UtcNow;
        Touch();
    }

    /// <summary>
    /// Marks the task run as cancelled.
    /// </summary>
    public void MarkCancelled(string reason)
    {
        State = TaskState.Cancelled;
        ErrorCode = "TASK_CANCELLED";
        ErrorMessage = reason;
        CompletedAt = DateTimeOffset.UtcNow;
        Touch();
    }

    /// <summary>
    /// Marks the task run as blocked.
    /// </summary>
    public void MarkBlocked(string reason)
    {
        State = TaskState.Blocked;
        ErrorMessage = reason;
        Touch();
    }

    /// <summary>
    /// Marks the task run as requiring approval.
    /// </summary>
    public void MarkRequiresApproval(string reason)
    {
        State = TaskState.RequiresApproval;
        ErrorMessage = reason;
        Touch();
    }

    /// <summary>
    /// Resumes a blocked/approval task.
    /// </summary>
    public void Resume()
    {
        if (State == TaskState.Blocked || State == TaskState.RequiresApproval)
        {
            State = TaskState.Running;
            ErrorMessage = null;
            Touch();
        }
    }

    /// <summary>
    /// Increments retry count and resets state.
    /// </summary>
    public void IncrementRetry()
    {
        RetryCount++;
        State = TaskState.Pending;
        ErrorCode = null;
        ErrorMessage = null;
        StartedAt = null;
        CompletedAt = null;
        ProgressPercent = 0;
        CompletedSteps = 0;
        Touch();
    }

    /// <summary>
    /// Checks if the task run can be retried.
    /// </summary>
    public bool CanRetry()
        => State.IsFailed() && RetryCount < MaxRetries;
}