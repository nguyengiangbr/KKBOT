// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KKBOT.Domain.Enums;

namespace KKBOT.Domain.Models;

/// <summary>
/// Represents a state transition for an instance or task.
/// </summary>
public class StateTransition : Entity
{
    /// <summary>
    /// Foreign key to the instance (optional if related to a task).
    /// </summary>
    public Guid? InstanceId { get; set; }

    /// <summary>
    /// Navigation property to the instance.
    /// </summary>
    [ForeignKey(nameof(InstanceId))]
    public virtual Instance? Instance { get; set; }

    /// <summary>
    /// Foreign key to the task run (optional).
    /// </summary>
    public Guid? TaskRunId { get; set; }

    /// <summary>
    /// Navigation property to the task run.
    /// </summary>
    [ForeignKey(nameof(TaskRunId))]
    public virtual TaskRun? TaskRun { get; set; }

    /// <summary>
    /// Previous state.
    /// </summary>
    [Required]
    public InstanceState FromState { get; set; }

    /// <summary>
    /// New state.
    /// </summary>
    [Required]
    public InstanceState ToState { get; set; }

    /// <summary>
    /// Reason for the transition.
    /// </summary>
    [MaxLength(1000)]
    public string? Reason { get; set; }

    /// <summary>
    /// Who/what triggered the transition.
    /// </summary>
    [MaxLength(100)]
    public string? TriggeredBy { get; set; }

    /// <summary>
    /// Correlation ID for tracking.
    /// </summary>
    [MaxLength(100)]
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Additional metadata.
    /// </summary>
    [Column(TypeName = "json")]
    public string? Metadata { get; set; }
}

/// <summary>
/// Represents a perception snapshot (frame analysis result).
/// </summary>
public class PerceptionSnapshot : Entity
{
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
    /// Timestamp when the frame was captured.
    /// </summary>
    [Required]
    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Frame index/sequence number.
    /// </summary>
    public long FrameIndex { get; set; }

    /// <summary>
    /// Capture latency in milliseconds.
    /// </summary>
    public double CaptureLatencyMs { get; set; }

    /// <summary>
    /// Perception pipeline latency in milliseconds.
    /// </summary>
    public double PerceptionLatencyMs { get; set; }

    /// <summary>
    /// Total latency in milliseconds.
    /// </summary>
    public double TotalLatencyMs => CaptureLatencyMs + PerceptionLatencyMs;

    /// <summary>
    /// Detected state ID (from module).
    /// </summary>
    [MaxLength(100)]
    public string? DetectedStateId { get; set; }

    /// <summary>
    /// Detected state name.
    /// </summary>
    [MaxLength(200)]
    public string? DetectedStateName { get; set; }

    /// <summary>
    /// Overall confidence score (0-1).
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Minimum confidence across all detectors used.
    /// </summary>
    public double MinConfidence { get; set; }

    /// <summary>
    /// Whether the snapshot passed temporal validation.
    /// </summary>
    public bool IsTemporalValidated { get; set; } = false;

    /// <summary>
    /// Detector results (JSON array of detector outputs).
    /// </summary>
    [Column(TypeName = "json")]
    public string? DetectorResults { get; set; }

    /// <summary>
    /// OCR results (JSON).
    /// </summary>
    [Column(TypeName = "json")]
    public string? OcrResults { get; set; }

    /// <summary>
    /// Region of interest used (JSON).
    /// </summary>
    [Column(TypeName = "json")]
    public string? RoiUsed { get; set; }

    /// <summary>
    /// Path to the captured frame image (for debugging).
    /// </summary>
    [MaxLength(500)]
    public string? FramePath { get; set; }

    /// <summary>
    /// Whether this snapshot resulted in an unknown state.
    /// </summary>
    public bool IsUnknown { get; set; } = false;

    /// <summary>
    /// Whether this snapshot triggered a recovery.
    /// </summary>
    public bool TriggeredRecovery { get; set; } = false;

    /// <summary>
    /// Whether the frame was dropped (not processed).
    /// </summary>
    public bool WasFrameDropped { get; set; } = false;

    /// <summary>
    /// Frame drop reason.
    /// </summary>
    [MaxLength(500)]
    public string? FrameDropReason { get; set; }
}

/// <summary>
/// Represents a health snapshot of an instance.
/// </summary>
public class HealthSnapshot : Entity
{
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
    /// Timestamp of the snapshot.
    /// </summary>
    [Required]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// CPU usage percentage.
    /// </summary>
    public double CpuUsagePercent { get; set; }

    /// <summary>
    /// Memory usage in MB.
    /// </summary>
    public long MemoryUsageMb { get; set; }

    /// <summary>
    /// Whether the instance is healthy.
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Instance state at snapshot time.
    /// </summary>
    public InstanceState InstanceState { get; set; }

    /// <summary>
    /// Current task ID.
    /// </summary>
    [MaxLength(100)]
    public string? CurrentTaskId { get; set; }

    /// <summary>
    /// Current task progress.
    /// </summary>
    public int TaskProgressPercent { get; set; }

    /// <summary>
    /// Number of frames captured.
    /// </summary>
    public long TotalFramesCaptured { get; set; }

    /// <summary>
    /// Frames dropped since last snapshot.
    /// </summary>
    public int FramesDroppedSinceLastCheck { get; set; }

    /// <summary>
    /// Average capture latency since last snapshot.
    /// </summary>
    public double? AverageCaptureLatencyMs { get; set; }

    /// <summary>
    /// Average perception latency since last snapshot.
    /// </summary>
    public double? AveragePerceptionLatencyMs { get; set; }

    /// <summary>
    /// Errors since last snapshot.
    /// </summary>
    public int ErrorsSinceLastCheck { get; set; }

    /// <summary>
    /// Recovery attempts since last snapshot.
    /// </summary>
    public int RecoveryAttemptsSinceLastCheck { get; set; }

    /// <summary>
    /// Additional metrics (JSON).
    /// </summary>
    [Column(TypeName = "json")]
    public string? AdditionalMetrics { get; set; }
}