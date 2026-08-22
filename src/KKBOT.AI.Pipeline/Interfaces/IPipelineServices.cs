// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KKBOT.AI.Pipeline;

public interface IModelRegistry
{
    Task<ModelMetadata> RegisterModelAsync(ModelMetadata model, CancellationToken ct = default);
    Task<ModelMetadata?> GetModelAsync(string modelId, string? version = null, CancellationToken ct = default);
    Task<IReadOnlyList<ModelMetadata>> ListModelsAsync(ModelListQuery query, CancellationToken ct = default);
    Task<bool> UpdateModelAsync(ModelMetadata model, CancellationToken ct = default);
    Task<bool> DeleteModelAsync(string modelId, string version, CancellationToken ct = default);
    Task<ModelMetadata> PromoteModelAsync(string modelId, string version, ModelStatus newStatus, CancellationToken ct = default);
}

public sealed record ModelListQuery(
    ModelType? Type = null,
    ModelFramework? Framework = null,
    ModelStatus? Status = null,
    string[]? Tags = null,
    string? Author = null,
    int Page = 1,
    int PageSize = 20,
    string SortBy = "updatedAt",
    bool SortDescending = true
);

public interface ITrainingOrchestrator
{
    Task<TrainingJob> StartTrainingAsync(StartTrainingRequest request, CancellationToken ct = default);
    Task<TrainingJob?> GetTrainingJobAsync(string jobId, CancellationToken ct = default);
    Task<IReadOnlyList<TrainingJob>> ListTrainingJobsAsync(TrainingJobQuery query, CancellationToken ct = default);
    Task<bool> CancelTrainingAsync(string jobId, CancellationToken ct = default);
    Task<bool> PauseTrainingAsync(string jobId, CancellationToken ct = default);
    Task<bool> ResumeTrainingAsync(string jobId, CancellationToken ct = default);
    Task<TrainingProgress> GetProgressAsync(string jobId, CancellationToken ct = default);
    Task<IReadOnlyList<TrainingCheckpoint>> GetCheckpointsAsync(string jobId, CancellationToken ct = default);
}

public sealed record StartTrainingRequest(
    string ModelId,
    string Name,
    TrainingConfig Config,
    string? ParentJobId = null
);

public sealed record TrainingJobQuery(
    string? ModelId = null,
    TrainingStatus? Status = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Page = 1,
    int PageSize = 20
);

public sealed record TrainingCheckpoint(
    string CheckpointId,
    string JobId,
    int Epoch,
    double Loss,
    Dictionary<string, double> Metrics,
    string Path,
    DateTime CreatedAt
);

