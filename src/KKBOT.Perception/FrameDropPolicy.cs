// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KKBOT.Perception;

/// <summary>
/// Frame drop policy with backpressure handling for perception pipeline.
/// </summary>
public sealed class FrameDropPolicy : IDisposable
{
    private readonly ILogger<FrameDropPolicy> _logger;
    private readonly FrameDropOptions _options;
    private readonly ConcurrentQueue<FrameData> _frameQueue = new();
    private readonly SemaphoreSlim _queueSemaphore;
    private long _totalFramesReceived = 0;
    private long _totalFramesProcessed = 0;
    private long _totalFramesDropped = 0;
    private long _totalFramesDroppedByPolicy = 0;
    private readonly Timer _statsTimer;
    private readonly Timer _backpressureTimer;
    private bool _disposed;
    private BackpressureLevel _currentBackpressureLevel = BackpressureLevel.None;

    public FrameDropPolicy(
        ILogger<FrameDropPolicy> logger,
        IOptions<FrameDropOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        
        _queueSemaphore = new SemaphoreSlim(_options.MaxQueueSize, _options.MaxQueueSize);

        // Stats reporting timer
        _statsTimer = new Timer(ReportStats, null, 
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

        // Backpressure monitoring timer
        _backpressureTimer = new Timer(MonitorBackpressure, null,
            TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

        _logger.LogInformation("Frame Drop Policy initialized. Max queue: {MaxQueue}, Drop policy: {Policy}", 
            _options.MaxQueueSize, _options.DropPolicy);
    }

    /// <summary>
    /// Enqueues a frame for processing with backpressure handling.
    /// </summary>
    public async ValueTask<FrameEnqueueResult> EnqueueAsync(
        FrameData frame,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _totalFramesReceived);

        // Check if we should drop immediately based on policy
        var dropDecision = ShouldDropFrame();
        if (dropDecision.ShouldDrop)
        {
            Interlocked.Increment(ref _totalFramesDropped);
            Interlocked.Increment(ref _totalFramesDroppedByPolicy);
            
            _logger.LogDebug("Frame {FrameId} dropped by policy: {Reason}", 
                frame.Id, dropDecision.Reason);

            return FrameEnqueueResult.Dropped(frame.Id, dropDecision.Reason);
        }

        // Try to acquire semaphore (non-blocking with timeout)
        var acquired = await _queueSemaphore.WaitAsync(
            TimeSpan.FromMilliseconds(_options.EnqueueTimeoutMs), 
            cancellationToken);

        if (!acquired)
        {
            Interlocked.Increment(ref _totalFramesDropped);
            
            _logger.LogWarning("Frame {FrameId} dropped - queue full (timeout)", frame.Id);
            return FrameEnqueueResult.Dropped(frame.Id, "Queue timeout");
        }

        try
        {
            _frameQueue.Enqueue(frame);
            Interlocked.Increment(ref _totalFramesProcessed);
            
            _logger.LogTrace("Frame {FrameId} enqueued. Queue depth: {Depth}", 
                frame.Id, _frameQueue.Count);

            return FrameEnqueueResult.Enqueued(frame.Id);
        }
        catch
        {
            _queueSemaphore.Release();
            throw;
        }
    }

    /// <summary>
    /// Dequeues a frame for processing.
    /// </summary>
    public bool TryDequeue(out FrameData? frame)
    {
        var result = _frameQueue.TryDequeue(out frame);
        if (result)
        {
            _queueSemaphore.Release();
        }
        return result;
    }

    /// <summary>
    /// Tries to dequeue with timeout.
    /// </summary>
    public async ValueTask<FrameData?> DequeueAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (_frameQueue.TryDequeue(out var frame))
            {
                _queueSemaphore.Release();
                return frame;
            }
            await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken);
        }
        return null;
    }

    /// <summary>
    /// Gets current queue statistics.
    /// </summary>
    public FrameDropStats GetStats()
    {
        return new FrameDropStats
        {
            QueueDepth = _frameQueue.Count,
            MaxQueueSize = _options.MaxQueueSize,
            QueueUtilizationPercent = (double)_frameQueue.Count / _options.MaxQueueSize * 100,
            TotalFramesReceived = Interlocked.Read(ref _totalFramesReceived),
            TotalFramesProcessed = Interlocked.Read(ref _totalFramesProcessed),
            TotalFramesDropped = Interlocked.Read(ref _totalFramesDropped),
            TotalFramesDroppedByPolicy = Interlocked.Read(ref _totalFramesDroppedByPolicy),
            CurrentBackpressureLevel = _currentBackpressureLevel,
            DropPolicy = _options.DropPolicy
        };
    }

    /// <summary>
    /// Determines if a frame should be dropped based on current policy.
    /// </summary>
    private DropDecision ShouldDropFrame()
    {
        var queueDepth = _frameQueue.Count;
        var utilization = (double)queueDepth / _options.MaxQueueSize;

        return _options.DropPolicy switch
        {
            FrameDropPolicyType.DropOldest => new DropDecision
            {
                ShouldDrop = queueDepth >= _options.MaxQueueSize,
                Reason = "Queue at capacity (DropOldest)"
            },
            FrameDropPolicyType.DropNewest => new DropDecision
            {
                ShouldDrop = queueDepth >= _options.MaxQueueSize,
                Reason = "Queue at capacity (DropNewest)"
            },
            FrameDropPolicyType.DropRandom => new DropDecision
            {
                ShouldDrop = queueDepth >= _options.MaxQueueSize && Random.Shared.NextDouble() < 0.5,
                Reason = "Queue at capacity (DropRandom)"
            },
            FrameDropPolicyType.Adaptive => new DropDecision
            {
                ShouldDrop = utilization > _options.AdaptiveDropThreshold,
                Reason = "Adaptive drop threshold exceeded: " + utilization.ToString("P1") + " > " + _options.AdaptiveDropThreshold.ToString("P1")
            },
            FrameDropPolicyType.PriorityBased => new DropDecision
            {
                // Priority-based dropping handled at enqueue time
                ShouldDrop = false,
                Reason = "Priority-based (handled at enqueue)"
            },
            _ => new DropDecision { ShouldDrop = false, Reason = "Unknown policy" }
        };
    }

    private void MonitorBackpressure(object? state)
    {
        if (_disposed) return;

        var utilization = (double)_frameQueue.Count / _options.MaxQueueSize;
        var newLevel = CalculateBackpressureLevel(utilization);

        if (newLevel != _currentBackpressureLevel)
        {
            var oldLevel = _currentBackpressureLevel;
            _currentBackpressureLevel = newLevel;
            
            _logger.LogWarning("Backpressure level changed: {OldLevel} -> {NewLevel} (Utilization: {Utilization:P1})", 
                oldLevel, newLevel, utilization);
        }
    }

    private BackpressureLevel CalculateBackpressureLevel(double utilization)
    {
        return utilization switch
        {
            >= 0.95 => BackpressureLevel.Critical,
            >= 0.80 => BackpressureLevel.High,
            >= 0.60 => BackpressureLevel.Medium,
            >= 0.40 => BackpressureLevel.Low,
            _ => BackpressureLevel.None
        };
    }

    private void ReportStats(object? state)
    {
        if (_disposed) return;

        var stats = GetStats();
        _logger.LogInformation(
            "Frame Drop Stats: Received={Received}, Processed={Processed}, Dropped={Dropped}, " +
            "Queue={Queue}/{MaxQueue} ({Utilization:P1}), Backpressure={Backpressure}",
            stats.TotalFramesReceived,
            stats.TotalFramesProcessed,
            stats.TotalFramesDropped,
            stats.QueueDepth,
            stats.MaxQueueSize,
            stats.QueueUtilizationPercent,
            stats.CurrentBackpressureLevel);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _statsTimer?.Dispose();
        _backpressureTimer?.Dispose();
        _queueSemaphore?.Dispose();
    }
}

