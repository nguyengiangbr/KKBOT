// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using KKBOT.Domain.Common;
using KKBOT.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace KKBOT.Workflow.StateMachine;

/// <summary>
/// Finite state machine implementation for game automation workflows.
/// </summary>
public sealed class StateMachine : IStateMachine, IDisposable
{
    private readonly ILogger<StateMachine> _logger;
    private readonly ConcurrentDictionary<string, StateDefinition> _states = new();
    private readonly List<StateTransitionLog> _history = new();
    private readonly object _lock = new();

    private StateDefinition? _currentState;
    private bool _initialized;
    private bool _disposed;

    public StateMachine(ILogger<StateMachine> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string CurrentStateId => _currentState?.Id ?? "None";
    public string CurrentStateName => _currentState?.Name ?? "None";
    public string? PreviousStateId { get; private set; }
    public IReadOnlyList<StateTransitionLog> History => _history.AsReadOnly();
    public bool IsInSafeState => _currentState?.Category is "safe_stop" or "idle" or "completed";
    public bool IsInUnknownState => _currentState?.Id is "unknown" or "manual_review";

    public Task InitializeAsync(IReadOnlyCollection<StateDefinition> states, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(states);

        if (_initialized)
            throw new InvalidOperationException("State machine is already initialized.");

        if (states.Count == 0)
            throw new ArgumentException("At least one state must be defined.", nameof(states));

        lock (_lock)
        {
            _states.Clear();
            _history.Clear();

            foreach (var state in states)
            {
                if (!_states.TryAdd(state.Id, state))
                    throw new InvalidOperationException($"Duplicate state ID: {state.Id}");
            }

            var initialState = _states.Values.FirstOrDefault(s => s.Category == "initial")
                               ?? _states.Values.First();

            _currentState = initialState;
            PreviousStateId = null;

            _history.Add(new StateTransitionLog(
                "None", "None",
                initialState.Id, initialState.Name,
                "Initialization", DateTimeOffset.UtcNow, TimeSpan.Zero));

            _initialized = true;
            _logger.LogInformation("State machine initialized with state '{StateId}'", initialState.Id);
        }

        return Task.CompletedTask;
    }

    public Task<Result<TransitionResult>> EvaluateAndTransitionAsync(
        PerceptionContext context,
        CancellationToken cancellationToken)
    {
        if (!_initialized)
            return Task.FromResult<Result<TransitionResult>>(
                Error.Create("STATE_MACHINE_NOT_INITIALIZED", "State machine has not been initialized."));

        if (_currentState == null)
            return Task.FromResult<Result<TransitionResult>>(
                Error.Create("NO_CURRENT_STATE", "No current state is set."));

        lock (_lock)
        {
            try
            {
                var availableTransitions = _currentState.NextStates;

                // Evaluate each potential next state
                StateDefinition? bestMatch = null;
                double bestConfidence = 0;
                bool isUnknown = true;
                var detectorResults = new List<DetectorResult>();

                foreach (var nextState in availableTransitions)
                {
                    try
                    {
                        var matches = nextState.EntryCondition(context);
                        if (matches)
                        {
                            isUnknown = false;
                            // Find the best matching state from perception context
                            if (context.CurrentConfidence > bestConfidence)
                            {
                                bestConfidence = context.CurrentConfidence;
                                bestMatch = nextState;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error evaluating entry condition for state '{StateId}'", nextState.Id);
                    }
                }

                if (isUnknown || bestMatch == null)
                {
                    // Find the unknown or safe fallback state
                    var unknownState = _states.Values.FirstOrDefault(s => s.Id == "unknown")
                                       ?? _states.Values.FirstOrDefault(s => s.Id == "manual_review")
                                       ?? _states.Values.FirstOrDefault(s => s.Category == "safe_stop");

                    if (unknownState != null && unknownState != _currentState)
                    {
                        return Task.FromResult(TransitionTo(unknownState, "Unknown state detected", context.CurrentConfidence, isUnknown));
                    }

                    // Stay in current state with unknown result
                    var transitionResult = new TransitionResult(
                        _currentState.Id, _currentState.Id, _currentState.Name,
                        context.CurrentConfidence, true, false, false, detectorResults);

                    return Task.FromResult(Result<TransitionResult>.Success(transitionResult));
                }

                if (bestMatch != _currentState)
                {
                    return Task.FromResult(TransitionTo(bestMatch, $"Detected state: {bestMatch.Name}", bestConfidence, false));
                }

                var stayResult = new TransitionResult(
                    _currentState.Id, _currentState.Id, _currentState.Name,
                    bestConfidence, false, false, false, detectorResults);

                return Task.FromResult(Result<TransitionResult>.Success(stayResult));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating state transition");
                return Task.FromResult<Result<TransitionResult>>(
                    Error.Create("TRANSITION_EVALUATION_FAILED", ex.Message));
            }
        }
    }

    public Task<Result<TransitionResult>> ForceTransitionAsync(
        string stateId, string? reason = null, CancellationToken cancellationToken = default)
    {
        if (!_initialized)
            return Task.FromResult<Result<TransitionResult>>(
                Error.Create("STATE_MACHINE_NOT_INITIALIZED", "State machine has not been initialized."));

        lock (_lock)
        {
            if (!_states.TryGetValue(stateId, out var targetState))
                return Task.FromResult<Result<TransitionResult>>(
                    Error.NotFound("STATE_NOT_FOUND", $"State '{stateId}' not found."));

            return Task.FromResult(TransitionTo(targetState, reason ?? "Forced transition", 1.0, false));
        }
    }

    public StateDefinition? GetCurrentState() => _currentState;

    public IReadOnlyList<StateDefinition> GetAvailableTransitions()
    {
        if (_currentState == null) return Array.Empty<StateDefinition>();
        return _currentState.NextStates.ToList().AsReadOnly();
    }

    public Task ResetAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            var initialState = _states.Values.FirstOrDefault(s => s.Category == "initial")
                               ?? _states.Values.First();

            PreviousStateId = _currentState?.Id;
            _currentState = initialState;

            _history.Add(new StateTransitionLog(
                PreviousStateId ?? "None", _currentState.Name,
                initialState.Id, initialState.Name,
                "Reset", DateTimeOffset.UtcNow, null));

            _logger.LogInformation("State machine reset to state '{StateId}'", initialState.Id);
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _states.Clear();
            _history.Clear();
        }
    }

    private Result<TransitionResult> TransitionTo(StateDefinition target, string reason, double confidence, bool isUnknown)
    {
        var fromState = _currentState!;
        var startTime = DateTimeOffset.UtcNow;

        PreviousStateId = fromState.Id;
        _currentState = target;

        var duration = DateTimeOffset.UtcNow - startTime;

        var log = new StateTransitionLog(
            fromState.Id, fromState.Name,
            target.Id, target.Name,
            reason, DateTimeOffset.UtcNow, duration);

        _history.Add(log);

        // Trim history to last 100 entries
        if (_history.Count > 100)
            _history.RemoveRange(0, _history.Count - 100);

        _logger.LogInformation(
            "Transition: {FromState} -> {ToState} (confidence={Confidence:P2}, reason={Reason})",
            fromState.Name, target.Name, confidence, reason);

        var result = new TransitionResult(
            fromState.Id, target.Id, target.Name,
            confidence, isUnknown, false, false);

        return Result<TransitionResult>.Success(result);
    }
}

/// <summary>
/// Definition of a state in the FSM.
/// </summary>
public sealed class StateDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public Func<PerceptionContext?, bool> EntryCondition { get; set; } = _ => false;
    public IReadOnlyList<StateDefinition> NextStates { get; set; } = Array.Empty<StateDefinition>();
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    public RetryPolicy RetryPolicy { get; set; } = new();
    public EvidencePolicy EvidencePolicy { get; set; } = new();
}

