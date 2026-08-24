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

namespace KKBOT.Sim2Real.RewardModeling;

/// <summary>
/// Reward modeling engine for learning reward functions from demonstrations.
/// Supports: GAIL, SQIL, CQL, IQL, Offline RL, and custom methods.
/// </summary>
public sealed class RewardModeler : IRewardModeler, IHostedService, IDisposable
{
    private readonly ILogger<RewardModeler> _logger;
    private readonly RewardModelingOptions _options;
    private readonly IMetricsRegistry _metrics;
    private readonly ConcurrentDictionary<string, RewardModel> _models = new();
    private readonly Channel<RewardModelingJob> _jobQueue;
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly Task _workerTask;
    private bool _disposed;

    public RewardModeler(
        ILogger<RewardModeler> logger,
        IOptions<RewardModelingOptions> options,
        IMetricsRegistry metrics)
    {
        _logger = logger;
        _options = options.Value;
        _metrics = metrics;

        _jobQueue = Channel.CreateUnbounded<RewardModelingJob>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        _workerTask = ProcessJobsAsync(_shutdownCts.Token);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Reward Modeler started");
        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Reward Modeler stopping");
        _shutdownCts.Cancel();
        _jobQueue.Writer.Complete();
        
        try
        {
            await _workerTask;
        }
        catch (OperationCanceledException) { }
    }

    public async Task<RewardModelResult> TrainRewardModelAsync(RewardModelingConfig config, CancellationToken ct = default)
    {
        var jobId = Guid.NewGuid().ToString("N");
        var job = new RewardModelingJob
        {
            JobId = jobId,
            Config = config,
            Status = RewardModelingJobStatus.Queued,
            CreatedAt = DateTimeOffset.UtcNow,
            Progress = 0
        };

        _models[jobId] = new RewardModel { Job = job };
        await _jobQueue.Writer.WriteAsync(job, ct);

        _logger.LogInformation("Reward modeling job {JobId} queued: {Method}", jobId, config.Method);

        var timeout = TimeSpan.FromHours(config.Epochs * 2);
        var completed = await WaitForCompletionAsync(jobId, timeout, ct);
        
        return completed ? GetResult(jobId) : new RewardModelResult { Success = false, ErrorMessage = "Timeout" };
    }

    public async Task<double> PredictRewardAsync(string modelId, float[] state, int action, float[] nextState, CancellationToken ct = default)
    {
        if (!_models.TryGetValue(modelId, out var model))
        {
            throw new InvalidOperationException($"Reward model {modelId} not found");
        }

        // In real implementation, would run inference on reward model
        // For now, return simulated reward
        return await Task.FromResult(0.5);
    }

    private async Task<bool> WaitForCompletionAsync(string jobId, TimeSpan timeout, CancellationToken ct)
    {
        var model = _models[jobId];
        var startTime = DateTimeOffset.UtcNow;
        
        while (model.Job.Status == RewardModelingJobStatus.Running || model.Job.Status == RewardModelingJobStatus.Queued)
        {
            if (ct.IsCancellationRequested || DateTimeOffset.UtcNow - startTime > timeout)
            {
                return false;
            }
            await Task.Delay(5000, ct);
        }
        
        return model.Job.Status == RewardModelingJobStatus.Completed;
    }

    private RewardModelResult GetResult(string jobId)
    {
        if (_models.TryGetValue(jobId, out var model))
        {
            return new RewardModelResult
            {
                Success = model.Job.Status == RewardModelingJobStatus.Completed,
                ModelPath = model.Job.OutputPath,
                Metrics = model.Job.Metrics,
                ErrorMessage = model.Job.ErrorMessage
            };
        }
        return new RewardModelResult { Success = false, ErrorMessage = "Job not found" };
    }

