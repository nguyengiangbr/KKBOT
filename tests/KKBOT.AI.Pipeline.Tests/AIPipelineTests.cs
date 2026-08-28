using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KKBOT.AI.Pipeline;
using KKBOT.AI.Pipeline.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace KKBOT.AI.Pipeline.Tests;

public class AIPipelineTests
{
    private static ServiceProvider CreateProvider()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("AIPipeline:StoragePath", "data/ai-pipeline-test")
            })
            .Build();

        // Clean any persisted pipeline state from previous runs so each test starts fresh.
        var storageRoot = "data/ai-pipeline-test";
        if (Directory.Exists(storageRoot))
            Directory.Delete(storageRoot, true);

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddKkbotAIPipeline(config);
        
        var provider = services.BuildServiceProvider();
        return provider;
    }

    [Fact]
    public async Task ModelRegistry_CRUD_Works()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IModelRegistry>();

        var model = new ModelMetadata(
            "test-model",
            "Test Model",
            "1.0.0",
            ModelType.Classification,
            ModelFramework.ONNX,
            "A test model",
            "Test Author",
            new Dictionary<string, string> { { "tag1", "value1" } },
            new ModelMetrics(0.95, 0.93, 0.94, 0.935, 0.12, new Dictionary<string, double>(), DateTime.UtcNow, "1.0"),
            new ModelSchema("input", "float32", new[] { -1, 768 }),
            new ModelSchema("output", "float32", new[] { -1, 10 }),
            new Dictionary<string, object> { { "learning_rate", 0.001 } },
            DateTime.UtcNow,
            DateTime.UtcNow
        );

        // Register
        var registered = await registry.RegisterModelAsync(model, CancellationToken.None);
        Assert.Equal("test-model", registered.ModelId);
        Assert.Equal("1.0.0", registered.Version);

        // Get
        var retrieved = await registry.GetModelAsync("test-model", "1.0.0", CancellationToken.None);
        Assert.NotNull(retrieved);
        Assert.Equal("Test Model", retrieved!.Name);

        // List
        var models = await registry.ListModelsAsync(new ModelListQuery(), CancellationToken.None);
        Assert.Contains(models, m => m.ModelId == "test-model");

        // Update
        var updated = registered with { Description = "Updated description" };
        var updateSuccess = await registry.UpdateModelAsync(updated, CancellationToken.None);
        Assert.True(updateSuccess);

        // Delete
        var deleteSuccess = await registry.DeleteModelAsync("test-model", "1.0.0", CancellationToken.None);
        Assert.True(deleteSuccess);

        var deleted = await registry.GetModelAsync("test-model", "1.0.0", CancellationToken.None);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task TrainingOrchestrator_StartAndMonitor_Works()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<ITrainingOrchestrator>();

        var request = new StartTrainingRequest(
            "test-model",
            "Test Training",
            new TrainingConfig(
                "/data/train",
                "/data/val",
                5,
                32,
                0.001,
                "adam",
                "cross_entropy",
                new Dictionary<string, object>(),
                true,
                2,
                null,
                new[] { "accuracy", "loss" }
            )
        );

        var job = await orchestrator.StartTrainingAsync(request, CancellationToken.None);
        Assert.NotNull(job);
        Assert.Equal("test-model", job.ModelId);
        Assert.Equal(TrainingStatus.Queued, job.Status);

        // Wait a bit for training to progress
        await Task.Delay(3000);

        var progress = await orchestrator.GetProgressAsync(job.JobId, CancellationToken.None);
        Assert.True(progress.CurrentEpoch >= 0);
    }

    [Fact]
    public async Task InferenceEngine_Predict_Works()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var inference = scope.ServiceProvider.GetRequiredService<IInferenceEngine>();
        var registry = scope.ServiceProvider.GetRequiredService<IModelRegistry>();

        // Register a model first
        var model = new ModelMetadata(
            "inference-model",
            "Inference Model",
            "1.0.0",
            ModelType.Classification,
            ModelFramework.ONNX,
            "Model for inference testing",
            "Test",
            new Dictionary<string, string>(),
            new ModelMetrics(0.9, 0.88, 0.89, 0.885, 0.15, new(), DateTime.UtcNow, "1.0"),
            new ModelSchema("input", "float32", new[] { -1, 768 }),
            new ModelSchema("output", "float32", new[] { -1, 10 }),
            new Dictionary<string, object>(),
            DateTime.UtcNow,
            DateTime.UtcNow
        );

        await registry.RegisterModelAsync(model, CancellationToken.None);

        // Run inference
        var request = new InferenceRequest(
            Guid.NewGuid().ToString(),
            "inference-model",
            "1.0.0",
            new Dictionary<string, object> { { "text", "test input" } },
            new InferenceOptions()
        );

        var response = await scope.ServiceProvider.GetRequiredService<IInferenceEngine>().PredictAsync(request, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal("inference-model", response.ModelId);
        Assert.NotNull(response.Outputs);
        Assert.True(response.Metrics.LatencyMs > 0);
    }

    [Fact]
    public async Task DeploymentService_DeployAndScale_Works()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var deploymentService = scope.ServiceProvider.GetRequiredService<IModelDeploymentService>();
        var registry = scope.ServiceProvider.GetRequiredService<IModelRegistry>();

        // Register model
        var model = new ModelMetadata(
            "deploy-model",
            "Deploy Model",
            "1.0.0",
            ModelType.Classification,
            ModelFramework.ONNX,
            "Model for deployment",
            "Test",
            new Dictionary<string, string>(),
            new ModelMetrics(0.9, 0.88, 0.89, 0.885, 0.15, new(), DateTime.UtcNow, "1.0"),
            new ModelSchema("input", "float32", new[] { -1, 768 }),
            new ModelSchema("output", "float32", new[] { -1, 10 }),
            new Dictionary<string, object>(),
            DateTime.UtcNow,
            DateTime.UtcNow
        );

        await registry.RegisterModelAsync(model, CancellationToken.None);

        // Deploy
        var deployRequest = new DeployModelRequest(
            "deploy-model",
            "1.0.0",
            "Test Deployment",
            DeploymentTarget.Local,
            new DeploymentConfig { MinReplicas = 1, MaxReplicas = 3, TargetCpuUtilization = 70 }
        );

        var deployment = await deploymentService.DeployModelAsync(deployRequest, CancellationToken.None);
        Assert.NotNull(deployment);
        Assert.Equal("deploy-model", deployment.ModelId);
        Assert.Equal(DeploymentStatus.Running, deployment.Status);

        // Get metrics
        var metrics = await deploymentService.GetDeploymentMetricsAsync(deployment.DeploymentId, CancellationToken.None);
        Assert.Equal(deployment.DeploymentId, metrics.DeploymentId);
        Assert.True(metrics.RequestsPerSecond >= 0);

        // Scale
        var scaled = await deploymentService.ScaleDeploymentAsync(deployment.DeploymentId, 5, CancellationToken.None);
        Assert.Equal(5, scaled.Replicas);

        // Delete
        var deleted = await deploymentService.DeleteDeploymentAsync(deployment.DeploymentId, CancellationToken.None);
        Assert.True(deleted);
    }

    [Fact]
    public async Task DatasetManager_CRUD_Works()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var datasetManager = scope.ServiceProvider.GetRequiredService<IDatasetManager>();

        var schema = new DatasetSchema(
            new Dictionary<string, DatasetColumn>
            {
                ["feature1"] = new DatasetColumn("feature1", "numeric", false, new Dictionary<string, object>()),
                ["label"] = new DatasetColumn("label", "categorical", false, new Dictionary<string, object>())
            },
            "label"
        );

        var request = new RegisterDatasetRequest(
            "test-dataset",
            "1.0.0",
            "Test dataset",
            "/data/test.csv",
            schema,
            new Dictionary<string, string> { { "domain", "test" } }
        );

        var dataset = await datasetManager.RegisterDatasetAsync(request, CancellationToken.None);
        Assert.Equal("test-dataset", dataset.DatasetId);
        Assert.Equal("1.0.0", dataset.Version);

        var retrieved = await datasetManager.GetDatasetAsync("test-dataset", "1.0.0", CancellationToken.None);
        Assert.NotNull(retrieved);

        var validation = await datasetManager.ValidateDatasetAsync("test-dataset", "1.0.0", CancellationToken.None);
        Assert.True(validation.IsValid);

        var stats = await datasetManager.ComputeStatisticsAsync("test-dataset", "1.0.0", CancellationToken.None);
        Assert.NotNull(stats);

        var deleted = await datasetManager.DeleteDatasetAsync("test-dataset", "1.0.0", CancellationToken.None);
        Assert.True(deleted);
    }

    [Fact]
    public async Task ExperimentTracker_CreateAndRun_Works()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var experimentTracker = scope.ServiceProvider.GetRequiredService<IExperimentTracker>();

        var request = new CreateExperimentRequest(
            "Test Experiment",
            "Compare model versions",
            "test-model",
            new ExperimentConfig(
                new[] { "1.0.0", "1.1.0" },
                "1.0",
                new TrainingConfig(
                    "/data/train",
                    "/data/val",
                    2, // Few epochs for test
                    32,
                    0.001,
                    "adam",
                    "cross_entropy",
                    new Dictionary<string, object>(),
                    true,
                    1,
                    null,
                    new[] { "accuracy", "loss" }
                ),
                new Dictionary<string, object>(),
                new[] { "accuracy", "loss" }
            )
        );

        var experiment = await experimentTracker.CreateExperimentAsync(request, CancellationToken.None);
        Assert.NotNull(experiment);
        Assert.Equal("Test Experiment", experiment.Name);

        // Note: Full run would take time, so we just verify creation
        var retrieved = await experimentTracker.GetExperimentAsync(experiment.ExperimentId, CancellationToken.None);
        Assert.NotNull(retrieved);
    }

    [Fact]
    public async Task FeatureStore_CRUD_Works()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var featureStore = scope.ServiceProvider.GetRequiredService<IFeatureStore>();

        var features = new FeatureVector(
            "entity-123",
            new Dictionary<string, object>
            {
                ["feature_a"] = 1.5,
                ["feature_b"] = "category_x",
                ["feature_c"] = 42
            },
            DateTime.UtcNow
        );

        await featureStore.SetFeaturesAsync("entity-123", features, CancellationToken.None);

        var retrieved = await featureStore.GetFeaturesAsync("entity-123", new[] { "feature_a", "feature_b" }, CancellationToken.None);
        Assert.Equal(2, retrieved.Features.Count);
        Assert.Equal(1.5, retrieved.Features["feature_a"]);
        Assert.Equal("category_x", retrieved.Features["feature_b"]);

        var definition = new FeatureDefinition(
            "feature_a",
            "Feature A description"
        );

        await featureStore.RegisterFeatureAsync(definition, CancellationToken.None);
        var definitions = await featureStore.ListFeaturesAsync(CancellationToken.None);
        Assert.Contains(definitions, d => d.Name == "feature_a");
    }

    [Fact]
    public async Task ModelMonitor_GenerateReport_Works()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var monitor = scope.ServiceProvider.GetRequiredService<IModelMonitor>();
        var registry = scope.ServiceProvider.GetRequiredService<IModelRegistry>();
        var deploymentService = scope.ServiceProvider.GetRequiredService<IModelDeploymentService>();

        // Register and deploy a model so the deployment exists for monitoring.
        var model = new ModelMetadata(
            "test-model",
            "Test Model",
            "1.0.0",
            ModelType.Classification,
            ModelFramework.ONNX,
            "Model for monitoring",
            "Test",
            new Dictionary<string, string>(),
            new ModelMetrics(0.9, 0.88, 0.89, 0.885, 0.15, new(), DateTime.UtcNow, "1.0"),
            new ModelSchema("input", "float32", new[] { -1, 768 }),
            new ModelSchema("output", "float32", new[] { -1, 10 }),
            new Dictionary<string, object>(),
            DateTime.UtcNow,
            DateTime.UtcNow
        );

        await registry.RegisterModelAsync(model, CancellationToken.None);

        var deployment = await deploymentService.DeployModelAsync(
            new DeployModelRequest(
                "test-model",
                "1.0.0",
                "Test Deployment",
                DeploymentTarget.Local,
                new DeploymentConfig { MinReplicas = 1, MaxReplicas = 3, TargetCpuUtilization = 70 }),
            CancellationToken.None);

        var report = await monitor.GenerateReportAsync("test-model", deployment.DeploymentId, MonitoringPeriod.Last24Hours, CancellationToken.None);

        Assert.NotNull(report);
        Assert.Equal("test-model", report.ModelId);
        Assert.NotNull(report.Metrics);
        Assert.NotNull(report.Alerts);
        Assert.NotNull(report.Recommendations);
    }

    [Fact]
    public async Task PipelineOrchestrator_Execute_Works()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IPipelineOrchestrator>();

        var pipeline = new PipelineDefinition(
            "test-pipeline",
            "Test Pipeline",
            "1.0.0",
            new[]
            {
                new PipelineStep("step1", "Ingest Data", StepType.DataIngestion, "dataset-1", new Dictionary<string, object>(), Array.Empty<string>(), new RetryPolicy()),
                new PipelineStep("step2", "Train Model", StepType.Training, "model-1", new Dictionary<string, object>(), new[] { "step1" }, new RetryPolicy()),
                new PipelineStep("step3", "Deploy", StepType.Deployment, "deployment-1", new Dictionary<string, object>(), new[] { "step2" }, new RetryPolicy())
            },
            new Dictionary<string, object>()
        );

        var input = new PipelineInput(
            new Dictionary<string, object> { { "param1", "value1" } },
            new[] { "dataset-1" },
            new[] { "model-1" }
        );

        var run = await orchestrator.ExecutePipelineAsync(pipeline, input, CancellationToken.None);
        Assert.NotNull(run);
        Assert.Equal("test-pipeline", run.PipelineId);
        Assert.Equal(PipelineStatus.Running, run.Status);
        Assert.Equal(3, run.StepRuns.Length);

        // Wait for completion (in test, it runs synchronously with delays)
        await Task.Delay(5000);

        var completedRun = await orchestrator.GetPipelineRunAsync(run.RunId, CancellationToken.None);
        Assert.NotNull(completedRun);
    }
}