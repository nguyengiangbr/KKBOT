// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace KKBOT.Contracts.Jobs;

/// <summary>
/// Request to create a job.
/// </summary>
public record CreateJobRequest
{
    [Required, MaxLength(100)]
    public string Key { get; init; } = string.Empty;

    [Required, MaxLength(200)]
    public string DisplayName { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; init; }

    [Required]
    public Guid ModuleId { get; init; }

    [Required, MaxLength(100)]
    public string TaskId { get; init; } = string.Empty;

    public Dictionary<string, object?>? Parameters { get; init; }

    public int Priority { get; init; } = 0;

    public int MaxConcurrency { get; init; } = 1;

    public TimeSpan? Timeout { get; init; }

    public RetryPolicyDto? RetryPolicy { get; init; }

    public bool RequiresApproval { get; init; }

    public string[]? Tags { get; init; }
}

/// <summary>
/// Retry policy DTO.
/// </summary>
public record RetryPolicyDto
{
    public int MaxRetries { get; init; } = 3;
    public TimeSpan? RetryDelay { get; init; }
    public bool ExponentialBackoff { get; init; } = true;
    public double BackoffMultiplier { get; init; } = 2.0;
    public TimeSpan? MaxRetryDelay { get; init; }
    public string[]? RetryableErrorCodes { get; init; }
}

/// <summary>
/// Request to run a job.
/// </summary>
public record RunJobRequest
{
    [Required]
    public Guid JobId { get; init; }

    [Required]
    public Guid InstanceId { get; init; }

    public Dictionary<string, object?>? ParameterOverrides { get; init; }

    [MaxLength(100)]
    public string? CorrelationId { get; init; }
}

/// <summary>
/// Job summary DTO.
/// </summary>
public record JobSummaryDto
{
    public Guid Id { get; init; }
    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string ModuleName { get; init; } = string.Empty;
    public string TaskId { get; init; } = string.Empty;
    public int Priority { get; init; }
    public bool IsEnabled { get; init; }
    public bool RequiresApproval { get; init; }
    public int TotalRuns { get; init; }
    public int SuccessfulRuns { get; init; }
    public int FailedRuns { get; init; }
    public double SuccessRate { get; init; }
    public string[]? Tags { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// Job run summary DTO.
/// </summary>
public record JobRunSummaryDto
{
    public Guid Id { get; init; }
    public Guid JobId { get; init; }
    public string? InstanceName { get; init; }
    public string State { get; init; } = string.Empty;
    public string StateColor { get; init; } = string.Empty;
    public string TriggerType { get; init; } = string.Empty;
    public string? TriggeredBy { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public TimeSpan? Duration { get; init; }
    public string? ErrorMessage { get; init; }
    public int RetryCount { get; init; }
    public bool WasCancelled { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// Task run DTO.
/// </summary>
public record TaskRunDto
{
    public Guid Id { get; init; }
    public string TaskId { get; init; } = string.Empty;
    public string? TaskName { get; init; }
    public string State { get; init; } = string.Empty;
    public string StateColor { get; init; } = string.Empty;
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public TimeSpan? Duration { get; init; }
    public string? ErrorMessage { get; init; }
    public int RetryCount { get; init; }
    public int ProgressPercent { get; init; }
    public string? CurrentStep { get; init; }
    public int TotalSteps { get; init; }
}