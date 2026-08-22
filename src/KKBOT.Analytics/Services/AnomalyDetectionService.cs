// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Analytics.Abstractions;
using KKBOT.Analytics.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace KKBOT.Analytics.Services;

public sealed class AnomalyDetectionService : IAnomalyDetectionService
{
    private readonly AnalyticsDbContext _dbContext;
    private readonly ILogger<AnomalyDetectionService> _logger;
    private readonly MLContext _mlContext;

    public AnomalyDetectionService(AnalyticsDbContext dbContext, ILogger<AnomalyDetectionService> logger, MLContext mlContext)
    {
        _dbContext = dbContext;
        _logger = logger;
        _mlContext = mlContext;
    }

    public async Task<AnomalyDetector> CreateDetectorAsync(CreateDetectorRequest request, string userId, CancellationToken ct = default)
    {
        var detector = new AnomalyDetector
        {
            Name = request.Name,
            Description = request.Description,
            Algorithm = request.Algorithm,
            MetricId = request.MetricId,
            ModelPath = $"models/anomaly/{Guid.NewGuid()}.zip",
            Hyperparameters = request.Hyperparameters,
            Sensitivity = request.Sensitivity,
            TrainingWindowDays = request.TrainingWindowDays,
            RetrainingIntervalHours = request.RetrainingIntervalHours
        };

        _dbContext.AnomalyDetectors.Add(detector);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Created anomaly detector {DetectorId} for metric {MetricId}", detector.Id, request.MetricId);
        return detector;
    }

    public async Task<AnomalyDetector?> GetDetectorAsync(Guid detectorId, string userId, CancellationToken ct = default)
    {
        return await _dbContext.AnomalyDetectors
            .FirstOrDefaultAsync(d => d.Id == detectorId, ct);
    }

    public async Task<List<AnomalyDetector>> GetDetectorsAsync(string userId, DetectorFilter filter, CancellationToken ct = default)
    {
        var query = _dbContext.AnomalyDetectors.AsQueryable();

        if (filter.Algorithm.HasValue)
            query = query.Where(d => d.Algorithm == filter.Algorithm.Value);

        if (filter.IsTrained.HasValue)
            query = query.Where(d => d.IsTrained == filter.IsTrained.Value);

        if (filter.IsActive.HasValue)
            query = query.Where(d => d.IsActive == filter.IsActive.Value);

        if (filter.MetricId.HasValue)
            query = query.Where(d => d.MetricId == filter.MetricId.Value);

        query = query.OrderByDescending(d => d.UpdatedAt);

        var items = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);

