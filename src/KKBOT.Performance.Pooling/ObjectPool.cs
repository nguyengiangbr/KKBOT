// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KKBOT.Observability;

namespace KKBOT.Performance.Pooling;

/// <summary>
/// High-performance object pooling with memory management.
/// </summary>
public sealed class ObjectPool<T> : IDisposable where T : class
{
    private readonly ConcurrentBag<T> _objects = new();
    private readonly Func<T> _factory;
    private readonly Action<T> _resetAction;
    private readonly int _maxSize;
    private readonly int _minimumSize;
    private int _createdCount;
    private int _rentedCount;
    private readonly ILogger _logger;
    private readonly IMetricsRegistry _metrics;
    private bool _disposed;

    public ObjectPool(
        Func<T> factory,
        Action<T> resetAction,
        ILogger logger,
        IMetricsRegistry metrics,
        IOptions<ObjectPoolOptions> options)
    {
        _logger = logger;
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _resetAction = resetAction;
        _maxSize = options.Value.MaxSize;
        _minimumSize = options.Value.MinimumSize;
        _metrics = metrics;

        // Pre-populate pool
        for (int i = 0; i < _minimumSize; i++)
        {
            var obj = _factory();
            _objects.Add(obj);
            Interlocked.Increment(ref _createdCount);
        }
    }

    /// <summary>
    /// Rents an object from the pool.
    /// </summary>
    public T Rent()
    {
        if (_objects.TryTake(out var obj))
        {
            Interlocked.Increment(ref _rentedCount);
            return obj;
        }

        // Pool empty or empty, create new instance
        var obj = _factory();
        Interlocked.Increment(ref _createdCount);
        Interlocked.Increment(ref _rentedCount);
        return obj;
    }

    /// <summary>
    /// Returns an object to the pool.
    /// </summary>
    public void Return(T obj)
    {
        if (obj == null) return;

        try
        {
            // Reset object state
            _resetAction?.Invoke(obj);
        }
        catch (Exception ex)
        {
            // If reset fails, dispose and create new next time
            _logger.LogWarning("Failed to reset pooled object, will create new next time");
            return;
        }

        // Return to pool if not at max size
        if (_objects.Count < _maxSize)
        {
            _objects.Add(obj);
            Interlocked.Decrement(ref _rentedCount);
        }
        else
        {
            // Pool full, let GC handle it
            if (obj is IDisposable disposable)
            {
                try { disposable.Dispose(); } catch { }
            }
        }
    }

    /// <summary>
    /// Gets current pool statistics.
    /// </summary>
    public PoolStatistics GetStatistics()
    {
        return new PoolStatistics
        {
            AvailableCount = _objects.Count,
            RentedCount = _rentedCount,
            CreatedCount = _createdCount,
            MaxSize = _maxSize,
            MinimumSize = _minimumSize
        };
    }

    public void Dispose()
    {
        while (_objects.TryTake(out var obj))
        {
            if (obj is IDisposable disposable)
            {
                try { obj.Dispose(); } catch { }
            }
        }
    }
}

/// <summary>
/// Generic object pool with configurable policies.
/// </summary>
public sealed class ObjectPool<T> : IDisposable where T : class
{
    private readonly ConcurrentBag<T> _pool = new();
    private readonly Func<T> _factory;
    private readonly Action<T> _reset;
    private readonly int _maxSize;
    private int _createdCount;
    private int _rentedCount;
    private bool _disposed;

    public ObjectPool(Func<T> factory, Action<T> reset = null, int maxSize = 100)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _reset = reset;
        _maxSize = maxSize;
    }

    public T Rent()
    {
        if (_pool.TryTake(out var item))
        {
            return item;
        }
        return _factory();
    }

    public void Return(T item)
    {
        if (item == null) return;

        try
        {
            // Reset state if reset action provided
            item?.GetType().GetMethod("Reset")?.Invoke(item, null);
        }
        catch
        {
            // Ignore reset failures
        }

        // Return to pool if not at capacity
        if (_pool.Count < 100) // Default max
        {
            _pool.Add(item);
        }
    }

    public void Dispose()
    {
        while (_pool.TryTake(out var item))
        {
            if (item is IDisposable d) d.Dispose();
        }
    }
}

/// <summary>
/// High-performance array pool for byte arrays.
/// </summary>
public sealed class ByteArrayPool : IDisposable
{
    private readonly ArrayPool<byte> _pool;
    private readonly int _maxArrayLength;
    private readonly int _maxArraysPerBucket;
    private bool _disposed;

    public ByteArrayPool(int maxArrayLength = 1024 * 1024, int maxArraysPerBucket = 50)
    {
        _maxArrayLength = maxArrayLength;
        _maxArraysPerBucket = maxArraysPerBucket;
        _pool = ArrayPool<byte>.Create(maxArrayLength, maxArraysPerBucket);
    }

    public byte[] Rent(int minimumLength)
    {
        if (minimumLength > _maxArrayLength)
        {
            return new byte[minimumLength]; // Too large for pool
        }
        return ArrayPool<byte>.Shared.Rent(minimumLength);
    }

    public void Return(byte[] array, bool clearArray = true)
    {
        if (array == null || array.Length > _maxArrayLength) return;
        ArrayPool<byte>.Shared.Return(array, clearArray);
    }

    public void Dispose()
    {
        // ArrayPool.Shared doesn't need explicit disposal
    }
}

