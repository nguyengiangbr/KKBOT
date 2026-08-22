// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace KKBOT.Domain.Enums;

/// <summary>
/// Represents the lifecycle state of an instance.
/// </summary>
public enum InstanceState
{
    /// <summary>
    /// Instance has been created but not yet provisioned.
    /// </summary>
    Created = 0,

    /// <summary>
    /// Instance is being provisioned (allocating resources, etc.).
    /// </summary>
    Provisioning = 1,

    /// <summary>
    /// Instance is connecting to the device.
    /// </summary>
    Connecting = 2,

    /// <summary>
    /// Instance is ready to run tasks.
    /// </summary>
    Ready = 3,

    /// <summary>
    /// Instance is actively running a task.
    /// </summary>
    Running = 4,

    /// <summary>
    /// Instance is paused by user or system.
    /// </summary>
    Paused = 5,

    /// <summary>
    /// Instance is waiting for a condition (resource, approval, etc.).
    /// </summary>
    Waiting = 6,

    /// <summary>
    /// Instance is recovering from an error.
    /// </summary>
    Recovering = 7,

    /// <summary>
    /// Instance requires manual review.
    /// </summary>
    ManualReview = 8,

    /// <summary>
    /// Instance has been stopped gracefully.
    /// </summary>
    Stopped = 9,

    /// <summary>
    /// Instance has failed and cannot continue without intervention.
    /// </summary>
    Failed = 10,

    /// <summary>
    /// Instance has been disposed and cleaned up.
    /// </summary>
    Disposed = 11
}

/// <summary>
/// Extension methods for InstanceState.
/// </summary>
public static class InstanceStateExtensions
{
    /// <summary>
    /// Determines if the state is a terminal state.
    /// </summary>
    public static bool IsTerminal(this InstanceState state)
        => state is InstanceState.Stopped or InstanceState.Failed or InstanceState.Disposed;

    /// <summary>
    /// Determines if the state indicates the instance is active.
    /// </summary>
    public static bool IsActive(this InstanceState state)
        => state is InstanceState.Running or InstanceState.Ready or InstanceState.Recovering;

    /// <summary>
    /// Determines if the state indicates the instance can accept new tasks.
    /// </summary>
    public static bool CanAcceptTasks(this InstanceState state)
        => state is InstanceState.Ready or InstanceState.Paused;

    /// <summary>
    /// Determines if the state is a healthy operational state.
    /// </summary>
    public static bool IsHealthy(this InstanceState state)
        => state is InstanceState.Ready or InstanceState.Running;

    /// <summary>
    /// Determines if the state requires attention.
    /// </summary>
    public static bool RequiresAttention(this InstanceState state)
        => state is InstanceState.Failed or InstanceState.ManualReview or InstanceState.Recovering;

    /// <summary>
    /// Gets the display name for the state.
    /// </summary>
    public static string GetDisplayName(this InstanceState state)
        => state switch
        {
            InstanceState.Created => "Created",
            InstanceState.Provisioning => "Provisioning",
            InstanceState.Connecting => "Connecting",
            InstanceState.Ready => "Ready",
            InstanceState.Running => "Running",
            InstanceState.Paused => "Paused",
            InstanceState.Waiting => "Waiting",
            InstanceState.Recovering => "Recovering",
            InstanceState.ManualReview => "Manual Review",
            InstanceState.Stopped => "Stopped",
            InstanceState.Failed => "Failed",
            InstanceState.Disposed => "Disposed",
            _ => state.ToString()
        };

    /// <summary>
    /// Gets the severity color for UI display.
    /// </summary>
    public static string GetSeverityColor(this InstanceState state)
        => state switch
        {
            InstanceState.Created => "#6c757d",
            InstanceState.Provisioning => "#0d6efd",
            InstanceState.Connecting => "#0d6efd",
            InstanceState.Ready => "#198754",
            InstanceState.Running => "#0d6efd",
            InstanceState.Paused => "#ffc107",
            InstanceState.Waiting => "#fd7e14",
            InstanceState.Recovering => "#fd7e14",
            InstanceState.ManualReview => "#dc3545",
            InstanceState.Stopped => "#6c757d",
            InstanceState.Failed => "#dc3545",
            InstanceState.Disposed => "#6c757d",
            _ => "#6c757d"
        };
}