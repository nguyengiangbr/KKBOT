// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KKBOT.AI.Pipeline;
using KKBOT.Identity.Authorization;
using Swashbuckle.AspNetCore.Annotations;

namespace KKBOT.API.Controllers;

[ApiController]
[Route("api/v1/ai/models")]
[Authorize]
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
    [RequirePermission(KKBOTPermissions.AIUse)]
    [ProducesResponseType(typeof(IEnumerable<ModelMetadata>), 200)]
    [SwaggerOperation("List models with filtering and pagination", "Requires ai.use permission")]
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
        var query = new ModelQuery(type, framework, status, tags, author, page, pageSize, sortBy, sortDescending);
        var models = await _modelRegistry.QueryAsync(query, ct);
        return Ok(models);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(KKBOTPermissions.AIUse, "model", "{id}")]
    [ProducesResponseType(typeof(ModelMetadata), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get model by ID", "Requires ai.use permission")]
    public async Task<ActionResult<ModelMetadata>> GetModel(Guid id, CancellationToken ct)
    {
        var model = await _modelRegistry.GetAsync(id, ct);
        return model == null ? NotFound() : Ok(model);
    }

    [HttpPost]
    [RequirePermission(KKBOTPermissions.AITrain)]
    [ProducesResponseType(typeof(ModelMetadata), 201)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Register a new model", "Requires ai.train permission")]
    public async Task<ActionResult<ModelMetadata>> RegisterModel([FromBody] RegisterModelRequest request, CancellationToken ct)
    {
        var model = await _modelRegistry.RegisterAsync(request, ct);
        return CreatedAtAction(nameof(GetModel), new { id = model.Id }, model);
    }

    [HttpPost("{id:guid}/train")]
    [RequirePermission(KKBOTPermissions.AITrain, "model", "{id}")]
    [ProducesResponseType(typeof(TrainingJob), 202)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Start model training", "Requires ai.train permission")]
    public async Task<ActionResult<TrainingJob>> TrainModel(Guid id, [FromBody] StartTrainingRequest request, CancellationToken ct)
    {
        var job = await _trainingOrchestrator.StartTrainingAsync(id, request, ct);
        return job == null ? NotFound() : Accepted(job);
    }

    [HttpPost("{id:guid}/deploy")]
    [RequirePermission(KKBOTPermissions.AIDeploy, "model", "{id}")]
    [ProducesResponseType(typeof(ModelDeployment), 202)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Deploy a model", "Requires ai.deploy permission")]
    public async Task<ActionResult<ModelDeployment>> DeployModel(Guid id, [FromBody] DeployModelRequest request, CancellationToken ct)
    {
        var deployment = await _deploymentService.DeployAsync(id, request, ct);
        return deployment == null ? NotFound() : Accepted(deployment);
    }

    [HttpPost("{id:guid}/infer")]
    [RequirePermission(KKBOTPermissions.AIUse, "model", "{id}")]
    [ProducesResponseType(typeof(InferenceResult), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Run inference on a model", "Requires ai.use permission")]
    public async Task<ActionResult<InferenceResult>> Infer(Guid id, [FromBody] InferenceRequest request, CancellationToken ct)
    {
        var result = await _inferenceEngine.InferAsync(id, request, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:guid}/infer/stream")]
    [RequirePermission(KKBOTPermissions.AIUse, "model", "{id}")]
    [ProducesResponseType(typeof(InferenceStreamResult), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Run streaming inference on a model", "Requires ai.use permission")]
    public async IAsyncEnumerable<InferenceStreamChunk> InferStream(Guid id, [FromBody] InferenceRequest request, CancellationToken ct)
    {
        await foreach (var chunk in _inferenceEngine.InferStreamAsync(id, request, ct))
        {
            yield return chunk;
        }
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(KKBOTPermissions.AIManage, "model", "{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Delete a model", "Requires ai.manage permission")]
    public async Task<IActionResult> DeleteModel(Guid id, CancellationToken ct)
    {
        var result = await _modelRegistry.DeleteAsync(id, ct);
        return result ? NoContent() : NotFound();
    }
}

[ApiController]
[Route("api/v1/ai/datasets")]
[Authorize]
[Produces("application/json")]
[SwaggerTag("AI Dataset Management")]
public sealed class DatasetsController : ControllerBase
{
    private readonly IDatasetRegistry _datasetRegistry;
    private readonly ILogger<DatasetsController> _logger;

    public DatasetsController(IDatasetRegistry datasetRegistry, ILogger<DatasetsController> logger)
    {
        _datasetRegistry = datasetRegistry;
        _logger = logger;
    }

    [HttpGet]
    [RequirePermission(KKBOTPermissions.AIUse)]
    [ProducesResponseType(typeof(IEnumerable<DatasetMetadata>), 200)]
    [SwaggerOperation("List datasets", "Requires ai.use permission")]
    public async Task<ActionResult<IEnumerable<DatasetMetadata>>> GetDatasets(
        [FromQuery] DatasetType? type,
        [FromQuery] string[]? tags,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new DatasetQuery(type, tags, page, pageSize);
        var datasets = await _datasetRegistry.QueryAsync(query, ct);
        return Ok(datasets);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(KKBOTPermissions.AIUse, "dataset", "{id}")]
    [ProducesResponseType(typeof(DatasetMetadata), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get dataset by ID", "Requires ai.use permission")]
    public async Task<ActionResult<DatasetMetadata>> GetDataset(Guid id, CancellationToken ct)
    {
        var dataset = await _datasetRegistry.GetAsync(id, ct);
        return dataset == null ? NotFound() : Ok(dataset);
    }

    [HttpPost]
    [RequirePermission(KKBOTPermissions.AITrain)]
    [ProducesResponseType(typeof(DatasetMetadata), 201)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Create a new dataset", "Requires ai.train permission")]
    public async Task<ActionResult<DatasetMetadata>> CreateDataset([FromBody] CreateDatasetRequest request, CancellationToken ct)
    {
        var dataset = await _datasetRegistry.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetDataset), new { id = dataset.Id }, dataset);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(KKBOTPermissions.AIManage, "dataset", "{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Delete a dataset", "Requires ai.manage permission")]
    public async Task<IActionResult> DeleteDataset(Guid id, CancellationToken ct)
    {
        var result = await _datasetRegistry.DeleteAsync(id, ct);
        return result ? NoContent() : NotFound();
    }
}

[ApiController]
[Route("api/v1/ai/experiments")]
[Authorize]
[Produces("application/json")]
[SwaggerTag("AI Experiment Management")]
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
    [RequirePermission(KKBOTPermissions.AIUse)]
    [ProducesResponseType(typeof(IEnumerable<ExperimentMetadata>), 200)]
    [SwaggerOperation("List experiments", "Requires ai.use permission")]
    public async Task<ActionResult<IEnumerable<ExperimentMetadata>>> GetExperiments(
        [FromQuery] ExperimentStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new ExperimentQuery(status, page, pageSize);
        var experiments = await _experimentTracker.QueryAsync(query, ct);
        return Ok(experiments);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(KKBOTPermissions.AIUse, "experiment", "{id}")]
    [ProducesResponseType(typeof(ExperimentMetadata), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get experiment by ID", "Requires ai.use permission")]
    public async Task<ActionResult<ExperimentMetadata>> GetExperiment(Guid id, CancellationToken ct)
    {
        var experiment = await _experimentTracker.GetAsync(id, ct);
        return experiment == null ? NotFound() : Ok(experiment);
    }

    [HttpPost]
    [RequirePermission(KKBOTPermissions.AITrain)]
    [ProducesResponseType(typeof(ExperimentMetadata), 201)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Create a new experiment", "Requires ai.train permission")]
    public async Task<ActionResult<ExperimentMetadata>> CreateExperiment([FromBody] CreateExperimentRequest request, CancellationToken ct)
    {
        var experiment = await _experimentTracker.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetExperiment), new { id = experiment.Id }, experiment);
    }

    [HttpPost("{id:guid}/start")]
    [RequirePermission(KKBOTPermissions.AITrain, "experiment", "{id}")]
    [ProducesResponseType(typeof(ExperimentMetadata), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Start an experiment", "Requires ai.train permission")]
    public async Task<ActionResult<ExperimentMetadata>> StartExperiment(Guid id, CancellationToken ct)
    {
        var experiment = await _experimentTracker.StartAsync(id, ct);
        return experiment == null ? NotFound() : Ok(experiment);
    }
}

public record RegisterModelRequest(
    string Name,
    ModelType Type,
    ModelFramework Framework,
    string ModelPath,
    string? Description = null,
    Dictionary<string, string>? Tags = null,
    ModelConfig? Config = null
);

public record StartTrainingRequest(
    Guid DatasetId,
    TrainingConfig Config,
    Dictionary<string, string>? Hyperparameters = null
);

public record DeployModelRequest(
    DeploymentTarget Target,
    DeploymentConfig Config
);

public record InferenceRequest(
    Dictionary<string, object> Input,
    InferenceOptions? Options = null
);

public record InferenceResult(
    Guid ModelId,
    Dictionary<string, object> Output,
    InferenceMetrics Metrics
);

public record InferenceStreamChunk(
    string ChunkType,
    Dictionary<string, object> Data,
    bool IsFinal
);

public record InferenceStreamResult(IAsyncEnumerable<InferenceStreamChunk> Stream);

public record CreateDatasetRequest(
    string Name,
    DatasetType Type,
    string SourcePath,
    string? Description = null,
    Dictionary<string, string>? Tags = null
);

public record CreateExperimentRequest(
    string Name,
    Guid ModelId,
    Guid DatasetId,
    ExperimentConfig Config,
    string? Description = null
);