/// <summary>
/// Frame data for perception pipeline.
/// </summary>
public sealed class FrameData
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public byte[] ImageData { get; init; } = Array.Empty<byte>();
    public int Width { get; init; }
    public int Height { get; init; }
    public int Priority { get; init; } = 0;
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// Result of frame enqueue operation.
/// </summary>
public sealed class FrameEnqueueResult
{
    public string FrameId { get; init; } = string.Empty;
    public bool Enqueued { get; init; }
    public string? DropReason { get; init; }

    public static FrameEnqueueResult Enqueued(string frameId)
    {
        return new FrameEnqueueResult { FrameId = frameId, Enqueued = true };
    }

    public static FrameEnqueueResult Dropped(string frameId, string reason)
    {
        return new FrameEnqueueResult { FrameId = frameId, Enqueued = false, DropReason = reason };
    }
}

/// <summary>
/// Decision on whether to drop a frame.
/// </summary>
internal sealed class DropDecision
{
    public bool ShouldDrop { get; init; }
    public string Reason { get; init; } = string.Empty;
}

/// <summary>
/// Frame drop statistics.
/// </summary>
public sealed class FrameDropStats
{
    public int QueueDepth { get; init; }
    public int MaxQueueSize { get; init; }
    public double QueueUtilizationPercent { get; init; }
    public long TotalFramesReceived { get; init; }
    public long TotalFramesProcessed { get; init; }
    public long TotalFramesDropped { get; init; }
    public long TotalFramesDroppedByPolicy { get; init; }
    public BackpressureLevel CurrentBackpressureLevel { get; init; }
    public FrameDropPolicyType DropPolicy { get; init; }
}

