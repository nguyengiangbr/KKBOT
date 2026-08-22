// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KKBOT.Domain.Models;

/// <summary>
/// Represents a historical version of a profile for rollback/audit.
/// </summary>
public class ProfileVersion : Entity
{
    /// <summary>
    /// Foreign key to the profile.
    /// </summary>
    [Required]
    public Guid ProfileId { get; set; }

    /// <summary>
    /// Navigation property to the profile.
    /// </summary>
    [ForeignKey(nameof(ProfileId))]
    public virtual Profile Profile { get; set; } = null!;

    /// <summary>
    /// Version number.
    /// </summary>
    [Required]
    public int Version { get; set; }

    /// <summary>
    /// Display configuration snapshot.
    /// </summary>
    [Column(TypeName = "json")]
    public string? DisplayConfig { get; set; }

    /// <summary>
    /// Perception configuration snapshot.
    /// </summary>
    [Column(TypeName = "json")]
    public string? PerceptionConfig { get; set; }

    /// <summary>
    /// Policy configuration snapshot.
    /// </summary>
    [Column(TypeName = "json")]
    public string? PolicyConfig { get; set; }

    /// <summary>
    /// Schedule configuration snapshot.
    /// </summary>
    [Column(TypeName = "json")]
    public string? ScheduleConfig { get; set; }

    /// <summary>
    /// Recovery configuration snapshot.
    /// </summary>
    [Column(TypeName = "json")]
    public string? RecoveryConfig { get; set; }

    /// <summary>
    /// Logging configuration snapshot.
    /// </summary>
    [Column(TypeName = "json")]
    public string? LoggingConfig { get; set; }

    /// <summary>
    /// Permissions configuration snapshot.
    /// </summary>
    [Column(TypeName = "json")]
    public string? PermissionsConfig { get; set; }

    /// <summary>
    /// Device overrides snapshot.
    /// </summary>
    [Column(TypeName = "json")]
    public string? DeviceOverrides { get; set; }

    /// <summary>
    /// Custom configuration snapshot.
    /// </summary>
    [Column(TypeName = "json")]
    public string? CustomConfig { get; set; }

    /// <summary>
    /// Who made the change.
    /// </summary>
    [MaxLength(100)]
    public string? ChangedBy { get; set; }

    /// <summary>
    /// Reason for the change.
    /// </summary>
    [MaxLength(500)]
    public string? ChangeReason { get; set; }

    /// <summary>
    /// Whether this version can be rolled back to.
    /// </summary>
    public bool IsRollbackTarget { get; set; } = true;
}