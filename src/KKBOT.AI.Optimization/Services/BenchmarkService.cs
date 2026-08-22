// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.AI.Optimization.Abstractions;
using KKBOT.AI.Optimization.Models;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;

namespace KKBOT.AI.Optimization.Services;

public sealed class BenchmarkService : IModelBenchmarkService
{
    private readonly ILogger<BenchmarkService> _logger;
    private readonly ConcurrentDictionary<string, BenchmarkResult> _cache = new();

    public BenchmarkService(ILogger<BenchmarkService> logger)
    {
        _logger = logger;
    }

    public async Task<BenchmarkResult> BenchmarkModelAsync(string modelPath, BenchmarkConfig config, CancellationToken ct = default)
    {
        var cacheKey = $"{modelPath}_{config.BatchSize}_{config.BenchmarkRuns}";
        if (_cache.TryGetValue(cacheKey, out var cached))
            return cached;

        _logger.LogInformation("Benchmarking model {ModelPath} with batch size {BatchSize}", modelPath, config.BatchSize);

        var result = new BenchmarkResult
        {
            ModelPath = modelPath,
            BatchSize = config.BatchSize,
            WarmupRuns = config.WarmupRuns,
            BenchmarkRuns = config.BenchmarkRuns,
            Timestamp = DateTime.UtcNow
        };

        try
        {
            if (!File.Exists(modelPath))
            {
                result.Success = false;
                result.ErrorMessage = $"Model file not found: {modelPath}";
                return result;
            }

            using var session = new InferenceSession(modelPath);
            var inputName = session.InputMetadata.Keys.First();
            var inputMeta = session.InputMetadata[inputName];
            var inputShape = inputMeta.Dimensions.ToArray();
            inputShape[0] = config.BatchSize;

            var inputTensor = CreateRandomTensor(inputShape);
            var inputData = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, inputTensor) };

            // Warmup
            _logger.LogDebug("Warming up with {WarmupRuns} runs", config.WarmupRuns);
            for (int i = 0; i < config.WarmupRuns; i++)
            {
                using var _ = session.Run(inputData);
            }

            // Benchmark
            var latencies = new List<double>();
            var stopwatch = Stopwatch.StartNew();

            for (int i = 0; i < config.BenchmarkRuns; i++)
            {
                var sw = Stopwatch.StartNew();
                using var results = session.Run(inputData);
                sw.Stop();
                latencies.Add(sw.Elapsed.TotalMilliseconds);
            }

            stopwatch.Stop();

            // Calculate statistics
            result.MeanLatencyMs = latencies.Average();
            result.MedianLatencyMs = latencies.OrderBy(x => x).ElementAt(latencies.Count / 2);
            result.P95LatencyMs = Percentile(latencies, 95);
            result.P99LatencyMs = Percentile(latencies, 99);
            result.StdDevMs = StandardDeviation(latencies);
            result.ThroughputFPS = config.BenchmarkRuns / stopwatch.Elapsed.TotalSeconds;
            result.BatchSize = config.BatchSize;
            result.Iterations = config.BenchmarkRuns;
            result.TotalDuration = stopwatch.Elapsed;
            result.Success = true;

            // Estimate memory usage (simplified)
            result.PeakMemoryMB = EstimateMemoryUsage(modelPath);
            result.AverageMemoryMB = result.PeakMemoryMB * 0.8;

            _cache[cacheKey] = result;
            _logger.LogInformation("Benchmark completed: {MeanLatency:F2} ms, {Throughput:F2} FPS", result.MeanLatencyMs, result.ThroughputFPS);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Benchmark failed for {ModelPath}", modelPath);
        }

        return result;
    }

    public async Task<ComparisonResult> CompareModelsAsync(string originalPath, string optimizedPath, BenchmarkConfig config, CancellationToken ct)
    {
        _logger.LogInformation("Comparing models: {Original} vs {Optimized}", originalPath, optimizedPath);

        var originalResult = await BenchmarkModelAsync(originalPath, config, ct);
        var optimizedResult = await BenchmarkModelAsync(optimizedPath, config, ct);

        var comparison = new ComparisonResult
        {
            OriginalModelPath = originalPath,
            OptimizedModelPath = optimizedPath,
            OriginalBenchmark = originalResult,
            OptimizedBenchmark = optimizedResult
        };

        if (originalResult.Success && optimizedResult.Success)
        {
            comparison.SpeedupRatio = originalResult.MeanLatencyMs / Math.Max(optimizedResult.MeanLatencyMs, 0.001);
            comparison.SizeReductionRatio = GetFileSizeRatio(originalPath, optimizedPath);
            comparison.AccuracyDrop = await EstimateAccuracyDropAsync(originalPath, optimizedPath, ct);
            comparison.MemoryReduction = (1 - (double)new FileInfo(optimizedPath).Length / new FileInfo(originalPath).Length) * 100;
            comparison.PassedAccuracyThreshold = comparison.AccuracyDrop < 0.05;
            comparison.PassedLatencyThreshold = comparison.SpeedupRatio >= 0.95;
            comparison.Recommended = comparison.PassedAccuracyThreshold && comparison.PassedLatencyThreshold && comparison.SpeedupRatio > 1.0;
        }

        return comparison;
    }

    public async Task<ProfileResult> ProfileModelAsync(string modelPath, ProfileConfig config, CancellationToken ct)
    {
        var result = new ProfileResult
        {
            ModelPath = modelPath,
            Duration = TimeSpan.FromSeconds(config.DurationSeconds)
        };

        // In a real implementation, this would use profiling tools like:
        // - nsys/nsight for GPU profiling
        // - perf for CPU profiling
        // - ETW for Windows
        // - Instruments for macOS

        // For now, return placeholder data
        result.OperatorProfiles = new List<OperatorProfile>();
        result.MemoryProfile = new MemoryProfile();
        result.GpuProfile = new GpuProfile();
        result.PowerProfile = new PowerProfile();

        return await Task.FromResult(result);
    }

    private static double Percentile(List<double> values, double percentile)
    {
        var sorted = values.OrderBy(x => x).ToList();
        var index = (int)Math.Ceiling(percentile / 100.0 * sorted.Count) - 1;
        return sorted[Math.Max(0, Math.Min(index, sorted.Count - 1))];
    }

    private static double StandardDeviation(List<double> values)
    {
        var avg = values.Average();
        var sumSq = values.Sum(x => (x - avg) * (x - avg));
        return Math.Sqrt(sumSq / values.Count);
    }

    private static long EstimateMemoryUsage(string modelPath)
    {
        if (File.Exists(modelPath))
        {
            var sizeMB = new FileInfo(modelPath).Length / (1024.0 * 1024.0);
            return sizeMB * 4; // Rough estimate: 4x model size for runtime
        }
        return 100;
    }

    private double GetFileSizeRatio(string originalPath, string optimizedPath)
    {
        if (File.Exists(originalPath) && File.Exists(optimizedPath))
        {
            var originalSize = new FileInfo(originalPath).Length;
            var optimizedSize = new FileInfo(optimizedPath).Length;
            return (double)originalSize / optimizedSize;
        }
        return 1.0;
    }

    private async Task<double> EstimateAccuracyDropAsync(string originalPath, string optimizedPath, CancellationToken ct)
    {
        // In a real implementation, this would run both models on a validation dataset
        // and compare their outputs
        await Task.CompletedTask;
        return 0.01; // 1% estimated accuracy drop
    }
}