public interface IInferenceEngine
{
    Task<InferenceResponse> PredictAsync(InferenceRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<InferenceResponse>> PredictBatchAsync(IReadOnlyList<InferenceRequest> requests, CancellationToken ct = default);
    Task<ModelInfo> GetModelInfoAsync(string modelId, string version, CancellationToken ct = default);
    Task<bool> WarmupModelAsync(string modelId, string version, CancellationToken ct = default);
}

public sealed record ModelInfo(
    string ModelId,
    string Version,
    ModelMetadata Metadata,
    bool IsLoaded,
    long MemoryUsageBytes,
    DateTime LastUsedAt
);

public interface IModelDeploymentService
{
    Task<ModelDeployment> DeployAsync(DeployModelRequest request, CancellationToken ct = default);
    Task<ModelDeployment?> GetDeploymentAsync(string deploymentId, CancellationToken ct = default);
    Task<IReadOnlyList<ModelDeployment>> ListDeploymentsAsync(DeploymentQuery query, CancellationToken ct = default);
    Task<ModelDeployment> UpdateDeploymentAsync(string deploymentId, DeploymentUpdateRequest request, CancellationToken ct = default);
    Task<bool> DeleteDeploymentAsync(string deploymentId, CancellationToken ct = default);
    Task<ModelDeployment> ScaleDeploymentAsync(string deploymentId, int replicas, CancellationToken ct = default);
    Task<DeploymentMetrics> GetDeploymentMetricsAsync(string deploymentId, CancellationToken ct = default);
}

public sealed record DeployModelRequest(
    string ModelId,
    string ModelVersion,
    string Name,
    DeploymentTarget Target,
    DeploymentConfig Config
);

public sealed record DeploymentUpdateRequest(
    int? Replicas = null,
    DeploymentConfig? Config = null
);

public sealed record DeploymentQuery(
    string? ModelId = null,
    DeploymentTarget? Target = null,
    DeploymentStatus? Status = null,
    int Page = 1,
    int PageSize = 20
);

public sealed record DeploymentMetrics(
    string DeploymentId,
    int CurrentReplicas,
    int DesiredReplicas,
    double CpuUtilization,
    double MemoryUtilization,
    double RequestsPerSecond,
    double AvgLatencyMs,
    double ErrorRate,
    long TotalRequests,
    DateTime LastUpdated
);

public interface IDatasetManager
{
    Task<Dataset> RegisterDatasetAsync(RegisterDatasetRequest request, CancellationToken ct = default);
    Task<Dataset?> GetDatasetAsync(string datasetId, string? version = null, CancellationToken ct = default);
    Task<IReadOnlyList<Dataset>> ListDatasetsAsync(DatasetQuery query, CancellationToken ct = default);
    Task<bool> DeleteDatasetAsync(string datasetId, string version, CancellationToken ct = default);
    Task<DatasetValidationResult> ValidateDatasetAsync(string datasetId, string version, CancellationToken ct = default);
    Task<DatasetStatistics> ComputeStatisticsAsync(string datasetId, string version, CancellationToken ct = default);
}

public sealed record RegisterDatasetRequest(
    string Name,
    string Version,
    string Description,
    string SourcePath,
    DatasetSchema Schema,
    Dictionary<string, string> Tags
);

public sealed record DatasetQuery(
    string? Name = null,
    string[]? Tags = null,
    int Page = 1,
    int PageSize = 20
);

public sealed record DatasetValidationResult(
    bool IsValid,
    IReadOnlyList<ValidationError> Errors,
    IReadOnlyList<ValidationWarning> Warnings,
    DatasetStatistics Statistics
);

public sealed record ValidationError(string Column, string Message, int? Row = null);
public sealed record ValidationWarning(string Column, string Message, int? Row = null);

public interface IExperimentTracker
{
    Task<Experiment> CreateExperimentAsync(CreateExperimentRequest request, CancellationToken ct = default);
    Task<Experiment?> GetExperimentAsync(string experimentId, CancellationToken ct = default);
    Task<IReadOnlyList<Experiment>> ListExperimentsAsync(ExperimentQuery query, CancellationToken ct = default);
    Task<Experiment> RunExperimentAsync(string experimentId, CancellationToken ct = default);
    Task<ExperimentComparison> CompareExperimentsAsync(string[] experimentIds, CancellationToken ct = default);
}

public sealed record CreateExperimentRequest(
    string Name,
    string Description,
    string ModelId,
    ExperimentConfig Config
);

public sealed record ExperimentQuery(
    string? ModelId = null,
    ExperimentStatus? Status = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Page = 1,
    int PageSize = 20
);

public sealed record ExperimentComparison(
    string[] ExperimentIds,
    Dictionary<string, ModelMetrics> Metrics,
    string BestExperimentId,
    Dictionary<string, object> DetailedComparison,
    string Summary
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
    string Description,
    string DataType,
    string Source,
    Dictionary<string, object> Metadata,
    DateTime CreatedAt
);

public interface IModelMonitor
{
    Task<MonitoringReport> GenerateReportAsync(string modelId, string deploymentId, MonitoringPeriod period, CancellationToken ct = default);
    Task<DataDriftReport> DetectDataDriftAsync(string modelId, string deploymentId, string baselineDatasetId, CancellationToken ct = default);
    Task<ConceptDriftReport> DetectConceptDriftAsync(string modelId, string deploymentId, CancellationToken ct = default);
    Task<PerformanceAlert[]> CheckAlertsAsync(string modelId, string deploymentId, CancellationToken ct = default);
}

public enum MonitoringPeriod
{
    LastHour,
    Last24Hours,
    Last7Days,
    Last30Days,
    Custom
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
    DateTime DetectedAt,
    double OverallDriftScore,
    Dictionary<string, double> FeatureDriftScores,
    Dictionary<string, DriftDetails> DriftedFeatures,
    bool IsSignificant
);

public sealed record DriftDetails(
    double DriftScore,
    string DriftType, // ks_test, psi, chi_square
    double PValue,
    object BaselineStats,
    object CurrentStats
);

public sealed record ConceptDriftReport(
    string ModelId,
    string DeploymentId,
    DateTime DetectedAt,
    double DriftScore,
    double PerformanceDrop,
    string DetectionMethod,
    bool IsSignificant
);

public sealed record PerformanceAlert(
    string AlertId,
    string ModelId,
    string DeploymentId,
    AlertSeverity Severity,
    string Message,
    string MetricName,
    double CurrentValue,
    double Threshold,
    DateTime TriggeredAt
);

public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}

public sealed record Recommendation(
    string Type, // retrain, scale, investigate, update_threshold
    string Description,
    int Priority, // 1 = high, 2 = medium, 3 = low
    Dictionary<string, object> Parameters
);

public interface IPipelineOrchestrator
{
    Task<PipelineRun> ExecutePipelineAsync(PipelineDefinition pipeline, PipelineInput input, CancellationToken ct = default);
    Task<PipelineRun?> GetPipelineRunAsync(string runId, CancellationToken ct = default);
    Task<IReadOnlyList<PipelineRun>> ListPipelineRunsAsync(PipelineRunQuery query, CancellationToken ct = default);
    Task<bool> CancelPipelineRunAsync(string runId, CancellationToken ct = default);
}

public sealed record PipelineDefinition(
    string PipelineId,
    string Name,
    string Version,
    PipelineStep[] Steps,
    Dictionary<string, object> Parameters
);

public sealed record PipelineStep(
    string StepId,
    string Name,
    StepType Type,
    string ComponentId, // modelId, datasetId, etc.
    Dictionary<string, object> Config,
    string[] DependsOn,
    RetryPolicy RetryPolicy
);

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
    Monitoring,
    Custom
}

public sealed record RetryPolicy(
    int MaxRetries = 3,
    int RetryDelaySeconds = 60,
    bool ExponentialBackoff = true
);

public sealed record PipelineInput(
    Dictionary<string, object> Parameters,
    string[] DatasetIds,
    string[] ModelIds
);

public sealed record PipelineRun(
    string RunId,
    string PipelineId,
    string PipelineVersion,
    PipelineStatus Status,
    PipelineStepRun[] StepRuns,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string? ErrorMessage,
    Dictionary<string, object> Outputs
);

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
    Skipped,
    Retrying
}

public sealed record PipelineRunQuery(
    string? PipelineId = null,
    PipelineStatus? Status = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Page = 1,
    int PageSize = 20
);