// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace KKBOT.Domain.Enums;

/// <summary>
/// Represents the execution state of a task.
/// </summary>
public enum TaskState
{
    /// <summary>
    /// Task is pending execution.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Task is currently running.
    /// </summary>
    Running = 1,

    /// <summary>
    /// Task completed successfully.
    /// </summary>
    Succeeded = 2,

    /// <summary>
    /// Task failed.
    /// </summary>
    Failed = 3,

    /// <summary>
    /// Task timed out.
    /// </summary>
    TimedOut = 4,

    /// <summary>
    /// Task was cancelled.
    /// </summary>
    Cancelled = 5,

    /// <summary>
    /// Task is blocked waiting for a condition.
    /// </summary>
    Blocked = 6,

    /// <summary>
    /// Task requires approval before proceeding.
    /// </summary>
    RequiresApproval = 7
}

/// <summary>
/// Extension methods for TaskState.
/// </summary>
public static class TaskStateExtensions
{
    /// <summary>
    /// Determines if the state is a terminal state.
    /// </summary>
    public static bool IsTerminal(this TaskState state)
        => state is TaskState.Succeeded or TaskState.Failed or TaskState.TimedOut or TaskState.Cancelled;

    /// <summary>
    /// Determines if the state indicates the task is still active.
    /// </summary>
    public static bool IsActive(this TaskState state)
        => state is TaskState.Pending or TaskState.Running or TaskState.Blocked or TaskState.RequiresApproval;

    /// <summary>
    /// Determines if the task completed successfully.
    /// </summary>
    public static bool IsSuccessful(this TaskState state)
        => state == TaskState.Succeeded;

    /// <summary>
    /// Determines if the task failed (including timeout).
    /// </summary>
    public static bool IsFailed(this TaskState state)
        => state is TaskState.Failed or TaskState.TimedOut;

    /// <summary>
    /// Determines if the task can be retried.
    /// </summary>
    public static bool CanRetry(this TaskState state)
        => state is TaskState.Failed or TaskState.TimedOut or TaskState.Cancelled;

    /// <summary>
    /// Gets the display name for the state.
    /// </summary>
    public static string GetDisplayName(this TaskState state)
        => state switch
        {
            TaskState.Pending => "Pending",
            TaskState.Running => "Running",
            TaskState.Succeeded => "Succeeded",
            TaskState.Failed => "Failed",
            TaskState.TimedOut => "Timed Out",
            TaskState.Cancelled => "Cancelled",
            TaskState.Blocked => "Blocked",
            TaskState.RequiresApproval => "Requires Approval",
            _ => state.ToString()
        };

    /// <summary>
    /// Gets the severity color for UI display.
    /// </summary>
    public static string GetSeverityColor(this TaskState state)
        => state switch
        {
            TaskState.Pending => "#6c757d",
            TaskState.Running => "#0d6efd",
            TaskState.Succeeded => "#198754",
            TaskState.Failed => "#dc3545",
            TaskState.TimedOut => "#fd7e14",
            TaskState.Cancelled => "#6c757d",
            TaskState.Blocked => "#ffc107",
            TaskState.RequiresApproval => "#dc3545",
            _ => "#6c757d"
        };
}