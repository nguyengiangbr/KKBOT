// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace KKBOT.Contracts.Instances;

/// <summary>
/// Request to create a new instance.
/// </summary>
public record CreateInstanceRequest
{
    /// <summary>
    /// Instance key (human-readable identifier).
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// Display name.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Description.
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; init; }

    /// <summary>
    /// Device ID to assign.
    /// </summary>
    [Required]
    public Guid DeviceId { get; init; }

    /// <summary>
    /// Module ID to assign.
    /// </summary>
    [Required]
    public Guid ModuleId { get; init; }

    /// <summary>
    /// Profile ID to assign.
    /// </summary>
    [Required]
    public Guid ProfileId { get; init; }

    /// <summary>
    /// Whether to auto-start the instance.
    /// </summary>
    public bool AutoStart { get; init; } = false;

    /// <summary>
    /// Priority for resource allocation.
    /// </summary>
    public int Priority { get; init; } = 0;

    /// <summary>
    /// Tags for grouping.
    /// </summary>
    public string[]? Tags { get; init; }
}

/// <summary>
/// Request to update an existing instance.
/// </summary>
public record UpdateInstanceRequest
{
    /// <summary>
    /// Display name.
    /// </summary>
    [MaxLength(200)]
    public string? DisplayName { get; init; }

    /// <summary>
    /// Description.
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; init; }

    /// <summary>
    /// Device ID to assign.
    /// </summary>
    public Guid? DeviceId { get; init; }

    /// <summary>
    /// Profile ID to assign.
    /// </summary>
    public Guid? ProfileId { get; init; }

    /// <summary>
    /// Whether the instance is enabled.
    /// </summary>
    public bool? IsEnabled { get; init; }

    /// <summary>
    /// Whether to auto-start the instance.
    /// </summary>
    public bool? AutoStart { get; init; }

    /// <summary>
    /// Priority for resource allocation.
    /// </summary>
    public int? Priority { get; init; }

    /// <summary>
    /// Tags for grouping.
    /// </summary>
    public string[]? Tags { get; init; }

    /// <summary>
    /// Maximum recovery attempts.
    /// </summary>
    public int? MaxRecoveryAttempts { get; init; }

    /// <summary>
    /// Heartbeat interval in seconds.
    /// </summary>
    public int? HeartbeatIntervalSeconds { get; init; }
}

/// <summary>
/// Instance summary DTO for list views.
/// </summary>
public record InstanceSummaryDto
{
    /// <summary>
    /// Instance ID.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Instance key.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// Display name.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Current state.
    /// </summary>
    public string State { get; init; } = string.Empty;

    /// <summary>
    /// State display color.
    /// </summary>
    public string StateColor { get; init; } = string.Empty;

    /// <summary>
    /// Device display name.
    /// </summary>
    public string? DeviceName { get; init; }

    /// <summary>
    /// Module display name.
    /// </summary>
    public string? ModuleName { get; init; }

    /// <summary>
    /// Profile display name.
    /// </summary>
    public string? ProfileName { get; init; }

    /// <summary>
    /// Current task.
    /// </summary>
    public string? CurrentTaskId { get; init; }

    /// <summary>
    /// CPU usage percentage.
    /// </summary>
    public double? CpuUsagePercent { get; init; }

    /// <summary>
    /// Memory usage in MB.
    /// </summary>
    public long? MemoryUsageMb { get; init; }

    /// <summary>
    /// Whether the instance is healthy.
    /// </summary>
    public bool IsHealthy { get; init; }

    /// <summary>
    /// Last heartbeat timestamp.
    /// </summary>
    public DateTimeOffset? LastHeartbeat { get; init; }

    /// <summary>
    /// Total tasks executed.
    /// </summary>
    public long TotalTasksExecuted { get; init; }

    /// <summary>
    /// Total tasks succeeded.
    /// </summary>
    public long TotalTasksSucceeded { get; init; }

    /// <summary>
    /// Total tasks failed.
    /// </summary>
    public long TotalTasksFailed { get; init; }

