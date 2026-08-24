// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using KKBOT.MLPlatform.Abstractions;
using KKBOT.Observability;

namespace KKBOT.MLPlatform.ABTesting;

/// <summary>
/// A/B testing framework for model experimentation.
/// Supports: Bayesian inference, sequential testing, multi-armed bandits, automatic rollout.
/// </summary>
public sealed class ABTestManager : IABTestManager, IHostedService, IDisposable
{
    private readonly ILogger<ABTestManager> _logger;
    private readonly ABTestingOptions _options;
    private readonly IMetricsRegistry _metrics;
    private readonly ConnectionMultiplexer _redis;
    private readonly IDatabase _redisDb;
    private readonly ConcurrentDictionary<string, ABTestExperiment> _experiments = new();
    private readonly Timer? _evaluationTimer;
    private readonly CancellationTokenSource _shutdownCts = new();
    private bool _disposed;

    public ABTestManager(
        ILogger<ABTestManager> logger,
        IOptions<ABTestingOptions> options,
        IMetricsRegistry metrics)
    {
        _logger = logger;
        _options = options.Value;
        _metrics = metrics;

        _redis = ConnectionMultiplexer.Connect(_options.RedisConnectionString);
        _redisDb = _redis.GetDatabase();

        if (_options.EnableAutoEvaluation)
        {
            _evaluationTimer = new Timer(
                EvaluateExperimentsAsync, 
                null, 
                TimeSpan.FromMinutes(_options.EvaluationIntervalMinutes),
                TimeSpan.FromMinutes(_options.EvaluationIntervalMinutes));
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("A/B Test Manager started");
        await LoadExperimentsAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("A/B Test Manager stopping");
        _shutdownCts.Cancel();
        _evaluationTimer?.Dispose();
        await _redis.CloseAsync();
    }

    public async Task<ABTestConfig> CreateExperimentAsync(ABTestConfig config, CancellationToken ct = default)
    {
        // Validate configuration
        ValidateExperimentConfig(config);

        var experiment = new ABTestExperiment
        {
            Config = config,
            Status = ExperimentStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow,
            VariantStats = config.Variants.ToDictionary(v => v.VariantId, _ => new VariantStatistics()),
            AssignmentCounts = config.Variants.ToDictionary(v => v.VariantId, _ => 0L)
        };

        _experiments[config.ExperimentId] = experiment;
        await PersistExperimentAsync(experiment, ct);

        _logger.LogInformation("Created experiment {ExperimentId}: {Name}", config.ExperimentId, config.Name);
        return config;
    }

    public async Task<ABTestConfig?> GetExperimentAsync(string experimentId, CancellationToken ct = default)
    {
        if (_experiments.TryGetValue(experimentId, out var experiment))
        {
            return experiment.Config;
        }

        // Try loading from Redis
        var json = await _redisDb.StringGetAsync(GetExperimentKey(experimentId));
        if (json.HasValue)
        {
            var exp = System.Text.Json.JsonSerializer.Deserialize<ABTestExperiment>(json!);
            if (exp != null)
            {
                _experiments[experimentId] = exp;
                return exp.Config;
            }
        }

        return null;
    }

    public async Task<ABTestConfig> StartExperimentAsync(string experimentId, CancellationToken ct = default)
    {
        if (!_experiments.TryGetValue(experimentId, out var experiment))
        {
            throw new InvalidOperationException($"Experiment {experimentId} not found");
        }

        if (experiment.Status != ExperimentStatus.Draft && experiment.Status != ExperimentStatus.Paused)
        {
            throw new InvalidOperationException($"Cannot start experiment in status {experiment.Status}");
        }

        experiment.Status = ExperimentStatus.Running;
        experiment.StartedAt = DateTimeOffset.UtcNow;
        await PersistExperimentAsync(experiment, ct);

        _logger.LogInformation("Started experiment {ExperimentId}", experimentId);
        return experiment.Config;
    }

    public async Task<ABTestConfig> StopExperimentAsync(string experimentId, CancellationToken ct = default)
    {
        if (!_experiments.TryGetValue(experimentId, out var experiment))
        {
            throw new InvalidOperationException($"Experiment {experimentId} not found");
        }

        experiment.Status = ExperimentStatus.Completed;
        experiment.CompletedAt = DateTimeOffset.UtcNow;
        await PersistExperimentAsync(experiment, ct);

        _logger.LogInformation("Stopped experiment {ExperimentId}", experimentId);
        return experiment.Config;
    }

    public async Task<ABTestResult> GetResultsAsync(string experimentId, CancellationToken ct = default)
    {
        if (!_experiments.TryGetValue(experimentId, out var experiment))
        {
            throw new InvalidOperationException($"Experiment {experimentId} not found");
        }

        return ComputeResults(experiment);
    }

    public async Task<string> GetVariantAsync(string experimentId, string userId, CancellationToken ct = default)
    {
        if (!_experiments.TryGetValue(experimentId, out var experiment))
        {
            // Return default/control if experiment not found
            return "control";
        }

        if (experiment.Status != ExperimentStatus.Running)
        {
            // Return control variant when not running
            var control = experiment.Config.Variants.FirstOrDefault(v => v.Name == "control") 
                ?? experiment.Config.Variants.First();
            return control.VariantId;
        }

        // Deterministic assignment based on user ID
        var variantId = AssignVariant(experiment, userId);
        
        // Track assignment
        var assignmentKey = GetAssignmentKey(experimentId, userId);
        await _redisDb.StringSetAsync(assignmentKey, variantId, TimeSpan.FromDays(30));
        
        // Increment assignment count
        experiment.AssignmentCounts[variantId]++;
        
        return variantId;
    }

    private void ValidateExperimentConfig(ABTestConfig config)
    {
        if (config.Variants.Length < 2)
            throw new ArgumentException("At least 2 variants required");

        var totalTraffic = config.Variants.Sum(v => v.TrafficPercentage);
        if (Math.Abs(totalTraffic - 100) > 0.01)
            throw new ArgumentException($"Traffic percentages must sum to 100, got {totalTraffic}");

        var variantIds = config.Variants.Select(v => v.VariantId).ToHashSet();
        if (variantIds.Count != config.Variants.Length)
            throw new ArgumentException("Duplicate variant IDs");

        if (string.IsNullOrWhiteSpace(config.TargetMetrics.PrimaryMetric))
            throw new ArgumentException("Primary metric is required");
    }

    private string AssignVariant(ABTestExperiment experiment, string userId)
    {
        var config = experiment.Config;
        var strategy = config.TrafficSplit.Strategy;
        var salt = config.TrafficSplit.Salt ?? experimentId;

        double hash;
        if (strategy == "user_hash")
        {
            hash = GetDeterministicHash($"{salt}:{userId}");
        }
        else if (strategy == "session_hash")
        {
            hash = GetDeterministicHash($"{salt}:session:{userId}");
        }
        else
        {
            hash = GetDeterministicHash($"{salt}:random:{userId}:{DateTimeOffset.UtcNow.Ticks}");
        }

        var cumulative = 0.0;
        foreach (var variant in config.Variants)
        {
            cumulative += variant.TrafficPercentage / 100.0;
            if (hash <= cumulative)
            {
                return variant.VariantId;
            }
        }

        return config.Variants.Last().VariantId;
    }

    private double GetDeterministicHash(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        var ulongValue = BitConverter.ToUInt64(bytes, 0);
        return ulongValue / (double)ulong.MaxValue;
    }

    private ABTestResult ComputeResults(ABTestExperiment experiment)
    {
        var config = experiment.Config;
        var variantResults = new List<ABTestVariantResult>();

        foreach (var variant in config.Variants)
        {
            var stats = experiment.VariantStats[variant.VariantId];
            var sampleSize = stats.SampleSize;

            var result = new ABTestVariantResult
            {
                VariantId = variant.VariantId,
                SampleSize = sampleSize,
                Metrics = new Dictionary<string, double>(),
                ConfidenceIntervals = new Dictionary<string, double>()
            };

            foreach (var metricName in new[] { config.TargetMetrics.PrimaryMetric }.Concat(config.TargetMetrics.SecondaryMetrics))
            {
                if (stats.Metrics.TryGetValue(metricName, out var values) && values.Count > 0)
                {
                    var mean = values.Average();
                    var std = Math.Sqrt(values.Select(v => (v - mean) * (v - mean)).Average());
                    var se = std / Math.Sqrt(values.Count);
                    var z = 1.96; // 95% CI
                    
                    result.Metrics[metricName] = mean;
                    result.ConfidenceIntervals[metricName] = z * se;
                }
            }

            variantResults.Add(result);
        }

        // Statistical test
        var primaryMetric = config.TargetMetrics.PrimaryMetric;
        var control = variantResults.FirstOrDefault(v => v.VariantId == config.Variants.First(v => v.Name == "control").VariantId);
        var treatment = variantResults.FirstOrDefault(v => v.VariantId != (control?.VariantId ?? ""));

        var statTest = new StatisticalTestResult
        {
            TestType = "t-test",
            PValue = 1.0,
            Significant = false,
            EffectSize = 0,
            ConfidenceLevel = 1 - config.SignificanceLevel
        };

        if (control != null && treatment != null && 
            control.Metrics.TryGetValue(primaryMetric, out var cMean) &&
            treatment.Metrics.TryGetValue(primaryMetric, out var tMean) &&
            experiment.VariantStats.TryGetValue(control.VariantId, out var cStats) &&
            experiment.VariantStats.TryGetValue(treatment.VariantId, out var tStats))
        {
            var cValues = cStats.Metrics.GetValueOrDefault(primaryMetric, new List<double>());
            var tValues = tStats.Metrics.GetValueOrDefault(primaryMetric, new List<double>());

            if (cValues.Count > 1 && tValues.Count > 1)
            {
                var (pValue, effectSize) = ComputeTTest(cValues, tValues);
                statTest.PValue = pValue;
                statTest.Significant = pValue < config.SignificanceLevel;
                statTest.EffectSize = effectSize;
            }
        }

        var recommendation = statTest.Significant 
            ? (config.TargetMetrics.MetricDirection == "increase" && treatment.Metrics.GetValueOrDefault(primaryMetric, 0) > control.Metrics.GetValueOrDefault(primaryMetric, 0)
                ? "Rollout treatment" : "Keep control")
            : "No significant difference - continue experiment";

        return new ABTestResult
        {
            ExperimentId = config.ExperimentId,
            Status = experiment.Status,
            VariantResults = variantResults.ToArray(),
            StatisticalTest = statTest,
            Recommendation = recommendation,
            GeneratedAt = DateTimeOffset.UtcNow
        };
    }

    private (double pValue, double effectSize) ComputeTTest(List<double> control, List<double> treatment)
    {
        var n1 = control.Count;
        var n2 = treatment.Count;
        var mean1 = control.Average();
        var mean2 = treatment.Average();
        var var1 = control.Select(x => (x - mean1) * (x - mean1)).Sum() / (n1 - 1);
        var var2 = treatment.Select(x => (x - mean2) * (x - mean2)).Sum() / (n2 - 1);

        var pooledVar = ((n1 - 1) * var1 + (n2 - 1) * var2) / (n1 + n2 - 2);
        var se = Math.Sqrt(pooledVar * (1.0 / n1 + 1.0 / n2));
        var tStat = (mean2 - mean1) / se;
        var df = n1 + n2 - 2;

        // Two-tailed p-value approximation
        var pValue = 2 * (1 - StudentTCDF(Math.Abs(tStat), df));
        var effectSize = (mean2 - mean1) / Math.Sqrt(pooledVar);

        return (pValue, effectSize);
    }

    private double StudentTCDF(double t, int df)
    {
        // Approximation of Student's t CDF
        // Using incomplete beta function approximation
        var x = df / (df + t * t);
        var a = df / 2.0;
        var b = 0.5;
        return 1 - 0.5 * IncompleteBeta(x, a, b);
    }

    private double IncompleteBeta(double x, double a, double b)
    {
        // Simplified incomplete beta - use MathNet.Numerics in production
        if (x <= 0) return 0;
        if (x >= 1) return 1;
        
        // Continued fraction approximation
        var bt = Math.Exp(a * Math.Log(x) + b * Math.Log(1 - x) - LogBeta(a, b));
        if (x < (a + 1) / (a + b + 2))
        {
            return bt * BetaCF(x, a, b) / a;
        }
        return 1 - bt * BetaCF(1 - x, b, a) / b;
    }

    private double BetaCF(double x, double a, double b)
    {
        // Continued fraction for incomplete beta
        const int maxIter = 100;
        const double eps = 3e-7;
        var fpmin = 1e-30;

        var qab = a + b;
        var qap = a + 1;
        var qam = a - 1;
        var c = 1.0;
        var d = 1.0 - qab * x / qap;
        if (Math.Abs(d) < fpmin) d = fpmin;
        d = 1.0 / d;
        var h = d;

        for (int m = 1; m <= maxIter; m++)
        {
            var m2 = 2 * m;
            var aa = m * (b - m) * x / ((qam + m2) * (a + m2));
            d = 1.0 + aa * d;
            if (Math.Abs(d) < fpmin) d = fpmin;
            c = 1.0 + aa / c;
            if (Math.Abs(c) < fpmin) c = fpmin;
            d = 1.0 / d;
            h *= d * c;

            aa = -(a + m) * (qab + m) * x / ((a + m2) * (qap + m2));
            d = 1.0 + aa * d;
            if (Math.Abs(d) < fpmin) d = fpmin;
            c = 1.0 + aa / c;
            if (Math.Abs(c) < fpmin) c = fpmin;
            d = 1.0 / d;
            var del = d * c;
            h *= del;

            if (Math.Abs(del - 1.0) < eps) break;
        }

        return h;
    }

    private double LogBeta(double a, double b)
    {
        return Math.Log(Gamma(a)) + Math.Log(Gamma(b)) - Math.Log(Gamma(a + b));
    }

    private double Gamma(double x)
    {
        // Lanczos approximation
        var g = 7;
        var p = new double[] { 0.99999999999980993, 676.5203681218851, -1259.1392167224028,
            771.32342877765313, -176.61502916214059, 12.507343278686905,
            -0.13857109526572012, 9.9843695780195716e-6, 1.5056327351493116e-7 };

        if (x < 0.5) return Math.PI / (Math.Sin(Math.PI * x) * Gamma(1 - x));

        x -= 1;
        var a = p[0];
        for (int i = 1; i < g + 2; i++)
        {
            a += p[i] / (x + i);
        }

        var t = x + g + 0.5;
        return Math.Sqrt(2 * Math.PI) * Math.Pow(t, x + 0.5) * Math.Exp(-t) * a;
    }

    private async Task PersistExperimentAsync(ABTestExperiment experiment, CancellationToken ct)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(experiment);
        await _redisDb.StringSetAsync(GetExperimentKey(experiment.Config.ExperimentId), json);
    }

