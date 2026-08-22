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

namespace KKBOT.Analytics.Services;

public sealed class MetricService : IMetricService
{
    private readonly AnalyticsDbContext _dbContext;
    private readonly ILogger<MetricService> _logger;

    public MetricService(AnalyticsDbContext dbContext, ILogger<MetricService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<MetricDefinition> CreateMetricAsync(CreateMetricRequest request, string userId, CancellationToken ct = default)
    {
        var metric = new MetricDefinition
        {
            Name = request.Name,
            DisplayName = request.DisplayName,
            Description = request.Description,
            Unit = request.Unit,
            Type = request.Type,
            Category = request.Category,
            Tags = request.Tags,
            QueryJson = request.QueryJson,
            DataSourceId = request.DataSourceId,
            RetentionDays = request.RetentionDays,
            AggregationIntervalSeconds = request.AggregationIntervalSeconds,
            Aggregation = request.Aggregation
        };

        _dbContext.Metrics.Add(metric);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Created metric {MetricId} for user {UserId}", metric.Id, userId);
        return metric;
    }

    public async Task<MetricDefinition?> GetMetricAsync(Guid metricId, string userId, CancellationToken ct = default)
    {
        return await _dbContext.Metrics
            .FirstOrDefaultAsync(m => m.Id == metricId, ct);
    }

    public async Task<List<MetricDefinition>> GetMetricsAsync(string userId, MetricFilter filter, CancellationToken ct = default)
    {
        var query = _dbContext.Metrics.AsQueryable();

        if (filter.Type.HasValue)
            query = query.Where(m => m.Type == filter.Type.Value);

        if (!string.IsNullOrWhiteSpace(filter.Category))
            query = query.Where(m => m.Category == filter.Category);

        if (filter.Tags?.Length > 0)
        {
            // Simplified tag filtering - would need proper JSON querying in production
        }

        if (filter.IsActive.HasValue)
            query = query.Where(m => m.IsActive == filter.IsActive.Value);

        query = query.OrderByDescending(m => m.UpdatedAt);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);

        return items;
    }

    public async Task<MetricDefinition> UpdateMetricAsync(Guid metricId, UpdateMetricRequest request, string userId, CancellationToken ct = default)
    {
        var metric = await _dbContext.Metrics.FirstOrDefaultAsync(m => m.Id == metricId, ct);
        if (metric == null)
            throw new InvalidOperationException("Metric not found");

        if (request.DisplayName != null) metric.DisplayName = request.DisplayName;
        if (request.Description != null) metric.Description = request.Description;
        if (request.Unit != null) metric.Unit = request.Unit;
        if (request.Category != null) metric.Category = request.Category;
        if (request.Tags != null) metric.Tags = request.Tags;
        if (request.QueryJson != null) metric.QueryJson = request.QueryJson;
        if (request.RetentionDays.HasValue) metric.RetentionDays = request.RetentionDays.Value;
        if (request.AggregationIntervalSeconds.HasValue) metric.AggregationIntervalSeconds = request.AggregationIntervalSeconds.Value;
        if (request.Aggregation.HasValue) metric.Aggregation = request.Aggregation.Value;
        if (request.IsActive.HasValue) metric.IsActive = request.IsActive.Value;

        metric.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
        return metric;
    }

    public async Task<bool> DeleteMetricAsync(Guid metricId, string userId, CancellationToken ct = default)
    {
        var metric = await _dbContext.Metrics.FirstOrDefaultAsync(m => m.Id == metricId, ct);
        if (metric == null) return false;

        _dbContext.Metrics.Remove(metric);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<MetricValue> RecordValueAsync(RecordMetricValueRequest request, CancellationToken ct = default)
    {
        var metric = await _dbContext.Metrics.FirstOrDefaultAsync(m => m.Id == request.MetricId, ct);
        if (metric == null)
            throw new InvalidOperationException($"Metric {request.MetricId} not found");

        var value = new MetricValue
        {
            MetricId = request.MetricId,
            Value = request.Value,
            Timestamp = request.Timestamp ?? DateTime.UtcNow,
            Labels = request.Labels ?? new Dictionary<string, string>(),
            Min = request.Min,
            Max = request.Max,
            Count = request.Count,
            Sum = request.Sum
        };

        _dbContext.MetricValues.Add(value);
        await _dbContext.SaveChangesAsync(ct);
        return value;
    }

    public async Task<List<MetricValue>> GetValuesAsync(Guid metricId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        return await _dbContext.MetricValues
            .Where(v => v.MetricId == metricId && v.Timestamp >= from && v.Timestamp <= to)
            .OrderBy(v => v.Timestamp)
            .ToListAsync(ct);
    }

    public async Task<AggregatedMetricResult> GetAggregatedAsync(Guid metricId, DateTime from, DateTime to, AggregationFunction aggregation, TimeSpan interval, CancellationToken ct = default)
    {
        var values = await _dbContext.MetricValues
            .Where(v => v.MetricId == metricId && v.Timestamp >= from && v.Timestamp <= to)
            .ToListAsync(ct);

        if (!values.Any())
            return new AggregatedMetricResult(new List<MetricValue>(), 0, 0, 0, 0, 0, 0);

        var aggregated = aggregation switch
        {
            AggregationFunction.Sum => values.Sum(v => v.Value),
            AggregationFunction.Avg => values.Average(v => v.Value),
            AggregationFunction.Min => values.Min(v => v.Value),
            AggregationFunction.Max => values.Max(v => v.Value),
            AggregationFunction.Count => values.Count,
            AggregationFunction.Count => values.Count,
            _ => values.Average(v => v.Value)
        };

        // Simple grouping by interval
        var grouped = values
            .GroupBy(v => new DateTime(v.Timestamp.Ticks / interval.Ticks * interval.Ticks))
            .Select(g => new MetricValue
            {
                MetricId = g.Key.Id,
                Timestamp = g.Key,
                Value = aggregation switch
                {
                    AggregationFunction.Sum => g.Sum(v => v.Value),
                    AggregationFunction.Avg => g.Average(v => v.Value),
                    AggregationFunction.Min => g.Min(v => v.Value),
                    AggregationFunction.Max => g.Max(v => v.Value),
                    _ => g.Average(v => v.Value)
                }
            })
            .OrderBy(v => v.Timestamp)
            .ToList();

        return new AggregatedMetricResult
        {
            Values = grouped,
            Sum = values.Sum(v => v.Value),
            Avg = values.Average(v => v.Value),
            Min = values.Min(v => v.Value),
            Max = values.Max(v => v.Value),
            Count = values.Count,
            StdDev = CalculateStdDev(values.Select(v => v.Value).ToList())
        };
    }

    private double CalculateStdDev(List<double> values)
    {
        if (values.Count < 2) return 0;
        var avg = values.Average();
        var sumSq = values.Sum(v => (v - avg) * (v - avg));
        return Math.Sqrt(sumSq / (values.Count - 1));
    }
}