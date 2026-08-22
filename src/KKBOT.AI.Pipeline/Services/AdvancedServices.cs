// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.AI.Pipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KKBOT.AI.Pipeline.Services;

public sealed class LocalExperimentTracker : IExperimentTracker
{
    private readonly ConcurrentDictionary<string, Experiment> _experiments = new();
    private readonly ILogger<LocalExperimentTracker> _logger;
    private readonly ITrainingOrchestrator _trainingOrchestrator;
    private readonly IModelRegistry _modelRegistry;

    public LocalExperimentTracker(
        ILogger<LocalExperimentTracker> logger,
        ITrainingOrchestrator trainingOrchestrator,
        IModelRegistry modelRegistry)
    {
        _logger = logger;
        _trainingOrchestrator = trainingOrchestrator;
        _modelRegistry = modelRegistry;
    }

    public async Task<Experiment> CreateExperimentAsync(CreateExperimentRequest request, CancellationToken ct = default)
    {
        var experiment = new Experiment
        {
            ExperimentId = Guid.NewGuid().ToString(),
            Name = request.Name,
            Description = request.Description,
            ModelId = request.ModelId,
            Config = request.Config,
            Status = ExperimentStatus.Created,
            Results = new ExperimentResults(new(), "", new(), ""),
            CreatedAt = DateTime.UtcNow
        };

        _experiments[experiment.ExperimentId] = experiment;
        _logger.LogInformation("Created experiment {ExperimentId} ({Name})", experiment.ExperimentId, experiment.Name);
        
        return experiment;
    }

    public Task<Experiment?> GetExperimentAsync(string experimentId, CancellationToken ct = default)
    {
        _experiments.TryGetValue(experimentId, out var experiment);
        return Task.FromResult(experiment);
    }

