// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace KKBOT.Domain.Enums;

/// <summary>
/// Represents the recovery ladder levels.
/// </summary>
public enum RecoveryLevel
{
    /// <summary>
    /// Level 1: Retry the operation.
    /// </summary>
    RetryOperation = 1,

    /// <summary>
    /// Level 2: Re-capture and re-evaluate.
    /// </summary>
    RecaptureAndReevaluate = 2,

    /// <summary>
    /// Level 3: Restart the current task.
    /// </summary>
    RestartTask = 3,

    /// <summary>
    /// Level 4: Restart the application.
    /// </summary>
    RestartApplication = 4,

    /// <summary>
    /// Level 5: Reconnect the device session.
    /// </summary>
    ReconnectDevice = 5,

    /// <summary>
    /// Level 6: Move to manual review.
    /// </summary>
    ManualReview = 6,

    /// <summary>
    /// Level 7: Stop the instance.
    /// </summary>
    StopInstance = 7
}

/// <summary>
/// Extension methods for RecoveryLevel.
/// </summary>
public static class RecoveryLevelExtensions
{
    /// <summary>
    /// Gets the display name for the recovery level.
    /// </summary>
    public static string GetDisplayName(this RecoveryLevel level)
        => level switch
        {
            RecoveryLevel.RetryOperation => "Retry Operation",
            RecoveryLevel.RecaptureAndReevaluate => "Recapture & Reevaluate",
            RecoveryLevel.RestartTask => "Restart Task",
            RecoveryLevel.RestartApplication => "Restart Application",
            RecoveryLevel.ReconnectDevice => "Reconnect Device",
            RecoveryLevel.ManualReview => "Manual Review",
            RecoveryLevel.StopInstance => "Stop Instance",
            _ => level.ToString()
        };

    /// <summary>
    /// Gets the description for the recovery level.
    /// </summary>
    public static string GetDescription(this RecoveryLevel level)
        => level switch
        {
            RecoveryLevel.RetryOperation => "Retry the failed operation with the same parameters",
            RecoveryLevel.RecaptureAndReevaluate => "Capture a new frame and re-run perception pipeline",
            RecoveryLevel.RestartTask => "Restart the current task from the beginning",
            RecoveryLevel.RestartApplication => "Stop and restart the target application",
            RecoveryLevel.ReconnectDevice => "Disconnect and reconnect the device session",
            RecoveryLevel.ManualReview => "Pause instance and request operator intervention",
            RecoveryLevel.StopInstance => "Gracefully stop the instance and mark as failed",
            _ => string.Empty
        };

    /// <summary>
    /// Determines if this level requires human intervention.
    /// </summary>
    public static bool RequiresHumanIntervention(this RecoveryLevel level)
        => level >= RecoveryLevel.ManualReview;

    /// <summary>
    /// Determines if this level is a terminal recovery action.
    /// </summary>
    public static bool IsTerminal(this RecoveryLevel level)
        => level >= RecoveryLevel.StopInstance;

    /// <summary>
    /// Gets the next recovery level.
    /// </summary>
    public static RecoveryLevel? GetNextLevel(this RecoveryLevel level)
        => level switch
        {
            RecoveryLevel.RetryOperation => RecoveryLevel.RecaptureAndReevaluate,
            RecoveryLevel.RecaptureAndReevaluate => RecoveryLevel.RestartTask,
            RecoveryLevel.RestartTask => RecoveryLevel.RestartApplication,
            RecoveryLevel.RestartApplication => RecoveryLevel.ReconnectDevice,
            RecoveryLevel.ReconnectDevice => RecoveryLevel.ManualReview,
            RecoveryLevel.ManualReview => RecoveryLevel.StopInstance,
            RecoveryLevel.StopInstance => null,
            _ => null
        };
}