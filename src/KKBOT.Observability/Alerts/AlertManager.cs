// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace KKBOT.Observability.Alerts;

public sealed class AlertManager
{
    private readonly ILogger<AlertManager> _logger;
    private readonly List<AlertRule> _rules = new();
    public AlertManager(ILogger<AlertManager> logger) => _logger = logger;
    public void AddRule(AlertRule rule) => _rules.Add(rule);
    public void Evaluate(string metricName, double value)
    {
        foreach (var rule in _rules)
        {
            if (rule.MetricName == metricName && value > rule.Threshold)
            {
                _logger.LogWarning("Alert triggered: {Name} > {Threshold} (value={Value})", rule.Name, rule.Threshold, value);
                // In real implementation we would send to webhook/slack.
            }
        }
    }
}

public sealed record AlertRule(string Name, string MetricName, double Threshold, string? NotificationChannel = null);