    public Task<IReadOnlyList<Experiment>> ListExperimentsAsync(ExperimentQuery query, CancellationToken ct = default)
    {
        var results = _experiments.Values.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query.ModelId))
            results = results.Where(e => e.ModelId == query.ModelId);

        if (query.Status.HasValue)
            results = results.Where(e => e.Status == query.Status.Value);

        if (query.FromDate.HasValue)
            results = results.Where(e => e.CreatedAt >= query.FromDate.Value);

        if (query.ToDate.HasValue)
            results = results.Where(e => e.CreatedAt <= query.ToDate.Value);

        results = results.OrderByDescending(e => e.CreatedAt);

        var paged = results.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();
        return Task.FromResult<IReadOnlyList<Experiment>>(paged);
    }

    public async Task<Experiment> RunExperimentAsync(string experimentId, CancellationToken ct = default)
    {
        if (!_experiments.TryGetValue(experimentId, out var experiment))
            throw new InvalidOperationException($"Experiment {experimentId} not found");

        experiment = experiment with 
        { 
            Status = ExperimentStatus.Running,
            StartedAt = DateTime.UtcNow
        };
        _experiments[experimentId] = experiment;

        _logger.LogInformation("Running experiment {ExperimentId}", experimentId);

        try
        {
            var metrics = new Dictionary<string, ModelMetrics>();
            var bestMetric = double.MinValue;
            string bestVersion = "";

            foreach (var version in experiment.Config.ModelVersions)
            {
                var trainingRequest = new StartTrainingRequest(
                    experiment.ModelId,
                    $"{experiment.Name} - {version}",
                    experiment.Config.TrainingConfig,
                    experiment.ExperimentId
                );

                var job = await _trainingOrchestrator.StartTrainingAsync(trainingRequest, ct);
                
                // Wait for completion (in real implementation, this would be async with polling)
                var completedJob = await WaitForCompletion(job.JobId, ct);
                
                if (completedJob != null && completedJob.Status == TrainingStatus.Completed)
                {
                    var modelMetrics = new ModelMetrics(
                        completedJob.Progress.CurrentMetrics.GetValueOrDefault("accuracy", 0),
                        completedJob.Progress.CurrentMetrics.GetValueOrDefault("precision", 0),
                        completedJob.Progress.CurrentMetrics.GetValueOrDefault("recall", 0),
                        completedJob.Progress.CurrentMetrics.GetValueOrDefault("f1", 0),
                        completedJob.Progress.CurrentLoss,
                        completedJob.Progress.CurrentMetrics,
                        DateTime.UtcNow,
                        experiment.Config.DatasetVersion
                    );

                    metrics[version] = modelMetrics;

                    if (modelMetrics.Accuracy > bestMetric)
                    {
                        bestMetric = modelMetrics.Accuracy;
                        bestVersion = version;
                    }
                }
            }

            var results = new ExperimentResults(
                metrics,
                bestVersion,
                new Dictionary<string, object>
                {
                    ["comparison"] = metrics.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value.Accuracy)
                },
                $"Best model version: {bestVersion} with accuracy {bestMetric:F4}"
            );

            experiment = experiment with
            {
                Status = ExperimentStatus.Completed,
                Results = results,
                CompletedAt = DateTime.UtcNow
            };

            _experiments[experimentId] = experiment;
            _logger.LogInformation("Experiment {ExperimentId} completed. Best version: {BestVersion}", experimentId, bestVersion);

            return experiment;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Experiment {ExperimentId} failed", experimentId);
            
            experiment = experiment with
            {
                Status = ExperimentStatus.Failed,
                CompletedAt = DateTime.UtcNow,
                Results = experiment.Results with { Conclusion = $"Failed: {ex.Message}" }
            };
            _experiments[experimentId] = experiment;
            throw;
        }
    }

    private async Task<TrainingJob?> WaitForCompletion(string jobId, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var job = await _trainingOrchestrator.GetTrainingJobAsync(jobId, ct);
            if (job == null) return null;

            if (job.Status == TrainingStatus.Completed || job.Status == TrainingStatus.Failed || job.Status == TrainingStatus.Cancelled)
            {
                return job;
            }

            await Task.Delay(5000, ct);
        }
        return null;
    }

    public async Task<ExperimentComparison> CompareExperimentsAsync(string[] experimentIds, CancellationToken ct = default)
    {
        var experiments = new List<Experiment>();
        
        foreach (var id in experimentIds)
        {
            var exp = await GetExperimentAsync(id, ct);
            if (exp != null) experiments.Add(exp);
        }

        var allMetrics = experiments
            .Where(e => e.Status == ExperimentStatus.Completed)
            .SelectMany(e => e.Results.ModelMetrics)
            .ToDictionary(kvp => $"{e.Name}_{kvp.Key}", kvp => kvp.Value);

        var best = allMetrics.OrderByDescending(kvp => kvp.Value.Accuracy).FirstOrDefault();

        return new ExperimentComparison(
            experimentIds,
            allMetrics,
            best.Key ?? "",
            new Dictionary<string, object>
            {
                ["accuracy_comparison"] = allMetrics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Accuracy),
                ["f1_comparison"] = allMetrics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.F1Score)
            },
            $"Best performing: {best.Key} with accuracy {best.Value.Accuracy:F4}"
        );
    }
}

public sealed class LocalFeatureStore : IFeatureStore
{
    private readonly ConcurrentDictionary<string, FeatureVector> _features = new();
    private readonly ConcurrentDictionary<string, FeatureDefinition> _definitions = new();
    private readonly ILogger<LocalFeatureStore> _logger;

    public LocalFeatureStore(ILogger<LocalFeatureStore> logger)
    {
        _logger = logger;
    }

    public Task<FeatureVector> GetFeaturesAsync(string entityId, string[] featureNames, CancellationToken ct = default)
    {
        var key = $"entity:{entityId}";
        
        if (_features.TryGetValue(key, out var vector))
        {
            var filtered = featureNames
                .Where(f => vector.Features.ContainsKey(f))
                .ToDictionary(f => f, f => vector.Features[f]);
            
            return Task.FromResult(new FeatureVector(entityId, filtered, vector.Timestamp));
        }

        return Task.FromResult(new FeatureVector(entityId, new Dictionary<string, object>(), DateTime.MinValue));
    }

    public Task<bool> SetFeaturesAsync(string entityId, FeatureVector features, CancellationToken ct = default)
    {
        var key = $"entity:{entityId}";
        _features[key] = features with { Timestamp = DateTime.UtcNow };
        return Task.FromResult(true);
    }