    private async Task ProcessJobsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            RewardModelingJob? job = null;
            try
            {
                job = await _jobQueue.Reader.ReadAsync(ct);
                await ExecuteRewardModelingAsync(job);
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
                _logger.LogError(ex, "Error processing reward modeling job {JobId}", job?.JobId);
                if (job != null)
                {
                    job.Status = RewardModelingJobStatus.Failed;
                    job.ErrorMessage = ex.Message;
                }
            }
        }
    }

    private async Task ExecuteRewardModelingAsync(RewardModelingJob job)
    {
        var stopwatch = Stopwatch.StartNew();
        job.Status = RewardModelingJobStatus.Running;
        job.StartedAt = DateTimeOffset.UtcNow;

        try
        {
            _logger.LogInformation("Starting reward modeling job {JobId}: {Method}", job.JobId, job.Config.Method);

            job.Progress = 10;
            job.CurrentStep = "Loading data";

            // Load expert demonstrations and/or offline data
            var (expertData, offlineData) = await LoadDataAsync(job.Config, job.CancellationToken.Token);

            job.Progress = 20;
            job.CurrentStep = "Initializing reward model";

            // Initialize reward model
            var rewardModel = InitializeRewardModel(job.Config);

            job.Progress = 30;
            job.CurrentStep = "Training";

            // Train based on method
            await TrainAsync(job, rewardModel, expertData, offlineData);

            job.Progress = 90;
            job.CurrentStep = "Exporting model";

            // Export reward model
            var outputPath = await ExportModelAsync(job, rewardModel);

            job.Progress = 100;
            job.Status = RewardModelingJobStatus.Completed;
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.OutputPath = outputPath;
            job.TotalDuration = stopwatch.Elapsed;

            _logger.LogInformation("Reward modeling job {JobId} completed in {Duration}", job.JobId, stopwatch.Elapsed);
            _metrics.RecordHttpRequestDuration("reward_modeling", stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (OperationCanceledException)
        {
            job.Status = RewardModelingJobStatus.Cancelled;
            _logger.LogInformation("Reward modeling job {JobId} cancelled", job.JobId);
        }
        catch (Exception ex)
        {
            job.Status = RewardModelingJobStatus.Failed;
            job.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Reward modeling job {JobId} failed", job.JobId);
        }
    }

    private async Task<(TrajectoryDataset expert, TrajectoryDataset offline)> LoadDataAsync(RewardModelingConfig config, CancellationToken ct)
    {
        // Load expert demonstrations
        var expert = await LoadTrajectoryDatasetAsync(config.ExpertDataPath, ct);
        
        // Load offline data (optional)
        TrajectoryDataset? offline = null;
        if (!string.IsNullOrEmpty(config.OfflineDataPath))
        {
            offline = await LoadTrajectoryDatasetAsync(config.OfflineDataPath, ct);
        }

        _logger.LogInformation("Loaded expert: {Count} trajectories, offline: {Count} trajectories", 
            expert.Trajectories.Count, offline?.Trajectories.Count ?? 0);

        return (expert, offline!);
    }

    private async Task<TrajectoryDataset> LoadTrajectoryDatasetAsync(string path, CancellationToken ct)
    {
        // In real implementation, would load from disk/cloud storage
        // For now, return placeholder
        return new TrajectoryDataset
        {
            Path = path,
            Trajectories = new List<Trajectory>(),
            StateDim = 256,
            ActionDim = 10
        };
    }

    private object InitializeRewardModel(RewardModelingConfig config)
    {
        // In real implementation, would initialize neural network
        // For now, return placeholder
        return new 
        { 
            Type = "RewardModel", 
            Architecture = "MLP",
            HiddenDim = config.HiddenDim,
            StateDim = config.StateDim,
            ActionDim = config.ActionDim
        };
    }

    private async Task TrainAsync(RewardModelingJob job, object rewardModel, TrajectoryDataset expertData, TrajectoryDataset? offlineData)
    {
        var config = job.Config;
        var method = config.Method.ToLower();
        var epochs = config.Epochs;
        var batchSize = config.BatchSize;
        var lr = config.LearningRate;

        switch (method)
        {
            case "gail":
                await TrainGAILAsync(job, rewardModel, expertData, config);
                break;
            case "sqil":
                await TrainSQILAsync(job, rewardModel, expertData, config);
                break;
            case "cql":
                await TrainCQLAsync(job, rewardModel, offlineData!, config);
                break;
            case "iql":
                await TrainIQLAsync(job, rewardModel, offlineData!, config);
                break;
            case "offline_rl":
                await TrainOfflineRLAsync(job, rewardModel, offlineData!, config);
                break;
            default:
                throw new NotSupportedException($"Method {config.Method} not supported");
        }
    }

    private async Task TrainGAILAsync(RewardModelingJob job, object rewardModel, TrajectoryDataset expertData, RewardModelingConfig config)
    {
        // GAIL: Generative Adversarial Imitation Learning
        // Discriminator D(s,a) vs Generator (policy)
        // D trained to distinguish expert vs policy trajectories
        // Policy trained to fool discriminator
        
        var config = job.Config;
        var epochs = config.Epochs;
        var batchSize = config.BatchSize;
        var lr = config.LearningRate;
        var gamma = config.DiscountFactor;
        var gaeLambda = config.GAELambda;
        var clipRatio = config.ClipRatio;
        var entropyCoeff = config.EntropyCoeff;
        var valueLossCoeff = config.ValueLossCoeff;

        var discriminator = InitializeDiscriminator();
        var policy = InitializePolicy();
        var valueFunction = InitializeValueFunction();

        var stepsPerEpoch = 100; // Placeholder

        for (int epoch = 0; epoch < epochs && !job.CancellationToken.Token.IsCancellationRequested; epoch++)
        {
            job.CurrentEpoch = epoch + 1;
            job.Progress = 30 + (int)((epoch / (double)epochs) * 60);

            double epochDiscLoss = 0;
            double epochPolicyLoss = 0;
            double epochValueLoss = 0;

            for (int step = 0; step < 100; step++) // Placeholder steps
            {
                if (job.CancellationToken.Token.IsCancellationRequested) break;

                // Sample expert trajectories
                var expertBatch = SampleExpertBatch(job.Config.ExpertDataPath, config.BatchSize);
                
                // Sample policy trajectories
                var policyBatch = CollectPolicyBatch(policy, config.NumEnvs, config.RolloutSteps);

                // Update discriminator
                var discLoss = UpdateDiscriminator(discriminator, expertBatch, policyBatch, lr);
                epochDiscLoss += discLoss;

                // Compute rewards from discriminator
                var rewards = ComputeRewards(discriminator, policyBatch);

                // Compute GAE
                var (advantages, returns) = ComputeGAE(policyBatch, rewards, gamma, job.Config.GAELambda);

                // Update policy (PPO)
                var (policyLoss, entropy) = UpdatePolicyPPO(policy, policyBatch, advantages, returns, lr, clipRatio, entropyCoeff);
                epochPolicyLoss += policyLoss;

                // Update value function
                var valueLoss = UpdateValueFunction(valueFunction, policyBatch, returns, lr, valueLossCoeff);
                epochValueLoss += valueLoss;
            }

            job.Metrics = new RewardModelMetrics
            {
                Accuracy = 1.0 - epochDiscLoss, // Discriminator accuracy
                AUC = 0.9, // Placeholder
                RewardCorrelation = 0.85,
                PolicyReturn = 100.0, // Placeholder
                Epoch = job.CurrentEpoch
            };

            _logger.LogInformation("GAIL Epoch {Epoch}/{Epochs}: D_Loss={DLoss:.4f}, Policy_Loss={PLoss:.4f}, Value_Loss={VLoss:.4f}",
                job.CurrentEpoch, epochs, epochDiscLoss / 100, epochPolicyLoss / 100, epochValueLoss / 100);
        }
    }

    private async Task TrainSQILAsync(RewardModelingJob job, object rewardModel, TrajectoryDataset expertData, RewardModelingConfig config)
    {
        // SQIL: Soft Q Imitation Learning
        // Combines expert data with policy data in replay buffer
        // Uses soft Q-learning with entropy regularization
        
        _logger.LogInformation("Training SQIL (placeholder)");
        await Task.CompletedTask;
    }

    private async Task TrainCQLAsync(RewardModelingJob job, object rewardModel, TrajectoryDataset offlineData, RewardModelingConfig config)
    {
        // CQL: Conservative Q-Learning
        // Adds regularization to prevent overestimation of OOD actions
        
        _logger.LogInformation("Training CQL (placeholder)");
        await Task.CompletedTask;
    }

    private async Task TrainIQLAsync(RewardModelingJob job, object rewardModel, TrajectoryDataset offlineData, RewardModelingConfig config)
    {
        // IQL: Implicit Q-Learning
        // Uses expectile regression for value function
        
        _logger.LogInformation("Training IQL (placeholder)");
        await Task.CompletedTask;
    }

    private async Task TrainOfflineRLAsync(RewardModelingJob job, object rewardModel, TrajectoryDataset offlineData, RewardModelingConfig config)
    {
        // Generic offline RL
        _logger.LogInformation("Training Offline RL (placeholder)");
        await Task.CompletedTask;
    }

    private object InitializeDiscriminator() => new { Type = "Discriminator" };
    private object InitializePolicy() => new { Type = "Policy" };
    private object InitializeValueFunction() => new { Type = "ValueFunction" };

    private Trajectory[] SampleExpertBatch(string path, int batchSize)
    {
        // Sample batch from expert data
        return Array.Empty<Trajectory>();
    }

    private Trajectory[] CollectPolicyBatch(object policy, int numEnvs, int rolloutSteps)
    {
        // Collect trajectories from current policy
        return Array.Empty<Trajectory>();
    }

    private double UpdateDiscriminator(object discriminator, Trajectory[] expertBatch, Trajectory[] policyBatch, double lr)
    {
        // Update discriminator to distinguish expert vs policy
        return 0.5; // Placeholder loss
    }

    private float[] ComputeRewards(object discriminator, Trajectory[] policyBatch)
    {
        // r(s,a) = -log(1 - D(s,a))
        return Array.Empty<float>();
    }

    private (float[] advantages, float[] returns) ComputeGAE(Trajectory[] batch, float[] rewards, double gamma, double lambda)
    {
        // Generalized Advantage Estimation
        return (Array.Empty<float>(), Array.Empty<float>());
    }

    private (double policyLoss, double entropy) UpdatePolicyPPO(object policy, Trajectory[] batch, float[] advantages, float[] returns, double lr, double clipRatio, double entropyCoeff)
    {
        // PPO policy update
        return (0.1, 0.01);
    }

    private double UpdateValueFunction(object valueFunction, Trajectory[] batch, float[] returns, double lr, double valueLossCoeff)
    {
        // Value function update
        return 0.1;
    }

    private async Task<string> ExportModelAsync(RewardModelingJob job, object rewardModel)
    {
        var outputDir = Path.Combine(job.Config.OutputPath, job.JobId);
        Directory.CreateDirectory(outputDir);

        var modelPath = Path.Combine(outputDir, "reward_model.onnx");
        await File.WriteAllTextAsync(modelPath, ""); // Placeholder

        return modelPath;
    }

    private RewardModelResult GetResult(string jobId)
    {
        if (_models.TryGetValue(jobId, out var model))
        {
            return new RewardModelResult
            {
                Success = model.Job.Status == RewardModelingJobStatus.Completed,
                ModelPath = model.Job.OutputPath,
                Metrics = model.Job.Metrics,
                ErrorMessage = model.Job.ErrorMessage
            };
        }
        return new RewardModelResult { Success = false, ErrorMessage = "Job not found" };
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

internal sealed class RewardModelingJob
{
    public string JobId { get; init; } = string.Empty;
    public RewardModelingConfig Config { get; init; } = new();
    public RewardModelingJobStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public int Progress { get; set; }
    public string CurrentStep { get; set; } = string.Empty;
    public int CurrentEpoch { get; set; }
    public RewardModelMetrics Metrics { get; set; } = new();
    public string ErrorMessage { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public CancellationTokenSource CancellationToken { get; init; } = new();
}

public enum RewardModelingJobStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled
}

internal sealed class RewardModel
{
    public RewardModelingJob Job { get; set; } = new();
}

internal sealed class TrajectoryDataset
{
    public string Path { get; set; } = string.Empty;
    public List<Trajectory> Trajectories { get; set; } = new();
    public int StateDim { get; set; }
    public int ActionDim { get; set; }
}

internal sealed class Trajectory
{
    public float[] States { get; set; } = Array.Empty<float>();
    public int[] Actions { get; set; } = Array.Empty<int>();
    public float[] Rewards { get; set; } = Array.Empty<float>();
    public float[] NextStates { get; set; } = Array.Empty<float>();
    public bool[] Dones { get; set; } = Array.Empty<bool>();
}

/// <summary>
/// Reward modeling configuration.
/// </summary>
public sealed class RewardModelingOptions
{
    public string DefaultMethod { get; set; } = "gail";
    public int DefaultEpochs { get; set; } = 100;
    public int DefaultBatchSize { get; set; } = 256;
    public double DefaultLearningRate { get; set; } = 3e-4;
    public string ModelStoragePath { get; set; } = "./models/reward";
    public int MaxConcurrentJobs { get; set; } = 2;
}

/// <summary>
/// DI Extensions.
/// </summary>
public static class RewardModelingExtensions
{
    public static IServiceCollection AddKkbotRewardModeling(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RewardModelingOptions>(configuration.GetSection("RewardModeling"));
        services.AddSingleton<IRewardModeler, RewardModeler>();
        services.AddHostedService(sp => sp.GetRequiredService<IRewardModeler>());
        return services;
    }
}
