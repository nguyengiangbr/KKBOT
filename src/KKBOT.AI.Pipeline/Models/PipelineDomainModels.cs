// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KKBOT.AI.Pipeline;

public enum PipelineStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}

public enum StepStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Retrying
}

public enum StepType
{
    DataIngestion,
    DataValidation,
    DataTransformation,
    FeatureEngineering,
    Training,
    Evaluation,
    ModelRegistration,
    Deployment,
    Monitoring
}

public enum MonitoringPeriod
{
    LastHour,
    Last24Hours,
    Last7Days,
    Last30Days,
    AllTime
}

public enum AlertSeverity
{
    Critical,
    Warning,
    Info
}

public sealed record ModelInfo(
    string ModelId,
    string Version,
    ModelMetadata Model,
    bool IsLoaded,
    long MemoryBytes,
    DateTime LastUsedAt
);

public sealed record ExperimentComparison(
    string[] ExperimentIds,
    Dictionary<string, ModelMetrics> ModelMetrics,
    string BestExperimentId,
    Dictionary<string, object> Comparison,
    string Conclusion
);

public interface IFeatureStore
{
    Task<FeatureVector> GetFeaturesAsync(string entityId, string[] featureNames, CancellationToken ct = default);
    Task<bool> SetFeaturesAsync(string entityId, FeatureVector features, CancellationToken ct = default);
    Task<FeatureVector> GetLatestFeaturesAsync(string entityId, CancellationToken ct = default);
    Task<IReadOnlyList<FeatureDefinition>> ListFeaturesAsync(CancellationToken ct = default);
    Task<FeatureDefinition> RegisterFeatureAsync(FeatureDefinition feature, CancellationToken ct = default);
}

public sealed record FeatureVector(
    string EntityId,
    Dictionary<string, object> Features,
    DateTime Timestamp
);

public sealed record FeatureDefinition(
    string Name,
    string? Description = null
);

public interface IModelMonitor
{
    Task<MonitoringReport> GenerateReportAsync(string modelId, string deploymentId, MonitoringPeriod period, CancellationToken ct = default);
    Task<DataDriftReport> DetectDataDriftAsync(string modelId, string deploymentId, string baselineDatasetId, CancellationToken ct = default);
    Task<ConceptDriftReport> DetectConceptDriftAsync(string modelId, string deploymentId, CancellationToken ct = default);
    Task<PerformanceAlert[]> CheckAlertsAsync(string modelId, string deploymentId, CancellationToken ct = default);
}

public sealed record MonitoringReport(
    string ModelId,
    string DeploymentId,
    MonitoringPeriod Period,
    DateTime GeneratedAt,
    DeploymentMetrics Metrics,
    DataDriftReport? DataDrift,
    ConceptDriftReport? ConceptDrift,
    PerformanceAlert[] Alerts,
    Recommendation[] Recommendations
);

public sealed record DataDriftReport(
    string ModelId,
    string DeploymentId,
    string BaselineDatasetId,
    DateTime GeneratedAt,
    double OverallDriftScore,
    Dictionary<string, double> FeatureDriftScores,
    Dictionary<string, DriftDetails> DriftedFeatures,
    bool IsSignificant
);

public sealed record DriftDetails(
    double Score,
    string Method,
    double PValue,
    object BaselineStats,
    object CurrentStats
);

public sealed record ConceptDriftReport(
    string ModelId,
    string DeploymentId,
    DateTime GeneratedAt,
    double PerformanceDrop,
    double DriftScore,
    string DetectionMethod,
    bool IsSignificant
);

public sealed record PerformanceAlert(
    string Id,
    string ModelId,
    string DeploymentId,
    AlertSeverity Severity,
    string Message,
    string MetricName,
    double MetricValue,
    double Threshold,
    DateTime Timestamp
);

public sealed record Recommendation(
    string Type,
    string Description,
    int Priority,
    Dictionary<string, object> Metadata
);

public interface IPipelineOrchestrator
{
    Task<PipelineRun> ExecutePipelineAsync(PipelineDefinition pipeline, PipelineInput input, CancellationToken ct = default);
    Task<PipelineRun?> GetPipelineRunAsync(string runId, CancellationToken ct = default);
    Task<IReadOnlyList<PipelineRun>> ListPipelineRunsAsync(PipelineRunQuery query, CancellationToken ct = default);
    Task<bool> CancelPipelineRunAsync(string runId, CancellationToken ct = default);
}

public sealed record PipelineRun
{
    public string RunId { get; init; } = string.Empty;
    public string PipelineId { get; init; } = string.Empty;
    public string PipelineVersion { get; init; } = string.Empty;
    public PipelineStatus Status { get; set; } = PipelineStatus.Pending;
    public PipelineStepRun[] StepRuns { get; set; } = System.Array.Empty<PipelineStepRun>();
    public DateTime StartedAt { get; init; } = DateTime.MinValue;
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object> Outputs { get; set; } = new();
}

public sealed record PipelineStepRun(
    string StepId,
    string Name,
    StepStatus Status,
    DateTime StartedAt,
    DateTime? CompletedAt,
    Dictionary<string, object> Inputs,
    Dictionary<string, object> Outputs,
    string? ErrorMessage,
    int RetryCount
);

public sealed record PipelineDefinition(
    string PipelineId,
    string Name,
    string Version,
    PipelineStep[] Steps,
    Dictionary<string, object> Parameters
);

public sealed record PipelineInput(
    Dictionary<string, object> Parameters,
    string[] InputDatasets,
    string[] InputModels
);

public sealed record PipelineStep(
    string StepId,
    string Name,
    StepType Type,
    string Input,
    Dictionary<string, object> Parameters,
    string[] DependsOn,
    RetryPolicy RetryPolicy
);

public sealed record RetryPolicy
{
    public int MaxRetries { get; init; } = 3;
    public int RetryDelaySeconds { get; init; } = 5;
}

public sealed record PipelineRunQuery
{
    public string? PipelineId { get; init; }
    public PipelineStatus? Status { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
