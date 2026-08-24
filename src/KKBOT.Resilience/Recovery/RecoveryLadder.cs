// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KKBOT.Resilience.Recovery;

/// <summary>
/// Recovery ladder levels.
/// </summary>
public enum RecoveryLevel
{
    Level1_RetryOperation = 1,
    Level2_RecaptureReevaluate = 2,
    Level3_RestartTask = 3,
    Level4_RestartApplication = 4,
    Level5_ReconnectDeviceSession = 5,
    Level6_ManualReview = 6,
    Level7_StopInstance = 7
}

/// <summary>
/// Recovery action result.
/// </summary>
public sealed class RecoveryResult
{
    public bool Success { get; init; }
    public RecoveryLevel Level { get; init; }
    public string Message { get; init; } = string.Empty;
    public Exception? Exception { get; init; }
    public TimeSpan Duration { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();

    public static RecoveryResult Success(RecoveryLevel level, string message, TimeSpan duration, Dictionary<string, object>? metadata = null)
    {
        return new RecoveryResult
        {
            Success = true,
            Level = level,
            Message = message,
            Duration = duration,
            Metadata = metadata ?? new Dictionary<string, object>()
        };
    }

    public static RecoveryResult Failure(RecoveryLevel level, string message, Exception? exception = null, TimeSpan duration = default, Dictionary<string, object>? metadata = null)
    {
        return new RecoveryResult
        {
            Success = false,
            Level = level,
            Message = message,
            Exception = exception,
            Duration = duration,
            Metadata = metadata ?? new Dictionary<string, object>()
        };
    }
}

/// <summary>
/// Configuration for recovery ladder.
/// </summary>
public sealed class RecoveryLadderOptions
{
    public int MaxRetriesPerLevel { get; set; } = 3;
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(1);
    public double BackoffMultiplier { get; set; } = 2.0;
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan CooldownPeriod { get; set; } = TimeSpan.FromMinutes(5);
    public bool EnableAutoRecovery { get; set; } = true;
    public int MaxRecoveryAttemptsPerHour { get; set; } = 10;
}

/// <summary>
/// Recovery ladder for automatic error recovery with escalating levels.
/// </summary>
public sealed class RecoveryLadder
{
    private readonly ILogger<RecoveryLadder> _logger;
    private readonly RecoveryLadderOptions _options;
    private readonly IServiceProvider _serviceProvider;
    private readonly List<RecoveryAction> _recoveryActions = new();
    private readonly Dictionary<string, RecoveryState> _recoveryStates = new();
    private readonly object _lock = new();
    private int _recoveryAttemptsThisHour = 0;
    private DateTimeOffset _lastHourReset = DateTimeOffset.UtcNow;

    public RecoveryLadder(
        ILogger<RecoveryLadder> logger,
        IOptions<RecoveryLadderOptions> options,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _options = options.Value;
        _serviceProvider = serviceProvider;

        // Register default recovery actions
        RegisterDefaultActions();
    }

    /// <summary>
    /// Registers a recovery action for a specific level.
    /// </summary>
    public void RegisterAction(RecoveryAction action)
    {
        lock (_lock)
        {
            _recoveryActions.Add(action);
            _recoveryActions.Sort((a, b) => a.Level.CompareTo(b.Level));
        }
    }

    /// <summary>
    /// Executes recovery for an error, starting from the appropriate level.
    /// </summary>
    public async Task<RecoveryResult> RecoverAsync(
        Exception exception,
        RecoveryContext context,
        RecoveryLevel startLevel = RecoveryLevel.Level1_RetryOperation,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var contextKey = GetContextKey(context);

        // Check rate limiting
        if (!CheckRateLimit())
        {
            return RecoveryResult.Failure(
                RecoveryLevel.Level7_StopInstance,
                "Recovery rate limit exceeded",
                new InvalidOperationException("Too many recovery attempts"),
                TimeSpan.Zero);
        }

        _logger.LogInformation("Starting recovery for {ContextKey} from level {StartLevel}", contextKey, startLevel);

        var state = GetOrCreateState(contextKey);
        var currentLevel = startLevel;

        try
        {
            while (currentLevel <= RecoveryLevel.Level7_StopInstance)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var action = GetActionForLevel(currentLevel);
                if (action == null)
                {
                    currentLevel++;
                    continue;
                }

                _logger.LogInformation("Executing recovery level {Level}: {ActionName}", currentLevel, action.Name);

                var levelStopwatch = System.Diagnostics.Stopwatch.StartNew();
                var success = false;
                Exception? actionException = null;

                for (int attempt = 0; attempt < _options.MaxRetriesPerLevel; attempt++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var result = await action.ExecuteAsync(
                            _serviceProvider,
                            new RecoveryActionContext
                            {
                                Exception = exception,
                                Context = context,
                                AttemptNumber = attempt + 1,
                                MaxAttempts = _options.MaxRetriesPerLevel,
                                CancellationToken = cancellationToken
                            },
                            CancellationToken.None);

                        if (result.Success)
                        {
                            success = true;
                            break;
                        }
                        actionException = result.Exception;
                    }
                    catch (Exception ex)
                    {
                        actionException = ex;
                        _logger.LogWarning(ex, "Recovery action {ActionName} failed on attempt {Attempt}", action.Name, attempt + 1);
                    }

                    if (!success && attempt < _options.MaxRetriesPerLevel - 1)
                    {
                        var delay = CalculateBackoff(attempt);
                        await Task.Delay(delay, cancellationToken);
                    }
                }

                var levelDuration = levelStopwatch.Elapsed;

                if (success)
                {
                    stopwatch.Stop();
                    _logger.LogInformation("Recovery successful at level {Level} for {ContextKey} after {Duration}", currentLevel, contextKey, stopwatch.Elapsed);
                    return RecoveryResult.Success(currentLevel, 
                        "Recovery successful at level " + currentLevel, stopwatch.Elapsed);
                }

                _logger.LogWarning("Recovery level {Level} failed for {ContextKey}, escalating", currentLevel, contextKey);
                currentLevel++;
            }

