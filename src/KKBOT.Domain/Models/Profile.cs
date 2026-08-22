// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KKBOT.Domain.Models;

/// <summary>
/// Represents a configuration profile for an instance.
/// Contains all settings needed to run a module on a device.
/// </summary>
public class Profile : Entity
{
    /// <summary>
    /// Unique key for the profile.
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
    /// Foreign key to the module this profile is for.
    /// </summary>
    [Required]
    public Guid ModuleId { get; set; }

    /// <summary>
    /// Navigation property to the module.
    /// </summary>
    [ForeignKey(nameof(ModuleId))]
    public virtual Module Module { get; set; } = null!;

    /// <summary>
    /// Module version this profile targets.
    /// </summary>
    [MaxLength(50)]
    [Required]
    public string ModuleVersion { get; set; } = string.Empty;

    /// <summary>
    /// Profile version (incremented on each change).
    /// </summary>
    [Required]
    public int Version { get; set; } = 1;

    /// <summary>
    /// Whether this is the active version.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Whether the profile is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Display configuration (resolution, scale, etc.).
    /// </summary>
    [Column(TypeName = "json")]
    public string? DisplayConfig { get; set; }

    /// <summary>
    /// Perception configuration (confidence thresholds, capture interval, etc.).
    /// </summary>
    [Column(TypeName = "json")]
    public string? PerceptionConfig { get; set; }

    /// <summary>
    /// Policy configuration (unknown state handling, recovery, approvals).
    /// </summary>
    [Column(TypeName = "json")]
    public string? PolicyConfig { get; set; }

    /// <summary>
    /// Schedule configuration.
    /// </summary>
    [Column(TypeName = "json")]
    public string? ScheduleConfig { get; set; }

    /// <summary>
    /// Recovery configuration.
    /// </summary>
    [Column(TypeName = "json")]
    public string? RecoveryConfig { get; set; }

    /// <summary>
    /// Logging configuration.
    /// </summary>
    [Column(TypeName = "json")]
    public string? LoggingConfig { get; set; }

    /// <summary>
    /// Permissions configuration.
    /// </summary>
    [Column(TypeName = "json")]
    public string? PermissionsConfig { get; set; }

    /// <summary>
    /// Device-specific overrides.
    /// </summary>
    [Column(TypeName = "json")]
    public string? DeviceOverrides { get; set; }

    /// <summary>
    /// Custom configuration (free-form JSON).
    /// </summary>
    [Column(TypeName = "json")]
    public string? CustomConfig { get; set; }

    /// <summary>
    /// Tags for categorization.
    /// </summary>
    [MaxLength(500)]
    public string? Tags { get; set; }

    /// <summary>
    /// Navigation property for profile versions.
    /// </summary>
    public virtual ICollection<ProfileVersion> Versions { get; set; } = new List<ProfileVersion>();

    /// <summary>
    /// Navigation property for instances using this profile.
    /// </summary>
    public virtual ICollection<Instance> Instances { get; set; } = new List<Instance>();

    /// <summary>
    /// Navigation property for schedules.
    /// </summary>
    public virtual ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();

    /// <summary>
    /// Creates a new version snapshot of this profile.
    /// </summary>
    public ProfileVersion CreateVersion(string? changedBy = null, string? changeReason = null)
    {
        return new ProfileVersion
        {
            ProfileId = Id,
            Version = Version,
            DisplayConfig = DisplayConfig,
            PerceptionConfig = PerceptionConfig,
            PolicyConfig = PolicyConfig,
            ScheduleConfig = ScheduleConfig,
            RecoveryConfig = RecoveryConfig,
            LoggingConfig = LoggingConfig,
            PermissionsConfig = PermissionsConfig,
            DeviceOverrides = DeviceOverrides,
            CustomConfig = CustomConfig,
            ChangedBy = changedBy,
            ChangeReason = changeReason
        };
    }

    /// <summary>
    /// Increments the profile version.
    /// </summary>
    public void IncrementVersion()
    {
        Version++;
        Touch();
    }
}