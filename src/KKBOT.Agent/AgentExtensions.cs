// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using KKBOT.Agent.Abstractions;
using KKBOT.Agent.Core;
using KKBOT.Agent.Execution;
using KKBOT.Agent.Memory;
using KKBOT.Agent.Planning;
using KKBOT.Agent.Safety;
using KKBOT.Observability;

namespace KKBOT.Agent;

public static class AgentExtensions
{
    public static IServiceCollection AddKkbotAgent(this IServiceCollection services, IConfiguration configuration)
    {
        // Options
        services.Configure<GameAgentOptions>(configuration.GetSection("GameAgent"));
        services.Configure<LlmPlannerOptions>(configuration.GetSection("GameAgent:Planner"));
        services.Configure<ToolExecutorOptions>(configuration.GetSection("GameAgent:Executor"));
        services.Configure<SafetyGuardrailsOptions>(configuration.GetSection("GameAgent:Safety"));

        // Core services
        services.AddSingleton<IAgentPlanner, LlmAgentPlanner>();
        services.AddSingleton<IToolExecutor, KkbotToolExecutor>();
        services.AddSingleton<IAgentExecutor, AgentExecutor>();
        services.AddSingleton<IAgentMemory, EfCoreAgentMemory>();
        services.AddSingleton<ISafetyGuardrails, SafetyGuardrails>();
        services.AddSingleton<IAgentFactory, AgentFactory>();
        
        // Agent implementations
        services.AddTransient<GameAgent>();
        
        // Register built-in tools
        services.AddTransient<IAiService, KKBOT.AI.Ollama.OllamaService>();
        
        // Add HTTP client for Ollama
        services.AddHttpClient("Ollama", client =>
        {
            var baseUrl = configuration["AI:Ollama:BaseUrl"] ?? "http://localhost:11434";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromMinutes(5);
        });

        return services;
    }
}

/// <summary>
/// Factory for creating agents.
/// </summary>
public sealed class AgentFactory : IAgentFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AgentFactory> _logger;

    public AgentFactory(IServiceProvider serviceProvider, ILogger<AgentFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<IGameAgent> CreateAgentAsync(AgentType type, AgentConfiguration config, CancellationToken cancellationToken)
    {
        var agentId = Guid.NewGuid().ToString("N");
        var name = $"agent-{agentId[..8]}";
        
        var agent = type switch
        {
            AgentType.Planning => CreatePlanningAgent(agentId, name, config),
            AgentType.Reactive => CreateReactiveAgent(agentId, name, config),
            AgentType.Hybrid => CreateHybridAgent(agentId, name, config),
            _ => throw new NotSupportedException($"Agent type {type} not supported")
        };

        await agent.ConfigureAsync(config);
        _logger.LogInformation("Created agent {AgentId} of type {Type}", agentId, type);
        
        return agent;
    }

    public IReadOnlyList<AgentType> GetSupportedTypes() => new[]
    {
        AgentType.Planning,
        AgentType.Reactive,
        AgentType.Hybrid
    };

    private GameAgent CreatePlanningAgent(string agentId, string name, AgentConfiguration config)
    {
        return _serviceProvider.GetRequiredService<GameAgent>();
    }

    private GameAgent CreateReactiveAgent(string agentId, string name, AgentConfiguration config)
    {
        // Reactive agent would use a simpler planner
        return _serviceProvider.GetRequiredService<GameAgent>();
    }

    private GameAgent CreateHybridAgent(string agentId, string name, AgentConfiguration config)
    {
        // Hybrid agent combines planning and reactive
        return _serviceProvider.GetRequiredService<GameAgent>();
    }
}