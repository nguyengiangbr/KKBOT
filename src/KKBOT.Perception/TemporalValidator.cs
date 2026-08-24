// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KKBOT.Perception;

/// <summary>
/// Temporal validation for perception results to detect anomalies and ensure consistency.
/// </summary>
public sealed class TemporalValidator : IDisposable
{
    private readonly ILogger<TemporalValidator> _logger;
    private readonly TemporalValidatorOptions _options;
    private readonly ConcurrentDictionary<string, PerceptionHistory> _histories = new();
    private readonly Timer _cleanupTimer;
    private readonly CancellationTokenSource _shutdownCts = new();
    private bool _disposed;

    public TemporalValidator(
        ILogger<TemporalValidator> logger,
        IOptions<TemporalValidatorOptions> options)
    {
        _logger = logger;
        _options = options.Value;

        // Cleanup timer for old history entries
        _cleanupTimer = new Timer(CleanupOldHistory, null,
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

        _logger.LogInformation("Temporal Validator initialized. Max history: {MaxHistory}, Window: {Window}", 
            _options.MaxHistorySize, _options.ValidationWindow);
    }

    /// <summary>
    /// Validates a perception result against temporal history.
    /// </summary>
    public async ValueTask<ValidationResult> ValidateAsync(
        string contextId,
        PerceptionResult result,
        CancellationToken cancellationToken = default)
    {
        var history = _histories.GetOrAdd(contextId, _ => new PerceptionHistory());
        
        lock (history)
        {
            history.AddResult(result);
            
            // Trim history if too large
            while (history.Results.Count > _options.MaxHistorySize)
            {
                history.Results.RemoveAt(0);
            }
        }

        var validationResults = new List<ValidationCheck>();
        var isValid = true;
        var warnings = new List<string>();
        var errors = new List<string>();

        // Check 1: Temporal consistency (sudden changes)
        var consistencyCheck = CheckTemporalConsistency(history, result);
        validationResults.Add(consistencyCheck);
        if (!consistencyCheck.Passed)
        {
            isValid = false;
            if (consistencyCheck.Severity == ValidationSeverity.Error)
                errors.Add(consistencyCheck.Message);
            else
                warnings.Add(consistencyCheck.Message);
        }

        // Check 2: Confidence stability
        var confidenceCheck = CheckConfidenceStability(history, result);
        validationResults.Add(confidenceCheck);
        if (!confidenceCheck.Passed)
        {
            isValid = false;
            if (confidenceCheck.Severity == ValidationSeverity.Error)
                errors.Add(confidenceCheck.Message);
            else
                warnings.Add(confidenceCheck.Message);
        }

        // Check 3: Result duplication (stuck detection)
        var duplicationCheck = CheckResultDuplication(history, result);
        validationResults.Add(duplicationCheck);
        if (!duplicationCheck.Passed)
        {
            if (duplicationCheck.Severity == ValidationSeverity.Error)
                errors.Add(duplicationCheck.Message);
            else
                warnings.Add(duplicationCheck.Message);
        }

        // Check 4: Temporal ordering
        var orderingCheck = CheckTemporalOrdering(history, result);
        validationResults.Add(orderingCheck);
        if (!orderingCheck.Passed)
        {
            isValid = false;
            errors.Add(orderingCheck.Message);
        }

        // Check 5: Feature drift detection
        var driftCheck = CheckFeatureDrift(history, result);
        validationResults.Add(driftCheck);
        if (!driftCheck.Passed)
        {
            if (driftCheck.Severity == ValidationSeverity.Error)
                errors.Add(driftCheck.Message);
            else
                warnings.Add(driftCheck.Message);
        }

        var result = new ValidationResult
        {
            ContextId = contextId,
            IsValid = isValid,
            Checks = validationResults,
            Warnings = warnings,
            Errors = errors,
            ValidatedAt = DateTimeOffset.UtcNow
        };

        if (!isValid)
        {
            _logger.LogWarning("Temporal validation failed for {ContextId}: {Errors}", 
                contextId, string.Join("; ", errors));
        }
        else if (warnings.Count > 0)
        {
            _logger.LogInformation("Temporal validation passed with warnings for {ContextId}: {Warnings}", 
                contextId, string.Join("; ", warnings));
        }
        else
        {
            _logger.LogDebug("Temporal validation passed for {ContextId}", contextId);
        }

        return result;
    }

    private ValidationCheck CheckTemporalConsistency(PerceptionHistory history, PerceptionResult result)
    {
        if (history.Results.Count < 2)
        {
            return ValidationCheck.Passed("TemporalConsistency", "Insufficient history for consistency check");
        }

        var previous = history.Results[^1];
        var current = result;

        // Check for sudden position changes
        if (previous.Position != null && current.Position != null)
        {
            var distance = CalculateDistance(previous.Position.Value, current.Position.Value);
            var maxExpectedDistance = _options.MaxExpectedMovementPerFrame;
            
            if (distance > maxExpectedDistance)
            {
                return ValidationCheck.Failed(
                    "TemporalConsistency",
                    $"Sudden position change detected: {distance:F2} > {maxExpectedDistance:F2}",
                    ValidationSeverity.Warning);
            }
        }

        // Check for sudden size changes
        if (previous.BoundingBox != null && current.BoundingBox != null)
        {
            var sizeChange = Math.Abs(current.BoundingBox.Value.Area - previous.BoundingBox.Value.Area) 
                / previous.BoundingBox.Value.Area;
            
            if (sizeChange > _options.MaxSizeChangeRatio)
            {
                return ValidationCheck.Failed(
                    "TemporalConsistency",
                    $"Sudden size change detected: {sizeChange:P1} > {_options.MaxSizeChangeRatio:P1}",
                    ValidationSeverity.Warning);
            }
        }

        return ValidationCheck.Passed("TemporalConsistency", "Position and size changes within expected bounds");
    }

    private ValidationCheck CheckConfidenceStability(PerceptionHistory history, PerceptionResult result)
    {
        if (history.Results.Count < _options.MinHistoryForConfidenceCheck)
        {
            return ValidationCheck.Passed("ConfidenceStability", "Insufficient history for confidence check");
        }

        var recentResults = history.Results.TakeLast(_options.MinHistoryForConfidenceCheck).ToList();
        var avgConfidence = recentResults.Average(r => r.Confidence);
        var currentConfidence = result.Confidence;
        var confidenceDrop = avgConfidence - currentConfidence;

        if (confidenceDrop > _options.MaxConfidenceDrop)
        {
            return ValidationCheck.Failed(
                "ConfidenceStability",
                $"Significant confidence drop detected: {confidenceDrop:P1} > {_options.MaxConfidenceDrop:P1} (avg: {avgConfidence:P1}, current: {currentConfidence:P1})",
                ValidationSeverity.Error);
        }

        // Check for confidence oscillation
        var confidenceValues = recentResults.Select(r => r.Confidence).ToList();
        var variance = CalculateVariance(confidenceValues);
        
        if (variance > _options.MaxConfidenceVariance)
        {
            return ValidationCheck.Failed(
                "ConfidenceStability",
                $"Confidence oscillation detected: variance {variance:F4} > {_options.MaxConfidenceVariance:F4}",
                ValidationSeverity.Warning);
        }

        return ValidationCheck.Passed("ConfidenceStability", "Confidence stable within expected bounds");
    }

    private ValidationCheck CheckResultDuplication(PerceptionHistory history, PerceptionResult result)
    {
        if (history.Results.Count < _options.DuplicationCheckWindow)
        {
            return ValidationCheck.Passed("ResultDuplication", "Insufficient history for duplication check");
        }

        var recentResults = history.Results.TakeLast(_options.DuplicationCheckWindow).ToList();
        var duplicateCount = recentResults.Count(r => AreResultsSimilar(r, result));
        var duplicateRatio = (double)duplicateCount / recentResults.Count;

        if (duplicateRatio > _options.MaxDuplicateRatio)
        {
            return ValidationCheck.Failed(
                "ResultDuplication",
                $"High result duplication detected: {duplicateRatio:P1} > {_options.MaxDuplicateRatio:P1} (duplicates: {duplicateCount}/{recentResults.Count})",
                ValidationSeverity.Error);
        }

        return ValidationCheck.Passed("ResultDuplication", "Result duplication within acceptable bounds");
    }

    private ValidationCheck CheckTemporalOrdering(PerceptionHistory history, PerceptionResult result)
    {
        if (history.Results.Count == 0)
        {
            return ValidationCheck.Passed("TemporalOrdering", "First result, no ordering check needed");
        }

        var lastResult = history.Results[^1];
        
        if (result.Timestamp <= lastResult.Timestamp)
        {
            return ValidationCheck.Failed(
                "TemporalOrdering",
                $"Result timestamp {result.Timestamp} not after previous {lastResult.Timestamp}",
                ValidationSeverity.Error);
        }

        var timeDiff = result.Timestamp - lastResult.Timestamp;
        if (timeDiff > _options.MaxFrameInterval)
        {
            return ValidationCheck.Failed(
                "TemporalOrdering",
                $"Large time gap between frames: {timeDiff.TotalMilliseconds:F0}ms > {_options.MaxFrameInterval.TotalMilliseconds:F0}ms",
                ValidationSeverity.Warning);
        }

        return ValidationCheck.Passed("TemporalOrdering", "Timestamps in correct order with reasonable intervals");
    }

    private ValidationCheck CheckFeatureDrift(PerceptionHistory history, PerceptionResult result)
    {
        if (history.Results.Count < _options.MinHistoryForDriftCheck)
        {
            return ValidationCheck.Passed("FeatureDrift", "Insufficient history for drift check");
        }

        var recentResults = history.Results.TakeLast(_options.MinHistoryForDriftCheck).ToList();
        
        // Compare feature vectors if available
        if (result.FeatureVector != null && recentResults.All(r => r.FeatureVector != null))
        {
            var avgFeatureVector = AverageVectors(recentResults.Select(r => r.FeatureVector!).ToList());
            var distance = CosineDistance(result.FeatureVector, avgFeatureVector);
            
            if (distance > _options.MaxFeatureDrift)
            {
                return ValidationCheck.Failed(
                    "FeatureDrift",
                    $"Feature drift detected: cosine distance {distance:F4} > {_options.MaxFeatureDrift:F4}",
                    ValidationSeverity.Warning);
            }
        }

        return ValidationCheck.Passed("FeatureDrift", "Feature vectors stable within expected bounds");
    }

    private double CalculateDistance(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private double CalculateVariance(List<double> values)
    {
        if (values.Count < 2) return 0;
        var mean = values.Average();
        return values.Sum(v => Math.Pow(v - mean, 2)) / values.Count;
    }

    private double[] AverageVectors(List<double[]> vectors)
    {
        var length = vectors[0].Length;
        var avg = new double[length];
        
        foreach (var v in vectors)
        {
            for (int i = 0; i < length; i++)
            {
                avg[i] += v[i];
            }
        }
        
        for (int i = 0; i < length; i++)
        {
            avg[i] /= vectors.Count;
        }
        return avg;
    }

    private double CosineDistance(double[] a, double[] b)
    {
        if (a.Length != b.Length) return 1.0;
        
        double dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        
        if (normA == 0 || normB == 0) return 1.0;
        return 1.0 - (dot / (Math.Sqrt(normA) * Math.Sqrt(normB)));
    }

    private bool AreResultsSimilar(PerceptionResult a, PerceptionResult b)
    {
        if (a.Label != b.Label) return false;
        
        if (a.Position != null && b.Position != null)
        {
            var distance = CalculateDistance(a.Position.Value, b.Position.Value);
            if (distance > 5) return false; // 5 pixel threshold
        }
        
        return Math.Abs(a.Confidence - b.Confidence) < 0.05;
    }

    private void CleanupOldHistory(object? state)
    {
        if (_disposed) return;

        var cutoff = DateTimeOffset.UtcNow - _options.HistoryRetentionPeriod;
        
        foreach (var kvp in _histories)
        {
            lock (kvp.Value)
            {
                kvp.Value.Results.RemoveAll(r => r.Timestamp < cutoff);
            }
        }

        // Remove empty histories
        var emptyKeys = _histories.Where(kvp => kvp.Value.Results.Count == 0).Select(kvp => kvp.Key).ToList();
        foreach (var key in emptyKeys)
        {
            _histories.TryRemove(key, out _);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _shutdownCts.Cancel();
        _cleanupTimer?.Dispose();
    }
}

/// <summary>
/// Perception result data.
/// </summary>
public sealed class PerceptionResult
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string Label { get; init; } = string.Empty;
    public double Confidence { get; init; } = 0;
    public Point? Position { get; init; }
    public Rectangle? BoundingBox { get; init; }
    public double[]? FeatureVector { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// Perception history for a context.
/// </summary>
internal sealed class PerceptionHistory
{
    public List<PerceptionResult> Results { get; init; } = new();

    public void AddResult(PerceptionResult result)
    {
        Results.Add(result);
    }
}

/// <summary>
/// Validation result.
/// </summary>
public sealed class ValidationResult
{
    public string ContextId { get; init; } = string.Empty;
    public bool IsValid { get; init; }
    public List<ValidationCheck> Checks { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    public List<string> Errors { get; init; } = new();
    public DateTimeOffset ValidatedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Individual validation check result.
/// </summary>
public sealed class ValidationCheck
{
    public string Name { get; init; } = string.Empty;
    public bool Passed { get; init; }
    public string Message { get; init; } = string.Empty;
    public ValidationSeverity Severity { get; init; } = ValidationSeverity.Info;

    public static ValidationCheck Passed(string name, string message)
    {
        return new ValidationCheck { Name = name, Passed = true, Message = message, Severity = ValidationSeverity.Info };
    }

    public static ValidationCheck Failed(string name, string message, ValidationSeverity severity = ValidationSeverity.Error)
    {
        return new ValidationCheck { Name = name, Passed = false, Message = message, Severity = severity };
    }
}

/// <summary>
/// Validation severity levels.
/// </summary>
public enum ValidationSeverity
{
    Info,
    Warning,
    Error
}

/// <summary>
/// Point for position tracking.
/// </summary>
public readonly record struct Point(double X, double Y);

/// <summary>
/// Rectangle for bounding box.
/// </summary>
public readonly record struct Rectangle(double X, double Y, double Width, double Height)
{
    public double Area => Width * Height;
}

/// <summary>
/// Options for temporal validator.
/// </summary>
public sealed class TemporalValidatorOptions
{
    public int MaxHistorySize { get; set; } = 100;
    public TimeSpan ValidationWindow { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan HistoryRetentionPeriod { get; set; } = TimeSpan.FromMinutes(30);
    public int MinHistoryForConfidenceCheck { get; set; } = 5;
    public int MinHistoryForDriftCheck { get; set; } = 10;
    public int DuplicationCheckWindow { get; set; } = 10;
    public double MaxExpectedMovementPerFrame { get; set; } = 50.0; // pixels
    public double MaxSizeChangeRatio { get; set; } = 0.5;
    public double MaxConfidenceDrop { get; set; } = 0.3;
    public double MaxConfidenceVariance { get; set; } = 0.05;
    public double MaxDuplicateRatio { get; set; } = 0.8;
    public double MaxFeatureDrift { get; set; } = 0.3;
    public TimeSpan MaxFrameInterval { get; set; } = TimeSpan.FromSeconds(5);
    public int MinHistoryForConfidenceCheck { get; set; } = 5;
    public int MinHistoryForDriftCheck { get; set; } = 10;
    public int DuplicationCheckWindow { get; set; } = 10;
    public double MaxExpectedMovementPerFrame { get; set; } = 50.0;
    public double MaxSizeChangeRatio { get; set; } = 0.5;
    public double MaxConfidenceDrop { get; set; } = 0.3;
    public double MaxConfidenceVariance { get; set; } = 0.05;
    public double MaxDuplicateRatio { get; set; } = 0.8;
    public double MaxFeatureDrift { get; set; } = 0.3;
    public TimeSpan MaxFrameInterval { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan HistoryRetentionPeriod { get; set; } = TimeSpan.FromMinutes(30);
}

/// <summary>
/// Extension methods for registering temporal validator.
/// </summary>
public static class TemporalValidatorExtensions
{
    public static IServiceCollection AddTemporalValidator(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TemporalValidatorOptions>(configuration.GetSection("TemporalValidator"));
        services.AddSingleton<TemporalValidator>();
        services.AddHostedService(sp => sp.GetRequiredService<TemporalValidator>());
        return services;
    }
}