    public Task<FeatureVector> GetLatestFeaturesAsync(string entityId, CancellationToken ct = default)
    {
        var key = $"entity:{entityId}";
        
        if (_features.TryGetValue(key, out var vector))
        {
            return Task.FromResult(vector);
        }

        return Task.FromResult(new FeatureVector(entityId, new Dictionary<string, object>(), DateTime.MinValue));
    }

    public Task<IReadOnlyList<FeatureDefinition>> ListFeaturesAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<FeatureDefinition>>(_definitions.Values.ToList());
    }

    public Task<FeatureDefinition> RegisterFeatureAsync(FeatureDefinition feature, CancellationToken ct = default)
    {
        _definitions[feature.Name] = feature;
        return Task.FromResult(feature);
    }
}

public sealed class LocalModelMonitor : IModelMonitor
{
    private readonly ILogger<LocalModelMonitor> _logger;
    private readonly IModelDeploymentService _deploymentService;

    public LocalModelMonitor(ILogger<LocalModelMonitor> logger, IModelDeploymentService deploymentService)
    {
        _logger = logger;
        _deploymentService = deploymentService;
    }

    public async Task<MonitoringReport> GenerateReportAsync(string modelId, string deploymentId, MonitoringPeriod period, CancellationToken ct = default)
    {
        var metrics = await _deploymentService.GetDeploymentMetricsAsync(deploymentId, ct);
        var dataDrift = await DetectDataDriftAsync(modelId, deploymentId, "baseline-dataset", ct);
        var conceptDrift = await DetectConceptDriftAsync(modelId, deploymentId, ct);
        var alerts = await CheckAlertsAsync(modelId, deploymentId, ct);

        var recommendations = GenerateRecommendations(metrics, dataDrift, conceptDrift, alerts);

        return new MonitoringReport(
            modelId,
            deploymentId,
            period,
            DateTime.UtcNow,
            metrics,
            dataDrift,
            conceptDrift,
            alerts,
            recommendations
        );
    }

    public async Task<DataDriftReport> DetectDataDriftAsync(string modelId, string deploymentId, string baselineDatasetId, CancellationToken ct = default)
    {
        // Simulate data drift detection
        await Task.Delay(100, ct);

        var featureDriftScores = new Dictionary<string, double>
        {
            ["feature_1"] = 0.05,
            ["feature_2"] = 0.12,
            ["feature_3"] = 0.03
        };

        var driftedFeatures = featureDriftScores
            .Where(kvp => kvp.Value > 0.1)
            .ToDictionary(
                kvp => kvp.Key,
                kvp => new DriftDetails(
                    kvp.Value,
                    "psi",
                    0.001,
                    new { mean = 0.5, std = 0.2 },
                    new { mean = 0.65, std = 0.25 }
                )
            );

        return new DataDriftReport(
            modelId,
            deploymentId,
            baselineDatasetId,
            DateTime.UtcNow,
            featureDriftScores.Values.DefaultIfEmpty(0).Max(),
            featureDriftScores,
            driftedFeatures,
            driftedFeatures.Count > 0
        );
    }

    public async Task<ConceptDriftReport> DetectConceptDriftAsync(string modelId, string deploymentId, CancellationToken ct = default)
    {
        await Task.Delay(100, ct);

        // Simulate concept drift detection using performance degradation
        var metrics = await _deploymentService.GetDeploymentMetricsAsync(deploymentId, ct);
        
        return new ConceptDriftReport(
            modelId,
            deploymentId,
            DateTime.UtcNow,
            metrics.ErrorRate > 0.05 ? 0.15 : 0.02,
            metrics.ErrorRate > 0.05 ? 0.03 : 0.0,
            "performance_monitoring",
            metrics.ErrorRate > 0.05
        );
    }

