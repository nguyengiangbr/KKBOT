// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace KKBOT.Domain.Common;

/// <summary>
/// Represents a structured error with code, message, details, and recovery information.
/// </summary>
public readonly record struct Error(
    string Code,
    string Message,
    string? TechnicalDetails = null,
    bool IsRetryable = false,
    ErrorSeverity Severity = ErrorSeverity.Error,
    string? RecoveryHint = null,
    IReadOnlyDictionary<string, object?>? Metadata = null)
{
    /// <summary>
    /// Creates an error with the specified code and message.
    /// </summary>
    public static Error Create(string code, string message) => new(code, message);

    /// <summary>
    /// Creates an error with full details.
    /// </summary>
    public static Error Create(
        string code,
        string message,
        string? technicalDetails = null,
        bool isRetryable = false,
        ErrorSeverity severity = ErrorSeverity.Error,
        string? recoveryHint = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
        => new(code, message, technicalDetails, isRetryable, severity, recoveryHint, metadata);

    /// <summary>
    /// Creates a retryable error.
    /// </summary>
    public static Error Retryable(string code, string message, string? recoveryHint = null)
        => new(code, message, null, true, ErrorSeverity.Warning, recoveryHint);

    /// <summary>
    /// Creates a critical error.
    /// </summary>
    public static Error Critical(string code, string message, string? technicalDetails = null)
        => new(code, message, technicalDetails, false, ErrorSeverity.Critical);

    /// <summary>
    /// Creates a validation error.
    /// </summary>
    public static Error Validation(string code, string message, string? field = null)
    {
        var metadata = field != null ? new Dictionary<string, object?> { ["field"] = field } : null;
        return new(code, message, null, false, ErrorSeverity.Error, null, metadata);
    }

    /// <summary>
    /// Creates an unauthorized error.
    /// </summary>
    public static Error Unauthorized(string message = "Unauthorized")
        => new(ErrorCodes.UNAUTHORIZED, message, null, false, ErrorSeverity.Error, "Check authentication credentials");

    /// <summary>
    /// Creates a forbidden error.
    /// </summary>
    public static Error Forbidden(string message = "Access denied")
        => new(ErrorCodes.FORBIDDEN, message, null, false, ErrorSeverity.Error, "Check permissions");

    /// <summary>
    /// Creates a not found error.
    /// </summary>
    public static Error NotFound(string code, string message)
        => new(code, message, null, false, ErrorSeverity.Warning, "Verify the resource exists");

    /// <summary>
    /// Creates a timeout error.
    /// </summary>
    public static Error Timeout(string code, string message, TimeSpan? timeout = null)
    {
        var metadata = timeout.HasValue ? new Dictionary<string, object?> { ["timeout"] = timeout.Value.ToString() } : null;
        return new(code, message, $"Operation timed out after {timeout}", true, ErrorSeverity.Error, "Increase timeout or check system performance", metadata);
    }

    /// <summary>
    /// Creates a device disconnected error.
    /// </summary>
    public static Error DeviceDisconnected(string deviceId)
        => new(ErrorCodes.DEVICE_DISCONNECTED, $"Device '{deviceId}' disconnected", deviceId, true, ErrorSeverity.Error, "Reconnect device and retry");

    /// <summary>
    /// Creates a capture failed error.
    /// </summary>
    public static Error CaptureFailed(string deviceId, string? details = null)
        => new(ErrorCodes.CAPTURE_FAILED, $"Failed to capture frame from device '{deviceId}'", details, true, ErrorSeverity.Error, "Check device connection and screen capture permissions");

    /// <summary>
    /// Creates an OCR failed error.
    /// </summary>
    public static Error OcrFailed(string region, string? details = null)
        => new(ErrorCodes.OCR_FAILED, $"OCR failed for region '{region}'", details, true, ErrorSeverity.Warning, "Check image quality and OCR configuration");

    /// <summary>
    /// Creates a detector low confidence error.
    /// </summary>
    public static Error DetectorLowConfidence(string detectorId, double confidence, double threshold)
        => new(ErrorCodes.DETECTOR_LOW_CONFIDENCE,
            $"Detector '{detectorId}' confidence {confidence:P2} below threshold {threshold:P2}",
            $"confidence={confidence}, threshold={threshold}",
            true,
            ErrorSeverity.Warning,
            "Adjust detector threshold or improve image quality",
            new Dictionary<string, object?> { ["detectorId"] = detectorId, ["confidence"] = confidence, ["threshold"] = threshold });

    /// <summary>
    /// Creates an unknown state error.
    /// </summary>
    public static Error UnknownState(string stateId, string? details = null)
        => new(ErrorCodes.STATE_UNKNOWN, $"Unknown state: {stateId}", details, false, ErrorSeverity.Error, "Review state definitions and add handling for this state");

    /// <summary>
    /// Creates a task timeout error.
    /// </summary>
    public static Error TaskTimeout(string taskId, TimeSpan timeout)
        => new(ErrorCodes.TASK_TIMEOUT, $"Task '{taskId}' timed out after {timeout}", $"taskId={taskId}, timeout={timeout}", true, ErrorSeverity.Error, "Increase task timeout or optimize task execution");

    /// <summary>
    /// Creates a recovery exhausted error.
    /// </summary>
    public static Error RecoveryExhausted(string instanceId, int attempts)
        => new(ErrorCodes.RECOVERY_EXHAUSTED, $"Recovery exhausted for instance '{instanceId}' after {attempts} attempts", $"instanceId={instanceId}, attempts={attempts}", false, ErrorSeverity.Critical, "Manual intervention required");

    /// <summary>
    /// Creates a profile invalid error.
    /// </summary>
    public static Error ProfileInvalid(string profileId, string reason)
        => new(ErrorCodes.PROFILE_INVALID, $"Profile '{profileId}' is invalid: {reason}", reason, false, ErrorSeverity.Error, "Fix profile configuration");

    /// <summary>
    /// Creates a module incompatible error.
    /// </summary>
    public static Error ModuleIncompatible(string moduleId, string version, string reason)
        => new(ErrorCodes.MODULE_INCOMPATIBLE, $"Module '{moduleId}' v{version} is incompatible: {reason}", $"moduleId={moduleId}, version={version}, reason={reason}", false, ErrorSeverity.Error, "Update module or use compatible version");

    /// <summary>
    /// Creates a policy denied error.
    /// </summary>
    public static Error PolicyDenied(string policyName, string reason)
        => new(ErrorCodes.POLICY_DENIED, $"Policy '{policyName}' denied: {reason}", reason, false, ErrorSeverity.Warning, "Review policy configuration or request approval");

    /// <summary>
    /// Creates a database unavailable error.
    /// </summary>
    public static Error DatabaseUnavailable(string? details = null)
        => new(ErrorCodes.DATABASE_UNAVAILABLE, "Database is unavailable", details, true, ErrorSeverity.Critical, "Check database connection and server status");

    /// <summary>
    /// Creates a license invalid error.
    /// </summary>
    public static Error LicenseInvalid(string reason)
        => new(ErrorCodes.LICENSE_INVALID, $"License invalid: {reason}", reason, false, ErrorSeverity.Critical, "Activate valid license");
}

/// <summary>
/// Error severity levels.
/// </summary>
public enum ErrorSeverity
{
    /// <summary>
    /// Informational, not an error.
    /// </summary>
    Information = 0,

    /// <summary>
    /// Warning, operation may continue but with degraded functionality.
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Error, operation failed but may be recoverable.
    /// </summary>
    Error = 2,

    /// <summary>
    /// Critical error, operation failed and requires immediate attention.
    /// </summary>
    Critical = 3
}

/// <summary>
/// Standard error codes used throughout KKBOT.
/// </summary>
public static class ErrorCodes
{
    // Authentication & Authorization
    public const string UNAUTHORIZED = "UNAUTHORIZED";
    public const string FORBIDDEN = "FORBIDDEN";
    public const string TOKEN_EXPIRED = "TOKEN_EXPIRED";
    public const string INVALID_CREDENTIALS = "INVALID_CREDENTIALS";

    // Device
    public const string DEVICE_NOT_FOUND = "DEVICE_NOT_FOUND";
    public const string DEVICE_DISCONNECTED = "DEVICE_DISCONNECTED";
    public const string DEVICE_UNAUTHORIZED = "DEVICE_UNAUTHORIZED";
    public const string DEVICE_OFFLINE = "DEVICE_OFFLINE";
    public const string DEVICE_BUSY = "DEVICE_BUSY";

    // Application
    public const string APP_NOT_FOUND = "APP_NOT_FOUND";
    public const string APP_NOT_RUNNING = "APP_NOT_RUNNING";
    public const string APP_CRASHED = "APP_CRASHED";
    public const string APP_START_FAILED = "APP_START_FAILED";
    public const string APP_STOP_FAILED = "APP_STOP_FAILED";

    // Capture & Perception
    public const string CAPTURE_FAILED = "CAPTURE_FAILED";
    public const string CAPTURE_TIMEOUT = "CAPTURE_TIMEOUT";
    public const string FRAME_INVALID = "FRAME_INVALID";
    public const string FRAME_CORRUPTED = "FRAME_CORRUPTED";
    public const string OCR_FAILED = "OCR_FAILED";
    public const string OCR_TIMEOUT = "OCR_TIMEOUT";
    public const string DETECTOR_FAILED = "DETECTOR_FAILED";
    public const string DETECTOR_LOW_CONFIDENCE = "DETECTOR_LOW_CONFIDENCE";
    public const string DETECTOR_NOT_FOUND = "DETECTOR_NOT_FOUND";
    public const string TEMPLATE_NOT_FOUND = "TEMPLATE_NOT_FOUND";
    public const string TEMPLATE_MISMATCH = "TEMPLATE_MISMATCH";

    // State Machine
    public const string STATE_UNKNOWN = "STATE_UNKNOWN";
    public const string STATE_TRANSITION_FAILED = "STATE_TRANSITION_FAILED";
    public const string STATE_TRANSITION_INVALID = "STATE_TRANSITION_INVALID";
    public const string NO_VALID_TRANSITION = "NO_VALID_TRANSITION";
    public const string ENTRY_CONDITION_FAILED = "ENTRY_CONDITION_FAILED";

    // Task & Job
    public const string TASK_TIMEOUT = "TASK_TIMEOUT";
    public const string TASK_CANCELLED = "TASK_CANCELLED";
    public const string TASK_FAILED = "TASK_FAILED";
    public const string TASK_BLOCKED = "TASK_BLOCKED";
    public const string TASK_REQUIRES_APPROVAL = "TASK_REQUIRES_APPROVAL";
    public const string JOB_TIMEOUT = "JOB_TIMEOUT";
    public const string JOB_CANCELLED = "JOB_CANCELLED";
    public const string JOB_FAILED = "JOB_FAILED";
    public const string CONCURRENCY_LIMIT_EXCEEDED = "CONCURRENCY_LIMIT_EXCEEDED";

    // Recovery
    public const string RECOVERY_EXHAUSTED = "RECOVERY_EXHAUSTED";
    public const string RECOVERY_FAILED = "RECOVERY_FAILED";
    public const string RECOVERY_NOT_CONFIGURED = "RECOVERY_NOT_CONFIGURED";

    // Profile & Module
    public const string PROFILE_INVALID = "PROFILE_INVALID";
    public const string PROFILE_NOT_FOUND = "PROFILE_NOT_FOUND";
    public const string PROFILE_VERSION_MISMATCH = "PROFILE_VERSION_MISMATCH";
    public const string MODULE_INCOMPATIBLE = "MODULE_INCOMPATIBLE";
    public const string MODULE_NOT_FOUND = "MODULE_NOT_FOUND";
    public const string MODULE_LOAD_FAILED = "MODULE_LOAD_FAILED";
    public const string MODULE_VALIDATION_FAILED = "MODULE_VALIDATION_FAILED";
    public const string ASSET_NOT_FOUND = "ASSET_NOT_FOUND";
    public const string ASSET_CORRUPTED = "ASSET_CORRUPTED";

    // Policy
    public const string POLICY_DENIED = "POLICY_DENIED";
    public const string POLICY_NOT_FOUND = "POLICY_NOT_FOUND";
    public const string POLICY_EVALUATION_FAILED = "POLICY_EVALUATION_FAILED";

    // Database & Persistence
    public const string DATABASE_UNAVAILABLE = "DATABASE_UNAVAILABLE";
    public const string DATABASE_CONNECTION_FAILED = "DATABASE_CONNECTION_FAILED";
    public const string DATABASE_QUERY_FAILED = "DATABASE_QUERY_FAILED";
    public const string DATABASE_MIGRATION_FAILED = "DATABASE_MIGRATION_FAILED";
    public const string CONCURRENCY_CONFLICT = "CONCURRENCY_CONFLICT";
    public const string RECORD_NOT_FOUND = "RECORD_NOT_FOUND";

    // License & Security
    public const string LICENSE_INVALID = "LICENSE_INVALID";
    public const string LICENSE_EXPIRED = "LICENSE_EXPIRED";
    public const string LICENSE_EXCEEDED = "LICENSE_EXCEEDED";
    public const string SIGNATURE_INVALID = "SIGNATURE_INVALID";
    public const string CHECKSUM_MISMATCH = "CHECKSUM_MISMATCH";

    // Configuration
    public const string CONFIG_INVALID = "CONFIG_INVALID";
    public const string CONFIG_MISSING = "CONFIG_MISSING";
    public const string SETTING_NOT_FOUND = "SETTING_NOT_FOUND";

    // External Services
    public const string OLLAMA_UNAVAILABLE = "OLLAMA_UNAVAILABLE";
    public const string OLLAMA_TIMEOUT = "OLLAMA_TIMEOUT";
    public const string OLLAMA_MODEL_NOT_FOUND = "OLLAMA_MODEL_NOT_FOUND";
    public const string SIGNALR_CONNECTION_FAILED = "SIGNALR_CONNECTION_FAILED";
    public const string ADB_COMMAND_FAILED = "ADB_COMMAND_FAILED";
    public const string ADB_SERVER_NOT_RUNNING = "ADB_SERVER_NOT_RUNNING";

    // General
    public const string INTERNAL_ERROR = "INTERNAL_ERROR";
    public const string NOT_IMPLEMENTED = "NOT_IMPLEMENTED";
    public const string OPERATION_CANCELLED = "OPERATION_CANCELLED";
    public const string INVALID_OPERATION = "INVALID_OPERATION";
    public const string RESOURCE_EXHAUSTED = "RESOURCE_EXHAUSTED";
    public const string RATE_LIMIT_EXCEEDED = "RATE_LIMIT_EXCEEDED";
}