// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KKBOT.Domain.Enums;

namespace KKBOT.Domain.Models;

/// <summary>
/// Represents an automation instance that runs on a device with a specific module and profile.
/// </summary>
public class Instance : Entity
{
    /// <summary>
    /// Unique key for the instance (human-readable).
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
    /// Current state of the instance.
    /// </summary>
    [Required]
    public InstanceState State { get; set; } = InstanceState.Created;

    /// <summary>
    /// Previous state (for transition tracking).
    /// </summary>
    public InstanceState? PreviousState { get; set; }

    /// <summary>
    /// Foreign key to the assigned device.
    /// </summary>
    public Guid? DeviceId { get; set; }

    /// <summary>
    /// Navigation property to the device.
    /// </summary>
    [ForeignKey(nameof(DeviceId))]
    public virtual Device? Device { get; set; }

    /// <summary>
    /// Foreign key to the assigned module.
    /// </summary>
    public Guid? ModuleId { get; set; }

    /// <summary>
    /// Navigation property to the module.
    /// </summary>
    [ForeignKey(nameof(ModuleId))]
    public virtual Module? Module { get; set; }

    /// <summary>
    /// Module version this instance is configured for.
    /// </summary>
    [MaxLength(50)]
    public string? ModuleVersion { get; set; }

    /// <summary>
    /// Foreign key to the assigned profile.
    /// </summary>
    public Guid? ProfileId { get; set; }

    /// <summary>
    /// Navigation property to the profile.
    /// </summary>
    [ForeignKey(nameof(ProfileId))]
    public virtual Profile? Profile { get; set; }

    /// <summary>
    /// Profile version this instance is using.
    /// </summary>
    [MaxLength(50)]
    public string? ProfileVersion { get; set; }

    /// <summary>
    /// Current task being executed.
    /// </summary>
    [MaxLength(100)]
    public string? CurrentTaskId { get; set; }

    /// <summary>
    /// Current job ID if running a job.
    /// </summary>
    public Guid? CurrentJobId { get; set; }

    /// <summary>
    /// Number of recovery attempts in current session.
    /// </summary>
    public int RecoveryAttempts { get; set; } = 0;

    /// <summary>
    /// Maximum recovery attempts allowed.
    /// </summary>
    public int MaxRecoveryAttempts { get; set; } = 3;

    /// <summary>
    /// Current recovery level if recovering.
    /// </summary>
    public RecoveryLevel? CurrentRecoveryLevel { get; set; }

    /// <summary>
    /// Last heartbeat timestamp.
    /// </summary>
    public DateTimeOffset? LastHeartbeat { get; set; }

    /// <summary>
    /// Heartbeat interval in seconds.
    /// </summary>
    public int HeartbeatIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// CPU usage percentage (last reported).
    /// </summary>
    public double? CpuUsagePercent { get; set; }

    /// <summary>
    /// Memory usage in MB (last reported).
    /// </summary>
    public long? MemoryUsageMb { get; set; }

    /// <summary>
    /// Error message if in failed state.
    /// </summary>
    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Error code if in failed state.
    /// </summary>
    [MaxLength(100)]
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Timestamp when the instance entered the current state.
    /// </summary>
    public DateTimeOffset StateEnteredAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Whether the instance is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Whether the instance should auto-start on host startup.
    /// </summary>
    public bool AutoStart { get; set; } = false;

    /// <summary>
    /// Priority for resource allocation (higher = more priority).
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Tags for grouping/filtering.
    /// </summary>
    [MaxLength(500)]
    public string? Tags { get; set; }

    /// <summary>
    /// Configuration override JSON (merges with profile config).
    /// </summary>
    [Column(TypeName = "json")]
    public string? ConfigOverride { get; set; }

    /// <summary>
    /// Last known frame timestamp.
    /// </summary>
    public DateTimeOffset? LastFrameAt { get; set; }

    /// <summary>
    /// Total frames captured.
    /// </summary>
    public long TotalFramesCaptured { get; set; } = 0;

    /// <summary>
    /// Total tasks executed.
    /// </summary>
    public long TotalTasksExecuted { get; set; } = 0;

    /// <summary>
    /// Total successful tasks.
    /// </summary>
    public long TotalTasksSucceeded { get; set; } = 0;

