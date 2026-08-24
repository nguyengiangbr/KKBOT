// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using KKBOT.Agent.Coordination.Abstractions;
using KKBOT.Observability;
using KKBOT.Observability.Tracing;

namespace KKBOT.Agent.Coordination;

/// <summary>
/// Redis-backed agent registry for distributed agent discovery.
/// </summary>
public sealed class RedisAgentRegistry : IAgentRegistry, IAsyncDisposable
{
    private readonly ILogger<RedisAgentRegistry> _logger;
    private readonly RedisAgentRegistryOptions _options;
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private readonly ISubscriber _subscriber;
    private readonly ConcurrentDictionary<string, AgentInfo> _localCache = new();
    private readonly IMetricsRegistry _metrics;
    private bool _disposed;

    private const string AgentKeyPrefix = "kkbot:agents:";
    private const string AgentIndexKey = "kkbot:agents:index";
    private const string CapabilityIndexPrefix = "kkbot:agents:capability:";
    private const string TagIndexPrefix = "kkbot:agents:tag:";
    private const string HeartbeatChannel = "kkbot:agents:heartbeat";

    public RedisAgentRegistry(
        ILogger<RedisAgentRegistry> logger,
        IOptions<RedisAgentRegistryOptions> options,
        IConnectionMultiplexer redis,
        IMetricsRegistry metrics)
    {
        _logger = logger;
        _options = options.Value;
        _redis = redis;
        _db = redis.GetDatabase();
        _subscriber = redis.GetSubscriber();
        _metrics = metrics;

        // Subscribe to heartbeat channel for real-time updates
        _ = _subscriber.SubscribeAsync(HeartbeatChannel, OnHeartbeatReceived);
    }

    public async Task RegisterAgentAsync(AgentInfo agent, CancellationToken ct = default)
    {
        using var activity = KkbotActivitySources.Coordination.StartActivity("registry.register_agent");
        activity?.SetTag("agent.id", agent.AgentId);

        var key = $"{AgentKeyPrefix}{agent.AgentId}";
        var agentJson = JsonSerializer.Serialize(agent);

        // Store agent data
        await _db.StringSetAsync(key, agentJson, TimeSpan.FromMinutes(_options.AgentTtlMinutes));

        // Add to index
        await _db.SetAddAsync(AgentIndexKey, agent.AgentId);

        // Index by capabilities
        foreach (var skill in agent.Capabilities.Skills)
        {
            await _db.SetAddAsync($"{CapabilityIndexPrefix}{skill}", agent.AgentId);
        }

        // Index by tags
        foreach (var tag in agent.Capabilities.Tags)
        {
            await _db.SetAddAsync($"{TagIndexPrefix}{tag}", agent.AgentId);
        }

        // Update local cache
        _localCache[agent.AgentId] = agent;

        // Publish registration event
        await PublishAgentEventAsync(new AgentMessage
        {
            Type = MessageType.Register,
            FromAgentId = agent.AgentId,
            PayloadType = nameof(AgentInfo),
            Payload = JsonSerializer.Serialize(agent)
        }, ct);

        _metrics.IncrementHttpRequests(); // Reuse metric
        _logger.LogInformation("Registered agent {AgentId} ({Name})", agent.AgentId, agent.Name);
    }

    public async Task UnregisterAgentAsync(string agentId, CancellationToken ct = default)
    {
        using var activity = KkbotActivitySources.Coordination.StartActivity("registry.unregister_agent");
        activity?.SetTag("agent.id", agentId);

        var key = $"{AgentKeyPrefix}{agentId}";
        var agentJson = await _db.StringGetAsync(key);

        if (!agentJson.IsNullOrEmpty)
        {
            var agent = JsonSerializer.Deserialize<AgentInfo>(agentJson!);

            // Remove from capability indexes
            if (agent != null)
            {
                foreach (var skill in agent.Capabilities.Skills)
                {
                    await _db.SetRemoveAsync($"{CapabilityIndexPrefix}{skill}", agentId);
                }

                foreach (var tag in agent.Capabilities.Tags)
                {
                    await _db.SetRemoveAsync($"{TagIndexPrefix}{tag}", agentId);
                }
            }
        }

        // Remove from index and data
        await _db.KeyDeleteAsync(key);
        await _db.SetRemoveAsync(AgentIndexKey, agentId);

        // Remove from local cache
        _localCache.TryRemove(agentId, out _);

        // Publish unregistration event
        await PublishAgentEventAsync(new AgentMessage
        {
            Type = MessageType.Unregister,
            FromAgentId = agentId,
            Payload = agentId
        }, ct);

        _logger.LogInformation("Unregistered agent {AgentId}", agentId);
    }

