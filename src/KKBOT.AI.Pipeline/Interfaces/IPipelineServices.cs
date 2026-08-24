// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.AI.Abstractions;

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
    
    /// <summary>
    /// Performs streaming inference on a model.
    /// </summary>
    IAsyncEnumerable<InferenceStreamChunk> PredictStreamAsync(InferenceRequest request, CancellationToken ct = default);
    
    Task<ModelInfo> GetModelInfoAsync(string modelId, string version, CancellationToken ct = default);
    Task<bool> WarmupModelAsync(string modelId, string version, CancellationToken ct = default);
}

public interface IModelDeploymentService
{
    Task<ModelDeployment> DeployModelAsync(DeployModelRequest request, CancellationToken ct = default);
    Task<ModelDeployment?> GetDeploymentAsync(string deploymentId, CancellationToken ct = default);
    Task<IReadOnlyList<ModelDeployment>> ListDeploymentsAsync(DeploymentQuery query, CancellationToken ct = default);
    Task<ModelDeployment> ScaleDeploymentAsync(string deploymentId, int replicas, CancellationToken ct = default);
    Task<ModelDeployment> UpdateDeploymentAsync(string deploymentId, DeploymentUpdateRequest request, CancellationToken ct = default);
    Task<bool> DeleteDeploymentAsync(string deploymentId, CancellationToken ct = default);
    Task<DeploymentMetrics> GetDeploymentMetricsAsync(string deploymentId, CancellationToken ct = default);
    Task<IReadOnlyList<DeploymentLog>> GetDeploymentLogsAsync(string deploymentId, int lines = 100, CancellationToken ct = default);
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

public sealed record DeploymentLog(
    string DeploymentId,
    string PodName,
    string Message,
    string Level,
    DateTime Timestamp
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