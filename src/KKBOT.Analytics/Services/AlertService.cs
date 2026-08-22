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

public sealed class AlertService : IAlertService
{
    private readonly AnalyticsDbContext _dbContext;
    private readonly ILogger<AlertService> _logger;

    public AlertService(AnalyticsDbContext dbContext, ILogger<AlertService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<AlertRule> CreateAlertRuleAsync(CreateAlertRuleRequest request, string userId, CancellationToken ct = default)
    {
        var rule = new AlertRule
        {
            Name = request.Name,
            Description = request.Description,
            MetricId = request.MetricId,
            Condition = request.Condition,
            Severity = request.Severity,
            Actions = request.Actions,
            EvaluationIntervalSeconds = request.EvaluationIntervalSeconds,
            ForDurationSeconds = request.ForDurationSeconds,
            TenantId = userId
        };

        _dbContext.AlertRules.Add(rule);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Created alert rule {RuleId} for metric {MetricId}", rule.Id, request.MetricId);
        return rule;
    }

    public async Task<AlertRule?> GetAlertRuleAsync(Guid ruleId, string userId, CancellationToken ct = default)
    {
        return await _dbContext.AlertRules
            .Include(r => r.Actions)
            .FirstOrDefaultAsync(r => r.Id == ruleId && r.TenantId == userId, ct);
    }

    public async Task<List<AlertRule>> GetAlertRulesAsync(string userId, AlertRuleFilter filter, CancellationToken ct = default)
    {
        var query = _dbContext.AlertRules
            .Include(r => r.Actions)
            .Where(r => r.TenantId == userId)
            .AsQueryable();

        if (filter.Severity.HasValue)
            query = query.Where(r => r.Severity == filter.Severity.Value);

        if (filter.IsActive.HasValue)
            query = query.Where(r => r.IsActive == filter.IsActive.Value);

        if (filter.MetricId.HasValue)
            query = query.Where(r => r.MetricId == filter.MetricId.Value);

        query = query.OrderByDescending(r => r.UpdatedAt);

        var items = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);

        return items;
    }

    public async Task<AlertRule> UpdateAlertRuleAsync(Guid ruleId, UpdateAlertRuleRequest request, string userId, CancellationToken ct = default)
    {
        var rule = await _dbContext.AlertRules
            .Include(r => r.Actions)
            .FirstOrDefaultAsync(r => r.Id == ruleId && r.TenantId == userId, ct);

        if (rule == null)
            throw new InvalidOperationException("Alert rule not found");

        if (request.Name != null) rule.Name = request.Name;
        if (request.Description != null) rule.Description = request.Description;
        if (request.Condition != null) rule.Condition = request.Condition;
        if (request.Severity.HasValue) rule.Severity = request.Severity.Value;
        if (request.Actions != null)
        {
            rule.Actions.Clear();
            rule.Actions.AddRange(request.Actions);
        }
        if (request.EvaluationIntervalSeconds.HasValue) rule.EvaluationIntervalSeconds = request.EvaluationIntervalSeconds.Value;
        if (request.ForDurationSeconds.HasValue) rule.ForDurationSeconds = request.ForDurationSeconds.Value;
        if (request.IsActive.HasValue) rule.IsActive = request.IsActive.Value;

        rule.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
        return rule;
    }

    public async Task<bool> DeleteAlertRuleAsync(Guid ruleId, string userId, CancellationToken ct = default)
    {
        var rule = await _dbContext.AlertRules.FirstOrDefaultAsync(r => r.Id == ruleId && r.TenantId == userId, ct);
        if (rule == null) return false;

        _dbContext.AlertRules.Remove(rule);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> TestAlertRuleAsync(Guid ruleId, string userId, CancellationToken ct = default)
    {
        var rule = await _dbContext.AlertRules
            .Include(r => r.Metric)
            .FirstOrDefaultAsync(r => r.Id == ruleId && r.TenantId == userId, ct);

        if (rule == null)
            throw new InvalidOperationException("Alert rule not found");

        // In a real implementation, this would evaluate the condition against recent data
        // For now, just return true
        return true;
    }

    public async Task<List<Alert>> GetAlertsAsync(string userId, AlertFilter filter, CancellationToken ct = default)
    {
        var query = _dbContext.Alerts
            .Include(a => a.AlertRule)
            .Where(a => a.AlertRule.TenantId == userId)
            .AsQueryable();

        if (filter.Status.HasValue)
            query = query.Where(a => a.Status == filter.Status.Value);

        if (filter.Severity.HasValue)
            query = query.Where(a => a.Severity == filter.Severity.Value);

        if (filter.AlertRuleId.HasValue)
            query = query.Where(a => a.AlertRuleId == filter.AlertRuleId.Value);

        if (filter.FromDate.HasValue)
            query = query.Where(a => a.StartedAt >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(a => a.StartedAt <= filter.ToDate.Value);

        query = query.OrderByDescending(a => a.StartedAt);

        var items = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);

        return items;
    }

    public async Task<Alert?> GetAlertAsync(Guid alertId, string userId, CancellationToken ct = default)
    {
        return await _dbContext.Alerts
            .Include(a => a.AlertRule)
            .FirstOrDefaultAsync(a => a.Id == alertId && a.AlertRule.TenantId == userId, ct);
    }

    public async Task<Alert> AcknowledgeAlertAsync(Guid alertId, AcknowledgeAlertRequest request, string userId, CancellationToken ct = default)
    {
        var alert = await _dbContext.Alerts
            .Include(a => a.AlertRule)
            .FirstOrDefaultAsync(a => a.Id == alertId && a.AlertRule.TenantId == userId, ct);

        if (alert == null)
            throw new InvalidOperationException("Alert not found");

        alert.Status = AlertStatus.Acknowledged;
        alert.AcknowledgedAt = DateTime.UtcNow;
        alert.AcknowledgedByUserId = userId;
        alert.AcknowledgedComment = request.Comment;

        await _dbContext.SaveChangesAsync(ct);
        return alert;
    }

    public async Task<bool> ResolveAlertAsync(Guid alertId, string userId, CancellationToken ct = default)
    {
        var alert = await _dbContext.Alerts
            .Include(a => a.AlertRule)
            .FirstOrDefaultAsync(a => a.Id == alertId && a.AlertRule.TenantId == userId, ct);

        if (alert == null) return false;

        alert.Status = AlertStatus.Resolved;
        alert.EndedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> SilenceAlertAsync(Guid alertId, SilenceAlertRequest request, string userId, CancellationToken ct = default)
    {
        var alert = await _dbContext.Alerts
            .Include(a => a.AlertRule)
            .FirstOrDefaultAsync(a => a.Id == alertId && a.AlertRule.TenantId == userId, ct);

        if (alert == null) return false;

        alert.Status = AlertStatus.Silenced;
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }
}