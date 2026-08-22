// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KKBOT.Domain.Enums;

namespace KKBOT.Domain.Models;

/// <summary>
/// Represents a recovery attempt on an instance.
/// </summary>
public class RecoveryAttempt : Entity
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
    /// Recovery level tried.
    /// </summary>
    [Required]
    public RecoveryLevel Level { get; set; }

    /// <summary>
    /// Recovery attempt number (1-based within same incident).
    /// </summary>
    public int AttemptNumber { get; set; } = 1;

    /// <summary>
    /// Timestamp when recovery started.
    /// </summary>
    [Required]
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Timestamp when recovery completed.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// Duration of the recovery attempt.
    /// </summary>
    public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : null;

    /// <summary>
    /// Whether the recovery was successful.
    /// </summary>
    public bool IsSuccessful { get; set; } = false;

    /// <summary>
    /// The error code that triggered recovery.
    /// </summary>
    [MaxLength(100)]
    public string? TriggerErrorCode { get; set; }

    /// <summary>
    /// Description of the error that triggered recovery.
    /// </summary>
    [MaxLength(1000)]
    public string? TriggerErrorMessage { get; set; }

    /// <summary>
    /// Result of the recovery (JSON).
    /// </summary>
    [Column(TypeName = "json")]
    public string? RecoveryResult { get; set; }

    /// <summary>
    /// Whether this attempt was the final attempt.
    /// </summary>
    public bool IsFinalAttempt { get; set; } = false;

    /// <summary>
    /// Recovery executor details.
    /// </summary>
    [MaxLength(500)]
    public string? Details { get; set; }
}

/// <summary>
/// Represents an audit event for security and compliance.
/// </summary>
public class AuditEvent : Entity
{
    /// <summary>
    /// Timestamp of the event.
    /// </summary>
    [Required]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Who performed the action.
    /// </summary>
    [MaxLength(100)]
    [Required]
    public string Actor { get; set; } = string.Empty;

    /// <summary>
    /// Actor type (operator, system, api).
    /// </summary>
    [MaxLength(50)]
    [Required]
    public string ActorType { get; set; } = string.Empty;

    /// <summary>
    /// What action was performed.
    /// </summary>
    [MaxLength(100)]
    [Required]
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Target type (instance, profile, module, system, etc.).
    /// </summary>
    [MaxLength(50)]
    public string? TargetType { get; set; }

    /// <summary>
    /// Target ID.
    /// </summary>
    [MaxLength(100)]
    public string? TargetId { get; set; }

    /// <summary>
    /// Pre-action state/value (JSON).
    /// </summary>
    [Column(TypeName = "json")]
    public string? BeforeValue { get; set; }

    /// <summary>
    /// Post-action state/value (JSON).
    /// </summary>
    [Column(TypeName = "json")]
    public string? AfterValue { get; set; }

    /// <summary>
    /// Result of the action (Success, Failure, Denied).
    /// </summary>
    [MaxLength(50)]
    [Required]
    public string Result { get; set; } = "Success";

    /// <summary>
    /// Failure reason if result is Failure/Denied.
    /// </summary>
    [MaxLength(1000)]
    public string? FailureReason { get; set; }

    /// <summary>
    /// Correlation ID for tracking.
    /// </summary>
    [MaxLength(100)]
    public string? CorrelationId { get; set; }

    /// <summary>
    /// IP address or origin of the request.
    /// </summary>
    [MaxLength(50)]
    public string? SourceIp { get; set; }

    /// <summary>
    /// Additional context (JSON).
    /// </summary>
    [Column(TypeName = "json")]
    public string? Context { get; set; }
}