/// <summary>
/// Frame drop policy types.
/// </summary>
public enum FrameDropPolicyType
{
    DropOldest,
    DropNewest,
    DropRandom,
    Adaptive,
    PriorityBased
}

/// <summary>
/// Backpressure levels.
/// </summary>
public enum BackpressureLevel
{
    None,
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Options for frame drop policy.
/// </summary>
public sealed class FrameDropOptions
{
    public int MaxQueueSize { get; set; } = 100;
    public FrameDropPolicyType DropPolicy { get; set; } = FrameDropPolicyType.Adaptive;
    public double AdaptiveDropThreshold { get; set; } = 0.8;
    public int EnqueueTimeoutMs { get; set; } = 100;
    public bool EnablePriorityQueuing { get; set; } = true;
}

/// <summary>
/// Extension methods for registering frame drop policy.
/// </summary>
public static class FrameDropPolicyExtensions
{
    public static IServiceCollection AddFrameDropPolicy(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FrameDropOptions>(configuration.GetSection("FrameDropPolicy"));
        services.AddSingleton<FrameDropPolicy>();
        services.AddHostedService(sp => sp.GetRequiredService<FrameDropPolicy>());
        return services;
    }
}

/// <summary>
/// Decision on whether to drop a frame.
/// </summary>
internal sealed class DropDecision
{
    public bool ShouldDrop { get; init; }
    public string Reason { get; init; } = string.Empty;
}

/// <summary>
/// Frame drop policy types.
/// </summary>
public enum FrameDropPolicyType
{
    DropOldest,
    DropNewest,
    DropRandom,
    Adaptive,
    PriorityBased
}

/// <summary>
/// Backpressure levels.
/// </summary>
public enum BackpressureLevel
{
    None,
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Options for frame drop policy.
/// </summary>
public sealed class FrameDropOptions
{
    public int MaxQueueSize { get; set; } = 100;
    public FrameDropPolicyType DropPolicy { get; set; } = FrameDropPolicyType.Adaptive;
    public double AdaptiveDropThreshold { get; set; } = 0.8;
    public int EnqueueTimeoutMs { get; set; } = 100;
    public bool EnablePriorityQueuing { get; set; } = true;
}

/// <summary>
/// Extension methods for registering frame drop policy.
/// </summary>
public static class FrameDropPolicyExtensions
{
    public static IServiceCollection AddFrameDropPolicy(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FrameDropOptions>(configuration.GetSection("FrameDropPolicy"));
        services.AddSingleton<FrameDropPolicy>();
        services.AddHostedService(sp => sp.GetRequiredService<FrameDropPolicy>());
        return services;
    }
}