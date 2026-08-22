// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using KKBOT.Domain.Common;
using KKBOT.Domain.Enums;

namespace KKBOT.Workflow.StateMachine;

/// <summary>
/// Represents a finite state machine for orchestrating game automation.
/// </summary>
public interface IStateMachine
{
    /// <summary>
    /// Gets the current state ID.
    /// </summary>
    string CurrentStateId { get; }

    /// <summary>
    /// Gets the current state name.
    /// </summary>
    string CurrentStateName { get; }

    /// <summary>
    /// Gets the previous state ID.
    /// </summary>
    string? PreviousStateId { get; }

    /// <summary>
    /// Gets the state history.
    /// </summary>
    IReadOnlyList<StateTransitionLog> History { get; }

    /// <summary>
    /// Gets whether the state machine is in a safe stopping state.
    /// </summary>
    bool IsInSafeState { get; }

    /// <summary>
    /// Gets whether the state machine is in an unknown state.
    /// </summary>
    bool IsInUnknownState { get; }

    /// <summary>
    /// Initializes the state machine with the defined states.
    /// </summary>
    Task InitializeAsync(IReadOnlyCollection<StateDefinition> states, CancellationToken cancellationToken);

    /// <summary>
    /// Attempts to transition to a new state based on a perception result.
    /// </summary>
    Task<Result<TransitionResult>> EvaluateAndTransitionAsync(
        PerceptionContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Forces a transition to a specific state.
    /// </summary>
    Task<Result<TransitionResult>> ForceTransitionAsync(
        string stateId,
        string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current state definition.
    /// </summary>
    StateDefinition? GetCurrentState();

    /// <summary>
    /// Gets the available next states from current position.
    /// </summary>
    IReadOnlyList<StateDefinition> GetAvailableTransitions();

    /// <summary>
    /// Resets the state machine to its initial state.
    /// </summary>
    Task ResetAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Log of a state transition.
/// </summary>
public record StateTransitionLog(
    string FromStateId,
    string FromStateName,
    string ToStateId,
    string ToStateName,
    string? Reason,
    DateTimeOffset Timestamp,
    TimeSpan? Duration);

/// <summary>
/// Result of a state transition evaluation.
/// </summary>
public record TransitionResult(
    string FromStateId,
    string ToStateId,
    string ToStateName,
    double Confidence,
    bool IsUnknown,
    bool TriggeredRecovery,
    bool RequiresApproval,
    IReadOnlyList<DetectorResult>? DetectorResults = null);

/// <summary>
/// Context for perception evaluation within the FSM.
/// </summary>
public record PerceptionContext(
    string InstanceId,
    byte[]? FrameData,
    int FrameWidth,
    int FrameHeight,
    double CurrentConfidence,
    IReadOnlyDictionary<string, object?>? AdditionalData = null);