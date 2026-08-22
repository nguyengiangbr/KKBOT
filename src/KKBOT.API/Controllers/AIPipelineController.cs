// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KKBOT.AI.Pipeline;
using Swashbuckle.AspNetCore.Annotations;

namespace KKBOT.API.Controllers;

[ApiController]
[Route("api/v1/ai/models")]
[Authorize(Policy = "RequirePermission:ai.use")]
[Produces("application/json")]
[SwaggerTag("AI Model Management")]
public sealed class ModelsController : ControllerBase
{
    private readonly IModelRegistry _modelRegistry;
    private readonly ITrainingOrchestrator _trainingOrchestrator;
    private readonly IInferenceEngine _inferenceEngine;
    private readonly IModelDeploymentService _deploymentService;
    private readonly ILogger<ModelsController> _logger;

    public ModelsController(
        IModelRegistry modelRegistry,
        ITrainingOrchestrator trainingOrchestrator,
        IInferenceEngine inferenceEngine,
        IModelDeploymentService deploymentService,
        ILogger<ModelsController> logger)
    {
        _modelRegistry = modelRegistry;
        _trainingOrchestrator = trainingOrchestrator;
        _inferenceEngine = inferenceEngine;
        _deploymentService = deploymentService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ModelMetadata>), 200)]
    [SwaggerOperation("List models with filtering and pagination")]
    public async Task<ActionResult<IEnumerable<ModelMetadata>>> GetModels(
        [FromQuery] ModelType? type,
        [FromQuery] ModelFramework? framework,
        [FromQuery] ModelStatus? status,
        [FromQuery] string[]? tags,
        [FromQuery] string? author,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string sortBy = "updatedAt",
        [FromQuery] bool sortDescending = true,
        CancellationToken ct = default)
    {
        var query = new ModelListQuery(type, framework, status, tags, author, page, pageSize, sortBy, sortDescending);
        var models = await _modelRegistry.ListModelsAsync(query, ct);
        return Ok(models);
    }

    [HttpGet("{modelId}")]
    [ProducesResponseType(typeof(ModelMetadata), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get model by ID (latest version)")]
    public async Task<ActionResult<ModelMetadata>> GetModel(string modelId, [FromQuery] string? version, CancellationToken ct)
    {
        var model = await _modelRegistry.GetModelAsync(modelId, version, ct);
        return model == null ? NotFound() : Ok(model);
    }

    [HttpPost]
    [Authorize(Policy = "RequirePermission:ai.manage_models")]
    [ProducesResponseType(typeof(ModelMetadata), 201)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Register a new model")]
    public async Task<ActionResult<ModelMetadata>> RegisterModel([FromBody] ModelMetadata model, CancellationToken ct)
    {
        try
        {
            var registered = await _modelRegistry.RegisterModelAsync(model, ct);
            return CreatedAtAction(nameof(GetModel), new { modelId = registered.ModelId, version = registered.Version }, registered);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPut("{modelId}/{version}")]
    [Authorize(Policy = "RequirePermission:ai.manage_models")]
    [ProducesResponseType(typeof(ModelMetadata), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Update model metadata")]
    public async Task<ActionResult<ModelMetadata>> UpdateModel(string modelId, string version, [FromBody] ModelMetadata model, CancellationToken ct)
    {
        var updated = model with { ModelId = modelId, Version = version };
        var success = await _modelRegistry.UpdateModelAsync(updated, ct);
        
        if (!success) return NotFound();
        
        var result = await _modelRegistry.GetModelAsync(modelId, version, ct);
        return Ok(result);
    }

    [HttpDelete("{modelId}/{version}")]
    [Authorize(Policy = "RequirePermission:ai.manage_models")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Delete a model version")]
    public async Task<IActionResult> DeleteModel(string modelId, string version, CancellationToken ct)
    {
        var success = await _modelRegistry.DeleteModelAsync(modelId, version, ct);
        return success ? NoContent() : NotFound();
    }

    [HttpPost("{modelId}/{version}/promote")]
    [Authorize(Policy = "RequirePermission:ai.manage_models")]
    [ProducesResponseType(typeof(ModelMetadata), 200)]
    [SwaggerOperation("Promote model to a new status (e.g., Ready, Deployed)")]
    public async Task<ActionResult<ModelMetadata>> PromoteModel(string modelId, string version, [FromBody] PromoteModelRequest request, CancellationToken ct)
    {
        var model = await _modelRegistry.PromoteModelAsync(modelId, version, request.NewStatus, ct);
        return Ok(model);
    }

    [HttpPost("training")]
    [Authorize(Policy = "RequirePermission:ai.manage_models")]
    [ProducesResponseType(typeof(TrainingJob), 201)]
    [SwaggerOperation("Start a training job")]
    public async Task<ActionResult<TrainingJob>> StartTraining([FromBody] StartTrainingRequest request, CancellationToken ct)
    {
        var job = await _trainingOrchestrator.StartTrainingAsync(request, ct);
        return CreatedAtAction(nameof(GetTrainingJob), new { jobId = job.JobId }, job);
    }

    [HttpGet("training/{jobId}")]
    [ProducesResponseType(typeof(TrainingJob), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get training job status")]
    public async Task<ActionResult<TrainingJob>> GetTrainingJob(string jobId, CancellationToken ct)
    {
        var job = await _trainingOrchestrator.GetTrainingJobAsync(jobId, ct);
        return job == null ? NotFound() : Ok(job);
    }

    [HttpGet("training")]
    [ProducesResponseType(typeof(IEnumerable<TrainingJob>), 200)]
    [SwaggerOperation("List training jobs")]
    public async Task<ActionResult<IEnumerable<TrainingJob>>> ListTrainingJobs(
        [FromQuery] string? modelId,
        [FromQuery] TrainingStatus? status,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new TrainingJobQuery(modelId, status, fromDate, toDate, page, pageSize);
        var jobs = await _trainingOrchestrator.ListTrainingJobsAsync(query, ct);
        return Ok(jobs);
    }

    [HttpPost("training/{jobId}/cancel")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Cancel a training job")]
    public async Task<IActionResult> CancelTraining(string jobId, CancellationToken ct)
    {
        var success = await _trainingOrchestrator.CancelTrainingAsync(jobId, ct);
        return success ? Ok(new { message = "Training cancelled" }) : NotFound();
    }

    [HttpPost("training/{jobId}/pause")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Pause a training job")]
    public async Task<IActionResult> PauseTraining(string jobId, CancellationToken ct)
    {
        var success = await _trainingOrchestrator.PauseTrainingAsync(jobId, ct);
        return success ? Ok(new { message = "Training paused" }) : NotFound();
    }

    [HttpPost("training/{jobId}/resume")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Resume a paused training job")]
    public async Task<IActionResult> ResumeTraining(string jobId, CancellationToken ct)
    {
        var success = await _trainingOrchestrator.ResumeTrainingAsync(jobId, ct);
        return success ? Ok(new { message = "Training resumed" }) : NotFound();
    }

    [HttpPost("predict")]
    [ProducesResponseType(typeof(InferenceResponse), 200)]
    [SwaggerOperation("Run inference on a model")]
    public async Task<ActionResult<InferenceResponse>> Predict([FromBody] InferenceRequest request, CancellationToken ct)
    {
        var response = await _inferenceEngine.PredictAsync(request, ct);
        return Ok(response);
    }

    [HttpPost("predict/batch")]
    [ProducesResponseType(typeof(IEnumerable<InferenceResponse>), 200)]
    [SwaggerOperation("Run batch inference")]
    public async Task<ActionResult<IEnumerable<InferenceResponse>>> PredictBatch([FromBody] IReadOnlyList<InferenceRequest> requests, CancellationToken ct)
    {
        var responses = await _inferenceEngine.PredictBatchAsync(requests, ct);
        return Ok(responses);
    }

    [HttpGet("{modelId}/{version}/info")]
    [ProducesResponseType(typeof(ModelInfo), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get model runtime info")]
    public async Task<ActionResult<ModelInfo>> GetModelInfo(string modelId, string version, CancellationToken ct)
    {
        var info = await _inferenceEngine.GetModelInfoAsync(modelId, version, ct);
        return info == null ? NotFound() : Ok(info);
    }

    [HttpPost("{modelId}/{version}/warmup")]
    [ProducesResponseType(200)]
    [SwaggerOperation("Warm up model for faster first inference")]
    public async Task<IActionResult> WarmupModel(string modelId, string version, CancellationToken ct)
    {
        var success = await _inferenceEngine.WarmupModelAsync(modelId, version, ct);
        return success ? Ok(new { message = "Model warmed up" }) : NotFound();
    }

    [HttpPost("deployments")]
    [Authorize(Policy = "RequirePermission:ai.manage_models")]
    [ProducesResponseType(typeof(ModelDeployment), 201)]
    [SwaggerOperation("Deploy a model version")]
    public async Task<ActionResult<ModelDeployment>> DeployModel([FromBody] DeployModelRequest request, CancellationToken ct)
    {
        var deployment = await _deploymentService.DeployAsync(request, ct);
        return CreatedAtAction(nameof(GetDeployment), new { deploymentId = deployment.DeploymentId }, deployment);
    }

    [HttpGet("deployments/{deploymentId}")]
    [ProducesResponseType(typeof(ModelDeployment), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get deployment status")]
    public async Task<ActionResult<ModelDeployment>> GetDeployment(string deploymentId, CancellationToken ct)
    {
        var deployment = await _deploymentService.GetDeploymentAsync(deploymentId, ct);
        return deployment == null ? NotFound() : Ok(deployment);
    }

    [HttpGet("deployments")]
    [ProducesResponseType(typeof(IEnumerable<ModelDeployment>), 200)]
    [SwaggerOperation("List deployments")]
    public async Task<ActionResult<IEnumerable<ModelDeployment>>> ListDeployments(
        [FromQuery] string? modelId,
        [FromQuery] DeploymentTarget? target,
        [FromQuery] DeploymentStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new DeploymentQuery(modelId, target, status, page, pageSize);
        var deployments = await _deploymentService.ListDeploymentsAsync(query, ct);
        return Ok(deployments);
    }

    [HttpPut("deployments/{deploymentId}")]
    [Authorize(Policy = "RequirePermission:ai.manage_models")]
    [ProducesResponseType(typeof(ModelDeployment), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Update deployment (scale, config)")]
    public async Task<ActionResult<ModelDeployment>> UpdateDeployment(string deploymentId, [FromBody] DeploymentUpdateRequest request, CancellationToken ct)
    {
        var deployment = await _deploymentService.UpdateDeploymentAsync(deploymentId, request, ct);
        return Ok(deployment);
    }

    [HttpDelete("deployments/{deploymentId}")]
    [Authorize(Policy = "RequirePermission:ai.manage_models")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Delete a deployment")]
    public async Task<IActionResult> DeleteDeployment(string deploymentId, CancellationToken ct)
    {
        var success = await _deploymentService.DeleteDeploymentAsync(deploymentId, ct);
        return success ? NoContent() : NotFound();
    }

    [HttpPost("deployments/{deploymentId}/scale")]
    [Authorize(Policy = "RequirePermission:ai.manage_models")]
    [ProducesResponseType(typeof(ModelDeployment), 200)]
    [SwaggerOperation("Scale deployment replicas")]
    public async Task<ActionResult<ModelDeployment>> ScaleDeployment(string deploymentId, [FromBody] ScaleDeploymentRequest request, CancellationToken ct)
    {
        var deployment = await _deploymentService.ScaleDeploymentAsync(deploymentId, request.Replicas, ct);
        return Ok(deployment);
    }

    [HttpGet("deployments/{deploymentId}/metrics")]
    [ProducesResponseType(typeof(DeploymentMetrics), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get deployment metrics")]
    public async Task<ActionResult<DeploymentMetrics>> GetDeploymentMetrics(string deploymentId, CancellationToken ct)
    {
        var metrics = await _deploymentService.GetDeploymentMetricsAsync(deploymentId, ct);
        return Ok(metrics);
    }
}

[ApiController]
[Route("api/v1/ai/datasets")]
[Authorize(Policy = "RequirePermission:ai.use")]
[Produces("application/json")]
[SwaggerTag("Dataset Management")]
public sealed class DatasetsController : ControllerBase
{
    private readonly IDatasetManager _datasetManager;
    private readonly ILogger<DatasetsController> _logger;

    public DatasetsController(IDatasetManager datasetManager, ILogger<DatasetsController> logger)
    {
        _datasetManager = datasetManager;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Dataset>), 200)]
    [SwaggerOperation("List datasets")]
    public async Task<ActionResult<IEnumerable<Dataset>>> GetDatasets(
        [FromQuery] string? name,
        [FromQuery] string[]? tags,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new DatasetQuery(name, tags, page, pageSize);
        var datasets = await _datasetManager.ListDatasetsAsync(query, ct);
        return Ok(datasets);
    }

    [HttpGet("{datasetId}")]
    [ProducesResponseType(typeof(Dataset), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get dataset by ID")]
    public async Task<ActionResult<Dataset>> GetDataset(string datasetId, [FromQuery] string? version, CancellationToken ct)
    {
        var dataset = await _datasetManager.GetDatasetAsync(datasetId, version, ct);
        return dataset == null ? NotFound() : Ok(dataset);
    }

    [HttpPost]
    [Authorize(Policy = "RequirePermission:ai.manage_models")]
    [ProducesResponseType(typeof(Dataset), 201)]
    [SwaggerOperation("Register a new dataset")]
    public async Task<ActionResult<Dataset>> RegisterDataset([FromBody] RegisterDatasetRequest request, CancellationToken ct)
    {
        try
        {
            var dataset = await _datasetManager.RegisterDatasetAsync(request, ct);
            return CreatedAtAction(nameof(GetDataset), new { datasetId = dataset.DatasetId, version = dataset.Version }, dataset);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpDelete("{datasetId}/{version}")]
    [Authorize(Policy = "RequirePermission:ai.manage_models")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Delete a dataset version")]
    public async Task<IActionResult> DeleteDataset(string datasetId, string version, CancellationToken ct)
    {
        var success = await _datasetManager.DeleteDatasetAsync(datasetId, version, ct);
        return success ? NoContent() : NotFound();
    }

    [HttpGet("{datasetId}/{version}/validate")]
    [ProducesResponseType(typeof(DatasetValidationResult), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Validate dataset")]
    public async Task<ActionResult<DatasetValidationResult>> ValidateDataset(string datasetId, string version, CancellationToken ct)
    {
        var result = await _datasetManager.ValidateDatasetAsync(datasetId, version, ct);
        return Ok(result);
    }

    [HttpGet("{datasetId}/{version}/statistics")]
    [ProducesResponseType(typeof(DatasetStatistics), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get dataset statistics")]
    public async Task<ActionResult<DatasetStatistics>> GetStatistics(string datasetId, string version, CancellationToken ct)
    {
        var stats = await _datasetManager.ComputeStatisticsAsync(datasetId, version, ct);
        return Ok(stats);
    }
}

[ApiController]
[Route("api/v1/ai/experiments")]
[Authorize(Policy = "RequirePermission:ai.manage_models")]
[Produces("application/json")]
[SwaggerTag("Experiment Tracking")]
public sealed class ExperimentsController : ControllerBase
{
    private readonly IExperimentTracker _experimentTracker;
    private readonly ILogger<ExperimentsController> _logger;

    public ExperimentsController(IExperimentTracker experimentTracker, ILogger<ExperimentsController> logger)
    {
        _experimentTracker = experimentTracker;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Experiment>), 200)]
    [SwaggerOperation("List experiments")]
    public async Task<ActionResult<IEnumerable<Experiment>>> GetExperiments(
        [FromQuery] string? modelId,
        [FromQuery] ExperimentStatus? status,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new ExperimentQuery(modelId, status, fromDate, toDate, page, pageSize);
        var experiments = await _experimentTracker.ListExperimentsAsync(query, ct);
        return Ok(experiments);
    }

    [HttpGet("{experimentId}")]
    [ProducesResponseType(typeof(Experiment), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get experiment by ID")]
    public async Task<ActionResult<Experiment>> GetExperiment(string experimentId, CancellationToken ct)
    {
        var experiment = await _experimentTracker.GetExperimentAsync(experimentId, ct);
        return experiment == null ? NotFound() : Ok(experiment);
    }

    [HttpPost]
    [ProducesResponseType(typeof(Experiment), 201)]
    [SwaggerOperation("Create a new experiment")]
    public async Task<ActionResult<Experiment>> CreateExperiment([FromBody] CreateExperimentRequest request, CancellationToken ct)
    {
        var experiment = await _experimentTracker.CreateExperimentAsync(request, ct);
        return CreatedAtAction(nameof(GetExperiment), new { experimentId = experiment.ExperimentId }, experiment);
    }

    [HttpPost("{experimentId}/run")]
    [ProducesResponseType(typeof(Experiment), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Run an experiment")]
    public async Task<ActionResult<Experiment>> RunExperiment(string experimentId, CancellationToken ct)
    {
        try
        {
            var experiment = await _experimentTracker.RunExperimentAsync(experimentId, ct);
            return Ok(experiment);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("compare")]
    [ProducesResponseType(typeof(ExperimentComparison), 200)]
    [SwaggerOperation("Compare multiple experiments")]
    public async Task<ActionResult<ExperimentComparison>> CompareExperiments([FromBody] CompareExperimentsRequest request, CancellationToken ct)
    {
        var comparison = await _experimentTracker.CompareExperimentsAsync(request.ExperimentIds, ct);
        return Ok(comparison);
    }
}

[ApiController]
[Route("api/v1/ai/pipelines")]
[Authorize(Policy = "RequirePermission:ai.manage_models")]
[Produces("application/json")]
[SwaggerTag("Pipeline Orchestration")]
public sealed class PipelinesController : ControllerBase
{
    private readonly IPipelineOrchestrator _orchestrator;
    private readonly ILogger<PipelinesController> _logger;

    public PipelinesController(IPipelineOrchestrator orchestrator, ILogger<PipelinesController> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    [HttpPost("run")]
    [ProducesResponseType(typeof(PipelineRun), 201)]
    [SwaggerOperation("Execute a pipeline")]
    public async Task<ActionResult<PipelineRun>> RunPipeline([FromBody] RunPipelineRequest request, CancellationToken ct)
    {
        var run = await _orchestrator.ExecutePipelineAsync(request.Pipeline, request.Input, ct);
        return CreatedAtAction(nameof(GetPipelineRun), new { runId = run.RunId }, run);
    }

    [HttpGet("runs/{runId}")]
    [ProducesResponseType(typeof(PipelineRun), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get pipeline run status")]
    public async Task<ActionResult<PipelineRun>> GetPipelineRun(string runId, CancellationToken ct)
    {
        var run = await _orchestrator.GetPipelineRunAsync(runId, ct);
        return run == null ? NotFound() : Ok(run);
    }

    [HttpGet("runs")]
    [ProducesResponseType(typeof(IEnumerable<PipelineRun>), 200)]
    [SwaggerOperation("List pipeline runs")]
    public async Task<ActionResult<IEnumerable<PipelineRun>>> ListPipelineRuns(
        [FromQuery] string? pipelineId,
        [FromQuery] PipelineStatus? status,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new PipelineRunQuery(pipelineId, status, fromDate, toDate, page, pageSize);
        var runs = await _orchestrator.ListPipelineRunsAsync(query, ct);
        return Ok(runs);
    }

    [HttpPost("runs/{runId}/cancel")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Cancel a pipeline run")]
    public async Task<IActionResult> CancelPipelineRun(string runId, CancellationToken ct)
    {
        var success = await _orchestrator.CancelPipelineRunAsync(runId, ct);
        return success ? Ok(new { message = "Pipeline cancelled" }) : NotFound();
    }
}

// Request models for controllers
public sealed record PromoteModelRequest(ModelStatus NewStatus);
public sealed record ScaleDeploymentRequest(int Replicas);
public sealed record CompareExperimentsRequest(string[] ExperimentIds);
public sealed record RunPipelineRequest(PipelineDefinition Pipeline, PipelineInput Input);