    /// <summary>
    /// Total failed tasks.
    /// </summary>
    public long TotalTasksFailed { get; set; } = 0;

    /// <summary>
    /// Navigation property for state transitions.
    /// </summary>
    public virtual ICollection<StateTransition> StateTransitions { get; set; } = new List<StateTransition>();

    /// <summary>
    /// Navigation property for task runs.
    /// </summary>
    public virtual ICollection<TaskRun> TaskRuns { get; set; } = new List<TaskRun>();

    /// <summary>
    /// Navigation property for health snapshots.
    /// </summary>
    public virtual ICollection<HealthSnapshot> HealthSnapshots { get; set; } = new List<HealthSnapshot>();

    /// <summary>
    /// Navigation property for recovery attempts.
    /// </summary>
    public virtual ICollection<RecoveryAttempt> RecoveryAttemptsHistory { get; set; } = new List<RecoveryAttempt>();

    /// <summary>
    /// Updates the instance state and records the transition.
    /// </summary>
    public void SetState(InstanceState newState, string? reason = null, string? triggeredBy = null)
    {
        if (State == newState) return;

        PreviousState = State;
        State = newState;
        StateEnteredAt = DateTimeOffset.UtcNow;
        Touch();

        var transition = new StateTransition
        {
            InstanceId = Id,
            FromState = PreviousState.Value,
            ToState = newState,
            Reason = reason,
            TriggeredBy = triggeredBy
        };
        StateTransitions.Add(transition);
    }

    /// <summary>
    /// Records a heartbeat.
    /// </summary>
    public void RecordHeartbeat(double? cpuUsage = null, long? memoryMb = null)
    {
        LastHeartbeat = DateTimeOffset.UtcNow;
        if (cpuUsage.HasValue) CpuUsagePercent = cpuUsage.Value;
        if (memoryMb.HasValue) MemoryUsageMb = memoryMb.Value;
        Touch();
    }

    /// <summary>
    /// Records a frame capture.
    /// </summary>
    public void RecordFrameCapture()
    {
        TotalFramesCaptured++;
        LastFrameAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Records a task completion.
    /// </summary>
    public void RecordTaskCompletion(bool succeeded)
    {
        TotalTasksExecuted++;
        if (succeeded)
            TotalTasksSucceeded++;
        else
            TotalTasksFailed++;
    }

    /// <summary>
    /// Increments recovery attempt counter.
    /// </summary>
    public void IncrementRecoveryAttempt(RecoveryLevel level)
    {
        RecoveryAttempts++;
        CurrentRecoveryLevel = level;
        Touch();
    }

    /// <summary>
    /// Resets recovery counter.
    /// </summary>
    public void ResetRecoveryCounter()
    {
        RecoveryAttempts = 0;
        CurrentRecoveryLevel = null;
        Touch();
    }

    /// <summary>
    /// Sets an error on the instance.
    /// </summary>
    public void SetError(string errorCode, string errorMessage)
    {
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        Touch();
    }

    /// <summary>
    /// Clears the error.
    /// </summary>
    public void ClearError()
    {
        ErrorCode = null;
        ErrorMessage = null;
        Touch();
    }

    /// <summary>
    /// Checks if the instance is healthy (heartbeat recent).
    /// </summary>
    public bool IsHealthy(TimeSpan? threshold = null)
    {
        if (!LastHeartbeat.HasValue) return false;
        var thresh = threshold ?? TimeSpan.FromSeconds(HeartbeatIntervalSeconds * 3);
        return DateTimeOffset.UtcNow - LastHeartbeat.Value < thresh;
    }

    /// <summary>
    /// Checks if the instance can be started.
    /// </summary>
    public bool CanStart()
        => IsEnabled && State is InstanceState.Created or InstanceState.Stopped or InstanceState.Failed
        && DeviceId.HasValue && ModuleId.HasValue && ProfileId.HasValue;

    /// <summary>
    /// Checks if the instance can be stopped.
    /// </summary>
    public bool CanStop()
        => State is InstanceState.Running or InstanceState.Paused or InstanceState.Waiting
            or InstanceState.Recovering or InstanceState.Ready;
}