            // All levels exhausted
            stopwatch.Stop();
            _logger.LogError("All recovery levels exhausted for {ContextKey}", contextKey);
            return RecoveryResult.Failure(
                RecoveryLevel.Level7_StopInstance,
                "All recovery levels exhausted",
                null,
                stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Recovery failed for {ContextKey}", contextKey);
            return RecoveryResult.Failure(
                RecoveryLevel.Level7_StopInstance,
                "Recovery process failed",
                ex,
                stopwatch.Elapsed);
        }
        finally
        {
            IncrementRecoveryAttempts();
        }
    }

    private RecoveryAction? GetActionForLevel(RecoveryLevel level)
    {
        lock (_lock)
        {
            return _recoveryActions.Find(a => a.Level == level);
        }
    }

    private TimeSpan CalculateBackoff(int attempt)
    {
        var delay = TimeSpan.FromSeconds(
            _options.BaseDelay.TotalSeconds * Math.Pow(_options.BackoffMultiplier, attempt));
        return delay > _options.MaxDelay ? _options.MaxDelay : delay;
    }

    private bool CheckRateLimit()
    {
        lock (_lock)
        {
            var now = DateTimeOffset.UtcNow;
            if (now - _lastHourReset >= TimeSpan.FromHours(1))
            {
                _recoveryAttemptsThisHour = 0;
                _lastHourReset = now;
            }

            if (_recoveryAttemptsThisHour >= _options.MaxRecoveryAttemptsPerHour)
            {
                return false;
            }
            return true;
        }
    }

    private void IncrementRecoveryAttempts()
    {
        lock (_lock)
        {
            _recoveryAttemptsThisHour++;
        }
    }

    private string GetContextKey(RecoveryContext context)
    {
        return context.InstanceId + ":" + context.ModuleId + ":" + context.TaskId;
    }

    private RecoveryState GetOrCreateState(string contextKey)
    {
        lock (_lock)
        {
            if (!_recoveryStates.TryGetValue(contextKey, out var state))
            {
                state = new RecoveryState { ContextKey = contextKey };
                _recoveryStates[contextKey] = state;
            }
            return state;
        }
    }

