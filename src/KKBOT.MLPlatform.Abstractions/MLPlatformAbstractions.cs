// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KKBOT.MLPlatform.Abstractions;

/// <summary>
/// Model metadata and versioning.
/// </summary>
public sealed record ModelMetadata
{
    public string ModelId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public ModelType Type { get; init; } = ModelType.Onnx;
    public ModelFramework Framework { get; init; } = ModelFramework.PyTorch;
    public string Description { get; init; } = string.Empty;
    public Dictionary<string, string> Tags { get; init; } = new();
    public ModelMetrics Metrics { get; init; } = new();
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public string CreatedBy { get; init; } = string.Empty;
    public string SourceModelId { get; init; } = string.Empty; // For distilled models
    public DistillationConfig DistillationConfig { get; init; } = null;
    public ModelSignature Signature { get; init; } = new();
}

public enum ModelType
{
    Onnx,
    TensorRT,
    TorchScript,
    TensorFlow,
    Wasm
}

public enum ModelFramework
{
    PyTorch,
    TensorFlow,
    JAX,
    ONNX,
    ScikitLearn,
    XGBoost,
    LightGBM
}

public sealed record ModelMetrics
{
    public double Accuracy { get; init; }
    public double Precision { get; init; }
    public double Recall { get; init; }
    public double F1Score { get; init; }
    public double LatencyMs { get; init; }
    public double ThroughputQps { get; init; }
    public long MemoryBytes { get; init; }
    public long ModelSizeBytes { get; init; }
    public Dictionary<string, double> CustomMetrics { get; init; } = new();
}

public sealed record DistillationConfig
{
    public string TeacherModelId { get; init; } = string.Empty;
    public string StudentArchitecture { get; init; } = string.Empty;
    public double Temperature { get; init; } = 4.0;
    public double Alpha { get; init; } = 0.7; // Weight for distillation loss
    public string Quantization { get; init; } = "INT8"; // NONE, INT8, INT4, FP16
    public int Epochs { get; init; } = 10;
    public double LearningRate { get; init; } = 1e-4;
    public string DatasetId { get; init; } = string.Empty;
}

public sealed record ModelSignature
{
    public ModelInput[] Inputs { get; init; } = Array.Empty<ModelInput>();
    public ModelOutput[] Outputs { get; init; } = Array.Empty<ModelOutput>();
}

public sealed record ModelInput
{
    public string Name { get; init; } = string.Empty;
    public string DataType { get; init; } = string.Empty; // float32, int32, etc.
    public int[] Shape { get; init; } = Array.Empty<int>(); // -1 for dynamic batch
    public string Description { get; init; } = string.Empty;
}

public sealed record ModelOutput
{
    public string Name { get; init; } = string.Empty;
    public string DataType { get; init; } = string.Empty;
    public int[] Shape { get; init; } = Array.Empty<int>();
    public string Description { get; init; } = string.Empty;
}