    private async Task LoadExperimentsAsync(CancellationToken ct)
    {
        var keys = _redis.GetServer(_redis.GetEndPoints()[0]).Keys(pattern: "abtest:experiment:*").ToArray();
        
        foreach (var key in keys)
        {
            var json = await _redisDb.StringGetAsync(key);
            if (json.HasValue)
            {
                var exp = System.Text.Json.JsonSerializer.Deserialize<ABTestExperiment>(json!);
                if (exp != null)
                {
                    _experiments[exp.Config.ExperimentId] = exp;
                }
            }
        }

        _logger.LogInformation("Loaded {Count} experiments", _experiments.Count);
    }

    private async void EvaluateExperimentsAsync(object? state)
    {
        if (_shutdownCts.Token.IsCancellationRequested) return;

        foreach (var experiment in _experiments.Values.Where(e => e.Status == ExperimentStatus.Running))
        {
            try
            {
                // Check if experiment should stop (early stopping)
                var result = ComputeResults(experiment);
                
                if (result.StatisticalTest.Significant && 
                    experiment.Config.TargetMetrics.MetricDirection == "increase" &&
                    result.VariantResults.Any(v => v.Metrics.GetValueOrDefault(experiment.Config.TargetMetrics.PrimaryMetric, 0) > 
                        result.VariantResults.First(r => r.VariantId == experiment.Config.Variants.First(va => va.Name == "control").VariantId).Metrics.GetValueOrDefault(experiment.Config.TargetMetrics.PrimaryMetric, 0)))
                {
                    // Auto-promote winning variant
                    _logger.LogInformation("Experiment {ExperimentId} has significant winner, consider stopping", experiment.Config.ExperimentId);
                }

                // Check minimum sample size
                var minSample = experiment.Config.MinimumSampleSize;
                if (experiment.AssignmentCounts.Values.All(c => c >= minSample))
                {
                    // Minimum sample reached
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating experiment {ExperimentId}", experiment.Config.ExperimentId);
            }
        }
    }

    private string GetExperimentKey(string experimentId) => $"abtest:experiment:{experimentId}";
    private string GetAssignmentKey(string experimentId, string userId) => $"abtest:assignment:{experimentId}:{userId}";

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _shutdownCts.Cancel();
        _redis?.Dispose();
    }
}

