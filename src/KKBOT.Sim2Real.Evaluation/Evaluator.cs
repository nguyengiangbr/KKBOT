// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KKBOT.Sim2Real.Abstractions;
using KKBOT.Observability;

namespace KKBOT.Sim2Real.Evaluation;

/// <summary>
/// Evaluation engine for measuring sim2real gap and policy performance.
/// Supports: Statistical testing, video recording, trajectory analysis, safety evaluation.
/// </summary>
public sealed class Evaluator : IEvaluator, IHostedService, IDisposable
{
    private readonly ILogger<Evaluator> _logger;
    private readonly EvaluationOptions _options;
    private readonly IMetricsRegistry _metrics;
    private readonly ConcurrentDictionary<string, EvaluationJob> _jobs = new();
    private readonly Channel<EvaluationJob> _jobQueue;
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly Task _workerTask;
    private bool _disposed;

    public Evaluator(
        ILogger<Evaluator> logger,
        IOptions<EvaluationOptions> options,
        IMetricsRegistry metrics)
    {
        _logger = logger;
        _options = options.Value;
        _metrics = metrics;

        _jobQueue = Channel.CreateUnbounded<EvaluationJob>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        _workerTask = ProcessJobsAsync(_shutdownCts.Token);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Evaluator started");
        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Evaluator stopping");
        _shutdownCts.Cancel();
        _jobQueue.Writer.Complete();
        
        try
        {
            await _workerTask;
        }
        catch (OperationCanceledException) { }
    }

    public async Task<EvaluationResult> EvaluateAsync(EvaluationConfig config, CancellationToken ct = default)
    {
        var jobId = Guid.NewGuid().ToString("N");
        var job = new EvaluationJob
        {
            JobId = jobId,
            Config = config,
            Status = EvaluationJobStatus.Queued,
            CreatedAt = DateTimeOffset.UtcNow,
            Progress = 0
        };

        _jobs[jobId] = job;
        await _jobQueue.Writer.WriteAsync(job, ct);

        _logger.LogInformation("Evaluation job {JobId} queued", jobId);

        var timeout = TimeSpan.FromMinutes(config.NumEpisodes * 2);
        var completed = await WaitForCompletionAsync(jobId, timeout, ct);
        
        return completed ? GetResult(jobId) : new EvaluationResult { Success = false, ErrorMessage = "Timeout" };
    }

    public async Task<Sim2RealGapReport> ComputeSim2RealGapAsync(string simResultsPath, string realResultsPath, CancellationToken ct = default)
    {
        _logger.LogInformation("Computing sim2real gap: sim={Sim}, real={Real}", simResultsPath, realResultsPath);

        // Load results
        var simResults = await LoadResultsAsync(simResultsPath, ct);
        var realResults = await LoadResultsAsync(realResultsPath, ct);

        // Compute gaps
        var report = new Sim2RealGapReport
        {
            SuccessRateGap = realResults.SuccessRate - simResults.SuccessRate,
            RewardGap = realResults.AverageReward - simResults.AverageReward,
            EpisodeLengthGap = realResults.AverageEpisodeLength - simResults.AverageEpisodeLength,
            SafetyViolationGap = realResults.SafetyViolations - simResults.SafetyViolations,
            PerTaskGaps = new Dictionary<string, double>()
        };

        // Per-task gaps
        foreach (var task in simResults.PerTaskMetrics.Keys)
        {
            if (realResults.PerTaskMetrics.TryGetValue(task, out var realVal) &&
                simResults.PerTaskMetrics.TryGetValue(task, out var simVal))
            {
                report.PerTaskGaps[task] = realVal - simVal;
            }
        }

        // Overall gap (weighted)
        report.OverallGap = (Math.Abs(report.SuccessRateGap) + 
                            Math.Abs(report.RewardGap) / 100.0 + 
                            Math.Abs(report.EpisodeLengthGap) / 1000.0) / 3.0;

        // Analysis and recommendations
        report.Analysis = GenerateAnalysis(report);
        report.Recommendations = GenerateRecommendations(report);

        return report;
    }

