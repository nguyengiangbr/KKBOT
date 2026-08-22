// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace KKBOT.AI.Pipeline;

public enum ModelType
{
    Classification,
    Regression,
    TextGeneration,
    ImageClassification,
    ObjectDetection,
    Embedding,
    Custom
}

public enum ModelFramework
{
    ONNX,
    TensorFlow,
    PyTorch,
    MLNet,
    Ollama,
    HuggingFace,
    Custom
}

public enum ModelStatus
{
    Draft,
    Training,
    Validating,
    Ready,
    Deployed,
    Archived,
    Failed
}

public enum DeploymentTarget
{
    Local,
    Cloud,
    Edge,
    Hybrid
}

public sealed record ModelMetadata(
    string ModelId,
    string Name,
    string Version,
    ModelType Type,
    ModelFramework Framework,
    string Description,
    string Author,
    Dictionary<string, string> Tags,
    ModelMetrics Metrics,
    ModelSchema InputSchema,
    ModelSchema OutputSchema,
    Dictionary<string, object> Hyperparameters,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public sealed record ModelSchema(
    string Name,
    string DataType, // float32, int64, string, etc.
    int[] Shape,     // e.g., [1, 3, 224, 224] or [-1, 768] for dynamic batch
    string? Description = null
);

public sealed record ModelMetrics(
    double Accuracy,
    double Precision,
    double Recall,
    double F1Score,
    double Loss,
    Dictionary<string, double> CustomMetrics,
    DateTime EvaluatedAt,
    string DatasetVersion
);

public sealed record TrainingJob(
    string JobId,
    string ModelId,
    string Name,
    TrainingConfig Config,
    TrainingStatus Status,
    TrainingProgress Progress,
    string? ErrorMessage,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string? CheckpointPath,
    Dictionary<string, object> Artifacts
);

public sealed record TrainingConfig(
    string DatasetPath,
    string ValidationDatasetPath,
    int Epochs,
    int BatchSize,
    double LearningRate,
    string Optimizer,      // adam, sgd, etc.
    string LossFunction,   // cross_entropy, mse, etc.
    Dictionary<string, object> Hyperparameters,
    bool EarlyStopping,
    int EarlyStoppingPatience,
    string? CheckpointDir,
    string[] MetricsToTrack
);

public enum TrainingStatus
{
    Queued,
    Preparing,
    Running,
    Paused,
    Completed,
    Failed,
    Cancelled
}

public sealed record TrainingProgress(
    int CurrentEpoch,
    int TotalEpochs,
    int CurrentBatch,
    int TotalBatches,
    double CurrentLoss,
    Dictionary<string, double> CurrentMetrics,
    double EstimatedTimeRemainingSeconds
);

public sealed record InferenceRequest(
    string RequestId,
    string ModelId,
    string ModelVersion,
    Dictionary<string, object> Inputs,
    InferenceOptions Options
);

public sealed record InferenceOptions(
    bool ReturnProbabilities = false,
    int TopK = 5,
    double Temperature = 1.0,
    int MaxTokens = 100,
    Dictionary<string, object> CustomOptions = default!
);

public sealed record InferenceResponse(
    string RequestId,
    string ModelId,
    string ModelVersion,
    Dictionary<string, object> Outputs,
    InferenceMetrics Metrics,
    bool Success,
    string? ErrorMessage
);

public sealed record InferenceMetrics(
    double LatencyMs,
    double QueueTimeMs,
    double PreprocessTimeMs,
    double InferenceTimeMs,
    double PostprocessTimeMs,
    long InputTokens,
    long OutputTokens
);

public sealed record ModelDeployment(
    string DeploymentId,
    string ModelId,
    string ModelVersion,
    string Name,
    DeploymentTarget Target,
    int Replicas,
    DeploymentConfig Config,
    DeploymentStatus Status,
    string EndpointUrl,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public sealed record DeploymentConfig(
    int MinReplicas = 1,
    int MaxReplicas = 10,
    int TargetCpuUtilization = 70,
    int TargetMemoryUtilization = 80,
    int RequestTimeoutSeconds = 30,
    bool EnableGpu = false,
    string? GpuType = null,
    Dictionary<string, string> EnvironmentVariables = default!
);

public enum DeploymentStatus
{
    Pending,
    Creating,
    Running,
    Updating,
    Scaling,
    Stopped,
    Failed
}

public sealed record Dataset(
    string DatasetId,
    string Name,
    string Version,
    string Description,
    string SourcePath,
    long SizeBytes,
    int SampleCount,
    DatasetSchema Schema,
    DatasetStatistics Statistics,
    Dictionary<string, string> Tags,
    DateTime CreatedAt,
    string CreatedBy
);

public sealed record DatasetSchema(
    Dictionary<string, DatasetColumn> Columns,
    string? TargetColumn = null
);

public sealed record DatasetColumn(
    string Name,
    string DataType, // numeric, categorical, text, image, datetime
    bool IsNullable,
    Dictionary<string, object> Statistics // min, max, mean, unique_count, etc.
);

public sealed record DatasetStatistics(
    long TotalSamples,
    long TotalFeatures,
    double MissingValueRatio,
    Dictionary<string, object> FeatureStats
);

public sealed record Experiment(
    string ExperimentId,
    string Name,
    string Description,
    string ModelId,
    ExperimentConfig Config,
    ExperimentStatus Status,
    ExperimentResults Results,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt
);

public sealed record ExperimentConfig(
    string[] ModelVersions,
    string DatasetVersion,
    TrainingConfig TrainingConfig,
    Dictionary<string, object> Parameters,
    string[] MetricsToCompare
);

public enum ExperimentStatus
{
    Created,
    Running,
    Completed,
    Failed,
    Cancelled
}

public sealed record ExperimentResults(
    Dictionary<string, ModelMetrics> ModelMetrics,
    string BestModelVersion,
    Dictionary<string, object> Comparison,
    string Conclusion
);