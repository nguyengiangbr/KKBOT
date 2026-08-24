// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KKBOT.MLPlatform.Abstractions;
using KKBOT.Observability;

namespace KKBOT.MLPlatform.OnlineLearning;

/// <summary>
/// Online/Continual Learning system for real-time model adaptation.
/// Supports: Experience replay, Elastic Weight Consolidation (EWC), concept drift detection.
/// </summary>
public sealed class OnlineLearningEngine : IHostedService, IDisposable
{
    private readonly ILogger<OnlineLearningEngine> _logger;
    private readonly OnlineLearningOptions _options;
    private readonly IMetricsRegistry _metrics;
    private readonly ConcurrentDictionary<string, OnlineModel> _models = new();
    private readonly Channel<LearningEvent> _eventChannel;
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly Task _trainingTask;
    private readonly Timer? _consolidationTimer;
    private bool _disposed;

    public OnlineLearningEngine(
        ILogger<OnlineLearningEngine> logger,
        IOptions<OnlineLearningOptions> options,
        IMetricsRegistry metrics)
    {
        _logger = logger;
        _options = options.Value;
        _metrics = metrics;

        _eventChannel = Channel.CreateBounded<LearningEvent>(new BoundedChannelOptions(_options.EventBufferSize)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

        _trainingTask = ProcessEventsAsync(_shutdownCts.Token);

        if (_options.EnableEWC)
        {
            _consolidationTimer = new Timer(
                ConsolidateWeightsAsync, 
                null, 
                TimeSpan.FromMinutes(_options.ConsolidationIntervalMinutes),
                TimeSpan.FromMinutes(_options.ConsolidationIntervalMinutes));
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Online Learning Engine started");
        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Online Learning Engine stopping");
        _shutdownCts.Cancel();
        _consolidationTimer?.Dispose();
        
        try
        {
            await _trainingTask;
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Registers a model for online learning.
    /// </summary>
    public async Task RegisterModelAsync(string modelId, OnlineModelConfig config, CancellationToken ct = default)
    {
        var model = new OnlineModel
        {
            ModelId = modelId,
            Config = config,
            Weights = InitializeWeights(config),
            ImportanceWeights = new Dictionary<string, float[]>(),
            FisherInformation = new Dictionary<string, float[]>(),
            ReplayBuffer = new CircularBuffer<ReplaySample>(config.ReplayBufferSize),
            StepCount = 0,
            LastUpdated = DateTimeOffset.UtcNow,
            PerformanceHistory = new CircularBuffer<double>(1000)
        };

        _models[modelId] = model;
        _logger.LogInformation("Registered online model {ModelId} with config: {Config}", modelId, config);
    }

    /// <summary>
    /// Records a learning event (observation + action + reward).
    /// </summary>
    public async Task RecordEventAsync(string modelId, LearningEvent learningEvent, CancellationToken ct = default)
    {
        if (!_models.TryGetValue(modelId, out var model))
        {
            _logger.LogWarning("Model {ModelId} not registered for online learning", modelId);
            return;
        }

        // Add to replay buffer
        model.ReplayBuffer.Add(new ReplaySample
        {
            Features = learningEvent.Features,
            Action = learningEvent.Action,
            Reward = learningEvent.Reward,
            NextFeatures = learningEvent.NextFeatures,
            Done = learningEvent.Done,
            Timestamp = learningEvent.Timestamp
        });

        // Add to event channel for async processing
        await _eventChannel.Writer.WriteAsync(learningEvent with { ModelId = modelId }, ct);
    }

    /// <summary>
    /// Gets current model performance metrics.
    /// </summary>
    public OnlineModelMetrics? GetMetrics(string modelId)
    {
        if (!_models.TryGetValue(modelId, out var model)) return null;

        return new OnlineModelMetrics
        {
            ModelId = modelId,
            StepCount = model.StepCount,
            AverageReward = model.PerformanceHistory.Any() ? model.PerformanceHistory.Average() : 0,
            RecentRewards = model.PerformanceHistory.ToArray(),
            LastUpdated = model.LastUpdated,
            BufferUtilization = (double)model.ReplayBuffer.Count / model.Config.ReplayBufferSize
        };
    }

    /// <summary>
    /// Triggers immediate training step.
    /// </summary>
    public async Task TriggerTrainingAsync(string modelId, CancellationToken ct = default)
    {
        if (!_models.TryGetValue(modelId, out var model)) return;

        await PerformTrainingStepAsync(model, ct);
    }

    private async Task ProcessEventsAsync(CancellationToken ct)
    {
        var batch = new List<LearningEvent>();

        while (!ct.IsCancellationRequested)
        {
            batch.Clear();

            // Wait for first event
            try
            {
                var evt = await _eventChannel.Reader.ReadAsync(ct);
                batch.Add(evt);
            }
            catch (ChannelClosedException)
            {
                break;
            }

            // Collect batch
            var batchSize = Math.Min(_options.BatchSize, _eventChannel.Reader.Count);
            for (int i = 0; i < batchSize; i++)
            {
                if (_eventChannel.Reader.TryRead(out var evt))
                {
                    batch.Add(evt);
                }
            }

            // Process batch
            if (batch.Count > 0)
            {
                await ProcessBatchAsync(batch, ct);
            }
        }
    }

    private async Task ProcessBatchAsync(List<LearningEvent> batch, CancellationToken ct)
    {
        var groups = batch.GroupBy(e => e.ModelId).ToList();

        foreach (var group in groups)
        {
            if (!_models.TryGetValue(group.Key, out var model)) continue;

            await PerformTrainingStepAsync(model, group.ToList(), ct);
        }
    }

    private async Task PerformTrainingStepAsync(OnlineModel model, CancellationToken ct = default)
    {
        await PerformTrainingStepAsync(model, new List<LearningEvent>(), ct);
    }

    private async Task PerformTrainingStepAsync(OnlineModel model, List<LearningEvent> events, CancellationToken ct)
    {
        if (model.ReplayBuffer.Count < model.Config.MinBatchSize) return;

        var stopwatch = Stopwatch.StartNew();
        var config = model.Config;

        try
        {
            // Sample from replay buffer
            var batchSize = Math.Min(config.BatchSize, model.ReplayBuffer.Count);
            var samples = model.ReplayBuffer.Sample(batchSize);

            // Compute gradients (simplified - real impl would use autograd)
            var gradients = ComputeGradients(model, samples);

            // Apply EWC regularization if enabled
            if (_options.EnableEWC && model.FisherInformation.Any())
            {
                gradients = ApplyEWCRegularization(model, gradients, config.EwcLambda);
            }

            // Update weights
            UpdateWeights(model, gradients, config.LearningRate);

            // Update Fisher Information for EWC
            if (_options.EnableEWC)
            {
                UpdateFisherInformation(model, samples);
            }

            model.StepCount++;
            model.LastUpdated = DateTimeOffset.UtcNow;

            // Track performance
            var avgReward = events.Any() ? events.Average(e => e.Reward) : 0;
            model.PerformanceHistory.Add(avgReward);

            stopwatch.Stop();
            _metrics.RecordHttpRequestDuration($"online_learning_{model.ModelId}", stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Training step failed for model {ModelId}", model.ModelId);
        }
    }

    private Dictionary<string, float[]> InitializeWeights(OnlineModelConfig config)
    {
        // Initialize weights based on architecture
        // This is a simplified placeholder
        var weights = new Dictionary<string, float[]>();
        
        // In practice, would load from pretrained model or initialize properly
        weights["initialized"] = new float[] { 1.0f };
        
        return weights;
    }

    private Dictionary<string, float[]> ComputeGradients(OnlineModel model, ReplaySample[] samples)
    {
        // Simplified gradient computation
        // Real implementation would use automatic differentiation
        var gradients = new Dictionary<string, float[]>();

        foreach (var kvp in model.Weights)
        {
            // Random gradient for demonstration
            var grad = new float[kvp.Value.Length];
            var random = new Random();
            for (int i = 0; i < grad.Length; i++)
            {
                grad[i] = (float)(random.NextDouble() - 0.5) * 0.01f;
            }
            gradients[kvp.Key] = grad;
        }

        return gradients;
    }

    private Dictionary<string, float[]> ApplyEWCRegularization(OnlineModel model, Dictionary<string, float[]> gradients, double lambda)
    {
        var regularized = new Dictionary<string, float[]>();

        foreach (var kvp in gradients)
        {
            if (model.FisherInformation.TryGetValue(kvp.Key, out var fisher) && 
                model.Weights.TryGetValue(kvp.Key, out var weights))
            {
                var regularizedGrad = new float[kvp.Value.Length];
                for (int i = 0; i < kvp.Value.Length; i++)
                {
                    // EWC penalty: lambda * F_i * (theta_i - theta*_i)
                    var ewcPenalty = (float)(lambda * fisher[i] * (weights[i] - model.OptimalWeights.GetValueOrDefault(kvp.Key, new float[weights.Length])[i]));
                    regularizedGrad[i] = kvp.Value[i] + ewcPenalty;
                }
                regularized[kvp.Key] = regularizedGrad;
            }
            else
            {
                regularized[kvp.Key] = kvp.Value;
            }
        }

        return regularized;
    }

    private void UpdateWeights(OnlineModel model, Dictionary<string, float[]> gradients, double learningRate)
    {
        foreach (var kvp in gradients)
        {
            if (model.Weights.TryGetValue(kvp.Key, out var weights))
            {
                for (int i = 0; i < weights.Length; i++)
                {
                    weights[i] -= (float)(learningRate * kvp.Value[i]);
                }
            }
        }
    }

    private void UpdateFisherInformation(OnlineModel model, ReplaySample[] samples)
    {
        // Compute Fisher Information Matrix diagonal (empirical)
        // F_i = E[(dL/dtheta_i)^2]
        
        foreach (var sample in samples)
        {
            // Compute gradients for this sample
            var sampleGradients = ComputeSampleGradients(model, sample);

            foreach (var kvp in sampleGradients)
            {
                if (!model.FisherInformation.ContainsKey(kvp.Key))
                {
                    model.FisherInformation[kvp.Key] = new float[kvp.Value.Length];
                }

                var fisher = model.FisherInformation[kvp.Key];
                for (int i = 0; i < fisher.Length; i++)
                {
                    // Running average of squared gradients
                    fisher[i] = (float)(0.99 * fisher[i] + 0.01 * kvp.Value[i] * kvp.Value[i]);
                }
            }
        }
    }

    private Dictionary<string, float[]> ComputeSampleGradients(OnlineModel model, ReplaySample sample)
    {
        // Simplified - compute gradients for single sample
        var gradients = new Dictionary<string, float[]>();
        
        foreach (var kvp in model.Weights)
        {
            var grad = new float[kvp.Value.Length];
            var random = new Random();
            for (int i = 0; i < grad.Length; i++)
            {
                grad[i] = (float)(random.NextDouble() - 0.5) * 0.01f;
            }
            gradients[kvp.Key] = grad;
        }

        return gradients;
    }

    private async void ConsolidateWeightsAsync(object? state)
    {
        if (_shutdownCts.Token.IsCancellationRequested) return;

        foreach (var model in _models.Values)
        {
            try
            {
                // Store optimal weights for EWC
                foreach (var kvp in model.Weights)
                {
                    model.OptimalWeights[kvp.Key] = kvp.Value.ToArray();
                }

                _logger.LogDebug("Consolidated weights for model {ModelId}", model.ModelId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Weight consolidation failed for model {ModelId}", model.ModelId);
            }
        }
    }

    /// <summary>
    /// Detects concept drift using statistical tests on performance history.
    /// </summary>
    public DriftDetectionResult DetectDrift(string modelId)
    {
        if (!_models.TryGetValue(modelId, out var model)) 
            return new DriftDetectionResult { Detected = false };

        var history = model.PerformanceHistory.ToArray();
        if (history.Length < 100) 
            return new DriftDetectionResult { Detected = false };

        // Split into reference and recent windows
        var referenceWindow = history.Take(history.Length / 2).ToArray();
        var recentWindow = history.Skip(history.Length / 2).ToArray();

        var refMean = referenceWindow.Average();
        var recentMean = recentWindow.Average();
        var refStd = Math.Sqrt(referenceWindow.Select(x => (x - refMean) * (x - refMean)).Average());
        var recentStd = Math.Sqrt(recentWindow.Select(x => (x - recentMean) * (x - recentMean)).Average());

        // KS-test or t-test for drift detection
        var tStat = (recentMean - refMean) / Math.Sqrt(refStd * refStd / referenceWindow.Length + recentStd * recentStd / recentWindow.Length);
        var pValue = 2 * (1 - NormalCDF(Math.Abs(tStat)));

        var driftDetected = pValue < _options.DriftThreshold;
        var driftMagnitude = Math.Abs(recentMean - refMean) / Math.Max(refStd, 1e-6);

        return new DriftDetectionResult
        {
            Detected = driftDetected,
            PValue = pValue,
            DriftMagnitude = driftMagnitude,
            ReferenceMean = refMean,
            RecentMean = recentMean,
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    private double NormalCDF(double x)
    {
        // Standard normal CDF approximation
        return 0.5 * (1 + Math.Erf(x / Math.Sqrt(2)));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _shutdownCts.Cancel();
        _trainingTask.Wait(TimeSpan.FromSeconds(30));
    }
}

// ==================== DATA MODELS ====================

internal sealed class OnlineModel
{
    public string ModelId { get; init; } = string.Empty;
    public OnlineModelConfig Config { get; init; } = new();
    public Dictionary<string, float[]> Weights { get; init; } = new();
    public Dictionary<string, float[]> ImportanceWeights { get; init; } = new();
    public Dictionary<string, float[]> FisherInformation { get; init; } = new();
    public Dictionary<string, float[]> OptimalWeights { get; init; } = new();
    public CircularBuffer<ReplaySample> ReplayBuffer { get; init; }
    public long StepCount { get; set; }
    public DateTimeOffset LastUpdated { get; set; }
    public CircularBuffer<double> PerformanceHistory { get; init; }
}

internal sealed class CircularBuffer<T>
{
    private readonly T[] _buffer;
    private int _head = 0;
    private int _count = 0;

    public CircularBuffer(int capacity)
    {
        _buffer = new T[capacity];
    }

    public int Count => _count;
    public int Capacity => _buffer.Length;

    public void Add(T item)
    {
        _buffer[_head] = item;
        _head = (_head + 1) % _buffer.Length;
        if (_count < _buffer.Length) _count++;
    }

    public T[] ToArray()
    {
        if (_count == 0) return Array.Empty<T>();
        if (_count < _buffer.Length) return _buffer.Take(_count).ToArray();

        var result = new T[_count];
        for (int i = 0; i < _count; i++)
        {
            result[i] = _buffer[(_head + i) % _buffer.Length];
        }
        return result;
    }

    public T[] Sample(int n)
    {
        var array = ToArray();
        if (n >= array.Length) return array;

        var random = new Random();
        return array.OrderBy(_ => random.Next()).Take(n).ToArray();
    }
}

internal sealed class ReplaySample
{
    public float[] Features { get; set; } = Array.Empty<float>();
    public int Action { get; set; }
    public float Reward { get; set; }
    public float[] NextFeatures { get; set; } = Array.Empty<float>();
    public bool Done { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}

internal sealed class LearningEvent
{
    public string ModelId { get; set; } = string.Empty;
    public float[] Features { get; set; } = Array.Empty<float>();
    public int Action { get; set; }
    public float Reward { get; set; }
    public float[] NextFeatures { get; set; } = Array.Empty<float>();
    public bool Done { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

public sealed record OnlineModelConfig
{
    public string Architecture { get; set; } = string.Empty;
    public int InputDim { get; set; }
    public int OutputDim { get; set; }
    public int HiddenDim { get; set; } = 256;
    public int ReplayBufferSize { get; set; } = 100000;
    public int BatchSize { get; set; } = 64;
    public int MinBatchSize { get; set; } = 32;
    public double LearningRate { get; set; } = 1e-4;
    public double EwcLambda { get; set; } = 1000.0;
    public string Optimizer { get; set; } = "Adam";
    public double WeightDecay { get; set; } = 1e-5;
}

public sealed record OnlineModelMetrics
{
    public string ModelId { get; init; } = string.Empty;
    public long StepCount { get; init; }
    public double AverageReward { get; init; }
    public double[] RecentRewards { get; init; } = Array.Empty<double>();
    public DateTimeOffset LastUpdated { get; init; }
    public double BufferUtilization { get; init; }
}

public sealed record DriftDetectionResult
{
    public bool Detected { get; init; }
    public double PValue { get; init; }
    public double DriftMagnitude { get; init; }
    public double ReferenceMean { get; init; }
    public double RecentMean { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Online learning configuration.
/// </summary>
public sealed class OnlineLearningOptions
{
    public int EventBufferSize { get; set; } = 10000;
    public int BatchSize { get; set; } = 64;
    public bool EnableEWC { get; set; } = true;
    public int ConsolidationIntervalMinutes { get; set; } = 60;
    public double DriftThreshold { get; set; } = 0.01;
    public bool EnableDriftDetection { get; set; } = true;
    public int DriftCheckIntervalMinutes { get; set; } = 30;
}

/// <summary>
/// DI Extensions.
/// </summary>
public static class OnlineLearningExtensions
{
    public static IServiceCollection AddKkbotOnlineLearning(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OnlineLearningOptions>(configuration.GetSection("OnlineLearning"));
        services.AddSingleton<OnlineLearningEngine>();
        services.AddHostedService(sp => sp.GetRequiredService<OnlineLearningEngine>());
        return services;
    }
}
