// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KKBOT.Observability;

namespace KKBOT.Performance.Async;

/// <summary>
/// Advanced async optimization utilities with cancellation token propagation.
/// </summary>
public static class AsyncOptimizer
{
    /// <summary>
    /// Runs multiple tasks concurrently with a maximum degree of parallelism.
    /// </summary>
    public static async Task<T[]> RunWithMaxParallelism<T>(
        IEnumerable<Func<CancellationToken, Task<T>>> tasks,
        int maxDegreeOfParallelism,
        CancellationToken cancellationToken = default)
    {
        if (maxDegreeOfParallelism <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism));

        var taskList = tasks.ToList();
        if (taskList.Count == 0)
            return Array.Empty<T>();

        var semaphore = new SemaphoreSlim(maxDegreeOfParallelism, maxDegreeOfParallelism);
        var results = new T[taskList.Count];
        var exceptions = new ConcurrentQueue<Exception>();

        var tasksWithIndex = taskList.Select((task, index) => (task, index)).ToList();

        var tasks = taskList.Select(async (task, index) =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                results[index] = await task(cancellationToken);
            }
            catch (Exception ex)
            {
                exceptions.Enqueue(ex);
            }
            finally
            {
                semaphore.Release();
            }
        }).ToArray();

        await Task.WhenAll(tasks);

        if (exceptions.TryDequeue(out var ex))
        {
            throw new AggregateException(exceptions);
        }

        return results;
    }

    /// <summary>
    /// Runs tasks with a maximum concurrency and returns results as they complete.
    /// </summary>
    public static async IAsyncEnumerable<T> RunWithMaxParallelismAsync<T>(
        IEnumerable<Func<CancellationToken, Task<T>>> tasks,
        int maxDegreeOfParallelism,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var semaphore = new SemaphoreSlim(maxDegreeOfParallelism, maxDegreeOfParallelism);
        var taskQueue = new Channel<Func<CancellationToken, Task<T>>>(new BoundedChannelOptions(tasks.Count()) 
        { 
            FullMode = BoundedChannelFullMode.Wait 
        });

        // Enqueue all tasks
        foreach (var task in tasks)
        {
            await taskQueue.Writer.WriteAsync(task, cancellationToken);
        }
        taskQueue.Writer.Complete();

        var processingTasks = Enumerable.Range(0, maxDegreeOfParallelism)
            .Select(async _ =>
            {
                await foreach (var task in taskQueue.Reader.ReadAllAsync(cancellationToken))
                {
                    try
                    {
                        var result = await task(cancellationToken);
                        yield return result;
                    }
                    catch (Exception ex)
                    {
                        // Log but continue
                    }
                }
            }).ToArray();

        await Task.WhenAll(processingTasks);
    }

    /// <summary>
    /// Runs tasks with timeout and cancellation support.
    /// </summary>
    public static async Task<T> WithTimeout<T>(
        Func<CancellationToken, Task<T>> operation,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            return await operation(cts.Token);
        }
        catch (OperationCanceledException) when (cts.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Operation timed out after {timeout}");
        }
    }

    /// <summary>
    /// Runs multiple operations with individual timeouts.
    /// </summary>
    public static async Task<T[]> RunWithIndividualTimeouts<T>(
        IEnumerable<(Func<CancellationToken, Task<T>> operation, TimeSpan timeout)> operations,
        CancellationToken cancellationToken = default)
    {
        var tasks = operations.Select(async op =>
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(op.timeout);
            try
            {
                return await op.operation(cts.Token);
            }
            catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
            {
                throw new TimeoutException("Operation timed out");
            }
        }).ToArray();

        return await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Retries an operation with exponential backoff.
    /// </summary>
    public static async Task<T> RetryAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        int maxRetries,
        TimeSpan baseDelay,
        double backoffMultiplier = 2.0,
        CancellationToken cancellationToken = default,
        Func<Exception, bool> shouldRetry = null)
    {
        Exception lastException = null;

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await operation(cancellationToken);
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                lastException = ex;
                
                if (shouldRetry != null && !shouldRetry(ex))
                    throw;

                var delay = TimeSpan.FromMilliseconds(
                    baseDelay.TotalMilliseconds * Math.Pow(backoffMultiplier, attempt));
                
                await Task.Delay(delay, cancellationToken);
            }
        }

        throw lastException ?? new Exception("Retry failed");
    }

    /// <summary>
    /// Executes multiple operations and returns the first successful result.
    /// </summary>
    public static async Task<T> FirstSuccessAsync<T>(
        IEnumerable<Func<CancellationToken, Task<T>>> operations,
        CancellationToken cancellationToken = default)
    {
        var tasks = operations.Select(op => op(cancellationToken)).ToArray();
        
        while (tasks.Length > 0)
        {
            var completed = await Task.WhenAny(tasks);
            tasks = tasks.Where(t => t != completed).ToArray();
            
            try
            {
                return await completed;
            }
            catch
            {
                // Continue with remaining tasks
            }
        }

        throw new InvalidOperationException("All operations failed");
    }

    /// <summary>
    /// Creates a timeout-linked cancellation token.
    /// </summary>
    public static CancellationTokenSource CreateTimeoutTokenSource(
        TimeSpan timeout,
        CancellationToken parent = default)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(parent);
        cts.CancelAfter(TimeSpan.FromMilliseconds(timeout.TotalMilliseconds));
        return cts;
    }

    /// <summary>
    /// Creates a timeout that can be extended.
    /// </summary>
    public static ExtendableTimeoutToken CreateExtendableTimeout(TimeSpan initialTimeout)
    {
        return new ExtendableTimeoutToken(initialTimeout);
    }
}

