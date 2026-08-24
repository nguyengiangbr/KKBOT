// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace KKBOT.Observability.Alerts;

/// <summary>
/// Alert rule definition.
/// </summary>
public sealed record AlertRule(
    string Name,
    string MetricName,
    AlertCondition Condition,
    double Threshold,
    TimeSpan EvaluationWindow,
    string? NotificationChannel = null,
    Dictionary<string, string>? Labels = null,
    string? Description = null,
    AlertSeverity Severity = AlertSeverity.Warning);

/// <summary>
/// Alert condition type.
/// </summary>
public enum AlertCondition
{
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    Equal,
    NotEqual
}

/// <summary>
/// Alert severity.
/// </summary>
public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}

/// <summary>
/// Active alert state.
/// </summary>
public sealed record ActiveAlert(
    string RuleName,
    string MetricName,
    double CurrentValue,
    double Threshold,
    AlertSeverity Severity,
    DateTimeOffset TriggeredAt,
    Dictionary<string, string> Labels,
    string? Description);

/// <summary>
/// Alert manager for evaluating metrics against rules.
/// </summary>
public sealed class AlertManager : IHostedService, IDisposable
{
    private readonly ILogger<AlertManager> _logger;
    private readonly IOptionsMonitor<AlertManagerOptions> _options;
    private readonly IMetricsRegistry _metricsRegistry;
    private readonly ConcurrentDictionary<string, ActiveAlert> _activeAlerts = new();
    private readonly List<AlertRule> _rules = new();
    private Timer? _evaluationTimer;
    private readonly object _lock = new();

    public AlertManager(
        ILogger<AlertManager> logger,
        IOptionsMonitor<AlertManagerOptions> options,
        IMetricsRegistry metricsRegistry)
    {
        _logger = logger;
        _options = options;
        _metricsRegistry = metricsRegistry;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting AlertManager");
        
        LoadRules();
        _options.OnChange(_ => LoadRules());

        _evaluationTimer = new Timer(EvaluateAllRules, null, 
            TimeSpan.FromSeconds(10), _options.CurrentValue.EvaluationInterval);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping AlertManager");
        _evaluationTimer?.Dispose();
        return Task.CompletedTask;
    }

    public void AddRule(AlertRule rule)
    {
        lock (_lock)
        {
            _rules.Add(rule);
            _logger.LogInformation("Added alert rule: {Name}", rule.Name);
        }
    }

    public void RemoveRule(string ruleName)
    {
        lock (_lock)
        {
            _rules.RemoveAll(r => r.Name == ruleName);
        }
    }

    public IReadOnlyList<ActiveAlert> GetActiveAlerts()
    {
        return _activeAlerts.Values.ToList().AsReadOnly();
    }

    public IReadOnlyList<AlertRule> GetRules()
    {
        lock (_lock)
        {
            return _rules.ToList().AsReadOnly();
        }
    }

    private void LoadRules()
    {
        var newRules = _options.CurrentValue.Rules;
        lock (_lock)
        {
            _rules.Clear();
            _rules.AddRange(newRules);
        }
        _logger.LogInformation("Loaded {Count} alert rules", newRules.Count);
    }