/// <summary>
/// Generic object pool with configurable policies.
/// </summary>
public sealed class ObjectPool<T> : IDisposable where T : class
{
    private readonly ConcurrentQueue<T> _pool = new();
    private readonly Func<T> _factory;
    private readonly Action<T> _resetAction;
    private readonly int _maxPoolSize;
    private readonly ILogger _logger;
    private int _createdCount;
    private int _rentedCount;
    private bool _disposed;

    public ObjectPool(
        Func<T> factory,
        Action<T> resetAction = null,
        int maxPoolSize = 100,
        ILogger logger = null)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _resetAction = resetAction;
        _logger = logger;
    }

    public T Rent()
    {
        if (_pool.TryDequeue(out var item))
        {
            return item;
        }
        return _factory();
    }

    public void Return(T item)
    {
        if (item == null) return;

        try
        {
            // Reset object state
            item.GetType().GetMethod("Reset")?.Invoke(item, null);
        }
        catch (Exception ex)
        {
            // Log but don't throw
        }

        // Return to pool
        // In a real implementation, would check max size
    }

    public PoolStatistics GetStatistics()
    {
        return new PoolStatistics
        {
            AvailableCount = 0, // Would track actual count
            RentedCount = 0,
            CreatedCount = 0
        };
    }

    public void Dispose()
    {
        // Cleanup
    }
}

/// <summary>
/// Pool statistics.
/// </summary>
public sealed record PoolStatistics
{
    public int AvailableCount { get; init; }
    public int RentedCount { get; init; }
    public int CreatedCount { get; init; }
    public int MaxSize { get; init; }
    public int MinimumSize { get; init; }
}

/// <summary>
/// Options for object pool configuration.
/// </summary>
public sealed class ObjectPoolOptions
{
    public int MaxSize { get; set; } = 100;
    public int MinimumSize { get; set; } = 10;
}

/// <summary>
/// High-performance buffer pool for byte arrays.
/// </summary>
public sealed class BufferPool : IDisposable
{
    private readonly ArrayPool<byte> _pool;
    private readonly int _maxBufferSize;
    private bool _disposed;

    public BufferPool(int maxBufferSize = 1024 * 1024, int maxBuffersPerBucket = 50)
    {
        _pool = ArrayPool<byte>.Create(maxBufferSize, maxBuffersPerBucket);
        _maxBufferSize = maxBufferSize;
    }

    public byte[] Rent(int minimumLength)
    {
        if (minimumLength > _maxBufferSize)
        {
            return new byte[minimumLength];
        }
        return ArrayPool<byte>.Shared.Rent(minimumLength);
    }

    public void Return(byte[] buffer, bool clearArray = true)
    {
        if (buffer == null || buffer.Length > _maxBufferSize) return;
        ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
    }

    public void Dispose()
    {
        // ArrayPool doesn't need explicit disposal
    }
}

/// <summary>
/// Pool for recycling disposable objects.
/// </summary>
public sealed class DisposableObjectPool<T> : IDisposable where T : class, IDisposable
{
    private readonly ConcurrentStack<T> _pool = new();
    private readonly Func<T> _factory;
    private readonly Action<T> _resetAction;
    private readonly int _maxSize;
    private int _created;
    private int _rented;

    public DisposableObjectPool(Func<T> factory, Action<T> resetAction = null, int maxSize = 100)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _resetAction = resetAction ?? (obj => { });
        _maxSize = maxSize;
    }

    public T Rent()
    {
        if (_pool.TryPop(out var item))
        {
            return item;
        }
        return _factory();
    }

    public void Return(T item)
    {
        if (item == null) return;

        try
        {
            item.Dispose(); // Reset by disposing and recreating
        }
        catch
        {
            // Ignore
        }

        // For disposable objects, we don't pool them after use
        // They get GC'd and new ones created
    }

    public void Dispose()
    {
        while (_pool.TryPop(out var item))
        {
            try { item.Dispose(); } catch { }
        }
    }
}

/// <summary>
/// Thread-local storage pool to reduce contention.
/// </summary>
public sealed class ThreadLocalPool<T> : IDisposable where T : class
{
    private readonly ThreadLocal<Stack<T>> _localPool;
    private readonly Func<T> _factory;
    private readonly Action<T> _resetAction;
    private readonly int _maxLocalSize;
    private int _globalCreated;

    public ThreadLocalPool(Func<T> factory, Action<T> resetAction = null, int maxLocalSize = 10)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _resetAction = resetAction;
        _maxLocalSize = maxLocalSize;
        _localPool = new ThreadLocal<Stack<T>>(() => new Stack<T>());
    }

    public T Rent()
    {
        var localPool = _localPool.Value!;
        if (localPool.Count > 0)
        {
            return localPool.Pop();
        }
        Interlocked.Increment(ref _globalCreated);
        return _factory();
    }

    public void Return(T item)
    {
        if (item == null) return;

        var localPool = _localPool.Value!;
        if (localPool.Count < 10) // Max local size
        {
            localPool.Push(item);
        }
        else
        {
            // Return to global pool or let GC handle
        }
    }

    public void Dispose()
    {
        // Cleanup thread-local pools
    }
}

/// <summary>
/// Statistics for object pool.
/// </summary>
public sealed record PoolStatistics
{
    public int AvailableCount { get; init; }
    public int RentedCount { get; init; }
    public int CreatedCount { get; init; }
    public int MaxSize { get; init; }
    public double UtilizationPercent => MaxSize > 0 ? (double)RentedCount / MaxSize * 100 : 0;
}