    public async Task<AgentInfo?> GetAgentAsync(string agentId, CancellationToken ct = default)
    {
        // Check local cache first
        if (_localCache.TryGetValue(agentId, out var cached))
        {
            return cached;
        }

        var key = $"{AgentKeyPrefix}{agentId}";
        var agentJson = await _db.StringGetAsync(key);

        if (agentJson.IsNullOrEmpty)
            return null;

        var agent = JsonSerializer.Deserialize<AgentInfo>(agentJson!);
        if (agent != null)
        {
            _localCache[agentId] = agent;
        }

        return agent;
    }

    public async Task<IReadOnlyList<AgentInfo>> GetAllAgentsAsync(CancellationToken ct = default)
    {
        var agentIds = await _db.SetMembersAsync(AgentIndexKey);
        var agents = new List<AgentInfo>();

        foreach (var id in agentIds)
        {
            var agent = await GetAgentAsync(id!, ct);
            if (agent != null)
            {
                agents.Add(agent);
            }
        }

        return agents.AsReadOnly();
    }

    public async Task<IReadOnlyList<AgentInfo>> GetAgentsByCapabilityAsync(string capability, CancellationToken ct = default)
    {
        var agentIds = await _db.SetMembersAsync($"{CapabilityIndexPrefix}{capability}");
        var agents = new List<AgentInfo>();

        foreach (var id in agentIds)
        {
            var agent = await GetAgentAsync(id!, ct);
            if (agent != null)
            {
                agents.Add(agent);
            }
        }

        return agents.AsReadOnly();
    }

    public async Task<IReadOnlyList<AgentInfo>> GetAgentsByTagAsync(string tag, CancellationToken ct = default)
    {
        var agentIds = await _db.SetMembersAsync($"{TagIndexPrefix}{tag}");
        var agents = new List<AgentInfo>();

        foreach (var id in agentIds)
        {
            var agent = await GetAgentAsync(id!, ct);
            if (agent != null)
            {
                agents.Add(agent);
            }
        }

        return agents.AsReadOnly();
    }

    public async Task<AgentInfo?> FindBestAgentForTaskAsync(AgentTask task, CancellationToken ct = default)
    {
        // Get agents with required capabilities
        var candidateIds = new HashSet<string>();

        // Start with agents having the required skill
        if (!string.IsNullOrEmpty(task.RequiredCapabilities.FirstOrDefault()))
        {
            var skill = task.RequiredCapabilities.First();
            var skillAgents = await GetAgentsByCapabilityAsync(skill, ct);
            foreach (var agent in skillAgents)
            {
                candidateIds.Add(agent.AgentId);
            }
        }
        else
        {
            // No specific skill required, get all online agents
            var allAgents = await GetAllAgentsAsync(ct);
            foreach (var agent in allAgents.Where(a => a.Status == AgentStatus.Online))
            {
                candidateIds.Add(agent.AgentId);
            }
        }

        // Filter by other required capabilities
        foreach (var capability in task.RequiredCapabilities.Skip(1))
        {
            var capAgents = await GetAgentsByCapabilityAsync(capability, ct);
            var capIds = new HashSet<string>(capAgents.Select(a => a.AgentId));
            candidateIds.IntersectWith(capIds);
        }

        // Filter by tags if specified
        if (task.RequiredCapabilities.Any(c => c.StartsWith("tag:")))
        {
            foreach (var tagCap in task.RequiredCapabilities.Where(c => c.StartsWith("tag:")))
            {
                var tag = tagCap.Substring(4);
                var tagAgents = await GetAgentsByTagAsync(tag, ct);
                var tagIds = new HashSet<string>(tagAgents.Select(a => a.AgentId));
                candidateIds.IntersectWith(tagIds);
            }
        }

        // Score candidates
        var candidates = new List<(AgentInfo Agent, double Score)>();

        foreach (var id in candidateIds)
        {
            var agent = await GetAgentAsync(id, ct);
            if (agent == null || agent.Status != AgentStatus.Online)
                continue;

            // Check capacity
            if (agent.Capabilities.MaxConcurrentTasks > 0)
            {
                // In real implementation, check current load
            }

            // Calculate score based on:
            // - Priority match
            // - Resource availability
            // - Affinity (preferred agent)
            var score = CalculateAgentScore(agent, task);
            candidates.Add((agent, score));
        }

        // Return highest scored agent
        return candidates.OrderByDescending(c => c.Score).FirstOrDefault().Agent;
    }

