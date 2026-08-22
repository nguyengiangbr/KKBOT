// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using KKBOT.AI.Pipeline.Services;
using KKBOT.AI.Pipeline;

namespace KKBOT.AI.Pipeline;

public sealed class AIPipelineOptions
{
    public string StoragePath { get; set; } = "data/ai-pipeline";
    public string ModelRegistryPath => Path.Combine(StoragePath, "models");
    public string DatasetPath => Path.Combine(StoragePath, "datasets");
    public string ExperimentPath => Path.Combine(StoragePath, "experiments");
    public int MaxConcurrentTrainingJobs { get; set; } = 2;
    public int DefaultTrainingTimeoutMinutes { get; set; } = 60;
    public bool EnableGpu { get; set; } = false;
    public string DefaultDeploymentTarget { get; set; } = "Local";
}

public static class AIPipelineExtensions
{
    public static IServiceCollection AddKkbotAIPipeline(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AIPipelineOptions>(configuration.GetSection("AIPipeline"));

        // Core services
        services.AddSingleton<IModelRegistry, LocalModelRegistry>();
        services.AddSingleton<ITrainingOrchestrator, LocalTrainingOrchestrator>();
        services.AddSingleton<IInferenceEngine, LocalInferenceEngine>();
        services.AddSingleton<IModelDeploymentService, LocalModelDeploymentService>();
        services.AddSingleton<IDatasetManager, LocalDatasetManager>();
        services.AddSingleton<IExperimentTracker, LocalExperimentTracker>();
        services.AddSingleton<IFeatureStore, LocalFeatureStore>();
        services.AddSingleton<IModelMonitor, LocalModelMonitor>();
        services.AddSingleton<IPipelineOrchestrator, LocalPipelineOrchestrator>();

        // HttpClient for external model repositories
        services.AddHttpClient("model-registry", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5);
        });

        return services;
    }
}