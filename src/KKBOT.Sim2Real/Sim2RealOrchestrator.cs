// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KKBOT.Sim2Real.Abstractions;
using KKBOT.Observability;
using KKBOT.MLPlatform.Abstractions;
using KKBOT.MLPlatform.ModelServer;

namespace KKBOT.Sim2Real;

/// <summary>
/// Sim2Real Training Pipeline Orchestrator.
/// Coordinates the full pipeline: Domain Randomization -> Domain Adaptation -> Reward Modeling -> Policy Deployment -> Evaluation.
/// </summary>
public sealed class Sim2RealOrchestrator : IHostedService, IDisposable
{
    private readonly ILogger<Sim2RealOrchestrator> _logger;
    private readonly Sim2RealOrchestratorOptions _options;
    private readonly IMetricsRegistry _metrics;
    private readonly IDomainRandomizer _domainRandomizer;
    private readonly IDomainAdapter _domainAdapter;
    private readonly IRewardModeler _rewardModeler;
    private readonly IPolicyDeployer _policyDeployer;
    private readonly IEvaluator _evaluator;
    private readonly IModelServer _modelServer;
    private readonly ConcurrentDictionary<string, PipelineJob> _pipelineJobs = new();
    private readonly Channel<PipelineJob> _jobQueue;
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly Task _workerTask;
    private bool _disposed;

    public Sim2RealOrchestrator(
        ILogger<Sim2RealOrchestrator> logger,
        IOptions<Sim2RealOrchestratorOptions> options,
        IMetricsRegistry metrics,
        IDomainRandomizer domainRandomizer,
        IDomainAdapter domainAdapter,
        IRewardModeler rewardModeler,
        IPolicyDeployer policyDeployer,
        IEvaluator evaluator,
        IModelServer modelServer)
    {
        _logger = logger;
        _options = options.Value;
        _metrics = metrics;
        _domainRandomizer = domainRandomizer;
        _domainAdapter = domainAdapter;
        _rewardModeler = rewardModeler;
        _policyDeployer = policyDeployer;
        _evaluator = evaluator;
        _modelServer = modelServer;

        _jobQueue = Channel.CreateUnbounded<PipelineJob>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        _workerTask = ProcessPipelineAsync(_shutdownCts.Token);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Sim2Real Orchestrator started");
        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Sim2Real Orchestrator stopping");
        _shutdownCts.Cancel();
        _jobQueue.Writer.Complete();
        
        try
        {
            await _workerTask;
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Submits a full Sim2Real training pipeline job.
    /// </summary>
    public async Task<string> SubmitPipelineAsync(Sim2RealTrainingJob job, CancellationToken ct = default)
    {
        var pipelineJob = new PipelineJob
        {
            JobId = job.JobId,
            TrainingJob = job,
            Status = PipelineStatus.Queued,
            CreatedAt = DateTimeOffset.UtcNow,
            CurrentStage = Sim2RealStage.DomainRandomization,
            StageProgress = 0
        };

        _pipelineJobs[job.JobId] = pipelineJob;
        await _jobQueue.Writer.WriteAsync(pipelineJob, ct);

        _logger.LogInformation("Sim2Real pipeline job {JobId} queued", job.JobId);
        return job.JobId;
    }

    /// <summary>
    /// Gets pipeline job status.
    /// </summary>
    public PipelineJobStatusInfo GetJobStatus(string jobId)
    {
        if (_pipelineJobs.TryGetValue(jobId, out var job))
        {
            return new PipelineJobStatusInfo
            {
                JobId = job.JobId,
                Name = job.TrainingJob.Name,
                Status = job.Status,
                CurrentStage = job.CurrentStage,
                StageProgress = job.StageProgress,
                OverallProgress = CalculateOverallProgress(job),
                CreatedAt = job.CreatedAt,
                StartedAt = job.StartedAt,
                CompletedAt = job.CompletedAt,
                CurrentStageStartedAt = job.CurrentStageStartedAt,
                ErrorMessage = job.ErrorMessage
            };
        }
        return new PipelineJobStatusInfo { JobId = jobId, Status = PipelineStatus.NotFound };
    }

    /// <summary>
    /// Cancels a running pipeline job.
    /// </summary>
    public async Task CancelPipelineAsync(string jobId, CancellationToken ct = default)
    {
        if (_pipelineJobs.TryGetValue(jobId, out var job))
        {
            job.CancellationToken.Cancel();
            job.Status = PipelineStatus.Cancelled;
            _logger.LogInformation("Sim2Real pipeline job {JobId} cancelled", jobId);
        }
    }

    /// <summary>
    /// Deploys trained policy to model server for inference.
    /// </summary>
    public async Task<string> DeployToModelServerAsync(string modelPath, string modelId, CancellationToken ct = default)
    {
        // Load model into model server
        // This would register the model with the ONNX model server
        _logger.LogInformation("Deploying model {ModelId} to model server", modelId);
        
        // In real implementation, would call model server API
        await Task.CompletedTask;
        
        return modelId;
    }

    /// <summary>
    /// Runs inference using deployed model.
    /// </summary>
    public async Task<InferenceResponse> RunInferenceAsync(string modelId, Dictionary<string, float[]> inputs, CancellationToken ct = default)
    {
        var request = new InferenceRequest
        {
            ModelId = modelId,
            Inputs = inputs
        };
        
        return await _modelServer.PredictAsync(request, ct);
    }

    private double CalculateOverallProgress(PipelineJob job)
    {
        var stageWeights = new Dictionary<Sim2RealStage, double>
        {
            [Sim2RealStage.DomainRandomization] = 0.15,
            [Sim2RealStage.DomainAdaptation] = 0.25,
            [Sim2RealStage.RewardModeling] = 0.25,
            [Sim2RealStage.PolicyTraining] = 0.15,
            [Sim2RealStage.PolicyDeployment] = 0.10,
            [Sim2RealStage.Evaluation] = 0.10
        };

        double progress = 0;
        foreach (var stage in stageWeights.Keys)
        {
            if ((int)stage < (int)job.CurrentStage)
            {
                progress += stageWeights[stage];
            }
            else if (stage == job.CurrentStage)
            {
                progress += stageWeights[stage] * (job.StageProgress / 100.0);
            }
        }
        return Math.Min(100, progress);
    }

    private async Task ProcessPipelineAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            PipelineJob? job = null;
            try
            {
                job = await _jobQueue.Reader.ReadAsync(ct);
                await ExecutePipelineAsync(job);
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
                _logger.LogError(ex, "Error processing pipeline job {JobId}", job?.JobId);
                if (job != null)
                {
                    job.Status = PipelineStatus.Failed;
                    job.ErrorMessage = ex.Message;
                }
            }
        }
    }