    /// <summary>
    /// Success rate (0-1).
    /// </summary>
    public double SuccessRate { get; init; }

    /// <summary>
    /// Error message if in failed state.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Created at timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// Detailed instance DTO.
/// </summary>
public record InstanceDetailDto : InstanceSummaryDto
{
    /// <summary>
    /// Description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Device ID.
    /// </summary>
    public Guid? DeviceId { get; init; }

    /// <summary>
    /// Module ID.
    /// </summary>
    public Guid? ModuleId { get; init; }

    /// <summary>
    /// Module version.
    /// </summary>
    public string? ModuleVersion { get; init; }

    /// <summary>
    /// Profile ID.
    /// </summary>
    public Guid? ProfileId { get; init; }

    /// <summary>
    /// Profile version.
    /// </summary>
    public string? ProfileVersion { get; init; }

    /// <summary>
    /// Max recovery attempts.
    /// </summary>
    public int MaxRecoveryAttempts { get; init; }

    /// <summary>
    /// Current recovery attempts.
    /// </summary>
    public int RecoveryAttempts { get; init; }

    /// <summary>
    /// Current recovery level.
    /// </summary>
    public string? CurrentRecoveryLevel { get; init; }

    /// <summary>
    /// Heartbeat interval in seconds.
    /// </summary>
    public int HeartbeatIntervalSeconds { get; init; }

    /// <summary>
    /// Whether auto-start is enabled.
    /// </summary>
    public bool AutoStart { get; init; }

    /// <summary>
    /// Priority.
    /// </summary>
    public int Priority { get; init; }

    /// <summary>
    /// Tags.
    /// </summary>
    public string[]? Tags { get; init; }

    /// <summary>
    /// Total frames captured.
    /// </summary>
    public long TotalFramesCaptured { get; init; }

    /// <summary>
    /// State history.
    /// </summary>
    public IReadOnlyList<StateTransitionDto>? StateHistory { get; init; }

    /// <summary>
    /// Recent errors.
    /// </summary>
    public IReadOnlyList<ErrorEventDto>? RecentErrors { get; init; }

    /// <summary>
    /// Recent health snapshots.
    /// </summary>
    public IReadOnlyList<HealthSnapshotDto>? RecentHealthSnapshots { get; init; }
}

/// <summary>
/// State transition DTO.
/// </summary>
public record StateTransitionDto
{
    /// <summary>
    /// From state.
    /// </summary>
    public string FromState { get; init; } = string.Empty;

    /// <summary>
    /// To state.
    /// </summary>
    public string ToState { get; init; } = string.Empty;

    /// <summary>
    /// Reason.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// When the transition occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Error event DTO.
/// </summary>
public record ErrorEventDto
{
    /// <summary>
    /// Error code.
    /// </summary>
    public string ErrorCode { get; init; } = string.Empty;

    /// <summary>
    /// Error message.
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// Severity.
    /// </summary>
    public string Severity { get; init; } = string.Empty;

    /// <summary>
    /// When the error occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Health snapshot DTO.
/// </summary>
public record HealthSnapshotDto
{
    /// <summary>
    /// Timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// CPU usage percentage.
    /// </summary>
    public double CpuUsagePercent { get; init; }

    /// <summary>
    /// Memory usage in MB.
    /// </summary>
    public long MemoryUsageMb { get; init; }

    /// <summary>
    /// Whether the instance is healthy.
    /// </summary>
    public bool IsHealthy { get; init; }

    /// <summary>
    /// Instance state.
    /// </summary>
    public string InstanceState { get; init; } = string.Empty;

    /// <summary>
    /// Current task ID.
    /// </summary>
    public string? CurrentTaskId { get; init; }

    /// <summary>
    /// Task progress.
    /// </summary>
    public int TaskProgressPercent { get; init; }

    /// <summary>
    /// Frames captured.
    /// </summary>
    public long TotalFramesCaptured { get; init; }
}