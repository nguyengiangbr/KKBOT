// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace KKBOT.Contracts.Profiles;

/// <summary>
/// Request to create a new profile.
/// </summary>
public record CreateProfileRequest
{
    [Required, MaxLength(100)]
    public string Key { get; init; } = string.Empty;

    [Required, MaxLength(200)]
    public string DisplayName { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; init; }

    [Required]
    public Guid ModuleId { get; init; }

    [Required, MaxLength(50)]
    public string ModuleVersion { get; init; } = "1.0.0";

    public ProfileSettingsDto? DisplayConfig { get; init; }

    public ProfileSettingsDto? PerceptionConfig { get; init; }

    public ProfileSettingsDto? PolicyConfig { get; init; }

    public ProfileSettingsDto? ScheduleConfig { get; init; }

    public ProfileSettingsDto? RecoveryConfig { get; init; }

    public ProfileSettingsDto? LoggingConfig { get; init; }

    public string[]? Tags { get; init; }
}

/// <summary>
/// Profile settings DTO (represents any config section).
/// </summary>
public record ProfileSettingsDto
{
    public Dictionary<string, object?>? Values { get; init; }
}

/// <summary>
/// Profile summary DTO.
/// </summary>
public record ProfileSummaryDto
{
    public Guid Id { get; init; }
    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string ModuleName { get; init; } = string.Empty;
    public string ModuleVersion { get; init; } = string.Empty;
    public int Version { get; init; }
    public bool IsActive { get; init; }
    public bool IsEnabled { get; init; }
    public int InstanceCount { get; init; }
    public string[]? Tags { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// Profile detail DTO.
/// </summary>
public record ProfileDetailDto : ProfileSummaryDto
{
    public string? Description { get; init; }
    public Guid ModuleId { get; init; }
    public ProfileSettingsDto? DisplayConfig { get; init; }
    public ProfileSettingsDto? PerceptionConfig { get; init; }
    public ProfileSettingsDto? PolicyConfig { get; init; }
    public ProfileSettingsDto? ScheduleConfig { get; init; }
    public ProfileSettingsDto? RecoveryConfig { get; init; }
    public ProfileSettingsDto? LoggingConfig { get; init; }
    public ProfileSettingsDto? PermissionsConfig { get; init; }
    public IReadOnlyList<ProfileVersionDto>? Versions { get; init; }
}

/// <summary>
/// Profile version DTO.
/// </summary>
public record ProfileVersionDto
{
    public Guid Id { get; init; }
    public int Version { get; init; }
    public string? ChangedBy { get; init; }
    public string? ChangeReason { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public bool IsRollbackTarget { get; init; }
}

/// <summary>
/// Request to rollback a profile to a previous version.
/// </summary>
public record RollbackProfileRequest
{
    [Required]
    public Guid TargetVersionId { get; init; }

    [MaxLength(500)]
    public string? Reason { get; init; }
}