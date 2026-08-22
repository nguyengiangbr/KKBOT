// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using KKBOT.Domain.Enums;
using KKBOT.Domain.Models;
using Microsoft.Extensions.Logging;

namespace KKBOT.Orchestration;

/// <summary>
/// Coordinates recovery attempts using the recovery ladder pattern.
/// </summary>
public sealed class RecoveryCoordinator : IRecoveryCoordinator, IDisposable
{
    private readonly ILogger<RecoveryCoordinator> _logger;
    private readonly IInstanceManager _instanceManager;
    private readonly Dictionary<Guid, RecoveryState> _recoveryStates = new();
    private readonly object _lock = new();
    private bool _disposed;

    private static readonly Dictionary<string, RecoveryLevel> ErrorToLevelMap = new()
    {
        [ErrorCodes.TASK_TIMEOUT] = RecoveryLevel.RetryOperation,
        [ErrorCodes.OCR_FAILED] = RecoveryLevel.RecaptureAndReevaluate,
        [ErrorCodes.DETECTOR_LOW_CONFIDENCE] = RecoveryLevel.RecaptureAndReevaluate,
        [ErrorCodes.TASK_FAILED] = RecoveryLevel.RestartTask,
        [ErrorCodes.APP_CRASHED] = RecoveryLevel.RestartApplication,
        [ErrorCodes.APP_NOT_RUNNING] = RecoveryLevel.RestartApplication,
        [ErrorCodes.DEVICE_DISCONNECTED] = RecoveryLevel.ReconnectDevice,
        [ErrorCodes.CAPTURE_FAILED] = RecoveryLevel.ReconnectDevice,
        [ErrorCodes.DEVICE_NOT_FOUND] = RecoveryLevel.ReconnectDevice,
    };

    public RecoveryCoordinator(ILogger<RecoveryCoordinator> logger, IInstanceManager instanceManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _instanceManager = instanceManager ?? throw new ArgumentNullException(nameof(instanceManager));
    }

    public Task<RecoveryLevel> DetermineNextLevelAsync(Guid instanceId, string errorCode, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (!_recoveryStates.TryGetValue(instanceId, out var state))
            {
                state = new RecoveryState();
                _recoveryStates[instanceId] = state;
            }

            // Map error code to initial level
            if (state.CurrentLevel == RecoveryLevel.RetryOperation && state.AttemptCount >= GetMaxRetries(RecoveryLevel.RetryOperation))
            {
                state.CurrentLevel = RecoveryLevel.RecaptureAndReevaluate;
                state.AttemptCount = 0;
            }
            else if (state.AttemptCount >= 3 && state.CurrentLevel < RecoveryLevel.StopInstance)
            {
                state.CurrentLevel = GetNextLevel(state.CurrentLevel);
                state.AttemptCount = 0;
            }

            if (!ErrorToLevelMap.TryGetValue(errorCode, out var suggestedLevel))
            {
                suggestedLevel = RecoveryLevel.RetryOperation;
            }

            var effectiveLevel = (RecoveryLevel)Math.Max((int)state.CurrentLevel, (int)suggestedLevel);
            state.CurrentLevel = effectiveLevel;
            state.AttemptCount++;
            state.LastErrorCode = errorCode;

            _logger.LogInformation(
                "Recovery for instance '{InstanceId}': Level={Level}, Attempt={Attempt}, Error={ErrorCode}",
                instanceId, effectiveLevel, state.AttemptCount, errorCode);

            return Task.FromResult(effectiveLevel);
        }
    }

    public async Task<bool> ExecuteRecoveryAsync(Guid instanceId, RecoveryLevel level, CancellationToken cancellationToken)
    {
        var instance = _instanceManager.GetInstance(instanceId);
        if (instance == null)
        {
            _logger.LogWarning("Instance '{InstanceId}' not found for recovery", instanceId);
            return false;
        }

        _logger.LogInformation("Executing recovery level {Level} for instance '{InstanceId}'", level, instanceId);

        try
        {
            switch (level)
            {
                case RecoveryLevel.RetryOperation:
                    instance.IncrementRecoveryAttempt(level);
                    _logger.LogInformation("Retrying operation for instance '{InstanceId}'", instanceId);
                    return true;

                case RecoveryLevel.RecaptureAndReevaluate:
                    instance.IncrementRecoveryAttempt(level);
                    _logger.LogInformation("Recapturing and re-evaluating for instance '{InstanceId}'", instanceId);
                    return true;

                case RecoveryLevel.RestartTask:
                    instance.IncrementRecoveryAttempt(level);
                    _logger.LogInformation("Restarting current task for instance '{InstanceId}'", instanceId);
                    return true;

                case RecoveryLevel.RestartApplication:
                    instance.IncrementRecoveryAttempt(level);
                    _logger.LogInformation("Restarting application for instance '{InstanceId}'", instanceId);
                    return true;

                case RecoveryLevel.ReconnectDevice:
                    instance.IncrementRecoveryAttempt(level);
                    _logger.LogInformation("Reconnecting device for instance '{InstanceId}'", instanceId);
                    return true;

                case RecoveryLevel.ManualReview:
                    instance.SetState(InstanceState.ManualReview, "Recovery escalated to manual review");
                    _logger.LogWarning("Instance '{InstanceId}' moved to manual review after recovery level {Level}", instanceId, level);
                    return true;

                case RecoveryLevel.StopInstance:
                    instance.SetState(InstanceState.Failed, "Recovery exhausted");
                    _logger.LogError("Instance '{InstanceId}' stopped after recovery exhaustion", instanceId);
                    return false;

                default:
                    _logger.LogWarning("Unknown recovery level {Level} for instance '{InstanceId}'", level, instanceId);
                    return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing recovery level {Level} for instance '{InstanceId}'", level, instanceId);
            return false;
        }
    }

    public Task ResetRecoveryCounterAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (_recoveryStates.TryGetValue(instanceId, out var state))
            {
                state.Reset();
                _logger.LogInformation("Recovery counter reset for instance '{InstanceId}'", instanceId);
            }
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        lock (_lock)
        {
            _recoveryStates.Clear();
        }
    }

    private static RecoveryLevel GetNextLevel(RecoveryLevel current)
        => current switch
        {
            RecoveryLevel.RetryOperation => RecoveryLevel.RecaptureAndReevaluate,
            RecoveryLevel.RecaptureAndReevaluate => RecoveryLevel.RestartTask,
            RecoveryLevel.RestartTask => RecoveryLevel.RestartApplication,
            RecoveryLevel.RestartApplication => RecoveryLevel.ReconnectDevice,
            RecoveryLevel.ReconnectDevice => RecoveryLevel.ManualReview,
            RecoveryLevel.ManualReview => RecoveryLevel.StopInstance,
            RecoveryLevel.StopInstance => RecoveryLevel.StopInstance,
            _ => RecoveryLevel.ManualReview
        };

    private static int GetMaxRetries(RecoveryLevel level)
        => level switch
        {
            RecoveryLevel.RetryOperation => 3,
            RecoveryLevel.RecaptureAndReevaluate => 2,
            RecoveryLevel.RestartTask => 2,
            RecoveryLevel.RestartApplication => 1,
            RecoveryLevel.ReconnectDevice => 2,
            _ => 1
        };

    private sealed class RecoveryState
    {
        public RecoveryLevel CurrentLevel { get; set; } = RecoveryLevel.RetryOperation;
        public int AttemptCount { get; set; }
        public string? LastErrorCode { get; set; }

        public void Reset()
        {
            CurrentLevel = RecoveryLevel.RetryOperation;
            AttemptCount = 0;
            LastErrorCode = null;
        }
    }
}