/// <summary>
/// Builder for constructing state definitions.
/// </summary>
public class StateDefinitionBuilder
{
    private readonly StateDefinition _definition = new();

    public StateDefinitionBuilder WithId(string id) { _definition.Id = id; return this; }
    public StateDefinitionBuilder WithName(string name) { _definition.Name = name; return this; }
    public StateDefinitionBuilder WithCategory(string category) { _definition.Category = category; return this; }
    public StateDefinitionBuilder WithEntryCondition(Func<PerceptionContext?, bool> condition) { _definition.EntryCondition = condition; return this; }
    public StateDefinitionBuilder WithNextStates(params StateDefinition[] states) { _definition.NextStates = states; return this; }
    public StateDefinitionBuilder WithTimeout(TimeSpan timeout) { _definition.Timeout = timeout; return this; }
    public StateDefinitionBuilder WithRetryPolicy(RetryPolicy policy) { _definition.RetryPolicy = policy; return this; }
    public StateDefinitionBuilder WithEvidencePolicy(EvidencePolicy policy) { _definition.EvidencePolicy = policy; return this; }

    public StateDefinition Build() => _definition;
}

/// <summary>
/// Retry policy for state operations.
/// </summary>
public record RetryPolicy
{
    public int MaxRetries { get; init; } = 3;
    public int RetryDelayMs { get; init; } = 1000;
    public bool ExponentialBackoff { get; init; } = true;
    public double BackoffMultiplier { get; init; } = 2.0;
    public int MaxRetryDelayMs { get; init; } = 30000;
    public string[]? RetryableErrorCodes { get; init; }
}

/// <summary>
/// Evidence policy for state transitions.
/// </summary>
public record EvidencePolicy
{
    public bool RequireScreenshot { get; init; } = true;
    public bool RequireOcrResult { get; init; } = false;
    public double RequireDetectorConfidence { get; init; } = 0.9;
    public int RetentionCount { get; init; } = 3;
}

/// <summary>
/// Detector result from perception analysis.
/// </summary>
public record DetectorResult
{
    public string DetectorId { get; init; } = string.Empty;
    public string DetectorName { get; init; } = string.Empty;
    public double Confidence { get; init; }
    public bool IsMatch { get; init; }
    public string? DetectedValue { get; init; }
    public double RawScore { get; init; }
}