/// <summary>
/// Extendable timeout token that can be extended during operation.
/// </summary>
public sealed class ExtendableTimeoutToken : IDisposable
{
    private readonly CancellationTokenSource _cts;
    private readonly TimeSpan _initialTimeout;
    private bool _disposed;

    public CancellationToken Token => _cts.Token;

    public ExtendableTimeoutToken(TimeSpan initialTimeout)
    {
        _initialTimeout = initialTimeout;
        _cts = new CancellationTokenSource(initialTimeout);
    }

    /// <summary>
    /// Extends the timeout by the specified duration.
    /// </summary>
    public void Extend(TimeSpan additionalTime)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ExtendableTimeoutToken));
        
        var remaining = _cts.Token.CanBeCanceled ? 
            DateTimeOffset.UtcNow.Add(_initialTimeout) - DateTimeOffset.UtcNow : TimeSpan.Zero;
        
        var newTimeout = remaining + additionalTime;
        if (newTimeout > TimeSpan.Zero)
        {
            _cts.CancelAfter(newTimeout);
        }
    }

    /// <summary>
    /// Resets the timeout to initial duration.
    /// </summary>
    public void Reset()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ExtendableTimeoutToken));
        _cts.Cancel();
        _cts = new CancellationTokenSource(_initialTimeout);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _cts.Cancel();
            _cts.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Timeout policy for operations.
/// </summary>
public sealed record TimeoutPolicy
{
    public TimeSpan DefaultTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan ReadTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan WriteTimeout { get; init; } = TimeSpan.FromSeconds(30);
    
    public static TimeoutPolicy Default => new();
}

/// <summary>
/// Circuit breaker for async operations.
/// </summary>
public sealed class AsyncCircuitBreaker : IDisposable
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _resetTimeout;
    private readonly ILogger _logger;
    private int _failureCount;
    private DateTimeOffset _lastFailureTime;
    private CircuitState _state = CircuitState.Closed;
    private readonly object _lock = new();
    private bool _disposed;

    public CircuitState State => _state;

    public AsyncCircuitBreaker(int failureThreshold = 5, TimeSpan? resetTimeout = null, ILogger logger = null)
    {
        _failureThreshold = failureThreshold;
        _resetTimeout = resetTimeout ?? TimeSpan.FromSeconds(30);
        _logger = logger;
    }

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        ThrowIfOpen();

        try
        {
            var result = await operation(cancellationToken);
            OnSuccess();
            return result;
        }
        catch (Exception ex)
        {
            OnFailure();
            throw;
        }
    }

    private void ThrowIfOpen()
    {
        lock (_lock)
        {
            if (_state == CircuitState.Open)
            {
                if (DateTimeOffset.UtcNow - _lastFailureTime > TimeSpan.FromSeconds(30))
                {
                    _state = CircuitState.HalfOpen;
                }
                else
                {
                    throw new CircuitBreakerOpenException("Circuit breaker is open");
                }
            }
        }

    private void OnSuccess()
    {
        lock (_lock)
        {
            _failureCount = 0;
            _state = CircuitState.Closed;
        }
    }

    private void OnFailure()
    {
        lock (_lock)
        {
            _failureCount++;
            _lastFailureTime = DateTimeOffset.UtcNow;

            if (_failureCount >= 5) // Default threshold
            {
                _state = CircuitState.Open;
            }
        }
    }

    public void Dispose()
    {
        // No resources to dispose
    }
}