    private double CalculateAgentScore(AgentInfo agent, AgentTask task)
    {
        double score = 0;

        // Preferred agent gets bonus
        if (!string.IsNullOrEmpty(task.RequiredCapabilities.FirstOrDefault()) &&
            task.RequiredCapabilities.Contains(agent.AgentId))
        {
            score += 100;
        }

        // Tag affinity
        foreach (var tag in task.RequiredCapabilities.Where(c => c.StartsWith("tag:")))
        {
            if (agent.Capabilities.Tags.Contains(tag.Substring(4)))
                score += 10;
        }

        // Resource availability (simplified)
        score += agent.Capabilities.Capacity.CpuCores * 0.1;

        // Prefer agents with matching tags
        foreach (var tag in agent.Capabilities.Tags)
        {
            if (task.RequiredCapabilities.Contains($"tag:{tag}"))
                score += 5;
        }

        return score;
    }

    public async Task UpdateAgentStatusAsync(string agentId, AgentStatus status, CancellationToken ct = default)
    {
        var agent = await GetAgentAsync(agentId, ct);
        if (agent != null)
        {
            var updated = agent with { Status = status };
            await RegisterAgentAsync(updated, ct);
        }
    }

    public async Task UpdateAgentHeartbeatAsync(string agentId, CancellationToken ct = default)
    {
        var agent = await GetAgentAsync(agentId, ct);
        if (agent != null)
        {
            var updated = agent with { LastHeartbeat = DateTimeOffset.UtcNow };
            // Update with short TTL for heartbeat
            var key = $"{AgentKeyPrefix}{agentId}";
            var agentJson = JsonSerializer.Serialize(updated);
            await _db.StringSetAsync(key, agentJson, TimeSpan.FromMinutes(_options.HeartbeatTtlMinutes));
            _localCache[agentId] = updated;
        }
    }

    private async Task OnHeartbeatReceived(RedisChannel channel, RedisValue message)
    {
        try
        {
            var heartbeat = JsonSerializer.Deserialize<AgentHeartbeat>(message!);
            if (heartbeat != null)
            {
                await UpdateAgentHeartbeatAsync(heartbeat.AgentId, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to process heartbeat");
        }
    }

    private async Task PublishAgentEventAsync(AgentMessage message, CancellationToken ct)
    {
        var channel = $"kkbot:agents:events:{message.Type}";
        await _subscriber.PublishAsync(channel, JsonSerializer.Serialize(message));
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await _subscriber.UnsubscribeAllAsync();
        _redis.Dispose();
    }
}

/// <summary>
/// Heartbeat message for agent liveness.
/// </summary>
internal sealed record AgentHeartbeat
{
    public string AgentId { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public AgentStatus Status { get; init; }
}

/// <summary>
/// Options for Redis agent registry.
/// </summary>
public sealed class RedisAgentRegistryOptions
{
    public int AgentTtlMinutes { get; set; } = 10;
    public int HeartbeatTtlMinutes { get; set; } = 2;
    public string RedisConnectionString { get; set; } = "localhost:6379";
}