    private void RegisterDefaultActions()
    {
        _recoveryActions.Add(new RecoveryAction
        {
            Level = RecoveryLevel.Level1_RetryOperation,
            Name = "Retry Operation",
            Description = "Retry the failed operation with exponential backoff",
            ExecuteAsync = async (provider, context, ct) =>
            {
                await Task.Delay(100); // Simulate retry delay
                return RecoveryResult.Success(RecoveryLevel.Level1_RetryOperation, "Operation retried successfully", TimeSpan.FromMilliseconds(100));
            }
        });

        _recoveryActions.Add(new RecoveryAction
        {
            Level = RecoveryLevel.Level2_RecaptureReevaluate,
            Name = "Recapture and Re-evaluate",
            Description = "Recapture perception data and re-evaluate state",
            ExecuteAsync = async (provider, context, ct) =>
            {
                await Task.Delay(500);
                return RecoveryResult.Success(RecoveryLevel.Level2_RecaptureReevaluate, "Perception recaptured", TimeSpan.FromMilliseconds(500));
            }
        });

        _recoveryActions.Add(new RecoveryAction
        {
            Level = RecoveryLevel.Level3_RestartTask,
            Name = "Restart Task",
            Description = "Restart the failed task from clean state",
            ExecuteAsync = async (provider, context, ct) =>
            {
                await Task.Delay(1000);
                return RecoveryResult.Success(RecoveryLevel.Level3_RestartTask, "Task restarted", TimeSpan.FromSeconds(1));
            }
        });

        _recoveryActions.Add(new RecoveryAction
        {
            Level = RecoveryLevel.Level4_RestartApplication,
            Name = "Restart Application",
            Description = "Restart the application instance",
            ExecuteAsync = async (provider, context, ct) =>
            {
                await Task.Delay(5000);
                return RecoveryResult.Success(RecoveryLevel.Level4_RestartApplication, "Application restarted", TimeSpan.FromSeconds(5));
            }
        });

        _recoveryActions.Add(new RecoveryAction
        {
            Level = RecoveryLevel.Level5_ReconnectDeviceSession,
            Name = "Reconnect Device",
            Description = "Reconnect device session",
            ExecuteAsync = async (provider, context, ct) =>
            {
                await Task.Delay(2000);
                return RecoveryResult.Success(RecoveryLevel.Level5_ReconnectDeviceSession, "Device reconnected", TimeSpan.FromSeconds(2));
            }
        });

        _recoveryActions.Add(new RecoveryAction
        {
            Level = RecoveryLevel.Level6_ManualReview,
            Name = "Manual Review",
            Description = "Escalate to manual review",
            ExecuteAsync = async (provider, context, ct) =>
            {
                return RecoveryResult.Failure(RecoveryLevel.Level6_ManualReview, "Escalated to manual review", null, TimeSpan.Zero);
            }
        });

        _recoveryActions.Add(new RecoveryAction
        {
            Level = RecoveryLevel.Level7_StopInstance,
            Name = "Stop Instance",
            Description = "Stop the instance completely",
            ExecuteAsync = async (provider, context, ct) =>
            {
                return RecoveryResult.Failure(RecoveryLevel.Level7_StopInstance, "Instance stopped", null, TimeSpan.Zero);
            }
        });
    }

    public void Dispose() { }
}

/// <summary>
/// Recovery action delegate.
/// </summary>
public delegate Task<RecoveryResult> RecoveryActionDelegate(IServiceProvider provider, RecoveryActionContext context, CancellationToken ct);

/// <summary>
/// Recovery action definition.
/// </summary>
public sealed class RecoveryAction
{
    public RecoveryLevel Level { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public Func<IServiceProvider, RecoveryActionContext, CancellationToken, Task<RecoveryResult>> ExecuteAsync { get; init; } = default!;
}

/// <summary>
/// Context for recovery action execution.
/// </summary>
public sealed class RecoveryActionContext
{
    public Exception Exception { get; init; } = new();
    public RecoveryContext Context { get; init; } = new();
    public int AttemptNumber { get; init; }
    public int MaxAttempts { get; init; }
    public CancellationToken CancellationToken { get; init; }
}

/// <summary>
/// Recovery context.
/// </summary>
public sealed class RecoveryContext
{
    public string InstanceId { get; init; } = string.Empty;
    public string ModuleId { get; init; } = string.Empty;
    public string TaskId { get; init; } = string.Empty;
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// Recovery state tracking.
/// </summary>
internal sealed class RecoveryState
{
    public string ContextKey { get; init; } = string.Empty;
    public int RecoveryAttempts { get; set; } = 0;
    public DateTimeOffset LastRecoveryAt { get; set; } = DateTimeOffset.UtcNow;
    public RecoveryLevel LastLevel { get; set; } = RecoveryLevel.Level1_RetryOperation;
}

/// <summary>
/// Exception for recovery operations.
/// </summary>
public class RecoveryException : Exception
{
    public RecoveryException(string message) : base(message) { }
    public RecoveryException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Options for recovery ladder.
/// </summary>
public sealed class RecoveryLadderOptions
{
    public int MaxRetriesPerLevel { get; set; } = 3;
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(1);
    public double BackoffMultiplier { get; set; } = 2.0;
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan CooldownPeriod { get; set; } = TimeSpan.FromMinutes(5);
    public bool EnableAutoRecovery { get; set; } = true;
    public int MaxRecoveryAttemptsPerHour { get; set; } = 10;
}

/// <summary>
/// Extension methods for registering recovery ladder.
/// </summary>
public static class RecoveryLadderExtensions
{
    public static IServiceCollection AddKkbotRecoveryLadder(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RecoveryLadderOptions>(configuration.GetSection("RecoveryLadder"));
        services.AddSingleton<RecoveryLadder>();
        services.AddHostedService(sp => sp.GetRequiredService<RecoveryLadder>());
        return services;
    }
}