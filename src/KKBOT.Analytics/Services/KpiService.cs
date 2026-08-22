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

public sealed class KpiService : IKpiService
{
    private readonly AnalyticsDbContext _dbContext;
    private readonly ILogger<KpiService> _logger;

    public KpiService(AnalyticsDbContext dbContext, ILogger<KpiService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<KpiDefinition> CreateKpiAsync(CreateKpiRequest request, string userId, CancellationToken ct = default)
    {
        var kpi = new KpiDefinition
        {
            Name = request.Name,
            DisplayName = request.DisplayName,
            Description = request.Description,
            Category = request.Category,
            Unit = request.Unit,
            FormatString = request.FormatString,
            MetricId = request.MetricId,
            Target = request.Target,
            Thresholds = request.Thresholds,
            UpdateFrequencySeconds = request.UpdateFrequencySeconds,
            OwnerId = userId,
            Tags = request.Tags
        };

        _dbContext.KpiDefinitions.Add(kpi);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Created KPI {KpiId} for user {UserId}", kpi.Id, userId);
        return kpi;
    }

    public async Task<KpiDefinition?> GetKpiAsync(Guid kpiId, string userId, CancellationToken ct = default)
    {
        return await _dbContext.KpiDefinitions
            .Include(k => k.Metric)
            .FirstOrDefaultAsync(k => k.Id == kpiId && k.OwnerId == userId, ct);
    }

    public async Task<List<KpiDefinition>> GetKpisAsync(string userId, KpiFilter filter, CancellationToken ct = default)
    {
        var query = _dbContext.KpiDefinitions
            .Include(k => k.Metric)
            .Where(k => k.OwnerId == userId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Category))
            query = query.Where(k => k.Category == filter.Category);

        if (filter.IsActive.HasValue)
            query = query.Where(k => k.IsActive == filter.IsActive.Value);

        if (!string.IsNullOrWhiteSpace(filter.OwnerId))
            query = query.Where(k => k.OwnerId == filter.OwnerId);

        query = query.OrderByDescending(k => k.UpdatedAt);

        var items = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);

        return items;
    }

    public async Task<KpiDefinition> UpdateKpiAsync(Guid kpiId, UpdateKpiRequest request, string userId, CancellationToken ct = default)
    {
        var kpi = await _dbContext.KpiDefinitions
            .FirstOrDefaultAsync(k => k.Id == kpiId && k.OwnerId == userId, ct);

        if (kpi == null)
            throw new InvalidOperationException("KPI not found");

        if (request.DisplayName != null) kpi.DisplayName = request.DisplayName;
        if (request.Description != null) kpi.Description = request.Description;
        if (request.Category != null) kpi.Category = request.Category;
        if (request.Unit != null) kpi.Unit = request.Unit;
        if (request.FormatString != null) kpi.FormatString = request.FormatString;
        if (request.Target != null) kpi.Target = request.Target;
        if (request.Thresholds != null) kpi.Thresholds = request.Thresholds;
        if (request.UpdateFrequencySeconds.HasValue) kpi.UpdateFrequencySeconds = request.UpdateFrequencySeconds.Value;
        if (request.Tags != null) kpi.Tags = request.Tags;
        if (request.IsActive.HasValue) kpi.IsActive = request.IsActive.Value;

        kpi.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
        return kpi;
    }

    public async Task<bool> DeleteKpiAsync(Guid kpiId, string userId, CancellationToken ct = default)
    {
        var kpi = await _dbContext.KpiDefinitions.FirstOrDefaultAsync(k => k.Id == kpiId && k.OwnerId == userId, ct);
        if (kpi == null) return false;

        _dbContext.KpiDefinitions.Remove(kpi);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<KpiValue> GetCurrentValueAsync(Guid kpiId, CancellationToken ct = default)
    {
        var kpi = await _dbContext.KpiDefinitions
            .Include(k => k.Metric)
            .FirstOrDefaultAsync(k => k.Id == kpiId, ct);

        if (kpi == null)
            throw new InvalidOperationException("KPI not found");

        var latestValue = await _dbContext.MetricValues
            .Where(v => v.MetricId == kpi.MetricId)
            .OrderByDescending(v => v.Timestamp)
            .FirstOrDefaultAsync(ct);

        if (latestValue == null)
            throw new InvalidOperationException("No metric data available");

        var value = new KpiValue
        {
            KpiId = kpiId,
            Timestamp = latestValue.Timestamp,
            Value = latestValue.Value,
            TargetValue = kpi.Target.Value,
            PercentToTarget = kpi.Target.Value != 0 ? (latestValue.Value / kpi.Target.Value) * 100 : 0,
            Trend = 0, // Would calculate from history
            Breakdown = new Dictionary<string, object>()
        };

        // Determine status
        var thresholds = kpi.Thresholds;
        if (latestValue.Value <= (thresholds.CriticalLow ?? double.MinValue))
            value.Status = KpiStatus.OffTrack;
        else if (latestValue.Value <= (thresholds.WarningLow ?? double.MinValue))
            value.Status = KpiStatus.AtRisk;
        else if (latestValue.Value >= (thresholds.CriticalHigh ?? double.MaxValue))
            value.Status = KpiStatus.OffTrack;
        else if (latestValue.Value >= (thresholds.WarningHigh ?? double.MaxValue))
            value.Status = KpiStatus.AtRisk;
        else if (latestValue.Value >= (thresholds.Target ?? 0) && kpi.Target.Direction == TargetDirection.HigherIsBetter)
            value.Status = KpiStatus.Achieved;
        else if (latestValue.Value <= (thresholds.Target ?? 0) && kpi.Target.Direction == TargetDirection.LowerIsBetter)
            value.Status = KpiStatus.Achieved;
        else
            value.Status = KpiStatus.OnTrack;

        return value;
    }

    public async Task<List<KpiValue>> GetKpiHistoryAsync(Guid kpiId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var kpi = await _dbContext.KpiDefinitions.FirstOrDefaultAsync(k => k.Id == kpiId, ct);
        if (kpi == null)
            throw new InvalidOperationException("KPI not found");

        var values = await _dbContext.MetricValues
            .Where(v => v.MetricId == kpi.MetricId && v.Timestamp >= from && v.Timestamp <= to)
            .OrderBy(v => v.Timestamp)
            .ToListAsync(ct);

        return values.Select(v => new KpiValue
        {
            KpiId = kpiId,
            Timestamp = v.Timestamp,
            Value = v.Value,
            TargetValue = kpi.Target.Value,
            PercentToTarget = kpi.Target.Value != 0 ? (v.Value / kpi.Target.Value) * 100 : 0,
            Trend = 0,
            Breakdown = new Dictionary<string, object>()
        }).ToList();
    }
}