    private void EvaluateAllRules(object? state)
    {
        try
        {
            lock (_lock)
            {
                foreach (var rule in _rules)
                {
                    EvaluateRule(rule);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating alert rules");
        }
    }

    private void EvaluateRule(AlertRule rule)
    {
        // In a real implementation, we would query the metrics registry
        // For now, this is a placeholder that shows the pattern
        // Actual implementation would use PromQL or query the metric directly
        
        _logger.LogDebug("Evaluating rule: {Name}", rule.Name);
        
        // Example: Check if we have a metric value
        // var value = _metricsRegistry.GetCurrentValue(rule.MetricName);
        // if (value.HasValue && EvaluateCondition(value.Value, rule.Condition, rule.Threshold))
        // {
        //     TriggerAlert(rule, value.Value);
        // }
        // else if (_activeAlerts.TryGetValue(rule.Name, out var existing))
        // {
        //     ResolveAlert(rule.Name);
        // }
    }

    private bool EvaluateCondition(double value, AlertCondition condition, double threshold)
    {
        return condition switch
        {
            AlertCondition.GreaterThan => value > threshold,
            AlertCondition.GreaterThanOrEqual => value >= threshold,
            AlertCondition.LessThan => value < threshold,
            AlertCondition.LessThanOrEqual => value <= threshold,
            AlertCondition.Equal => Math.Abs(value - threshold) < 0.001,
            AlertCondition.NotEqual => Math.Abs(value - threshold) >= 0.001,
            _ => false
        };
    }

    private void TriggerAlert(AlertRule rule, double currentValue)
    {
        var alert = new ActiveAlert(
            rule.Name,
            rule.MetricName,
            currentValue,
            rule.Threshold,
            rule.Severity,
            DateTimeOffset.UtcNow,
            rule.Labels ?? new Dictionary<string, string>(),
            rule.Description);

        _activeAlerts[rule.Name] = alert;

        _logger.LogWarning("ALERT TRIGGERED: {Name} - {Metric} = {Value} (threshold: {Threshold}) [{Severity}]",
            rule.Name, rule.MetricName, currentValue, rule.Threshold, rule.Severity);

        // Send notification (webhook, Slack, PagerDuty, etc.)
        if (!string.IsNullOrEmpty(rule.NotificationChannel))
        {
            SendNotificationAsync(rule.NotificationChannel, alert).FireAndForget(_logger);
        }
    }

    private void ResolveAlert(string ruleName)
    {
        if (_activeAlerts.TryRemove(ruleName, out var alert))
        {
            _logger.LogInformation("ALERT RESOLVED: {Name} (was active for {Duration})",
                ruleName, DateTimeOffset.UtcNow - alert.TriggeredAt);
        }
    }

    private async Task SendNotificationAsync(string channel, ActiveAlert alert)
    {
        // Implementation would send to webhook, Slack, etc.
        await Task.CompletedTask;
    }

    public void Dispose()
    {
        _evaluationTimer?.Dispose();
    }
}

/// <summary>
/// Options for AlertManager.
/// </summary>
public sealed class AlertManagerOptions
{
    public TimeSpan EvaluationInterval { get; set; } = TimeSpan.FromMinutes(1);
    public List<AlertRule> Rules { get; set; } = new()
    {
        new AlertRule(
            "High Instance CPU",
            "kkbot_instance_cpu_usage_percent",
            AlertCondition.GreaterThan,
            80,
            TimeSpan.FromMinutes(5),
            Severity: AlertSeverity.Warning,
            Description: "Instance CPU usage exceeds 80%"),
        
        new AlertRule(
            "High Instance Memory",
            "kkbot_instance_memory_usage_mb",
            AlertCondition.GreaterThan,
            1024,
            TimeSpan.FromMinutes(5),
            Severity: AlertSeverity.Warning,
            Description: "Instance memory usage exceeds 1GB"),
        
        new AlertRule(
            "Job Queue Depth",
            "kkbot_job_queue_depth",
            AlertCondition.GreaterThan,
            100,
            TimeSpan.FromMinutes(2),
            Severity: AlertSeverity.Critical,
            Description: "Job queue depth exceeds 100"),
        
        new AlertRule(
            "Device Capture Latency High",
            "kkbot_device_capture_latency_ms",
            AlertCondition.GreaterThan,
            500,
            TimeSpan.FromMinutes(5),
            Severity: AlertSeverity.Warning,
            Description: "Device capture latency exceeds 500ms"),
        
        new AlertRule(
            "AI Inference Latency High",
            "kkbot_ai_inference_latency_ms",
            AlertCondition.GreaterThan,
            10000,
            TimeSpan.FromMinutes(5),
            Severity: AlertSeverity.Warning,
            Description: "AI inference latency exceeds 10s"),
        
        new AlertRule(
            "Detection Confidence Low",
            "kkbot_detection_confidence",
            AlertCondition.LessThan,
            0.5,
            TimeSpan.FromMinutes(10),
            Severity: AlertSeverity.Warning,
            Description: "Detection confidence below 50%"),
        
        new AlertRule(
            "Plugin Load Failures",
            "kkbot_plugin_loads_failed_total",
            AlertCondition.GreaterThan,
            0,
            TimeSpan.FromMinutes(1),
            Severity: AlertSeverity.Critical,
            Description: "Plugin load failures detected")
    };
}

/// <summary>
/// Interface for metrics registry access.
/// </summary>
public interface IMetricsRegistry
{
    double? GetCurrentValue(string metricName);
}

/// <summary>
/// Extension for fire-and-forget tasks with logging.
/// </summary>
file static class TaskExtensions
{
    public static void FireAndForget(this Task task, ILogger logger)
    {
        _ = task.ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                logger.LogError(t.Exception, "Fire-and-forget task failed");
            }
        }, TaskScheduler.Default);
    }
}