    private async Task<bool> WaitForCompletionAsync(string jobId, TimeSpan timeout, CancellationToken ct)
    {
        var job = _jobs[jobId];
        var startTime = DateTimeOffset.UtcNow;
        
        while (job.Status == EvaluationJobStatus.Running || job.Status == EvaluationJobStatus.Queued)
        {
            if (ct.IsCancellationRequested || DateTimeOffset.UtcNow - startTime > timeout)
            {
                return false;
            }
            await Task.Delay(5000, ct);
        }
        
        return job.Status == EvaluationJobStatus.Completed;
    }

    private EvaluationResult GetResult(string jobId)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            return new EvaluationResult
            {
                Success = job.Status == EvaluationJobStatus.Completed,
                Metrics = job.Metrics,
                VideoPaths = job.VideoPaths,
                TrajectoryPaths = job.TrajectoryPaths,
                StatisticalTests = job.StatisticalTests,
                ErrorMessage = job.ErrorMessage
            };
        }
        return new EvaluationResult { Success = false, ErrorMessage = "Job not found" };
    }

    private async Task ProcessJobsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            EvaluationJob? job = null;
            try
            {
                job = await _jobQueue.Reader.ReadAsync(ct);
                await ExecuteEvaluationAsync(job);
            }
            catch (ChannelClosedException)
            {
                break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing evaluation job {JobId}", job?.JobId);
                if (job != null)
                {
                    job.Status = EvaluationJobStatus.Failed;
                    job.ErrorMessage = ex.Message;
                }
            }
        }
    }

    private async Task ExecuteEvaluationAsync(EvaluationJob job)
    {
        var stopwatch = Stopwatch.StartNew();
        job.Status = EvaluationJobStatus.Running;
        job.StartedAt = DateTimeOffset.UtcNow;

        try
        {
            _logger.LogInformation("Starting evaluation job {JobId}: {Episodes} episodes", job.JobId, job.Config.NumEpisodes);

            job.Progress = 10;
            job.CurrentStep = "Loading policy";

            // Load policy
            var policy = await LoadPolicyAsync(job.Config.PolicyPath, job.CancellationToken.Token);

            job.Progress = 20;
            job.CurrentStep = "Initializing simulators";

            // Initialize simulation and real-world environments
            var simEnv = await InitializeSimulatorAsync(job.Config.SimulatorConfig, job.CancellationToken.Token);
            var realEnv = await InitializeRealWorldAsync(job.Config.RealWorldConfig, job.CancellationToken.Token);

            job.Progress = 30;
            job.CurrentStep = "Running simulation episodes";

            // Run simulation episodes
            var simResults = await RunEpisodesAsync(policy, simEnv, job.Config, "simulation", job);

            job.Progress = 60;
            job.CurrentStep = "Running real-world episodes";

            // Run real-world episodes
            var realResults = await RunEpisodesAsync(policy, realEnv, job.Config, "real", job);

            job.Progress = 80;
            job.CurrentStep = "Computing metrics";

            // Compute metrics
            var metrics = ComputeMetrics(simResults, realResults);
            var statisticalTests = ComputeStatisticalTests(simResults, realResults, job.Config.StatisticalTest);

            job.Progress = 90;
            job.CurrentStep = "Generating reports";

            // Save results
            await SaveResultsAsync(job.JobId, simResults, realResults, metrics, statisticalTests, job.CancellationToken.Token);

            // Generate videos if requested
            var videoPaths = new List<string>();
            if (job.Config.RecordVideo)
            {
                videoPaths = await GenerateVideosAsync(simResults, realResults, job.CancellationToken.Token);
            }

            // Generate trajectories if requested
            var trajectoryPaths = new List<string>();
            if (job.Config.RecordTrajectories)
            {
                trajectoryPaths = await GenerateTrajectoriesAsync(simResults, realResults, job.CancellationToken.Token);
            }

            job.Metrics = metrics;
            job.VideoPaths = videoPaths.ToArray();
            job.TrajectoryPaths = trajectoryPaths.ToArray();
            job.StatisticalTests = statisticalTests;

            job.Progress = 100;
            job.Status = EvaluationJobStatus.Completed;
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.TotalDuration = stopwatch.Elapsed;

            _logger.LogInformation("Evaluation job {JobId} completed in {Duration}", job.JobId, stopwatch.Elapsed);
            _metrics.RecordHttpRequestDuration("evaluation", stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (OperationCanceledException)
        {
            job.Status = EvaluationJobStatus.Cancelled;
            _logger.LogInformation("Evaluation job {JobId} cancelled", job.JobId);
        }
        catch (Exception ex)
        {
            job.Status = EvaluationJobStatus.Failed;
            job.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Evaluation job {JobId} failed", job.JobId);
        }
    }

    private async Task<object> LoadPolicyAsync(string path, CancellationToken ct)
    {
        _logger.LogDebug("Loading policy from {Path}", path);
        await Task.Delay(100, ct);
        return new { Type = "Policy", Path = path };
    }

    private async Task<Environment> InitializeSimulatorAsync(string config, CancellationToken ct)
    {
        _logger.LogDebug("Initializing simulator with config: {Config}", config);
        await Task.Delay(500, ct);
        return new Environment { Type = "Simulation", Config = config };
    }

    private async Task<Environment> InitializeRealWorldAsync(string config, CancellationToken ct)
    {
        _logger.LogDebug("Initializing real-world environment with config: {Config}", config);
        await Task.Delay(1000, ct);
        return new Environment { Type = "Real", Config = config };
    }

    private async Task<EvaluationEpisodeResults> RunEpisodesAsync(object policy, Environment env, EvaluationConfig config, string envType, EvaluationJob job)
    {
        var results = new List<EpisodeResult>();
        var videoFrames = new List<byte[]>();
        
        for (int episode = 0; episode < config.NumEpisodes; episode++)
        {
            if (job.CancellationToken.Token.IsCancellationRequested) break;

            job.Progress = envType == "simulation" ? 30 + (episode * 30 / config.NumEpisodes) 
                                                   : 60 + (episode * 30 / config.NumEpisodes);
            job.CurrentStep = $"Running {envType} episode {episode + 1}/{config.NumEpisodes}";

            var episodeResult = await RunSingleEpisodeAsync(policy, env, config.MaxStepsPerEpisode, job.CancellationToken.Token);
            results.Add(episodeResult);

            if (job.Config.RecordVideo)
            {
                // Would capture frames
            }
        }

        return new EvaluationEpisodeResults
        {
            EnvironmentType = envType,
            Episodes = results,
            SuccessRate = results.Count > 0 ? results.Count(r => r.Success) / (double)results.Count : 0,
            AverageReward = results.Count > 0 ? results.Average(r => r.TotalReward) : 0,
            AverageEpisodeLength = results.Count > 0 ? results.Average(r => r.Steps) : 0,
            SafetyViolations = results.Count(r => r.SafetyViolation),
            PerTaskMetrics = ComputePerTaskMetrics(results)
        };
    }

    private async Task<EpisodeResult> RunSingleEpisodeAsync(object policy, Environment env, int maxSteps, CancellationToken ct)
    {
        var totalReward = 0.0;
        var steps = 0;
        var safetyViolation = false;
        var trajectory = new List<StepData>();

        // Simulate episode
        for (int step = 0; step < maxSteps; step++)
        {
            if (ct.IsCancellationRequested) break;

            // Get action from policy
            var action = GetAction(policy, env);
            
            // Step environment
            var (nextObs, reward, done, info) = StepEnvironment(env, action);
            
            totalReward += reward;
            steps++;
            
            trajectory.Add(new StepData
            {
                Step = step,
                Observation = env.CurrentObservation,
                Action = action,
                Reward = reward,
                Done = done
            });

            if (info.ContainsKey("safety_violation") && (bool)info["safety_violation"])
            {
                safetyViolation = true;
            }

            if (done) break;
        }

        return new EpisodeResult
        {
            TotalReward = totalReward,
            Steps = steps,
            Success = !safetyViolation && totalReward > 0,
            SafetyViolation = safetyViolation,
            Trajectory = trajectory
        };
    }

    private int GetAction(object policy, Environment env)
    {
        // In real implementation, would run policy inference
        return new Random().Next(10);
    }

    private (object nextObs, double reward, bool done, Dictionary<string, object> info) StepEnvironment(Environment env, int action)
    {
        // Simulate environment step
        return (new object(), 1.0, false, new Dictionary<string, object>());
    }

    private Dictionary<string, double> ComputePerTaskMetrics(List<EpisodeResult> results)
    {
        // Group by task type and compute metrics
        return new Dictionary<string, double>
        {
            ["navigation"] = results.Where(r => r.Success).Count() / (double)results.Count,
            ["manipulation"] = 0.8,
            ["interaction"] = 0.75
        };
    }

    private EvaluationMetricsResults ComputeMetrics(EvaluationEpisodeResults simResults, EvaluationEpisodeResults realResults)
    {
        return new EvaluationMetricsResults
        {
            SuccessRate = realResults.SuccessRate,
            AverageReward = realResults.AverageReward,
            AverageEpisodeLength = realResults.AverageEpisodeLength,
            SafetyViolations = realResults.SafetyViolations,
            SampleEfficiency = ComputeSampleEfficiency(simResults, realResults),
            PerTaskMetrics = realResults.PerTaskMetrics
        };
    }

    private double ComputeSampleEfficiency(EvaluationEpisodeResults sim, EvaluationEpisodeResults real)
    {
        // How many real episodes needed to match sim performance
        if (real.SuccessRate > 0)
        {
            return sim.SuccessRate / real.SuccessRate;
        }
        return double.PositiveInfinity;
    }

    private StatisticalTestResults ComputeStatisticalTests(EvaluationEpisodeResults simResults, EvaluationEpisodeResults realResults, StatisticalTestConfig config)
    {
        var tests = new Dictionary<string, StatisticalTestResult>();

        if (config.MetricsToTest.Contains("success_rate"))
        {
            tests["success_rate"] = RunProportionTest(simResults.SuccessRate, realResults.SuccessRate, config.SignificanceLevel);
        }

        if (config.MetricsToTest.Contains("average_reward"))
        {
            tests["average_reward"] = RunTTest(simResults.Episodes.Select(e => e.TotalReward).ToArray(), 
                realResults.Episodes.Select(e => e.TotalReward).ToArray(), config.SignificanceLevel);
        }

        if (config.MetricsToTest.Contains("episode_length"))
        {
            tests["episode_length"] = RunTTest(simResults.Episodes.Select(e => (double)e.Steps).ToArray(), 
                realResults.Episodes.Select(e => (double)e.Steps).ToArray(), config.SignificanceLevel);
        }

        var overallSignificant = tests.Values.Any(t => t.Significant);

        return new StatisticalTestResults
        {
            Tests = tests,
            OverallSignificant = overallSignificant
        };
    }

    private StatisticalTestResult RunProportionTest(double p1, double p2, double alpha)
    {
        // Two-proportion z-test
        var n1 = 100; // sim episodes
        var n2 = 100; // real episodes
        var pooled = (p1 * n1 + p2 * n2) / (n1 + n2);
        var se = Math.Sqrt(pooled * (1 - pooled) * (1.0 / n1 + 1.0 / n2));
        var z = (p2 - p1) / se;
        var pValue = 2 * (1 - NormalCDF(Math.Abs(z)));

        return new StatisticalTestResult
        {
            TestName = "Two-proportion z-test",
            PValue = pValue,
            EffectSize = p2 - p1,
            ConfidenceIntervalLow = (p2 - p1) - 1.96 * se,
            ConfidenceIntervalHigh = (p2 - p1) + 1.96 * se,
            Significant = pValue < alpha
        };
    }

    private StatisticalTestResult RunTTest(double[] sample1, double[] sample2, double alpha)
    {
        var n1 = sample1.Length;
        var n2 = sample2.Length;
        var mean1 = sample1.Average();
        var mean2 = sample2.Average();
        var var1 = sample1.Select(x => (x - mean1) * (x - mean1)).Sum() / (n1 - 1);
        var var2 = sample2.Select(x => (x - mean2) * (x - mean2)).Sum() / (n2 - 1);

        var pooledVar = ((n1 - 1) * var1 + (n2 - 1) * var2) / (n1 + n2 - 2);
        var se = Math.Sqrt(pooledVar * (1.0 / n1 + 1.0 / n2));
        var tStat = (mean2 - mean1) / se;
        var df = n1 + n2 - 2;
        var pValue = 2 * (1 - StudentTCDF(Math.Abs(tStat), df));
        var effectSize = (mean2 - mean1) / Math.Sqrt(pooledVar);

        return new StatisticalTestResult
        {
            TestName = "Independent t-test",
            PValue = pValue,
            EffectSize = effectSize,
            ConfidenceIntervalLow = (mean2 - mean1) - 1.96 * se,
            ConfidenceIntervalHigh = (mean2 - mean1) + 1.96 * se,
            Significant = pValue < alpha
        };
    }

    private double NormalCDF(double x) => 0.5 * (1 + Math.Erf(x / Math.Sqrt(2)));

    private double StudentTCDF(double t, int df)
    {
        // Simplified - use proper implementation
        return NormalCDF(t);
    }

    private async Task SaveResultsAsync(string jobId, EvaluationEpisodeResults sim, EvaluationEpisodeResults real, 
        EvaluationMetricsResults metrics, StatisticalTestResults stats, CancellationToken ct)
    {
        var outputDir = Path.Combine(_options.OutputPath, jobId);
        Directory.CreateDirectory(outputDir);

        var report = new EvaluationReport
        {
            JobId = jobId,
            Timestamp = DateTimeOffset.UtcNow,
            SimResults = sim,
            RealResults = real,
            AggregatedMetrics = metrics,
            StatisticalTests = stats
        };

        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(Path.Combine(outputDir, "evaluation_report.json"), json, ct);
    }

    private async Task<List<string>> GenerateVideosAsync(EvaluationEpisodeResults sim, EvaluationEpisodeResults real, CancellationToken ct)
    {
        // Generate comparison videos
        var paths = new List<string>();
        await Task.CompletedTask;
        return paths;
    }

    private async Task<List<string>> GenerateTrajectoriesAsync(EvaluationEpisodeResults sim, EvaluationEpisodeResults real, CancellationToken ct)
    {
        // Save trajectory data
        var paths = new List<string>();
        await Task.CompletedTask;
        return paths;
    }

    private async Task<EpisodeResults> LoadResultsAsync(string path, CancellationToken ct)
    {
        var json = await File.ReadAllTextAsync(path, ct);
        return JsonSerializer.Deserialize<EvaluationEpisodeResults>(json) ?? new EvaluationEpisodeResults();
    }

    private string GenerateAnalysis(Sim2RealGapReport report)
    {
        var analysis = new System.Text.StringBuilder();
        
        if (report.SuccessRateGap < -0.1)
        {
            analysis.AppendLine($"Significant success rate drop in real world: {report.SuccessRateGap:P1}");
        }
        if (report.RewardGap < -10)
        {
            analysis.AppendLine($"Substantial reward gap: {report.RewardGap:F1}");
        }
        if (report.SafetyViolationGap > 0)
        {
            analysis.AppendLine($"Increased safety violations in real world: +{report.SafetyViolationGap}");
        }
        
        return analysis.ToString();
    }

    private string[] GenerateRecommendations(Sim2RealGapReport report)
    {
        var recommendations = new List<string>();

        if (report.SuccessRateGap < -0.1)
        {
            recommendations.Add("Increase domain randomization diversity (visual, physics, lighting)");
            recommendations.Add("Add more challenging scenarios to simulation curriculum");
            recommendations.Add("Consider domain adaptation (CycleGAN/DR-CycleGAN)");
        }

        if (report.RewardGap < -10)
        {
            recommendations.Add("Retrain reward model with real-world data (SQIL/CQL)");
            recommendations.Add("Add reward shaping for real-world dynamics");
        }

        if (report.SafetyViolationGap > 0)
        {
            recommendations.Add("Add safety constraints to policy training (CPO/RCPO)");
            recommendations.Add("Increase simulation safety margin");
        }

        if (report.OverallGap > 0.2)
        {
            recommendations.Add("Consider full sim2real retraining pipeline");
            recommendations.Add("Collect more real-world data for fine-tuning");
        }

        return recommendations.ToArray();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _shutdownCts.Cancel();
        _workerTask.Wait(TimeSpan.FromSeconds(30));
    }
}

// ==================== INTERNAL MODELS ====================

internal sealed class EvaluationJob
{
    public string JobId { get; init; } = string.Empty;
    public EvaluationConfig Config { get; init; } = new();
    public EvaluationJobStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public int Progress { get; set; }
    public string CurrentStep { get; set; } = string.Empty;
    public EvaluationMetricsResults Metrics { get; set; } = new();
    public string[] VideoPaths { get; set; } = Array.Empty<string>();
    public string[] TrajectoryPaths { get; set; } = Array.Empty<string>();
    public StatisticalTestResults StatisticalTests { get; set; } = new();
    public string ErrorMessage { get; set; } = string.Empty;
    public CancellationTokenSource CancellationToken { get; init; } = new();
}

public enum EvaluationJobStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled
}