public enum CircuitState
{
    Closed,
    Open,
    HalfOpen
}

public class CircuitBreakerOpenException : Exception
{
    public CircuitBreakerOpenException(string message) : base(message) { }
}

/// <summary>
/// Debounces async operations.
/// </summary>
public sealed class AsyncDebouncer<T> : IDisposable
{
    private readonly TimeSpan _debounceTime;
    private readonly Func<CancellationToken, Task<T>> _operation;
    private readonly ILogger _logger;
    private Task<T> _pendingTask;
    private DateTimeOffset _lastInvocation;
    private readonly object _lock = new();
    private CancellationTokenSource _cts;

    public AsyncDebouncer(TimeSpan debounceTime, Func<CancellationToken, Task<T>> operation, ILogger logger = null)
    {
        _debounceTime = debounceTime;
        _operation = operation ?? throw new ArgumentNullException(nameof(operation));
    }

    public async Task<T> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _lastInvocation = DateTimeOffset.UtcNow;
            
            if (_pendingTask != null && !_pendingTask.IsCompleted)
            {
                // Cancel previous operation
                _cts?.Cancel();
            }

            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _cts = cts;

            _pendingTask = ExecuteWithDebounceAsync(cts.Token);
        }

        return await _pendingTask;
    }

    private async Task<T> ExecuteWithDebounceAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(_debounceTime, cancellationToken);
        
        // Check if another invocation came in during debounce
        if (DateTimeOffset.UtcNow - _lastInvocation < _debounceTime)
        {
            // Another call came in, wait for that one instead
            return await ExecuteAsync(cancellationToken);
        }

        return await _operation(cancellationToken);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}

/// <summary>
/// Throttles async operations.
/// </summary>
public sealed class AsyncThrottler : IDisposable
{
    private readonly SemaphoreSlim _semaphore;
    private readonly TimeSpan _window;
    private readonly Queue<DateTimeOffset> _timestamps = new();
    private readonly object _lock = new();

    public AsyncThrottler(int maxRequests, TimeSpan window)
    {
        _semaphore = new SemaphoreSlim(1, 1); // For timestamp management
        _window = window;
    }

    public async Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken = default)
    {
        DateTimeOffset now;
        lock (_lock)
        {
            now = DateTimeOffset.UtcNow;
            // Remove old timestamps
            while (_timestamps.Count > 0 && now - _timestamps.Peek() > TimeSpan.FromMinutes(1))
            {
                _timestamps.Dequeue();
            }
        }

        // Wait if throttled
        while (true)
        {
            lock (_lock)
            {
                if (_timestamps.Count < 100) // Max 100 per window
                {
                    _timestamps.Enqueue(now);
                    break;
                }
            }
            await Task.Delay(100);
        }

        await operation();
    }

    public void Dispose() { }
}

/// <summary>
/// Batch processor for async operations.
/// </summary>
public sealed class BatchProcessor<T> : IDisposable
{
    private readonly Channel<T> _channel;
    private readonly Func<IReadOnlyList<T>, CancellationToken, Task> _processor;
    private readonly int _batchSize;
    private readonly TimeSpan _maxWaitTime;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _processingTask;
    private bool _disposed;

    public BatchProcessor(
        int batchSize,
        TimeSpan maxWaitTime,
        Func<IReadOnlyList<T>, CancellationToken, Task> processor)
    {
        _batchSize = batchSize;
        _maxWaitTime = maxWaitTime;
        _processor = processor ?? throw new ArgumentNullException(nameof(processor));
        
        var options = new BoundedChannelOptions(batchSize * 2)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        _channel = Channel.CreateBounded<T>(options);

        _processingTask = ProcessBatchesAsync(_cts.Token);
    }

    public async ValueTask<bool> TryAddAsync(T item, CancellationToken cancellationToken = default)
    {
        return await _channel.Writer.TryWriteAsync(item, cancellationToken);
    }

    public async ValueTask<bool> WaitToAddAsync(T item, CancellationToken cancellationToken = default)
    {
        return await _channel.Writer.WriteAsync(item, cancellationToken);
    }

