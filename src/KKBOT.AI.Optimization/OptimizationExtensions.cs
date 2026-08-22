// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ML;
using KKBOT.AI.Optimization.Services;
using KKBOT.AI.Optimization.Abstractions;

namespace KKBOT.AI.Optimization;

public static class OptimizationExtensions
{
    public static IServiceCollection AddKkbotOptimization(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OptimizationOptions>(configuration.GetSection("Optimization"));

        // Core services
        services.AddSingleton<IModelRegistry, ModelRegistry>();
        services.AddSingleton<IEdgeDeviceManager, EdgeDeviceManager>();

        // Core optimization services
        services.AddSingleton<IModelConverter, ModelConverter>();
        services.AddSingleton<IQuantizationService, QuantizationService>();
        services.AddSingleton<IModelBenchmarkService, BenchmarkService>();
        services.AddSingleton<IOptimizationPipeline, OptimizationPipelineService>();
        services.AddSingleton<IModelOptimizer, ModelOptimizer>();

        // Deployment services
        services.AddSingleton<IEdgeDeploymentService, EdgeDeploymentService>();

        // ML.NET
        services.AddSingleton<MLContext>();

        // HTTP client for vulnerability scanning
        services.AddHttpClient("vulnerability-db", client =>
        {
            client.BaseAddress = new System.Uri("https://api.osv.dev/v1/");
            client.Timeout = TimeSpan.FromMinutes(5);
        });

        return services;
    }
}

// Pipeline service implementation
namespace KKBOT.AI.Optimization.Services;

public interface IOptimizationPipelineService
{
    Task<OptimizationResult> ExecutePipelineAsync(OptimizationPipeline pipeline, CancellationToken ct = default);
    Task<OptimizationResult?> GetPipelineResultAsync(string pipelineId, CancellationToken ct = default);
}

public sealed class OptimizationPipelineService : IOptimizationPipelineService
{
    private readonly IModelOptimizer _optimizer;
    private readonly ILogger<OptimizationPipelineService> _logger;
    private readonly ConcurrentDictionary<string, OptimizationResult> _pipelineResults = new();

    public OptimizationPipelineService(IModelOptimizer optimizer, ILogger<OptimizationPipelineService> logger)
    {
        _optimizer = optimizer;
        _logger = logger;
    }

    public async Task<OptimizationResult> ExecutePipelineAsync(OptimizationPipeline pipeline, CancellationToken ct = default)
    {
        var pipelineId = pipeline.PipelineId;
        _logger.LogInformation("Starting optimization pipeline {PipelineId} ({Name})", pipelineId, pipeline.Name);

        var config = new OptimizationConfig
        {
            OptimizationId = pipelineId,
            ModelId = pipeline.GlobalOptions.GetValueOrDefault("modelId", "unknown"),
            TargetPlatform = pipeline.GlobalOptions.GetValueOrDefault("targetPlatform", TargetPlatform.Cpu),
            PrecisionMode = pipeline.GlobalOptions.GetValueOrDefault("precisionMode", PrecisionMode.Fp16),
            EnabledStages = pipeline.Steps.Select(s => s.Stage).ToList(),
            Quantization = pipeline.GlobalOptions.GetValueOrDefault("quantization", new QuantizationConfig()),
            Calibration = pipeline.GlobalOptions.GetValueOrDefault("calibration", new CalibrationConfig()),
            Benchmark = pipeline.GlobalOptions.GetValueOrDefault("benchmark", new BenchmarkConfig()),
            EnabledStages = pipeline.Steps.Select(s => s.Stage).ToList()
        };

        var result = await _optimizer.OptimizeAsync(config, CancellationToken.None);
        _pipelineResults[pipelineId] = result;

        return result;
    }

    public Task<OptimizationResult?> GetPipelineResultAsync(string pipelineId, CancellationToken ct = default)
    {
        _pipelineResults.TryGetValue(pipelineId, out var result);
        return Task.FromResult(result);
    }
}

public sealed class OptimizationOptions
{
    public int DefaultTimeoutMinutes { get; set; } = 60;
    public int MaxConcurrentOptimizations { get; set; } = 4;
    public bool FailOnAccuracyDrop { get; set; } = true;
    public double MaxAccuracyDropThreshold { get; set; } = 0.02;
    public double MaxLatencyIncreaseThreshold { get; set; } = 1.1;
    public string ModelsPath { get; set; } = "models";
    public string ArtifactsPath { get; set; } = "artifacts";
}