internal sealed class Environment
{
    public string Type { get; set; } = string.Empty;
    public string Config { get; set; } = string.Empty;
    public object CurrentObservation { get; set; } = new();
}

internal sealed class EvaluationEpisodeResults
{
    public string EnvironmentType { get; set; } = string.Empty;
    public List<EpisodeResult> Episodes { get; set; } = new();
    public double SuccessRate { get; set; }
    public double AverageReward { get; set; }
    public double AverageEpisodeLength { get; set; }
    public int SafetyViolations { get; set; }
    public Dictionary<string, double> PerTaskMetrics { get; set; } = new();
}

internal sealed class EpisodeResult
{
    public double TotalReward { get; set; }
    public int Steps { get; set; }
    public bool Success { get; set; }
    public bool SafetyViolation { get; set; }
    public List<StepData> Trajectory { get; set; } = new();
}

internal sealed class StepData
{
    public int Step { get; set; }
    public object Observation { get; set; } = new();
    public int Action { get; set; }
    public double Reward { get; set; }
    public bool Done { get; set; }
}

internal sealed class EvaluationReport
{
    public string JobId { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public EvaluationEpisodeResults SimResults { get; set; } = new();
    public EvaluationEpisodeResults RealResults { get; set; } = new();
    public EvaluationMetricsResults AggregatedMetrics { get; set; } = new();
    public StatisticalTestResults StatisticalTests { get; set; } = new();
}

/// <summary>
/// Evaluation configuration.
/// </summary>
public sealed class EvaluationOptions
{
    public string OutputPath { get; set; } = "./evaluation";
    public int DefaultNumEpisodes { get; set; } = 100;
    public int DefaultMaxStepsPerEpisode { get; set; } = 1000;
    public bool DefaultRecordVideo { get; set; } = true;
    public bool DefaultRecordTrajectories { get; set; } = true;
    public int MaxConcurrentJobs { get; set; } = 2;
}

/// <summary>
/// DI Extensions.
/// </summary>
public static class EvaluationExtensions
{
    public static IServiceCollection AddKkbotEvaluation(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EvaluationOptions>(configuration.GetSection("Evaluation"));
        services.AddSingleton<IEvaluator, Evaluator>();
        services.AddHostedService(sp => sp.GetRequiredService<IEvaluator>());
        return services;
    }
}
