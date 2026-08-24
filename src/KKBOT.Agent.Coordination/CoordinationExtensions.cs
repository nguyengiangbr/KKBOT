// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using KKBOT.Agent.Coordination.Abstractions;
using KKBOT.Agent.Coordination.Locking;
using KKBOT.Agent.Coordination.Messaging;
using KKBOT.Agent.Coordination.Registry;
using KKBOT.Agent.Coordination.Services;
using KKBOT.Agent.Coordination.Strategies;
using KKBOT.Observability;

namespace KKBOT.Agent.Coordination;

public static class CoordinationExtensions
{
    public static IServiceCollection AddKkbotAgentCoordination(this IServiceCollection services, IConfiguration configuration)
    {
        // Options
        services.Configure<RedisAgentRegistryOptions>(configuration.GetSection("Coordination:Registry"));
        services.Configure<RedisMessageBusOptions>(configuration.GetSection("Coordination:MessageBus"));
        services.Configure<RedisDistributedLockOptions>(configuration.GetSection("Coordination:Lock"));
        services.Configure<CoordinationServiceOptions>(configuration.GetSection("Coordination:Service"));
        services.Configure<DefaultStrategyOptions>(configuration.GetSection("Coordination:Strategy"));

        // Redis connection
        var redisConnectionString = configuration.GetConnectionString("Redis") 
            ?? configuration["Coordination:Redis:ConnectionString"] 
            ?? "localhost:6379";

        services.AddSingleton<IConnectionMultiplexer>(sp => 
            ConnectionMultiplexer.Connect(redisConnectionString));

        // Core coordination services
        services.AddSingleton<IAgentRegistry, RedisAgentRegistry>();
        services.AddSingleton<IMessageBus, RedisMessageBus>();
        services.AddSingleton<IDistributedLock, RedisDistributedLock>();
        services.AddSingleton<ICoordinationService, CoordinationService>();
        services.AddSingleton<ICoordinationStrategy, DefaultCoordinationStrategy>();

        // Register metrics
        services.AddSingleton<IMetricsRegistry>(sp => sp.GetRequiredService<MetricsRegistry>());

        return services;
    }
}

/// <summary>
/// Background service for agent heartbeat and health monitoring.
/// </summary>
public sealed class AgentCoordinationHostedService : BackgroundService
{
    private readonly IAgentRegistry _agentRegistry;
    private readonly ICoordinationService _coordinationService;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<AgentCoordinationHostedService> _logger;
    private readonly AgentCoordinationHostedServiceOptions _options;
    private string? _agentId;

    public AgentCoordinationHostedService(
        IAgentRegistry agentRegistry,
        ICoordinationService coordinationService,
        IMessageBus messageBus,
        ILogger<AgentCoordinationHostedService> logger,
        IOptions<AgentCoordinationHostedServiceOptions> options)
    {
        _agentRegistry = agentRegistry;
        _coordinationService = coordinationService;
        _messageBus = messageBus;
        _logger = logger;
        _options = options.Value;
    }

    public void SetAgentId(string agentId)
    {
        _agentId = agentId;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrEmpty(_agentId))
        {
            _logger.LogWarning("Agent ID not set, coordination service will not register this agent");
            return;
        }

        // Register this agent
        var agentInfo = new AgentInfo
        {
            AgentId = _agentId,
            Name = _options.AgentName ?? _agentId,
            Type = _options.AgentType,
            Status = AgentStatus.Online,
            Capabilities = _options.Capabilities,
            Endpoint = _options.Endpoint,
            Metadata = _options.Metadata
        };

        await _agentRegistry.RegisterAgentAsync(agentInfo, stoppingToken);

        // Subscribe to relevant messages
        await _messageBus.SubscribeAsync(_agentId, new[]
        {
            MessageType.TaskRequest,
            MessageType.DelegateTask,
            MessageType.ConsensusPropose,
            MessageType.ConsensusVote,
            MessageType.LeaderElection,
            MessageType.StateSync
        }, HandleMessageAsync, stoppingToken);

        // Start heartbeat
        var heartbeatTimer = new PeriodicTimer(TimeSpan.FromSeconds(_options.HeartbeatIntervalSeconds));

        try
        {
            while (!stoppingToken.IsCancellationRequested && await heartbeatTimer.WaitForNextTickAsync(stoppingToken))
            {
                await _agentRegistry.UpdateAgentHeartbeatAsync(_agentId, stoppingToken);
                
                // Publish heartbeat
                await _messageBus.PublishAsync(new AgentMessage
                {
                    Type = MessageType.Heartbeat,
                    FromAgentId = _agentId,
                    Payload = JsonSerializer.Serialize(new AgentHeartbeat
                    {
                        AgentId = _agentId,
                        Status = AgentStatus.Online
                    })
                }, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        finally
        {
            // Unregister agent
            await _agentRegistry.UpdateAgentStatusAsync(_agentId, AgentStatus.Offline, stoppingToken);
            await _messageBus.UnsubscribeAsync(_agentId, stoppingToken);
        }
    }

    private async Task HandleMessageAsync(AgentMessage message, CancellationToken ct)
    {
        // Handle incoming coordination messages
        // This would be implemented based on specific agent logic
        await Task.CompletedTask;
    }
}

internal sealed record AgentHeartbeat
{
    public string AgentId { get; init; } = string.Empty;
    public AgentStatus Status { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Options for agent coordination hosted service.
/// </summary>
public sealed class AgentCoordinationHostedServiceOptions
{
    public string? AgentName { get; set; }
    public AgentType AgentType { get; set; } = AgentType.Planning;
    public AgentCapabilities Capabilities { get; set; } = new();
    public string Endpoint { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
    public int HeartbeatIntervalSeconds { get; set; } = 10;
}