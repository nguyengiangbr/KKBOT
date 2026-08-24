// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KKBOT.Resilience.CircuitBreaker;

/// <summary>
/// Circuit breaker states.
/// </summary>
public enum CircuitBreakerState
{
    Closed,
    Open,
    HalfOpen
}

/// <summary>
/// Circuit breaker options.
/// </summary>
public sealed class CircuitBreakerOptions
{
    public int FailureThreshold { get; set; } = 5;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromSeconds(10);
    public int MinimumThroughput { get; set; } = 10;
}

/// <summary>
/// Circuit breaker implementation with three states: Closed, Open, HalfOpen.
/// </summary>
public sealed class CircuitBreaker : IDisposable
{
    private readonly CircuitBreakerOptions _options;
    private readonly ILogger _logger;
    private readonly object _lock = new();
    private CircuitBreakerState _state = CircuitBreakerState.Closed;
    private int _failureCount = 0;
    private int _successCount = 0;
    private DateTimeOffset _lastFailureTime = DateTimeOffset.MinValue;
    private DateTimeOffset _stateChangedAt = DateTimeOffset.UtcNow;
    private Timer? _halfOpenTimer;
    private int _halfOpenSuccesses = 0;
    private bool _disposed;

    public CircuitBreakerState State
    {
        get
        {
            lock (_lock)
            {
                return _state;
            }
        }
    }

    public int FailureCount
    {
        get
        {
            lock (_lock)
            {
                return _failureCount;
            }
        }
    }

    public int SuccessCount
    {
        get
        {
            lock (_lock)
            {
                return _successCount;
            }
        }
    }

    public CircuitBreaker(ILogger logger, IOptions<CircuitBreakerOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default)
    {
        // Check if we can execute
        if (!CanExecute())
        {
            throw new CircuitBreakerOpenException("Circuit breaker is open");
        }

        try
        {
            var result = await operation(cancellationToken);
            OnSuccess();
            return result;
        }
        catch (Exception ex) when (!IsFatalException(ex))
        {
            OnFailure();
            throw;
        }
    }

    public async Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
    {
        if (!CanExecute())
        {
            throw new CircuitBreakerOpenException("Circuit breaker is open");
        }

        try
        {
            await operation(cancellationToken);
            OnSuccess();
        }
        catch (Exception ex) when (!IsFatalException(ex))
        {
            OnFailure();
            throw;
        }
    }

    private bool CanExecute()
    {
        lock (_lock)
        {
            switch (_state)
            {
                case CircuitBreakerState.Closed:
                    return true;
                case CircuitBreakerState.Open:
                    // Check if timeout has elapsed to transition to half-open
                    if (DateTimeOffset.UtcNow - _stateChangedAt >= _options.Timeout)
                    {
                        TransitionToHalfOpen();
                        return true;
                    }
                    return false;
                case CircuitBreakerState.HalfOpen:
                    return true;
                default:
                    return false;
            }
        }
    }

    private void OnSuccess()
    {
        lock (_lock)
        {
            _successCount++;
            
            switch (_state)
            {
                case CircuitBreakerState.HalfOpen:
                    _halfOpenSuccesses++;
                    if (_halfOpenSuccesses >= _options.MinimumThroughput)
                    {
                        TransitionToClosed();
                    }
                    break;
                case CircuitBreakerState.Closed:
                    _failureCount = 0; // Reset failure count on success
                    break;
            }
        }
    }

    private void OnFailure()
    {
        lock (_lock)
        {
            _failureCount++;
            _lastFailureTime = DateTimeOffset.UtcNow;

            switch (_state)
            {
                case CircuitBreakerState.Closed:
                    if (_failureCount >= _options.FailureThreshold)
                    {
                        TransitionToOpen();
                    }
                    break;
                case CircuitBreakerState.HalfOpen:
                    // Any failure in half-open goes back to open
                    TransitionToOpen();
                    break;
            }
        }
    }

    private void TransitionToOpen()
    {
        _state = CircuitBreakerState.Open;
        _stateChangedAt = DateTimeOffset.UtcNow;
        _halfOpenSuccesses = 0;
        
        // Schedule transition to half-open
        _halfOpenTimer?.Dispose();
        _halfOpenTimer = new Timer(_ => TransitionToHalfOpen(), null, _options.Timeout, Timeout.InfiniteTimeSpan);
        
        _logger.LogWarning("Circuit breaker opened due to {FailureCount} failures", _failureCount);
    }

    private void TransitionToHalfOpen()
    {
        lock (_lock)
        {
            if (_state == CircuitBreakerState.Open)
            {
                _state = CircuitBreakerState.HalfOpen;
                _stateChangedAt = DateTimeOffset.UtcNow;
                _halfOpenSuccesses = 0;
                _logger.LogInformation("Circuit breaker transitioned to half-open");
            }
        }
    }

    private void TransitionToClosed()
    {
        lock (_lock)
        {
            _state = CircuitBreakerState.Closed;
            _stateChangedAt = DateTimeOffset.UtcNow;
            _failureCount = 0;
            _successCount = 0;
            _halfOpenSuccesses = 0;
            _halfOpenTimer?.Dispose();
            _halfOpenTimer = null;
            _logger.LogInformation("Circuit breaker closed");
        }
    }

    private bool IsFatalException(Exception ex)
    {
        return ex is OutOfMemoryException
            || ex is StackOverflowException
            || ex is ThreadAbortException
            || ex is AccessViolationException;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _halfOpenTimer?.Dispose();
    }
}

/// <summary>
/// Exception thrown when circuit breaker is open.
/// </summary>
public class CircuitBreakerOpenException : Exception
{
    public CircuitBreakerOpenException(string message) : base(message) { }
    public CircuitBreakerOpenException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Factory for creating circuit breakers.
/// </summary>
public sealed class CircuitBreakerFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly CircuitBreakerOptions _defaultOptions;
    private readonly ConcurrentDictionary<string, CircuitBreaker> _breakers = new();

    public CircuitBreakerFactory(ILoggerFactory loggerFactory, IOptions<CircuitBreakerOptions> options)
    {
        _loggerFactory = loggerFactory;
        _defaultOptions = options.Value;
    }

    public CircuitBreaker GetOrCreate(string name, CircuitBreakerOptions? options = null)
    {
        return _breakers.GetOrAdd(name, _ => new CircuitBreaker(
            _loggerFactory.CreateLogger<CircuitBreaker>(),
            Options.Create(options ?? _defaultOptions)));
    }

    public bool TryGet(string name, out CircuitBreaker? breaker)
    {
        return _breakers.TryGetValue(name, out breaker);
    }

    public void Remove(string name)
    {
        if (_breakers.TryRemove(name, out var breaker))
        {
            breaker.Dispose();
        }
    }
}

/// <summary>
/// Extension methods for registering circuit breaker.
/// </summary>
public static class CircuitBreakerExtensions
{
    public static IServiceCollection AddKkbotCircuitBreaker(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CircuitBreakerOptions>(configuration.GetSection("CircuitBreaker"));
        services.AddSingleton<CircuitBreakerFactory>();
        return services;
    }
}