/// <summary>
/// Inference request/response.
/// </summary>
public sealed record InferenceRequest
{
    public string ModelId { get; init; } = string.Empty;
    public string ModelVersion { get; init; } = "latest";
    public Dictionary<string, float[]> Inputs { get; init; } = new();
    public InferenceOptions Options { get; init; } = new();
    public string RequestId { get; init; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record InferenceOptions
{
    public bool ReturnProbabilities { get; init; } = false;
    public int TopK { get; init; } = 1;
    public double Temperature { get; init; } = 1.0;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
    public bool EnableProfiling { get; init; } = false;
}

public sealed record InferenceResponse
{
    public string RequestId { get; init; } = string.Empty;
    public string ModelId { get; init; } = string.Empty;
    public string ModelVersion { get; init; } = string.Empty;
    public Dictionary<string, float[]> Outputs { get; init; } = new();
    public InferenceMetrics Metrics { get; init; } = new();
    public bool Success { get; init; } = true;
    public string ErrorMessage { get; init; } = string.Empty;
    public DateTimeOffset CompletedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record InferenceMetrics
{
    public double LatencyMs { get; init; }
    public double QueueTimeMs { get; init; }
    public double ComputeTimeMs { get; init; }
    public long MemoryUsedBytes { get; init; }
    public int BatchSize { get; init; }
}

/// <summary>
/// Feature store interfaces.
/// </summary>
public sealed record FeatureVector
{
    public string EntityId { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty; // player, item, session, etc.
    public Dictionary<string, FeatureValue> Features { get; init; } = new();
    public DateTimeOffset EventTimestamp { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedTimestamp { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record FeatureValue
{
    public object Value { get; init; }
    public FeatureType Type { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}

public enum FeatureType
{
    Numerical,
    Categorical,
    Text,
    Embedding,
    Boolean,
    Timestamp
}

public sealed record FeatureDefinition
{
    public string Name { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty;
    public FeatureType Type { get; init; }
    public string Description { get; init; } = string.Empty;
    public string Owner { get; init; } = string.Empty;
    public Dictionary<string, string> Tags { get; init; } = new();
    public FeatureValidation Validation { get; init; } = new();
}

public sealed record FeatureValidation
{
    public double? MinValue { get; init; }
    public double? MaxValue { get; init; }
    public string[] AllowedValues { get; init; } = Array.Empty<string>();
    public string RegexPattern { get; init; } = string.Empty;
    public bool AllowNull { get; init; } = false;
}

/// <summary>
/// Training job specification.
/// </summary>
public sealed record TrainingJob
{
    public string JobId { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = string.Empty;
    public TrainingType Type { get; init; } = TrainingType.Distillation;
    public TrainingConfig Config { get; init; } = new();
    public TrainingStatus Status { get; init; } = TrainingStatus.Pending;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
    public TrainingMetrics Metrics { get; init; } = new();
    public string[] ArtifactUrls { get; init; } = Array.Empty<string>();
}

public enum TrainingType
{
    Distillation,
    FineTuning,
    FullTraining,
    OnlineLearning,
    HyperparameterTuning
}

public sealed record TrainingConfig
{
    public string BaseModelId { get; init; } = string.Empty;
    public string DatasetId { get; init; } = string.Empty;
    public Dictionary<string, object> Hyperparameters { get; init; } = new();
    public ResourceRequirements Resources { get; init; } = new();
    public int MaxDurationMinutes { get; init; } = 60;
    public CheckpointConfig Checkpointing { get; init; } = new();
    public EarlyStoppingConfig EarlyStopping { get; init; } = new();
}

public sealed record ResourceRequirements
{
    public int GpuCount { get; init; } = 1;
    public string GpuType { get; init; } = "any"; // A100, V100, T4, etc.
    public int CpuCores { get; init; } = 4;
    public long MemoryGb { get; init; } = 16;
    public long StorageGb { get; init; } = 50;
}

public sealed record CheckpointConfig
{
    public bool Enabled { get; init; } = true;
    public int IntervalMinutes { get; init; } = 10;
    public int MaxCheckpoints { get; init; } = 5;
    public string StoragePath { get; init; } = string.Empty;
}

public sealed record EarlyStoppingConfig
{
    public bool Enabled { get; init; } = true;
    public string MonitorMetric { get; init; } = "val_loss";
    public string Mode { get; init; } = "min"; // min, max
    public double MinDelta { get: init; } = 1e-4;
    public int Patience { get: init; } = 5;
}

public enum TrainingStatus
{
    Pending,
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled,
    Preempted
}

public sealed record TrainingMetrics
{
    public double TrainLoss { get: init; }
    public double ValLoss { get: init; }
    public double Accuracy { get: init; }
    public double LearningRate { get: init; }
    public int Epoch { get: init; }
    public int Step { get: init; }
    public double ThroughputSamplesPerSec { get: init; }
    public double GpuUtilization { get: init; }
    public double MemoryUtilization { get: init; }
    public Dictionary<string, double> CustomMetrics { get: init; } = new();
}

/// <summary>
/// Dataset metadata.
/// </summary>
public sealed record DatasetMetadata
{
    public string DatasetId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public long SampleCount { get; init; }
    public long SizeBytes { get; init; }
    public string StoragePath { get; init; } = string.Empty;
    public string Format { get: init; } = "parquet"; // parquet, tfrecord, jsonl
    public Schema Schema { get: init; } = new();
    public Dictionary<string, string> Tags { get: init; } = new();
    public DateTimeOffset CreatedAt { get: init; } = DateTimeOffset.UtcNow;
    public DataQualityReport QualityReport { get: init; } = new();
}

public sealed record Schema
{
    public SchemaField[] Fields { get: init; } = Array.Empty<SchemaField>();
}

public sealed record SchemaField
{
    public string Name { get: init; } = string.Empty;
    public string Type { get: init; } = string.Empty;
    public bool Nullable { get: init; } = false;
    public string Description { get: init; } = string.Empty;
}

public sealed record DataQualityReport
{
    public double Completeness { get: init; } // 0-1
    public double Consistency { get: init; }
    public double Validity { get: init; }
    public double Uniqueness { get: init; }
    public Dictionary<string, double> ColumnStats { get: init; } = new();
    public string[] Issues { get: init; } = Array.Empty<string>();
}

/// <summary>
/// A/B test configuration.
/// </summary>
public sealed record ABTestConfig
{
    public string ExperimentId { get: init; } = Guid.NewGuid().ToString("N");
    public string Name { get: init; } = string.Empty;
    public string Description { get: init; } = string.Empty;
    public ABTestVariant[] Variants { get: init; } = Array.Empty<ABTestVariant>();
    public TrafficSplit TrafficSplit { get: init; } = new();
    public DateTimeOffset StartDate { get: init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndDate { get: init; }
    public ExperimentStatus Status { get: init; } = ExperimentStatus.Draft;
    public ABTestMetrics TargetMetrics { get: init; } = new();
    public int MinimumSampleSize { get: init; } = 1000;
    public double SignificanceLevel { get: init; } = 0.05;
    public double MinimumDetectableEffect { get: init; } = 0.01;
}

public sealed record ABTestVariant
{
    public string VariantId { get: init; } = string.Empty;
    public string Name { get: init; } = string.Empty;
    public string ModelId { get: init; } = string.Empty;
    public string ModelVersion { get: init; } = string.Empty;
    public double TrafficPercentage { get: init; }
    public Dictionary<string, object> ConfigOverrides { get: init; } = new();
}

public sealed record TrafficSplit
{
    public string Strategy { get: init; } = "random"; // random, user_hash, session_hash
    public string Salt { get: init; } = string.Empty;
}

public enum ExperimentStatus
{
    Draft,
    Running,
    Paused,
    Completed,
    Archived
}

public sealed record ABTestMetrics
{
    public string PrimaryMetric { get: init; } = string.Empty;
    public string[] SecondaryMetrics { get: init; } = Array.Empty<string>();
    public string MetricDirection { get: init; } = "increase"; // increase, decrease
}

/// <summary>
/// Core service interfaces.
/// </summary>
public interface IModelRegistry
{
    Task<ModelMetadata> RegisterModelAsync(ModelMetadata metadata, CancellationToken ct = default);
    Task<ModelMetadata?> GetModelAsync(string modelId, string version = "latest", CancellationToken ct = default);
    Task<IReadOnlyList<ModelMetadata>> ListModelsAsync(ModelFilter filter, CancellationToken ct = default);
    Task<bool> DeleteModelAsync(string modelId, string version, CancellationToken ct = default);
    Task<ModelMetadata> PromoteModelAsync(string modelId, string version, string targetStage, CancellationToken ct = default);
}

public sealed record ModelFilter
{
    public string[] Tags { get; init; } = Array.Empty<string>();
    public ModelType? Type { get; init; }
    public ModelFramework? Framework { get; init; }
    public string Stage { get; init; } = string.Empty; // staging, production, archived
    public int PageSize { get: init; } = 20;
    public int PageToken { get: init; } = 0;
}

public interface IModelServer
{
    Task<InferenceResponse> PredictAsync(InferenceRequest request, CancellationToken ct = default);
    Task<IAsyncEnumerable<InferenceResponse>> PredictStreamAsync(InferenceRequest request, CancellationToken ct = default);
    Task<ModelHealth> GetHealthAsync(string modelId, CancellationToken ct = default);
    Task<IReadOnlyList<ModelInstance>> GetInstancesAsync(string modelId, CancellationToken ct = default);
}

public sealed record ModelHealth
{
    public string ModelId { get: init; } = string.Empty;
    public bool Healthy { get: init; }
    public int ReadyInstances { get: init; }
    public int TotalInstances { get: init; }
    public double AvgLatencyMs { get: init; }
    public double ErrorRate { get: init; }
    public Dictionary<string, string> Details { get: init; } = new();
}

public sealed record ModelInstance
{
    public string InstanceId { get: init; } = string.Empty;
    public string Host { get: init; } = string.Empty;
    public int Port { get: init; }
    public bool Ready { get: init; }
    public int ActiveRequests { get: init; }
    public DateTimeOffset StartedAt { get: init; }
}

public interface IFeatureStore
{
    Task WriteFeaturesAsync(IEnumerable<FeatureVector> features, CancellationToken ct = default);
    Task<FeatureVector?> GetFeaturesAsync(string entityType, string entityId, IEnumerable<string> featureNames, DateTimeOffset? asOf = null, CancellationToken ct = default);
    Task<IReadOnlyList<FeatureVector>> GetHistoricalFeaturesAsync(string entityType, IEnumerable<string> entityIds, IEnumerable<string> featureNames, DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default);
    Task<FeatureDefinition> RegisterFeatureAsync(FeatureDefinition definition, CancellationToken ct = default);
    Task<IReadOnlyList<FeatureDefinition>> ListFeaturesAsync(string entityType, CancellationToken ct = default);
}

public interface ITrainingOrchestrator
{
    Task<TrainingJob> SubmitJobAsync(TrainingJob job, CancellationToken ct = default);
    Task<TrainingJob?> GetJobAsync(string jobId, CancellationToken ct = default);
    Task CancelJobAsync(string jobId, CancellationToken ct = default);
    Task<IAsyncEnumerable<TrainingJobEvent>> WatchJobAsync(string jobId, CancellationToken ct = default);
}

public sealed record TrainingJobEvent
{
    public string JobId { get: init; } = string.Empty;
    public TrainingStatus Status { get: init; }
    public TrainingMetrics Metrics { get: init; } = new();
    public string Message { get: init; } = string.Empty;
    public DateTimeOffset Timestamp { get: init; } = DateTimeOffset.UtcNow;
}

public interface IABTestManager
{
    Task<ABTestConfig> CreateExperimentAsync(ABTestConfig config, CancellationToken ct = default);
    Task<ABTestConfig?> GetExperimentAsync(string experimentId, CancellationToken ct = default);
    Task<ABTestConfig> StartExperimentAsync(string experimentId, CancellationToken ct = default);
    Task<ABTestConfig> StopExperimentAsync(string experimentId, CancellationToken ct = default);
    Task<ABTestResult> GetResultsAsync(string experimentId, CancellationToken ct = default);
    Task<string> GetVariantAsync(string experimentId, string userId, CancellationToken ct = default);
}

public sealed record ABTestResult
{
    public string ExperimentId { get: init; } = string.Empty;
    public ExperimentStatus Status { get: init; }
    public ABTestVariantResult[] VariantResults { get: init; } = Array.Empty<ABTestVariantResult>();
    public StatisticalTestResult StatisticalTest { get: init; } = new();
    public string Recommendation { get: init; } = string.Empty;
    public DateTimeOffset GeneratedAt { get: init; } = DateTimeOffset.UtcNow;
}

public sealed record ABTestVariantResult
{
    public string VariantId { get: init; } = string.Empty;
    public int SampleSize { get: init; }
    public Dictionary<string, double> Metrics { get: init; } = new();
    public Dictionary<string, double> ConfidenceIntervals { get: init; } = new();
}

public sealed record StatisticalTestResult
{
    public string TestType { get: init; } = string.Empty; // t-test, chi-square, etc.
    public double PValue { get: init; }
    public bool Significant { get: init; }
    public double EffectSize { get: init; }
    public double ConfidenceLevel { get: init; }
}