    public async Task<PerformanceAlert[]> CheckAlertsAsync(string modelId, string deploymentId, CancellationToken ct = default)
    {
        var metrics = await _deploymentService.GetDeploymentMetricsAsync(deploymentId, ct);
        var alerts = new List<PerformanceAlert>();

        if (metrics.ErrorRate > 0.1)
        {
            alerts.Add(new PerformanceAlert(
                Guid.NewGuid().ToString(),
                modelId,
                deploymentId,
                AlertSeverity.Critical,
                $"Error rate ({metrics.ErrorRate:P2}) exceeds critical threshold (10%)",
                "error_rate",
                metrics.ErrorRate,
                0.1,
                DateTime.UtcNow
            ));
        }
        else if (metrics.ErrorRate > 0.05)
        {
            alerts.Add(new PerformanceAlert(
                Guid.NewGuid().ToString(),
                modelId,
                deploymentId,
                AlertSeverity.Warning,
                $"Error rate ({metrics.ErrorRate:P2}) exceeds warning threshold (5%)",
                "error_rate",
                metrics.ErrorRate,
                0.05,
                DateTime.UtcNow
            ));
        }

        if (metrics.AvgLatencyMs > 500)
        {
            alerts.Add(new PerformanceAlert(
                Guid.NewGuid().ToString(),
                modelId,
                deploymentId,
                AlertSeverity.Warning,
                $"Average latency ({metrics.AvgLatencyMs:F1}ms) exceeds threshold (500ms)",
                "avg_latency_ms",
                metrics.AvgLatencyMs,
                500,
                DateTime.UtcNow
            ));
        }

        if (metrics.CpuUtilization > 90)
        {
            alerts.Add(new PerformanceAlert(
                Guid.NewGuid().ToString(),
                modelId,
                deploymentId,
                AlertSeverity.Critical,
                $"CPU utilization ({metrics.CpuUtilization:F1}%) exceeds critical threshold (90%)",
                "cpu_utilization",
                metrics.CpuUtilization,
                90,
                DateTime.UtcNow
            ));
        }

        return alerts.ToArray();
    }

    private Recommendation[] GenerateRecommendations(
        DeploymentMetrics metrics,
        DataDriftReport? dataDrift,
        ConceptDriftReport? conceptDrift,
        PerformanceAlert[] alerts)
    {
        var recommendations = new List<Recommendation>();

        if (dataDrift?.IsSignificant == true)
        {
            recommendations.Add(new Recommendation(
                "retrain",
                "Significant data drift detected. Consider retraining with recent data.",
                1,
                new Dictionary<string, object> { ["drift_score"] = dataDrift.OverallDriftScore }
            ));
        }

        if (conceptDrift?.IsSignificant == true)
        {
            recommendations.Add(new Recommendation(
                "retrain",
                "Concept drift detected. Model performance has degraded.",
                1,
                new Dictionary<string, object> { ["performance_drop"] = conceptDrift.PerformanceDrop }
            ));
        }

        if (metrics.ErrorRate > 0.05)
        {
            recommendations.Add(new Recommendation(
                "investigate",
                "High error rate detected. Investigate failing predictions.",
                2,
                new Dictionary<string, object> { ["error_rate"] = metrics.ErrorRate }
            ));
        }

        if (metrics.AvgLatencyMs > 200)
        {
            recommendations.Add(new Recommendation(
                "scale",
                "High latency detected. Consider scaling up or optimizing model.",
                2,
                new Dictionary<string, object> { ["avg_latency_ms"] = metrics.AvgLatencyMs }
            ));
        }

        if (metrics.CpuUtilization > 80)
        {
            recommendations.Add(new Recommendation(
                "scale",
                "High CPU utilization. Consider adding replicas.",
                2,
                new Dictionary<string, object> { ["cpu_utilization"] = metrics.CpuUtilization }
            ));
        }

        return recommendations.ToArray();
    }
}

public sealed class LocalPipelineOrchestrator : IPipelineOrchestrator
{
    private readonly ConcurrentDictionary<string, PipelineRun> _runs = new();
    private readonly ILogger<LocalPipelineOrchestrator> _logger;

    public LocalPipelineOrchestrator(ILogger<LocalPipelineOrchestrator> logger)
    {
        _logger = logger;
    }