    private async Task ExecutePipelineAsync(PipelineJob job)
    {
        var stopwatch = Stopwatch.StartNew();
        job.Status = PipelineStatus.Running;
        job.StartedAt = DateTimeOffset.UtcNow;

        try
        {
            _logger.LogInformation("Starting Sim2Real pipeline {JobId}: {Name}", job.JobId, job.TrainingJob.Name);

            var trainingJob = job.TrainingJob;

            // Stage 1: Domain Randomization
            if (ShouldRunStage(trainingJob, Sim2RealStage.DomainRandomization))
            {
                await RunStageAsync(job, Sim2RealStage.DomainRandomization, async () =>
                {
                    job.CurrentStageStartedAt = DateTimeOffset.UtcNow;
                    var result = await _domainRandomizer.RandomizeAsync(trainingJob.DomainRandomization, job.CancellationToken.Token);
                    job.StageOutputs["domain_randomization"] = result;
                });
            }

            // Stage 2: Domain Adaptation
            if (ShouldRunStage(trainingJob, Sim2RealStage.DomainAdaptation))
            {
                await RunStageAsync(job, Sim2RealStage.DomainAdaptation, async () =>
                {
                    job.CurrentStageStartedAt = DateTimeOffset.UtcNow;
                    var result = await _domainAdapter.AdaptAsync(trainingJob.DomainAdaptation, job.CancellationToken.Token);
                    job.StageOutputs["domain_adaptation"] = result;
                });
            }

            // Stage 3: Reward Modeling
            if (ShouldRunStage(trainingJob, Sim2RealStage.RewardModeling))
            {
                await RunStageAsync(job, Sim2RealStage.RewardModeling, async () =>
                {
                    job.CurrentStageStartedAt = DateTimeOffset.UtcNow;
                    var result = await _rewardModeler.TrainRewardModelAsync(trainingJob.RewardModeling, job.CancellationToken.Token);
                    job.StageOutputs["reward_modeling"] = result;
                });
            }

            // Stage 4: Policy Training (using reward model)
            if (ShouldRunStage(trainingJob, Sim2RealStage.PolicyTraining))
            {
                await RunStageAsync(job, Sim2RealStage.PolicyTraining, async () =>
                {
                    job.CurrentStageStartedAt = DateTimeOffset.UtcNow;
                    // Policy training would use the reward model from stage 3
                    // This is a placeholder for the actual training loop
                    await TrainPolicyWithRewardModelAsync(job);
                });
            }

            // Stage 5: Policy Deployment
            if (ShouldRunStage(trainingJob, Sim2RealStage.PolicyDeployment))
            {
                await RunStageAsync(job, Sim2RealStage.PolicyDeployment, async () =>
                {
                    job.CurrentStageStartedAt = DateTimeOffset.UtcNow;
                    var result = await _policyDeployer.DeployAsync(trainingJob.PolicyDeployment, job.CancellationToken.Token);
                    job.StageOutputs["policy_deployment"] = result;
                });
            }

            // Stage 6: Evaluation
            if (ShouldRunStage(trainingJob, Sim2RealStage.Evaluation))
            {
                await RunStageAsync(job, Sim2RealStage.Evaluation, async () =>
                {
                    job.CurrentStageStartedAt = DateTimeOffset.UtcNow;
                    var result = await _evaluator.EvaluateAsync(trainingJob.Evaluation, job.CancellationToken.Token);
                    job.StageOutputs["evaluation"] = result;
                });
            }

            job.Status = PipelineStatus.Completed;
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.TotalDuration = stopwatch.Elapsed;

            // Compile final metrics
            CompileFinalMetrics(job);

            _logger.LogInformation("Sim2Real pipeline {JobId} completed in {Duration}", job.JobId, stopwatch.Elapsed);
            _metrics.RecordHttpRequestDuration("sim2real_pipeline", stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (OperationCanceledException)
        {
            job.Status = PipelineStatus.Cancelled;
            _logger.LogInformation("Sim2Real pipeline {JobId} cancelled", job.JobId);
        }
        catch (Exception ex)
        {
            job.Status = PipelineStatus.Failed;
            job.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Sim2Real pipeline {JobId} failed", job.JobId);
        }
    }

    private bool ShouldRunStage(Sim2RealTrainingJob job, Sim2RealStage stage)
    {
        // Check if stage is enabled in configuration
        // For now, run all stages
        return true;
    }

    private async Task RunStageAsync(PipelineJob job, Sim2RealStage stage, Func<Task> stageAction)
    {
        job.CurrentStage = stage;
        job.StageProgress = 0;
        
        _logger.LogInformation("Pipeline {JobId} starting stage: {Stage}", job.JobId, stage);

        try
        {
            await stageAction();
            job.StageProgress = 100;
            _logger.LogInformation("Pipeline {JobId} completed stage: {Stage}", job.JobId, stage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pipeline {JobId} failed at stage: {Stage}", job.JobId, stage);
            throw;
        }
    }

    private async Task TrainPolicyWithRewardModelAsync(PipelineJob job)
    {
        // This would implement the actual policy training loop using the reward model
        // from the reward modeling stage
        _logger.LogInformation("Training policy with reward model (placeholder)");
        
        // Simulate training
        for (int epoch = 0; epoch < 10; epoch++)
        {
            if (job.CancellationToken.Token.IsCancellationRequested) break;
            
            job.StageProgress = (epoch + 1) * 10;
            await Task.Delay(100, job.CancellationToken.Token);
        }
    }

    private void CompileFinalMetrics(PipelineJob job)
    {
        // Compile metrics from all stages into final Sim2RealMetrics
        var metrics = new Sim2RealMetrics();

        if (job.StageOutputs.TryGetValue("domain_randomization", out var dr))
        {
            // Extract DR metrics
        }

        if (job.StageOutputs.TryGetValue("domain_adaptation", out var da))
        {
            if (da is AdaptationResult daResult)
            {
                metrics.DA_FID = daResult.Metrics.FID;
                metrics.DA_LPIPS = daResult.Metrics.LPIPS;
                metrics.DA_SSIM = daResult.Metrics.SSIM;
            }
        }

        if (job.StageOutputs.TryGetValue("reward_modeling", out var rm))
        {
            if (rm is RewardModelResult rmResult)
            {
                metrics.RM_RewardAccuracy = rmResult.Metrics.Accuracy;
                metrics.RM_PolicyPerformance = rmResult.Metrics.PolicyReturn;
            }
        }

        if (job.StageOutputs.TryGetValue("policy_deployment", out var pd))
        {
            if (pd is DeploymentResult pdResult)
            {
                metrics.Deploy_LatencyMs = pdResult.Metrics.LatencyMs;
                metrics.Deploy_ThroughputFPS = pdResult.Metrics.ThroughputFPS;
                metrics.Deploy_MemoryMB = pdResult.Metrics.MemoryMB;
                metrics.Deploy_ModelSizeMB = pdResult.Metrics.ModelSizeMB;
            }
        }

        if (job.StageOutputs.TryGetValue("evaluation", out var ev))
        {
            if (ev is EvaluationResult evResult)
            {
                metrics.Eval_SimSuccessRate = evResult.Metrics.SuccessRate; // Would need sim results
                metrics.Eval_RealSuccessRate = evResult.Metrics.SuccessRate;
                metrics.Eval_AverageReward = evResult.Metrics.AverageReward;
                metrics.Eval_SafetyViolations = evResult.Metrics.SafetyViolations;
            }
        }

        // Compute sim2real gap if we have both sim and real results
        // metrics.Eval_Sim2RealGap = ...

        // Overall score (weighted combination)
        metrics.OverallScore = ComputeOverallScore(metrics);

        job.TrainingJob.Metrics = metrics;
    }

    private double ComputeOverallScore(Sim2RealMetrics metrics)
    {
        // Weighted score: lower is better for gaps, higher is better for success rates
        var score = 0.0;
        var weights = new Dictionary<string, double>
        {
            ["success_rate"] = 0.3,
            ["reward"] = 0.2,
            ["fid"] = 0.15,
            ["latency"] = 0.1,
            ["safety"] = 0.15,
            ["sim2real_gap"] = 0.1
        };

        // Normalize and weight each metric
        // This is simplified - real implementation would have proper normalization
        score = 0.75; // Placeholder

        return score;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _shutdownCts.Cancel();
        _workerTask.Wait(TimeSpan.FromSeconds(30));
    }
}

/// <summary>
/// Pipeline job state.
/// </summary>
internal sealed class PipelineJob
{
    public string JobId { get; init; } = string.Empty;
    public Sim2RealTrainingJob TrainingJob { get; init; } = new();
    public PipelineStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public Sim2RealStage CurrentStage { get; set; }
    public int StageProgress { get; set; }
    public DateTimeOffset? CurrentStageStartedAt { get; set; }
    public Dictionary<string, object> StageOutputs { get; init; } = new();
    public string ErrorMessage { get; set; } = string.Empty;
    public CancellationTokenSource CancellationToken { get; init; } = new();
}

public enum PipelineStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled,
    NotFound
}

public sealed record PipelineJobStatusInfo
{
    public string JobId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public PipelineStatus Status { get; init; }
    public Sim2RealStage CurrentStage { get; init; }
    public int StageProgress { get; init; }
    public double OverallProgress { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public DateTimeOffset? CurrentStageStartedAt { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
}

/// <summary>
/// Sim2Real orchestrator configuration.
/// </summary>
public sealed class Sim2RealOrchestratorOptions
{
    public bool EnableAutoRetry { get; set; } = true;
    public int MaxRetries { get; set; } = 3;
    public TimeSpan StageTimeout { get; set; } = TimeSpan.FromHours(4);
    public TimeSpan PipelineTimeout { get; set; } = TimeSpan.FromHours(24);
    public bool EnableCheckpointing { get; set; } = true;
    public int CheckpointIntervalMinutes { get; set; } = 30;
}

/// <summary>
/// DI Extensions.
/// </summary>
public static class Sim2RealOrchestratorExtensions
{
    public static IServiceCollection AddKkbotSim2Real(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<Sim2RealOrchestratorOptions>(configuration.GetSection("Sim2RealOrchestrator"));
        services.AddSingleton<Sim2RealOrchestrator>();
        services.AddHostedService(sp => sp.GetRequiredService<Sim2RealOrchestrator>());
        return services;
    }
}
