// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KKBOT.Domain.Models;

/// <summary>
/// Represents a schedule for running jobs or starting instances.
/// </summary>
public class Schedule : Entity
{
    /// <summary>
    /// Unique key for the schedule.
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
    /// Foreign key to the profile (optional, for starting instances).
    /// </summary>
    public Guid? ProfileId { get; set; }

    /// <summary>
    /// Navigation property to the profile.
    /// </summary>
    [ForeignKey(nameof(ProfileId))]
    public virtual Profile? Profile { get; set; }

    /// <summary>
    /// Foreign key to the job (optional, for running jobs).
    /// </summary>
    public Guid? JobId { get; set; }

    /// <summary>
    /// Navigation property to the job.
    /// </summary>
    [ForeignKey(nameof(JobId))]
    public virtual Job? Job { get; set; }

    /// <summary>
    /// Cron expression for scheduling.
    /// </summary>
    [MaxLength(100)]
    public string? CronExpression { get; set; }

    /// <summary>
    /// Interval (for simple interval-based schedules).
    /// </summary>
    public TimeSpan? Interval { get; set; }

    /// <summary>
    /// Start time (for one-shot schedules).
    /// </summary>
    public DateTimeOffset? StartAt { get; set; }

    /// <summary>
    /// End time (optional).
    /// </summary>
    public DateTimeOffset? EndAt { get; set; }

    /// <summary>
    /// Timezone for the schedule.
    /// </summary>
    [MaxLength(50)]
    public string Timezone { get; set; } = "UTC";

    /// <summary>
    /// Whether the schedule is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Schedule type.
    /// </summary>
    [MaxLength(50)]
    [Required]
    public string ScheduleType { get; set; } = "Interval"; // Interval, Cron, OneShot

    /// <summary>
    /// Last run timestamp.
    /// </summary>
    public DateTimeOffset? LastRunAt { get; set; }

    /// <summary>
    /// Next run timestamp (calculated).
    /// </summary>
    public DateTimeOffset? NextRunAt { get; set; }

    /// <summary>
    /// Total runs executed.
    /// </summary>
    public long TotalRuns { get; set; } = 0;

    /// <summary>
    /// Successful runs.
    /// </summary>
    public long SuccessfulRuns { get; set; } = 0;

    /// <summary>
    /// Failed runs.
    /// </summary>
    public long FailedRuns { get; set; } = 0;

    /// <summary>
    /// Maximum number of runs (null = unlimited).
    /// </summary>
    public long? MaxRuns { get; set; }

    /// <summary>
    /// Parameters to pass to the job (JSON).
    /// </summary>
    [Column(TypeName = "json")]
    public string? Parameters { get; set; }

    /// <summary>
    /// Whether to skip if previous run is still active.
    /// </summary>
    public bool SkipIfRunning { get; set; } = true;

    /// <summary>
    /// Tags for categorization.
    /// </summary>
    [MaxLength(500)]
    public string? Tags { get; set; }

    /// <summary>
    /// Calculates the next run time.
    /// </summary>
    public DateTimeOffset? CalculateNextRun()
    {
        if (!IsEnabled) return null;

        if (StartAt.HasValue && StartAt.Value > DateTimeOffset.UtcNow)
        {
            return StartAt.Value;
        }

        if (EndAt.HasValue && EndAt.Value <= DateTimeOffset.UtcNow)
        {
            return null;
        }

        if (MaxRuns.HasValue && TotalRuns >= MaxRuns.Value)
        {
            return null;
        }

        if (Interval.HasValue)
        {
            var lastRun = LastRunAt ?? DateTimeOffset.UtcNow;
            return lastRun.Add(Interval.Value);
        }

        // Cron expression parsing would go here
        return null;
    }

    /// <summary>
    /// Records a schedule execution.
    /// </summary>
    public void RecordRun(bool succeeded)
    {
        LastRunAt = DateTimeOffset.UtcNow;
        TotalRuns++;
        if (succeeded)
            SuccessfulRuns++;
        else
            FailedRuns++;

        NextRunAt = CalculateNextRun();
        Touch();
    }
}

/// <summary>
/// Represents a configuration setting.
/// </summary>
public class ConfigurationSetting : Entity
{
    /// <summary>
    /// Setting key.
    /// </summary>
    [MaxLength(200)]
    [Required]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Setting value.
    /// </summary>
    [MaxLength(5000)]
    public string? Value { get; set; }

    /// <summary>
    /// Setting value type.
    /// </summary>
    [MaxLength(50)]
    public string ValueType { get; set; } = "string";

    /// <summary>
    /// Category/group.
    /// </summary>
    [MaxLength(100)]
    public string? Category { get; set; }

    /// <summary>
    /// Description of the setting.
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Default value.
    /// </summary>
    [MaxLength(5000)]
    public string? DefaultValue { get; set; }

    /// <summary>
    /// Validation regex or rules (JSON).
    /// </summary>
    [Column(TypeName = "json")]
    public string? ValidationRules { get; set; }

    /// <summary>
    /// Whether the setting is secret/encrypted.
    /// </summary>
    public bool IsSecret { get; set; } = false;

    /// <summary>
    /// Whether the setting is read-only.
    /// </summary>
    public bool IsReadOnly { get; set; } = false;

    /// <summary>
    /// Whether the setting is visible in UI.
    /// </summary>
    public bool IsVisible { get; set; } = true;
}