    public async Task<PipelineRun> ExecutePipelineAsync(PipelineDefinition pipeline, PipelineInput input, CancellationToken ct = default)
    {
        var run = new PipelineRun
        {
            RunId = Guid.NewGuid().ToString(),
            PipelineId = pipeline.PipelineId,
            PipelineVersion = pipeline.Version,
            Status = PipelineStatus.Running,
            StepRuns = pipeline.Steps.Select(s => new PipelineStepRun(
                s.StepId,
                s.Name,
                StepStatus.Pending,
                DateTime.MinValue,
                null,
                new Dictionary<string, object>(),
                new Dictionary<string, object>(),
                null,
                0
            )).ToArray(),
            StartedAt = DateTime.UtcNow
        };

        _runs[run.RunId] = run;
        _logger.LogInformation("Started pipeline run {RunId} for pipeline {PipelineId}", run.RunId, pipeline.PipelineId);

        _ = Task.Run(() => ExecutePipelineStepsAsync(run, pipeline, input, ct), ct);

        return run;
    }

    private async Task ExecutePipelineStepsAsync(PipelineRun run, PipelineDefinition pipeline, PipelineInput input, CancellationToken ct)
    {
        var stepRuns = run.StepRuns.ToDictionary(s => s.StepId);
        var completedSteps = new HashSet<string>();
        var stepOutputs = new Dictionary<string, Dictionary<string, object>>();

        try
        {
            var orderedSteps = TopologicalSort(pipeline.Steps);

            foreach (var step in orderedSteps)
            {
                ct.ThrowIfCancellationRequested();

                var stepRun = stepRuns[step.StepId];
                
                // Update to running
                stepRun = stepRun with { Status = StepStatus.Running, StartedAt = DateTime.UtcNow };
                stepRuns[step.StepId] = stepRun;
                run = run with { StepRuns = stepRuns.Values.ToArray() };
                _runs[run.RunId] = run;

                _logger.LogInformation("Executing pipeline step {StepId} ({Name})", step.StepId, step.Name);

                try
                {
                    // Prepare inputs from dependencies
                    var inputs = new Dictionary<string, object>(input.Parameters);
                    foreach (var dep in step.DependsOn)
                    {
                        if (stepOutputs.TryGetValue(dep, out var depOutputs))
                        {
                            foreach (var kvp in depOutputs)
                            {
                                inputs[$"{dep}.{kvp.Key}"] = kvp.Value;
                            }
                        }
                    }

                    // Execute step based on type
                    var outputs = await ExecuteStepAsync(step, inputs, ct);

                    stepRun = stepRun with
                    {
                        Status = StepStatus.Completed,
                        CompletedAt = DateTime.UtcNow,
                        Inputs = inputs,
                        Outputs = outputs
                    };

                    stepOutputs[step.StepId] = outputs;
                    completedSteps.Add(step.StepId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Pipeline step {StepId} failed", step.StepId);
                    
                    stepRun = stepRun with
                    {
                        Status = StepStatus.Failed,
                        CompletedAt = DateTime.UtcNow,
                        ErrorMessage = ex.Message
                    };

                    // Check retry policy
                    if (stepRun.RetryCount < step.RetryPolicy.MaxRetries)
                    {
                        stepRun = stepRun with { Status = StepStatus.Retrying, RetryCount = stepRun.RetryCount + 1 };
                        _logger.LogWarning("Retrying step {StepId} (attempt {RetryCount}/{MaxRetries})", 
                            step.StepId, stepRun.RetryCount, step.RetryPolicy.MaxRetries);
                        
                        await Task.Delay(step.RetryPolicy.RetryDelaySeconds * 1000, ct);
                        continue; // Retry
                    }

                    throw;
                }

                stepRuns[step.StepId] = stepRun;
                run = run with { StepRuns = stepRuns.Values.ToArray() };
                _runs[run.RunId] = run;
            }

            run = run with 
            { 
                Status = PipelineStatus.Completed,
                CompletedAt = DateTime.UtcNow,
                StepRuns = stepRuns.Values.ToArray(),
                Outputs = stepOutputs.Values.LastOrDefault() ?? new()
            };
            _runs[run.RunId] = run;

            _logger.LogInformation("Pipeline run {RunId} completed successfully", run.RunId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pipeline run {RunId} failed", run.RunId);
            
            run = run with
            {
                Status = PipelineStatus.Failed,
                CompletedAt = DateTime.UtcNow,
                ErrorMessage = ex.Message
            };
            _runs[run.RunId] = run;
        }
    }

    private async Task<Dictionary<string, object>> ExecuteStepAsync(PipelineStep step, Dictionary<string, object> inputs, CancellationToken ct)
    {
        await Task.Delay(1000, ct); // Simulate work

        return step.Type switch
        {
            StepType.DataIngestion => new Dictionary<string, object> { ["dataset_path"] = "/data/raw/dataset.csv", ["row_count"] = 10000 },
            StepType.DataValidation => new Dictionary<string, object> { ["valid"] = true, ["issues"] = Array.Empty<string>() },
            StepType.DataTransformation => new Dictionary<string, object> { ["transformed_path"] = "/data/processed/dataset.parquet", ["features"] = 150 },
            StepType.FeatureEngineering => new Dictionary<string, object> { ["feature_count"] = 200, ["feature_names"] = Enumerable.Range(1, 200).Select(i => $"feat_{i}").ToArray() },
            StepType.Training => new Dictionary<string, object> { ["model_path"] = "/models/model_v1.onnx", ["metrics"] = new { accuracy = 0.92, loss = 0.15 } },
            StepType.Evaluation => new Dictionary<string, object> { ["accuracy"] = 0.91, ["f1"] = 0.89, ["precision"] = 0.90, ["recall"] = 0.88 },
            StepType.ModelRegistration => new Dictionary<string, object> { ["model_id"] = "model_123", ["version"] = "1.0.0" },
            StepType.Deployment => new Dictionary<string, object> { ["deployment_id"] = "deploy_456", ["endpoint"] = "https://api.example.com/predict" },
            StepType.Monitoring => new Dictionary<string, object> { ["monitoring_enabled"] = true, ["alerts_configured"] = 5 },
            _ => new Dictionary<string, object> { ["result"] = "completed" }
        };
    }

    private static PipelineStep[] TopologicalSort(PipelineStep[] steps)
    {
        var graph = steps.ToDictionary(s => s.StepId, s => s.DependsOn.ToList());
        var result = new List<PipelineStep>();
        var visited = new HashSet<string>();
        var temp = new HashSet<string>();

        void Visit(string id)
        {
            if (temp.Contains(id))
                throw new InvalidOperationException($"Cycle detected in pipeline at step {id}");
            
            if (!visited.Contains(id))
            {
                temp.Add(id);
                foreach (var dep in graph[id])
                    Visit(dep);
                temp.Remove(id);
                visited.Add(id);
                result.Add(steps.First(s => s.StepId == id));
            }
        }

        foreach (var step in steps)
            Visit(step.StepId);

        return result.ToArray();
    }

    public Task<PipelineRun?> GetPipelineRunAsync(string runId, CancellationToken ct = default)
    {
        _runs.TryGetValue(runId, out var run);
        return Task.FromResult(run);
    }

    public Task<IReadOnlyList<PipelineRun>> ListPipelineRunsAsync(PipelineRunQuery query, CancellationToken ct = default)
    {
        var results = _runs.Values.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query.PipelineId))
            results = results.Where(r => r.PipelineId == query.PipelineId);

        if (query.Status.HasValue)
            results = results.Where(r => r.Status == query.Status.Value);

        if (query.FromDate.HasValue)
            results = results.Where(r => r.StartedAt >= query.FromDate.Value);

        if (query.ToDate.HasValue)
            results = results.Where(r => r.StartedAt <= query.ToDate.Value);

        results = results.OrderByDescending(r => r.StartedAt);

        var paged = results.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();
        return Task.FromResult<IReadOnlyList<PipelineRun>>(paged);
    }

    public Task<bool> CancelPipelineRunAsync(string runId, CancellationToken ct = default)
    {
        if (_runs.TryGetValue(runId, out var run) && run.Status == PipelineStatus.Running)
        {
            run = run with { Status = PipelineStatus.Cancelled, CompletedAt = DateTime.UtcNow };
            _runs[runId] = run;
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }
}