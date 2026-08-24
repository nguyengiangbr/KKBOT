// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.AI.Pipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KKBOT.AI.Pipeline.Services;

public sealed class LocalModelRegistry : IModelRegistry
{
    private readonly ConcurrentDictionary<string, ModelMetadata> _models = new();
    private readonly ILogger<LocalModelRegistry> _logger;
    private readonly string _storagePath;

    public LocalModelRegistry(ILogger<LocalModelRegistry> logger, IOptions<AIPipelineOptions> options)
    {
        _logger = logger;
        _storagePath = Path.Combine(options.Value.StoragePath, "models");
        Directory.CreateDirectory(_storagePath);
        LoadFromDisk();
    }

    private void LoadFromDisk()
    {
        var indexFile = Path.Combine(_storagePath, "registry-index.json");
        if (File.Exists(indexFile))
        {
            try
            {
                var json = File.ReadAllText(indexFile);
                var models = JsonSerializer.Deserialize<List<ModelMetadata>>(json);
                if (models != null)
                {
                    foreach (var model in models)
                    {
                        _models[GetKey(model.ModelId, model.Version)] = model;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load model registry index");
            }
        }
    }

    private void SaveToDisk()
    {
        try
        {
            var indexFile = Path.Combine(_storagePath, "registry-index.json");
            var json = JsonSerializer.Serialize(_models.Values.ToList(), new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(indexFile, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save model registry index");
        }
    }

    private static string GetKey(string modelId, string version) => $"{modelId}@{version}";

    public Task<ModelMetadata> RegisterModelAsync(ModelMetadata model, CancellationToken ct = default)
    {
        var key = GetKey(model.ModelId, model.Version);
        
        if (_models.ContainsKey(key))
        {
            throw new InvalidOperationException($"Model {model.ModelId} v{model.Version} already exists");
        }

        var registeredModel = model with
        {
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _models[key] = registeredModel;
        SaveToDisk();

        _logger.LogInformation("Registered model {ModelId} v{Version} ({Type})", model.ModelId, model.Version, model.Type);
        return Task.FromResult(registeredModel);
    }

    public Task<ModelMetadata?> GetModelAsync(string modelId, string? version = null, CancellationToken ct = default)
    {
        if (version != null)
        {
            _models.TryGetValue(GetKey(modelId, version), out var model);
            return Task.FromResult(model);
        }

        // Return latest version
        var matches = _models.Values
            .Where(m => m.ModelId == modelId)
            .OrderByDescending(m => m.Version)
            .FirstOrDefault();

        return Task.FromResult(matches);
    }

    public Task<IReadOnlyList<ModelMetadata>> ListModelsAsync(ModelListQuery query, CancellationToken ct = default)
    {
        var results = _models.Values.AsEnumerable();

        if (query.Type.HasValue)
            results = results.Where(m => m.Type == query.Type.Value);

        if (query.Framework.HasValue)
            results = results.Where(m => m.Framework == query.Framework.Value);

        if (query.Status.HasValue)
            results = results.Where(m => m.Metrics.Accuracy > 0); // Approximation

        if (query.Tags?.Length > 0)
        {
            results = results.Where(m => query.Tags!.All(t => m.Tags.ContainsKey(t)));
        }

        if (!string.IsNullOrWhiteSpace(query.Author))
        {
            results = results.Where(m => m.Author.Equals(query.Author, StringComparison.OrdinalIgnoreCase));
        }

        results = query.SortBy.ToLowerInvariant() switch
        {
            "name" => query.SortDescending ? results.OrderByDescending(m => m.Name) : results.OrderBy(m => m.Name),
            "createdat" => query.SortDescending ? results.OrderByDescending(m => m.CreatedAt) : results.OrderBy(m => m.CreatedAt),
            "accuracy" => query.SortDescending ? results.OrderByDescending(m => m.Metrics.Accuracy) : results.OrderBy(m => m.Metrics.Accuracy),
            _ => query.SortDescending ? results.OrderByDescending(m => m.UpdatedAt) : results.OrderBy(m => m.UpdatedAt)
        };

        var paged = results.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();
        return Task.FromResult<IReadOnlyList<ModelMetadata>>(paged);
    }

    public Task<bool> UpdateModelAsync(ModelMetadata model, CancellationToken ct = default)
    {
        var key = GetKey(model.ModelId, model.Version);
        
        if (!_models.ContainsKey(key))
            return Task.FromResult(false);

        var updated = model with { UpdatedAt = DateTime.UtcNow };
        _models[key] = updated;
        SaveToDisk();

        _logger.LogInformation("Updated model {ModelId} v{Version}", model.ModelId, model.Version);
        return Task.FromResult(true);
    }

    public Task<bool> DeleteModelAsync(string modelId, string version, CancellationToken ct = default)
    {
        var removed = _models.TryRemove(GetKey(modelId, version), out _);
        if (removed) SaveToDisk();
        return Task.FromResult(removed);
    }

    public Task<ModelMetadata> PromoteModelAsync(string modelId, string version, ModelStatus newStatus, CancellationToken ct = default)
    {
        var model = GetModelAsync(modelId, version, ct).Result;
        if (model == null)
            throw new InvalidOperationException($"Model {modelId} v{version} not found");

        // In a real implementation, we'd update the model status
        // For now, we return the model as-is
        return Task.FromResult(model);
    }
}

public sealed class LocalTrainingOrchestrator : ITrainingOrchestrator
{
    private readonly ConcurrentDictionary<string, TrainingJob> _jobs = new();
    private readonly ILogger<LocalTrainingOrchestrator> _logger;
    private readonly IModelRegistry _modelRegistry;

    public LocalTrainingOrchestrator(ILogger<LocalTrainingOrchestrator> logger, IModelRegistry modelRegistry)
    {
        _logger = logger;
        _modelRegistry = modelRegistry;
    }

    public async Task<TrainingJob> StartTrainingAsync(StartTrainingRequest request, CancellationToken ct = default)
    {
        var job = new TrainingJob
        {
            JobId = Guid.NewGuid().ToString(),
            ModelId = request.ModelId,
            Name = request.Name,
            Config = request.Config,
            Status = TrainingStatus.Queued,
            Progress = new TrainingProgress(0, request.Config.Epochs, 0, 0, 0, new(), 0),
            StartedAt = DateTime.UtcNow
        };

        _jobs[job.JobId] = job;
        
        _logger.LogInformation("Started training job {JobId} for model {ModelId}", job.JobId, request.ModelId);

        // Simulate training in background
        _ = Task.Run(() => SimulateTrainingAsync(job, ct), ct);

        return job;
    }

    private async Task SimulateTrainingAsync(TrainingJob job, CancellationToken ct)
    {
        try
        {
            job = job with { Status = TrainingStatus.Preparing };
            _jobs[job.JobId] = job;
            await Task.Delay(1000, ct);

            job = job with { Status = TrainingStatus.Running };
            _jobs[job.JobId] = job;

            for (int epoch = 1; epoch <= job.Config.Epochs; epoch++)
            {
                ct.ThrowIfCancellationRequested();
                
                // Simulate epoch
                await Task.Delay(2000, ct);

                var progress = new TrainingProgress(
                    epoch,
                    job.Config.Epochs,
                    epoch,
                    job.Config.Epochs,
                    1.0 / epoch, // Decreasing loss
                    new Dictionary<string, double> { ["accuracy"] = 0.5 + (0.4 * epoch / job.Config.Epochs) },
                    (job.Config.Epochs - epoch) * 2.0
                );

                job = job with { Progress = progress };
                _jobs[job.JobId] = job;
            }

            job = job with 
            { 
                Status = TrainingStatus.Completed,
                Progress = job.Progress with { CurrentEpoch = job.Config.Epochs },
                CompletedAt = DateTime.UtcNow
            };
            _jobs[job.JobId] = job;

            _logger.LogInformation("Training job {JobId} completed", job.JobId);
        }
        catch (OperationCanceledException)
        {
            job = job with { Status = TrainingStatus.Cancelled };
            _jobs[job.JobId] = job;
        }
        catch (Exception ex)
        {
            job = job with 
            { 
                Status = TrainingStatus.Failed,
                ErrorMessage = ex.Message,
                CompletedAt = DateTime.UtcNow
            };
            _jobs[job.JobId] = job;
            _logger.LogError(ex, "Training job {JobId} failed", job.JobId);
        }
    }

    public Task<TrainingJob?> GetTrainingJobAsync(string jobId, CancellationToken ct = default)
    {
        _jobs.TryGetValue(jobId, out var job);
        return Task.FromResult(job);
    }

    public Task<IReadOnlyList<TrainingJob>> ListTrainingJobsAsync(TrainingJobQuery query, CancellationToken ct = default)
    {
        var results = _jobs.Values.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query.ModelId))
            results = results.Where(j => j.ModelId == query.ModelId);

        if (query.Status.HasValue)
            results = results.Where(j => j.Status == query.Status.Value);

        if (query.FromDate.HasValue)
            results = results.Where(j => j.StartedAt >= query.FromDate.Value);

        if (query.ToDate.HasValue)
            results = results.Where(j => j.StartedAt <= query.ToDate.Value);

        results = results.OrderByDescending(j => j.StartedAt);

        var paged = results.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();
        return Task.FromResult<IReadOnlyList<TrainingJob>>(paged);
    }

    public Task<bool> CancelTrainingAsync(string jobId, CancellationToken ct = default)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            if (job.Status == TrainingStatus.Running || job.Status == TrainingStatus.Queued)
            {
                var updated = job with { Status = TrainingStatus.Cancelled, CompletedAt = DateTime.UtcNow };
                _jobs[jobId] = updated;
                return Task.FromResult(true);
            }
        }
        return Task.FromResult(false);
    }

    public Task<bool> PauseTrainingAsync(string jobId, CancellationToken ct = default)
    {
        if (_jobs.TryGetValue(jobId, out var job) && job.Status == TrainingStatus.Running)
        {
            var updated = job with { Status = TrainingStatus.Paused };
            _jobs[jobId] = updated;
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<bool> ResumeTrainingAsync(string jobId, CancellationToken ct = default)
    {
        if (_jobs.TryGetValue(jobId, out var job) && job.Status == TrainingStatus.Paused)
        {
            var updated = job with { Status = TrainingStatus.Running };
            _jobs[jobId] = updated;
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<TrainingProgress> GetProgressAsync(string jobId, CancellationToken ct = default)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            return Task.FromResult(job.Progress);
        }
        return Task.FromResult(new TrainingProgress(0, 0, 0, 0, 0, new(), 0));
    }

    public Task<IReadOnlyList<TrainingCheckpoint>> GetCheckpointsAsync(string jobId, CancellationToken ct = default)
    {
        // Return empty for now
        return Task.FromResult<IReadOnlyList<TrainingCheckpoint>>(Array.Empty<TrainingCheckpoint>());
    }
}

public sealed class LocalInferenceEngine : IInferenceEngine
{
    private readonly ConcurrentDictionary<string, ModelInfo> _loadedModels = new();
    private readonly ILogger<LocalInferenceEngine> _logger;
    private readonly IModelRegistry _modelRegistry;

    public LocalInferenceEngine(ILogger<LocalInferenceEngine> logger, IModelRegistry modelRegistry)
    {
        _logger = logger;
        _modelRegistry = modelRegistry;
    }

    public async Task<InferenceResponse> PredictAsync(InferenceRequest request, CancellationToken ct = default)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            // Get model info
            var modelInfo = await GetOrLoadModelAsync(request.ModelId, request.ModelVersion, ct);
            
            var preprocessStart = DateTime.UtcNow;
            // Simulate preprocessing
            await Task.Delay(10, ct);
            var preprocessTime = (DateTime.UtcNow - preprocessStart).TotalMilliseconds;

            var inferenceStart = DateTime.UtcNow;
            // Simulate inference
            await Task.Delay(50, ct);
            var inferenceTime = (DateTime.UtcNow - inferenceStart).TotalMilliseconds;

            var postprocessStart = DateTime.UtcNow;
            // Simulate postprocessing
            await Task.Delay(5, ct);
            var postprocessTime = (DateTime.UtcNow - postprocessStart).TotalMilliseconds;

            var totalLatency = (DateTime.UtcNow - startTime).TotalMilliseconds;

            // Generate mock output based on model type
            var model = await _modelRegistry.GetModelAsync(request.ModelId, request.ModelVersion, ct);
            var outputs = GenerateMockOutput(model, request);

            return new InferenceResponse(
                request.RequestId,
                request.ModelId,
                request.ModelVersion,
                outputs,
                new InferenceMetrics(
                    totalLatency,
                    0,
                    preprocessTime,
                    inferenceTime,
                    postprocessTime,
                    0,
                    0
                ),
                true,
                null
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Inference failed for request {RequestId}", request.RequestId);
            return new InferenceResponse(
                request.RequestId,
                request.ModelId,
                request.ModelVersion,
                new Dictionary<string, object>(),
                new InferenceMetrics(0, 0, 0, 0, 0, 0, 0),
                false,
                ex.Message
            );
        }
    }

    public async Task<IReadOnlyList<InferenceResponse>> PredictBatchAsync(IReadOnlyList<InferenceRequest> requests, CancellationToken ct = default)
    {
        var tasks = requests.Select(r => PredictAsync(r, ct));
        return await Task.WhenAll(tasks);
    }

    public async IAsyncEnumerable<InferenceStreamChunk> PredictStreamAsync(InferenceRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        _logger.LogInformation("Starting streaming inference for model {ModelId} v{Version}", request.ModelId, request.ModelVersion);
        
        var modelInfo = await GetOrLoadModelAsync(request.ModelId, request.ModelVersion, ct);
        var modelMetadata = await _modelRegistry.GetModelAsync(request.ModelId, request.ModelVersion, ct);
        
        var startTime = DateTime.UtcNow;
        var requestId = request.RequestId ?? Guid.NewGuid().ToString();
        
        try
        {
            // Simulate streaming response based on model type
            string fullResponse = "";
            
            if (modelMetadata?.Type == ModelType.TextGeneration)
            {
                // Simulate token-by-token generation
                var mockText = "This is a generated response from the model. ";
                var tokens = mockText.Split(' ');
                
                foreach (var token in tokens)
                {
                    if (ct.IsCancellationRequested) break;
                    
                    fullResponse += token + " ";
                    yield return new InferenceStreamChunk(
                        token + " ",
                        false,
                        new Dictionary<string, object> 
                        { 
                            ["tokens_generated"] = fullResponse.Split(' ').Length - 1,
                            ["model_id"] = request.ModelId
                        });
                    
                    await Task.Delay(50, ct); // Simulate token generation delay
                }
            }
            else
            {
                // For non-text models, yield the full response at once
                var response = await PredictAsync(request, ct);
                fullResponse = System.Text.Json.JsonSerializer.Serialize(response.Output);
                yield return new InferenceStreamChunk(fullResponse, true, new Dictionary<string, object> { ["final"] = true });
            }

            // Final chunk with metrics
            var totalLatency = (DateTime.UtcNow - startTime).TotalMilliseconds;
            yield return new InferenceStreamChunk(
                "",
                true,
                new Dictionary<string, object>
                {
                    ["final"] = true,
                    ["total_latency_ms"] = totalLatency,
                    ["tokens_total"] = fullResponse.Split(' ').Length
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Streaming inference failed for request {RequestId}", requestId);
            yield return new InferenceStreamChunk(
                $"Error: {ex.Message}",
                true,
                new Dictionary<string, object> { ["error"] = true, ["message"] = ex.Message });
        }
    }

    public async Task<ModelInfo> GetModelInfoAsync(string modelId, string version, CancellationToken ct = default)
    {
        return await GetOrLoadModelAsync(modelId, version, ct);
    }

    public async Task<bool> WarmupModelAsync(string modelId, string version, CancellationToken ct = default)
    {
        var model = await _modelRegistry.GetModelAsync(modelId, version, ct);
        if (model == null) return false;

        // Simulate warmup
        await Task.Delay(100, ct);
        
        var key = $"{modelId}@{version}";
        if (_loadedModels.TryGetValue(key, out var info))
        {
            _loadedModels[key] = info with { IsLoaded = true, LastUsedAt = DateTime.UtcNow };
        }
        
        return true;
    }

    private async Task<ModelInfo> GetOrLoadModelAsync(string modelId, string version, CancellationToken ct)
    {
        var key = $"{modelId}@{version}";
        
        if (_loadedModels.TryGetValue(key, out var existing))
        {
            return existing with { LastUsedAt = DateTime.UtcNow };
        }

        var model = await _modelRegistry.GetModelAsync(modelId, version, ct);
        if (model == null)
            throw new InvalidOperationException($"Model {modelId} v{version} not found");

        // Simulate loading
        await Task.Delay(500, ct);

        var info = new ModelInfo(
            modelId,
            version,
            model,
            true,
            1024 * 1024 * 100, // 100MB mock
            DateTime.UtcNow
        );

        _loadedModels[key] = info;
        return info;
    }

    private Dictionary<string, object> GenerateMockOutput(ModelMetadata? model, InferenceRequest request)
    {
        if (model == null) return new Dictionary<string, object>();

        return model.Type switch
        {
            ModelType.Classification => new Dictionary<string, object>
            {
                ["predictions"] = new[] { 0.8, 0.15, 0.05 },
                ["labels"] = new[] { "class_a", "class_b", "class_c" },
                ["confidence"] = 0.8
            },
            ModelType.Regression => new Dictionary<string, object>
            {
                ["value"] = 42.5,
                ["confidence_interval"] = new[] { 40.0, 45.0 }
            },
            ModelType.TextGeneration => new Dictionary<string, object>
            {
                ["generated_text"] = "This is a generated response from the model.",
                ["tokens_used"] = 25
            },
            ModelType.Embedding => new Dictionary<string, object>
            {
                ["embedding"] = new float[768], // 768-dim embedding
                ["dimensions"] = 768
            },
            _ => new Dictionary<string, object> { ["result"] = "mock_output" }
        };
    }
}

public sealed class LocalModelDeploymentService : IModelDeploymentService
{
    private readonly ConcurrentDictionary<string, ModelDeployment> _deployments = new();
    private readonly ILogger<LocalModelDeploymentService> _logger;
    private readonly IModelRegistry _modelRegistry;

    public LocalModelDeploymentService(ILogger<LocalModelDeploymentService> logger, IModelRegistry modelRegistry)
    {
        _logger = logger;
        _modelRegistry = modelRegistry;
    }

    public async Task<ModelDeployment> DeployAsync(DeployModelRequest request, CancellationToken ct = default)
    {
        var model = await _modelRegistry.GetModelAsync(request.ModelId, request.ModelVersion, ct);
        if (model == null)
            throw new InvalidOperationException($"Model {request.ModelId} v{request.ModelVersion} not found");

        var deployment = new ModelDeployment(
            Guid.NewGuid().ToString(),
            request.ModelId,
            request.ModelVersion,
            request.Name,
            request.Target,
            1,
            request.Config,
            DeploymentStatus.Creating,
            $"http://localhost:8080/api/v1/models/{request.ModelId}/v{request.ModelVersion}/predict",
            DateTime.UtcNow,
            null
        );

        _deployments[deployment.DeploymentId] = deployment;

        // Simulate deployment
        await Task.Delay(2000, ct);

        var deployed = deployment with { Status = DeploymentStatus.Running };
        _deployments[deployment.DeploymentId] = deployed;

        _logger.LogInformation("Deployed model {ModelId} v{Version} as {DeploymentId}", 
            request.ModelId, request.ModelVersion, deployment.DeploymentId);

        return deployed;
    }

    public Task<ModelDeployment?> GetDeploymentAsync(string deploymentId, CancellationToken ct = default)
    {
        _deployments.TryGetValue(deploymentId, out var deployment);
        return Task.FromResult(deployment);
    }

    public Task<IReadOnlyList<ModelDeployment>> ListDeploymentsAsync(DeploymentQuery query, CancellationToken ct = default)
    {
        var results = _deployments.Values.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query.ModelId))
            results = results.Where(d => d.ModelId == query.ModelId);

        if (query.Target.HasValue)
            results = results.Where(d => d.Target == query.Target.Value);

        if (query.Status.HasValue)
            results = results.Where(d => d.Status == query.Status.Value);

        results = results.OrderByDescending(d => d.CreatedAt);

        var paged = results.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();
        return Task.FromResult<IReadOnlyList<ModelDeployment>>(paged);
    }

    public Task<ModelDeployment> UpdateDeploymentAsync(string deploymentId, DeploymentUpdateRequest request, CancellationToken ct = default)
    {
        if (!_deployments.TryGetValue(deploymentId, out var deployment))
            throw new InvalidOperationException($"Deployment {deploymentId} not found");

        var updated = deployment with
        {
            Replicas = request.Replicas ?? deployment.Replicas,
            Config = request.Config ?? deployment.Config,
            Status = DeploymentStatus.Updating,
            UpdatedAt = DateTime.UtcNow
        };

        _deployments[deploymentId] = updated;

        // Simulate update
        _ = Task.Run(async () =>
        {
            await Task.Delay(1000, ct);
            var final = updated with { Status = DeploymentStatus.Running };
            _deployments[deploymentId] = final;
        }, ct);

        return Task.FromResult(updated);
    }

    public Task<bool> DeleteDeploymentAsync(string deploymentId, CancellationToken ct = default)
    {
        if (!_deployments.TryGetValue(deploymentId, out var deployment))
            return Task.FromResult(false);

        var updated = deployment with { Status = DeploymentStatus.Stopped };
        _deployments[deploymentId] = updated;

        // Simulate deletion
        _ = Task.Run(async () =>
        {
            await Task.Delay(1000, ct);
            _deployments.TryRemove(deploymentId, out _);
        }, ct);

        return Task.FromResult(true);
    }

    public Task<ModelDeployment> ScaleDeploymentAsync(string deploymentId, int replicas, CancellationToken ct = default)
    {
        if (!_deployments.TryGetValue(deploymentId, out var deployment))
            throw new InvalidOperationException($"Deployment {deploymentId} not found");

        var updated = deployment with
        {
            Replicas = replicas,
            Status = DeploymentStatus.Scaling,
            UpdatedAt = DateTime.UtcNow
        };

        _deployments[deploymentId] = updated;

        // Simulate scaling
        _ = Task.Run(async () =>
        {
            await Task.Delay(1000, ct);
            var final = updated with { Status = DeploymentStatus.Running, Replicas = replicas };
            _deployments[deploymentId] = final;
        }, ct);

        return Task.FromResult(updated);
    }

    public Task<DeploymentMetrics> GetDeploymentMetricsAsync(string deploymentId, CancellationToken ct = default)
    {
        if (!_deployments.TryGetValue(deploymentId, out var deployment))
            throw new InvalidOperationException($"Deployment {deploymentId} not found");

        // Return mock metrics
        var metrics = new DeploymentMetrics(
            deploymentId,
            deployment.Replicas,
            deployment.Replicas,
            45.5,
            60.2,
            125.3,
            45.8,
            0.02,
            15420,
            DateTime.UtcNow
        );

        return Task.FromResult(metrics);
    }
}

public sealed class LocalDatasetManager : IDatasetManager
{
    private readonly ConcurrentDictionary<string, Dataset> _datasets = new();
    private readonly ILogger<LocalDatasetManager> _logger;
    private readonly string _storagePath;

    public LocalDatasetManager(ILogger<LocalDatasetManager> logger, IOptions<AIPipelineOptions> options)
    {
        _logger = logger;
        _storagePath = Path.Combine(options.Value.StoragePath, "datasets");
        Directory.CreateDirectory(_storagePath);
        LoadFromDisk();
    }

    private void LoadFromDisk()
    {
        var indexFile = Path.Combine(_storagePath, "datasets-index.json");
        if (File.Exists(indexFile))
        {
            try
            {
                var json = File.ReadAllText(indexFile);
                var datasets = JsonSerializer.Deserialize<List<Dataset>>(json);
                if (datasets != null)
                {
                    foreach (var ds in datasets)
                    {
                        _datasets[GetKey(ds.DatasetId, ds.Version)] = ds;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load dataset index");
            }
        }
    }

    private void SaveToDisk()
    {
        try
        {
            var indexFile = Path.Combine(_storagePath, "datasets-index.json");
            var json = JsonSerializer.Serialize(_datasets.Values.ToList(), new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(indexFile, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save dataset index");
        }
    }

    private static string GetKey(string datasetId, string version) => $"{datasetId}@{version}";

    public Task<Dataset> RegisterDatasetAsync(RegisterDatasetRequest request, CancellationToken ct = default)
    {
        var key = GetKey(request.Name, request.Version);
        
        if (_datasets.ContainsKey(key))
        {
            throw new InvalidOperationException($"Dataset {request.Name} v{request.Version} already exists");
        }

        var dataset = new Dataset
        {
            DatasetId = request.Name,
            Name = request.Name,
            Version = request.Version,
            Description = request.Description,
            SourcePath = request.SourcePath,
            SizeBytes = 0,
            SampleCount = 0,
            Schema = request.Schema,
            Statistics = new DatasetStatistics(0, 0, 0, new()),
            Tags = request.Tags,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "system"
        };

        _datasets[key] = dataset;
        SaveToDisk();

        _logger.LogInformation("Registered dataset {DatasetId} v{Version}", request.Name, request.Version);
        return Task.FromResult(dataset);
    }

    public Task<Dataset?> GetDatasetAsync(string datasetId, string? version = null, CancellationToken ct = default)
    {
        if (version != null)
        {
            _datasets.TryGetValue(GetKey(datasetId, version), out var dataset);
            return Task.FromResult(dataset);
        }

        var matches = _datasets.Values
            .Where(d => d.DatasetId == datasetId)
            .OrderByDescending(d => d.Version)
            .FirstOrDefault();

        return Task.FromResult(matches);
    }

    public Task<IReadOnlyList<Dataset>> ListDatasetsAsync(DatasetQuery query, CancellationToken ct = default)
    {
        var results = _datasets.Values.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query.Name))
        {
            results = results.Where(d => d.Name.Contains(query.Name, StringComparison.OrdinalIgnoreCase));
        }

        if (query.Tags?.Length > 0)
        {
            results = results.Where(d => query.Tags!.All(t => d.Tags.ContainsKey(t)));
        }

        results = results.OrderByDescending(d => d.CreatedAt);

        var paged = results.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();
        return Task.FromResult<IReadOnlyList<Dataset>>(paged);
    }

    public Task<bool> DeleteDatasetAsync(string datasetId, string version, CancellationToken ct = default)
    {
        var removed = _datasets.TryRemove(GetKey(datasetId, version), out _);
        if (removed) SaveToDisk();
        return Task.FromResult(removed);
    }

    public Task<DatasetValidationResult> ValidateDatasetAsync(string datasetId, string version, CancellationToken ct = default)
    {
        var dataset = GetDatasetAsync(datasetId, version, ct).Result;
        if (dataset == null)
            return Task.FromResult(new DatasetValidationResult(false, 
                new[] { new ValidationError("", "Dataset not found") }, 
                Array.Empty<ValidationWarning>(), 
                new DatasetStatistics(0, 0, 0, new())));

        return Task.FromResult(new DatasetValidationResult(
            true,
            Array.Empty<ValidationError>(),
            Array.Empty<ValidationWarning>(),
            dataset.Statistics
        ));
    }

    public Task<DatasetStatistics> ComputeStatisticsAsync(string datasetId, string version, CancellationToken ct = default)
    {
        var dataset = GetDatasetAsync(datasetId, version, ct).Result;
        return Task.FromResult(dataset?.Statistics ?? new DatasetStatistics(0, 0, 0, new()));
    }
}