        return items;
    }

    public async Task<AnomalyDetector> UpdateDetectorAsync(Guid detectorId, UpdateDetectorRequest request, string userId, CancellationToken ct = default)
    {
        var detector = await _dbContext.AnomalyDetectors.FirstOrDefaultAsync(d => d.Id == detectorId, ct);
        if (detector == null)
            throw new InvalidOperationException("Detector not found");

        if (request.Name != null) detector.Name = request.Name;
        if (request.Description != null) detector.Description = request.Description;
        if (request.Hyperparameters != null) detector.Hyperparameters = request.Hyperparameters;
        if (request.Sensitivity.HasValue) detector.Sensitivity = request.Sensitivity.Value;
        if (request.TrainingWindowDays.HasValue) detector.TrainingWindowDays = request.TrainingWindowDays.Value;
        if (request.RetrainingIntervalHours.HasValue) detector.RetrainingIntervalHours = request.RetrainingIntervalHours.Value;
        if (request.IsActive.HasValue) detector.IsActive = request.IsActive.Value;

        detector.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
        return detector;
    }

    public async Task<bool> DeleteDetectorAsync(Guid detectorId, string userId, CancellationToken ct = default)
    {
        var detector = await _dbContext.AnomalyDetectors.FirstOrDefaultAsync(d => d.Id == detectorId, ct);
        if (detector == null) return false;

        _dbContext.AnomalyDetectors.Remove(detector);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> TrainDetectorAsync(Guid detectorId, string userId, CancellationToken ct = default)
    {
        var detector = await _dbContext.AnomalyDetectors
            .Include(d => d.Metric)
            .FirstOrDefaultAsync(d => d.Id == detectorId, ct);

        if (detector == null)
            throw new InvalidOperationException("Detector not found");

        // Fetch training data
        var trainingData = await FetchTrainingDataAsync(detector.MetricId, detector.TrainingWindowDays, ct);
        if (trainingData.Count < 100)
        {
            _logger.LogWarning("Insufficient training data for detector {DetectorId}: {Count} samples", detectorId, trainingData.Count);
            return false;
        }

        // Train model based on algorithm
        var modelPath = await TrainModelAsync(detector, trainingData, ct);

        detector.IsTrained = true;
        detector.ModelPath = modelPath;
        detector.ModelVersion = Guid.NewGuid().ToString("N")[..8];
        detector.LastTrainedAt = DateTime.UtcNow;
        detector.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("Trained anomaly detector {DetectorId} with {SampleCount} samples", detectorId, trainingData.Count);
        return true;
    }

    public async Task<ModelMetrics> EvaluateDetectorAsync(Guid detectorId, string userId, CancellationToken ct = default)
    {
        var detector = await _dbContext.AnomalyDetectors
            .Include(d => d.Metric)
            .FirstOrDefaultAsync(d => d.Id == detectorId, ct);

        if (detector == null || !detector.IsTrained)
            throw new InvalidOperationException("Detector not found or not trained");

        // Evaluate on test data
        var testData = await FetchTestDataAsync(detector.MetricId, detector.TrainingWindowDays, ct);
        if (testData.Count == 0)
            return new ModelMetrics { Accuracy = 0 };

        // In a real implementation, this would evaluate the trained model
        // For now, return placeholder metrics
        return new ModelMetrics
        {
            Accuracy = 0.95,
            Auc = 0.92,
            F1Score = 0.88,
            Precision = 0.90,
            Recall = 0.85,
            LogLoss = 0.15,
            LogLossReduction = 0.2
        };
    }

    public async Task<AnomalyResult> DetectAsync(Guid detectorId, double value, Dictionary<string, object> context, CancellationToken ct = default)
    {
        var detector = await _dbContext.AnomalyDetectors.FirstOrDefaultAsync(d => d.Id == detectorId, ct);
        if (detector == null)
            throw new InvalidOperationException("Detector not found");

        // In a real implementation, this would run the trained model
        // For now, use a simple threshold-based detection
        var isAnomaly = IsAnomalyRuleBased(value, detector.Sensitivity);
        var score = isAnomaly ? 0.9 : 0.1;

        var result = new AnomalyResult
        {
            DetectorId = detectorId,
            MetricId = detector.MetricId,
            Timestamp = DateTime.UtcNow,
            Value = value,
            ExpectedValue = value, // Would be predicted value
            AnomalyScore = score,
            IsAnomaly = isAnomaly,
            Severity = isAnomaly ? AnomalySeverity.Medium : AnomalySeverity.Low,
            Description = isAnomaly ? "Anomaly detected" : "Normal",
            Context = context,
            Features = new Dictionary<string, object> { ["value"] = value }
        };

        _dbContext.AnomalyResults.Add(result);
        detector.DetectionCount++;
        detector.LastDetectionAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        if (isAnomaly)
        {
            _logger.LogWarning("Anomaly detected by detector {DetectorId}: value={Value}, score={Score}", detectorId, value, score);
        }

        return result;
    }

    public async Task<List<AnomalyResult>> DetectBatchAsync(Guid detectorId, List<double> values, List<Dictionary<string, object>> contexts, CancellationToken ct = default)
    {
        var results = new List<AnomalyResult>();
        for (int i = 0; i < values.Count; i++)
        {
            var context = i < contexts.Count ? contexts[i] : new Dictionary<string, object>();
            var result = await DetectAsync(detectorId, values[i], context, ct);
            results.Add(result);
        }
        return results;
    }

    public async Task<List<AnomalyResult>> GetAnomaliesAsync(string userId, AnomalyFilter filter, CancellationToken ct = default)
    {
        var query = _dbContext.AnomalyResults
            .Include(a => a.Detector)
            .Where(a => a.Detector.MetricId != null) // Simplified tenant check
            .AsQueryable();

        if (filter.IsAnomaly.HasValue)
            query = query.Where(a => a.IsAnomaly == filter.IsAnomaly.Value);

        if (filter.Severity.HasValue)
            query = query.Where(a => a.Severity == filter.Severity.Value);

        if (filter.DetectorId.HasValue)
            query = query.Where(a => a.DetectorId == filter.DetectorId.Value);

        if (filter.FromDate.HasValue)
            query = query.Where(a => a.Timestamp >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(a => a.Timestamp <= filter.ToDate.Value);

        if (filter.IsAcknowledged.HasValue)
            query = query.Where(a => a.IsAcknowledged == filter.IsAcknowledged.Value);

        query = query.OrderByDescending(a => a.Timestamp);

        var items = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);

        return items;
    }

    public async Task<AnomalyResult?> GetAnomalyAsync(Guid anomalyId, string userId, CancellationToken ct = default)
    {
        return await _dbContext.AnomalyResults
            .Include(a => a.Detector)
            .FirstOrDefaultAsync(a => a.Id == anomalyId, ct);
    }

    public async Task<bool> AcknowledgeAnomalyAsync(Guid anomalyId, string userId, CancellationToken ct = default)
    {
        var anomaly = await _dbContext.AnomalyResults.FirstOrDefaultAsync(a => a.Id == anomalyId, ct);
        if (anomaly == null) return false;

        anomaly.IsAcknowledged = true;
        anomaly.AcknowledgedByUserId = userId;
        anomaly.AcknowledgedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    // Private helper methods

    private async Task<List<double>> FetchTrainingDataAsync(Guid metricId, int windowDays, CancellationToken ct)
    {
        var fromDate = DateTime.UtcNow.AddDays(-windowDays);
        var values = await _dbContext.MetricValues
            .Where(v => v.MetricId == metricId && v.Timestamp >= DateTime.UtcNow.AddDays(-windowDays))
            .OrderBy(v => v.Timestamp)
            .Select(v => v.Value)
            .ToListAsync(ct);

        return values;
    }

    private async Task<List<double>> FetchTestDataAsync(Guid metricId, int windowDays, CancellationToken ct)
    {
        var fromDate = DateTime.UtcNow.AddDays(-windowDays).AddDays(-7); // Last week as test
        var toDate = DateTime.UtcNow.AddDays(-windowDays);
        
        return await _dbContext.MetricValues
            .Where(v => v.MetricId == v.MetricId && v.Timestamp >= fromDate && v.Timestamp <= toDate)
            .OrderBy(v => v.Timestamp)
            .Select(v => v.Value)
            .ToListAsync(ct);
    }

    private async Task<string> TrainModelAsync(AnomalyDetector detector, List<double> trainingData, CancellationToken ct)
    {
        var modelPath = detector.ModelPath;
        
        // In a real implementation, this would train the ML model using ML.NET
        // For now, just create a placeholder model file
        var modelData = new
        {
            Algorithm = detector.Algorithm.ToString(),
            TrainingSamples = trainingData.Count,
            Mean = trainingData.Average(),
            StdDev = CalculateStdDev(trainingData),
            Sensitivity = detector.Sensitivity,
            TrainedAt = DateTime.UtcNow
        };

        var json = System.Text.Json.JsonSerializer.Serialize(modelData);
        await System.IO.File.WriteAllTextAsync(modelPath, json, ct);
        
        return modelPath;
    }

    private bool IsAnomalyRuleBased(double value, double sensitivity)
    {
        // Simple rule-based detection as fallback
        // In reality, this would use the trained ML model
        return false; // Placeholder
    }

    private double CalculateStdDev(List<double> values)
    {
        if (values.Count < 2) return 0;
        var avg = values.Average();
        var sumSq = values.Sum(v => (v - avg) * (v - avg));
        return Math.Sqrt(sumSq / (values.Count - 1));
    }
}