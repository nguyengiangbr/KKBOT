// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace KKBOT.Contracts.Dashboard;

/// <summary>
/// Dashboard summary data for the overview page.
/// </summary>
public record DashboardSummaryDto
{
    public int TotalInstances { get; init; }
    public int RunningInstances { get; init; }
    public int PausedInstances { get; init; }
    public int RecoveringInstances { get; init; }
    public int FailedInstances { get; init; }
    public int ManualReviewInstances { get; init; }
    public double AverageCpuUsage { get; init; }
    public double AverageMemoryUsageMb { get; init; }
    public long TotalFramesCaptured { get; init; }
    public long TotalTasksExecuted { get; init; }
    public long TotalTasksSucceeded { get; init; }
    public long TotalTasksFailed { get; init; }
    public double OverallSuccessRate { get; init; }
    public int ActiveJobs { get; init; }
    public int QueueDepth { get; init; }
    public long FrameDropTotal { get; init; }
    public double AverageCaptureLatencyMs { get; init; }
    public double AveragePerceptionLatencyMs { get; init; }
    public int DevicesOnline { get; init; }
    public int DevicesOffline { get; init; }
    public int DevicesAvailable { get; init; }
}

/// <summary>
/// Real-time update event sent over SignalR.
/// </summary>
public record InstanceStateChangedEvent
{
    public Guid InstanceId { get; init; }
    public string PreviousState { get; init; } = string.Empty;
    public string NewState { get; init; } = string.Empty;
    public string? Reason { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Task progress update event.
/// </summary>
public record TaskProgressEvent
{
    public Guid InstanceId { get; init; }
    public Guid TaskRunId { get; init; }
    public string TaskId { get; init; } = string.Empty;
    public int ProgressPercent { get; init; }
    public string? CurrentStep { get; init; }
    public int CompletedSteps { get; init; }
    public int TotalSteps { get; init; }
    public string State { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Health alert event.
/// </summary>
public record HealthAlertEvent
{
    public string AlertType { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public Guid? InstanceId { get; init; }
    public string? ErrorCode { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Dashboard metrics update event.
/// </summary>
public record DashboardMetricsEvent
{
    public int TotalInstances { get; init; }
    public int RunningInstances { get; init; }
    public double AverageCpuUsage { get; init; }
    public double AverageMemoryUsageMb { get; init; }
    public long TotalTasksExecuted { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// System status DTO.
/// </summary>
public record SystemStatusDto
{
    public string Version { get; init; } = string.Empty;
    public string RuntimeVersion { get; init; } = string.Empty;
    public TimeSpan Uptime { get; init; }
    public string DatabaseType { get; init; } = string.Empty;
    public bool DatabaseConnected { get; init; }
    public bool SignalRConnected { get; init; }
    public long TotalOperations { get; init; }
    public DateTimeOffset StartedAt { get; init; }
}

/// <summary>
/// Paginated response wrapper.
/// </summary>
public record PagedResponse<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

/// <summary>
/// Standard API response wrapper.
/// </summary>
public record ApiResponse<T>
{
    public bool IsSuccess { get; init; }
    public T? Data { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public string? CorrelationId { get; init; }

    public static ApiResponse<T> Success(T data) => new()
    {
        IsSuccess = true,
        Data = data
    };

    public static ApiResponse<T> Failure(string errorCode, string errorMessage, string? correlationId = null) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
        CorrelationId = correlationId
    };
}