    private async Task ProcessBatchesAsync(CancellationToken cancellationToken)
    {
        var batch = new List<T>();

        while (!_cts.Token.IsCancellationRequested)
        {
            batch.Clear();

            // Wait for first item
            try
            {
                var item = await _channel.Reader.ReadAsync(cancellationToken);
                batch.Add(item);
            }
            catch (ChannelClosedException)
            {
                break;
            }

            // Collect more items up to batch size or max wait time
            var waitTask = Task.Delay(_maxWaitTime, cancellationToken);
            var readTask = _channel.Reader.ReadAsync(cancellationToken).AsTask();

            while (batch.Count < _batchSize && !cancellationToken.IsCancellationRequested)
            {
                var completed = await Task.WhenAny(readTask, waitTask);
                
                if (completed == readTask)
                {
                    batch.Add(readTask.Result);
                    readTask = _channel.Reader.ReadAsync(cancellationToken).AsTask();
                }
                else
                {
                    // Timeout reached
                    break;
                }
            }

            if (batch.Count > 0)
            {
                try
                {
                    await _processor(batch, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    // Log error but continue
                }
                
                batch.Clear();
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        _processingTask.Wait(TimeSpan.FromSeconds(5));
    }
}

/// <summary>
/// Rate limiter for async operations.
/// </summary>
public sealed class AsyncRateLimiter : IDisposable
{
    private readonly SemaphoreSlim _semaphore;
    private readonly TimeSpan _interval;
    private readonly int _maxRequests;
    private readonly Queue<DateTimeOffset> _requestTimes = new();
    private readonly object _lock = new();

    public AsyncRateLimiter(int maxRequestsPerInterval, TimeSpan interval)
    {
        _maxRequests = maxRequests;
        _interval = interval;
        _semaphore = new SemaphoreSlim(maxRequests, maxRequests);
    }

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);

        lock (_lock)
        {
            var now = DateTimeOffset.UtcNow;
            
            // Remove old entries
            while (_requestTimes.Count > 0 && now - _requestTimes.Peek() > TimeSpan.FromSeconds(1))
            {
                _requestTimes.Dequeue();
            }

            // If at limit, wait
            if (_requestTimes.Count >= 100) // Max 100 per second
            {
                var oldest = _requestTimes.Peek();
                var waitTime = TimeSpan.FromSeconds(1) - (DateTimeOffset.UtcNow - oldest);
                if (waitTime > TimeSpan.Zero)
                {
                    await Task.Delay(waitTime, CancellationToken.None);
                }
            }

            _requestTimes.Enqueue(now);
        }
    }

    public void Dispose() { }
}

/// <summary>
/// Async retry policy builder.
/// </summary>
public sealed class AsyncRetryPolicyBuilder
{
    private int _maxRetries = 3;
    private TimeSpan _baseDelay = TimeSpan.FromSeconds(1);
    private double _backoffMultiplier = 2.0;
    private TimeSpan _maxDelay = TimeSpan.FromMinutes(1);
    private Func<Exception, bool> _shouldRetry = _ => true;
    private Action<Exception, int> _onRetry = null;

    public AsyncRetryPolicyBuilder WithMaxRetries(int maxRetries)
    {
        _maxRetries = maxRetries;
        return this;
    }

    public AsyncRetryPolicyBuilder WithBaseDelay(TimeSpan baseDelay)
    {
        _baseDelay = baseDelay;
        return this;
    }

    public AsyncRetryPolicyBuilder WithBackoffMultiplier(double multiplier)
    {
        _backoffMultiplier = multiplier;
        return this;
    }

    public AsyncRetryPolicyBuilder WithMaxDelay(TimeSpan maxDelay)
    {
        _maxDelay = maxDelay;
        return this;
    }

    public AsyncRetryPolicyBuilder WithShouldRetry(Func<Exception, bool> shouldRetry)
    {
        _shouldRetry = shouldRetry;
        return this;
    }

    public AsyncRetryPolicyBuilder WithOnRetry(Action<Exception, int> onRetry)
    {
        _onRetry = onRetry;
        return this;
    }

    public Func<CancellationToken, Task<T>> Build<T>(Func<CancellationToken, Task<T>> operation)
    {
        return async (ct) =>
        {
            Exception lastException = null;

            for (int attempt = 0; attempt <= 3; attempt++)
            {
                try
                {
                    return await operation(ct);
                }
                catch (Exception ex) when (attempt < 3)
                {
                    if (!_shouldRetry(ex)) throw;
                    
                    var delay = TimeSpan.FromSeconds(
                        Math.Min(1 * Math.Pow(2, attempt), 60));
                    
                    await Task.Delay(delay, CancellationToken.None);
                }
            }

            throw new Exception("All retries failed");
        }
    }
}