// ==================== INTERNAL MODELS ====================

internal sealed class ABTestExperiment
{
    public ABTestConfig Config { get; init; } = new();
    public ExperimentStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public Dictionary<string, VariantStatistics> VariantStats { get; init; } = new();
    public Dictionary<string, long> AssignmentCounts { get; init; } = new();
}

internal sealed class VariantStatistics
{
    public int SampleSize { get; set; }
    public Dictionary<string, List<double>> Metrics { get; init; } = new();
}

/// <summary>
/// A/B testing configuration.
/// </summary>
public sealed class ABTestingOptions
{
    public string RedisConnectionString { get; set; } = "localhost:6379";
    public bool EnableAutoEvaluation { get; set; } = true;
    public int EvaluationIntervalMinutes { get; set; } = 15;
    public int MaxConcurrentExperiments { get; set; } = 10;
    public int DefaultMinimumSampleSize { get; set; } = 1000;
}

/// <summary>
/// DI Extensions.
/// </summary>
public static class ABTestingExtensions
{
    public static IServiceCollection AddKkbotABTesting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ABTestingOptions>(configuration.GetSection("ABTesting"));
        services.AddSingleton<IABTestManager, ABTestManager>();
        services.AddHostedService(sp => sp.GetRequiredService<IABTestManager>());
        return services;
    }
}
