// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KKBOT.Observability;
using KKBOT.Observability.Tracing;

namespace KKBOT.Performance.Profiling;

/// <summary>
/// Performance profiler for detecting bottlenecks and monitoring performance.
/// </summary>
public sealed class PerformanceProfiler : IDisposable
{
    private readonly ILogger<PerformanceProfiler> _logger;
    private readonly ProfilingOptions _options;
    private readonly IMetricsRegistry _metrics;
    private readonly ConcurrentDictionary<string, MethodProfile> _methodProfiles = new();
    private readonly ConcurrentDictionary<string, AllocationProfile> _allocationProfiles = new();
    private readonly Timer? _reportTimer;
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly IMetricsRegistry _metrics;
    private bool _disposed;

    public PerformanceProfiler(
        ILogger<PerformanceProfiler> logger,
        IOptions<ProfilingOptions> options,
        IMetricsRegistry metrics)
    {
        _logger = logger;
        _options = options.Value;
        _metrics = metrics;

        if (_options.EnableContinuousProfiling)
        {
            _reportTimer = new Timer(ReportAsync, null, 
                TimeSpan.FromSeconds(_options.ReportIntervalSeconds), 
                TimeSpan.FromSeconds(_options.ReportIntervalSeconds));
        }
    }

    /// <summary>
    /// Profiles a method execution.
    /// </summary>
    public T Profile<T>(string methodName, Func<T> action, 
        [CallerMemberName] string callerName = "", 
        [CallerFilePath] string callerFile = "", 
        [CallerLineNumber] int callerLine = 0)
    {
        var key = $"{callerName}::{methodName}";
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var result = action();
            stopwatch.Stop();
            RecordMethodExecution(key, stopwatch.Elapsed, true);
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            RecordMethodExecution(key, stopwatch.Elapsed, false);
            throw;
        }
    }

    /// <summary>
    /// Profiles an async method execution.
    /// </summary>
    public async Task<T> ProfileAsync<T>(string methodName, Func<Task<T>> action,
        [CallerMemberName] string callerName = "", 
        [CallerFilePath] string callerFile = "", 
        [CallerLineNumber] int callerLine = 0)
    {
        var key = $"{callerName}::{methodName}";
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var result = await action();
            stopwatch.Stop();
            RecordMethodExecution(key, stopwatch.Elapsed, true);
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            RecordMethodExecution(key, stopwatch.Elapsed, false);
            throw;
        }
    }

    /// <summary>
    /// Profiles a void method execution.
    /// </summary>
    public void Profile(string methodName, Action action,
        [CallerMemberName] string callerName = "", 
        [CallerFilePath] string callerFile = "", 
        [CallerLineNumber] int callerLine = 0)
    {
        var key = $"{callerName}::{methodName}";
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            action();
            stopwatch.Stop();
            RecordMethodExecution(key, stopwatch.Elapsed, true);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            RecordMethodExecution(key, stopwatch.Elapsed, false);
            throw;
        }
    }

    /// <summary>
    /// Profiles an async void method execution.
    /// </summary>
    public async Task ProfileAsync(string methodName, Func<Task> action,
        [CallerMemberName] string callerName = "", 
        [CallerFilePath] string callerFile = "", 
        [CallerLineNumber] int callerLine = 0)
    {
        var key = $"{callerName}::{methodName}";
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            await action();
            stopwatch.Stop();
            RecordMethodExecution(key, stopwatch.Elapsed, true);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            RecordMethodExecution(key, stopwatch.Elapsed, false);
            throw;
        }
    }

    /// <summary>
    /// Records memory allocation for a code block.
    /// </summary>
    public T TrackAllocations<T>(string operationName, Func<T> action,
        [CallerMemberName] string callerName = "", 
        [CallerFilePath] string callerFile = "", 
        [CallerLineNumber] int callerLine = 0)
    {
        if (!_options.TrackAllocations)
            return action();

        var key = $"{callerName}::{operationName}";
        var gen0Before = GC.CollectionCount(0);
        var gen1Before = GC.CollectionCount(1);
        var gen2Before = GC.CollectionCount(2);
        var memBefore = GC.GetTotalMemory(false);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = action();
            stopwatch.Stop();
            
            var memAfter = GC.GetTotalMemory(false);
            var gen0After = GC.CollectionCount(0);
            var gen1After = GC.CollectionCount(1);
            var gen2After = GC.CollectionCount(2);

            var key = $"{callerName}::{operationName}";
            var profile = _allocationProfiles.GetOrAdd(operationName, _ => new AllocationProfile());
            
            profile.TotalAllocations += memAfter - memAfter; // This would be measured differently in real impl
            profile.Gen0Collections += gen0After - gen0Before;
            profile.Gen1Collections += gen1After - gen1Before;
            profile.Gen2Collections += gen2After - gen2Before;
            profile.TotalTimeMs += stopwatch.Elapsed.TotalMilliseconds;
            profile.ExecutionCount++;

            return result;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Tracks memory allocations for an async operation.
    /// </summary>
    public async Task<T> TrackAllocationsAsync<T>(string operationName, Func<Task<T>> action,
        [CallerMemberName] string callerName = "", 
        [CallerFilePath] string callerFile = "", 
        [CallerLineNumber] int callerLine = 0)
    {
        if (!_options.TrackAllocations)
            return await action();

        var key = $"{callerName}::{operationName}";
        var memBefore = GC.GetTotalMemory(true);
        var gen0Before = GC.CollectionCount(0);
        var gen1Before = GC.CollectionCount(1);
        var gen2Before = GC.CollectionCount(2);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await action();
            stopwatch.Stop();
            
            var memAfter = GC.GetTotalMemory(false);
            var gen0After = GC.CollectionCount(0);
            var gen1After = GC.CollectionCount(1);
            var gen2After = GC.CollectionCount(2);

            var profile = _allocationProfiles.GetOrAdd(operationName, _ => new AllocationProfile());
            
            profile.TotalAllocations += memAfter - memBefore;
            profile.Gen0Collections += gen0After - gen0Before;
            profile.Gen1Collections += gen1After - gen1Before;
            profile.Gen2Collections += gen2After - gen2Before;
            profile.TotalTimeMs += stopwatch.Elapsed.TotalMilliseconds;
            profile.ExecutionCount++;

            return result;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    private void RecordMethodExecution(string key, TimeSpan elapsed, bool success)
    {
        var profile = _methodProfiles.GetOrAdd(key, _ => new MethodProfile());
        
        profile.TotalCalls++;
        profile.TotalTimeMs += elapsed.TotalMilliseconds;
        profile.LastExecution = DateTimeOffset.UtcNow;
        
        if (success)
        {
            profile.SuccessCount++;
            profile.TotalTimeMs += elapsed.TotalMilliseconds;
            profile.AverageTimeMs = profile.TotalTimeMs / profile.SuccessCount;
            profile.MaxTimeMs = Math.Max(profile.MaxTimeMs, elapsed.TotalMilliseconds);
            profile.MinTimeMs = profile.MinTimeMs == 0 ? elapsed.TotalMilliseconds : Math.Min(profile.MinTimeMs, elapsed.TotalMilliseconds);
        }
        else
        {
            profile.ErrorCount++;
        }

        // Update metrics
        _metrics.RecordHttpRequestDuration(key, elapsed.TotalMilliseconds);
    }

    private async void ReportAsync(object? state)
    {
        if (_shutdownCts.Token.IsCancellationRequested) return;

        try
        {
            var report = GenerateReport();
            
            // Log summary
            _logger.LogInformation("Performance Report: {MethodCount} methods, {TotalCalls} calls, Avg: {AvgMs:F2}ms, Errors: {ErrorCount}",
                report.MethodProfiles.Count,
                report.TotalCalls,
                report.AverageExecutionTimeMs,
                report.TotalErrors);

            // Log slowest methods
            foreach (var slow in report.SlowestMethods.Take(10))
            {
                _logger.LogWarning("Slow method: {Method} - Avg: {AvgMs:F2}ms, Max: {MaxMs:F2}ms, Calls: {Calls}, Errors: {Errors}",
                    slow.MethodName, slow.AverageTimeMs, slow.MaxTimeMs, slow.CallCount, slow.ErrorCount);
            }

            // Log allocation hotspots
            if (_options.TrackAllocations)
            {
                foreach (var alloc in report.TopAllocations.Take(5))
                {
                    _logger.LogInformation("Allocation hotspot: {Operation} - {Allocs:N0} bytes, Gen0: {Gen0}, Gen1: {Gen1}, Gen2: {Gen2}",
                        alloc.OperationName, alloc.TotalBytesAllocated, alloc.Gen0Collections, alloc.Gen1Collections, alloc.Gen2Collections);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating performance report");
        }
    }

    public ProfilingReport GenerateReport()
    {
        var profiles = _methodProfiles.Values.OrderByDescending(p => p.AverageTimeMs).ToList();
        var totalCalls = _methodProfiles.Values.Sum(p => p.TotalCalls);
        var totalErrors = _methodProfiles.Values.Sum(p => p.ErrorCount);
        var avgTime = _methodProfiles.Values.Where(p => p.SuccessCount > 0).Any() 
            ? _methodProfiles.Values.Where(p => p.SuccessCount > 0).Average(p => p.AverageTimeMs) 
            : 0;

        return new ProfilingReport
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            MethodProfiles = _methodProfiles.Values.OrderByDescending(p => p.AverageTimeMs).ToList(),
            AllocationProfiles = _allocationProfiles.Values.OrderByDescending(p => p.TotalAllocations).ToList(),
            TotalCalls = _methodProfiles.Values.Sum(p => p.TotalCalls),
            TotalErrors = _methodProfiles.Values.Sum(p => p.ErrorCount),
            AverageExecutionTimeMs = _methodProfiles.Values.Where(p => p.SuccessCount > 0).Any() 
                ? _methodProfiles.Values.Where(p => p.SuccessCount > 0).Average(p => p.AverageTimeMs) 
                : 0,
            SlowestMethods = _methodProfiles.Values
                .Where(p => p.SuccessCount > 0)
                .OrderByDescending(p => p.AverageTimeMs)
                .Take(20)
                .Select(p => new SlowMethodInfo
                {
                    MethodName = p.MethodName,
                    AverageTimeMs = p.AverageTimeMs,
                    MaxTimeMs = p.MaxTimeMs,
                    MinTimeMs = p.MinTimeMs,
                    CallCount = p.TotalCalls,
                    ErrorCount = p.ErrorCount,
                    ErrorRate = p.TotalCalls > 0 ? (double)p.ErrorCount / p.TotalCalls : 0
                }).ToList(),
            TopAllocations = _allocationProfiles.Values
                .OrderByDescending(p => p.TotalAllocations)
                .Take(10)
                .Select(p => new AllocationHotspot
                {
                    OperationName = p.OperationName,
                    TotalBytesAllocated = p.TotalAllocations,
                    Gen0Collections = p.Gen0Collections,
                    Gen1Collections = p.Gen1Collections,
                    Gen2Collections = p.Gen2Collections,
                    AverageAllocationPerCall = p.ExecutionCount > 0 ? p.TotalAllocations / p.ExecutionCount : 0
                }).ToList()
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _reportTimer?.Dispose();
        _shutdownCts.Cancel();
        _shutdownCts.Dispose();
    }
}

/// <summary>
/// Profiling options.
/// </summary>
public sealed class ProfilingOptions
{
    public bool EnableContinuousProfiling { get; set; } = true;
    public int ReportIntervalSeconds { get; set; } = 60;
    public bool TrackAllocations { get; set; } = true;
    public bool EnableDetailedTracing { get; set; } = false;
    public int MaxProfilesToKeep { get; set; } = 10000;
    public double SlowMethodThresholdMs { get; set; } = 100;
}

/// <summary>
/// Profiling report.
/// </summary>
public sealed record ProfilingReport
{
    public DateTimeOffset GeneratedAt { get; init; }
    public IReadOnlyList<MethodProfile> MethodProfiles { get; init; } = Array.Empty<MethodProfile>();
    public IReadOnlyList<AllocationProfile> AllocationProfiles { get; init; } = Array.Empty<AllocationProfile>();
    public long TotalCalls { get; init; }
    public long TotalErrors { get; init; }
    public double AverageExecutionTimeMs { get; init; }
    public IReadOnlyList<SlowMethodInfo> SlowestMethods { get; init; } = Array.Empty<SlowMethodInfo>();
    public IReadOnlyList<AllocationHotspot> TopAllocations { get; init; } = Array.Empty<AllocationHotspot>();
}

/// <summary>
/// Method execution profile.
/// </summary>
public sealed class MethodProfile
{
    public string MethodName { get; init; } = string.Empty;
    public long TotalCalls { get; set; }
    public long SuccessCount { get; set; }
    public long ErrorCount { get; set; }
    public double TotalTimeMs { get; set; }
    public double AverageTimeMs => SuccessCount > 0 ? TotalTimeMs / SuccessCount : 0;
    public double MaxTimeMs { get; set; }
    public double MinTimeMs { get; set; } = double.MaxValue;
    public DateTimeOffset LastExecution { get; set; }
    public double ErrorRate => TotalCalls > 0 ? (double)ErrorCount / TotalCalls : 0;
}

public sealed record SlowMethodInfo(
    string MethodName,
    double AverageTimeMs,
    double MaxTimeMs,
    double MinTimeMs,
    long CallCount,
    long ErrorCount,
    double ErrorRate
);

/// <summary>
/// Allocation profile for tracking memory allocations.
/// </summary>
public sealed class AllocationProfile
{
    public string OperationName { get; init; } = string.Empty;
    public long TotalAllocations { get; set; }
    public long Gen0Collections { get; set; }
    public long Gen1Collections { get; set; }
    public long Gen2Collections { get; set; }
    public double TotalTimeMs { get; set; }
    public long ExecutionCount { get; set; }
    public double AverageAllocationPerCall => ExecutionCount > 0 ? TotalAllocations / ExecutionCount : 0;
}

/// <summary>
/// Allocation hotspot.
/// </summary>
public sealed record AllocationHotspot
{
    public string OperationName { get; init; } = string.Empty;
    public long TotalBytesAllocated { get; init; }
    public long Gen0Collections { get: init; }
    public long Gen1Collections { get: init; }
    public long Gen2Collections { get: init; }
    public double AverageAllocationPerCall { get; init; }
}

/// <summary>
/// Profiling options.
/// </summary>
public sealed class ProfilingOptions
{
    public bool EnableContinuousProfiling { get; set; } = true;
    public int ReportIntervalSeconds { get; set; } = 60;
    public bool TrackAllocations { get; set; } = true;
    public bool EnableDetailedTracing { get; set; } = false;
    public int MaxProfilesToKeep { get; set; } = 10000;
    public double SlowMethodThresholdMs { get; set; } = 100;
}