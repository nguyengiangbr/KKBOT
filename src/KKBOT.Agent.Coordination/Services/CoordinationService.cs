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
/// Coordination service for managing agent groups and coordination logic.
/// </summary>
public sealed class CoordinationService : ICoordinationService, IAsyncDisposable
{
    private readonly ILogger<CoordinationService> _logger;
    private readonly CoordinationServiceOptions _options;
    private readonly IAgentRegistry _agentRegistry;
    private readonly IMessageBus _messageBus;
    private readonly IDistributedLock _distributedLock;
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private readonly ISubscriber _subscriber;
    private readonly IMetricsRegistry _metrics;
    private readonly ConcurrentDictionary<string, CoordinationGroup> _groups = new();
    private readonly ICoordinationStrategy _defaultStrategy;
    private bool _disposed;

    private const string GroupKeyPrefix = "kkbot:groups:";
    private const string GroupMembersPrefix = "kkbot:groups:members:";
    private const string GroupTasksPrefix = "kkbot:groups:tasks:";
    private const string ConsensusChannelPrefix = "kkbot:consensus:";
    private const string ElectionChannelPrefix = "kkbot:election:";

    public CoordinationService(
        ILogger<CoordinationService> logger,
        IOptions<CoordinationServiceOptions> options,
        IAgentRegistry agentRegistry,
        IMessageBus messageBus,
        IDistributedLock distributedLock,
        IConnectionMultiplexer redis,
        IMetricsRegistry metrics,
        ICoordinationStrategy defaultStrategy)
    {
        _logger = logger;
        _options = options.Value;
        _agentRegistry = agentRegistry;
        _messageBus = messageBus;
        _distributedLock = distributedLock;
        _redis = redis;
        _db = redis.GetDatabase();
        _subscriber = redis.GetSubscriber();
        _metrics = metrics;
        _defaultStrategy = defaultStrategy;

        // Subscribe to coordination channels
        _ = _subscriber.SubscribeAsync("kkbot:coordination:events", OnCoordinationEventReceived);
    }

    public async Task<CoordinationGroup> CreateGroupAsync(CoordinationGroup group, CancellationToken ct = default)
    {
        using var activity = KkbotActivitySources.Coordination.StartActivity("coordination.create_group");
        activity?.SetTag("group.id", group.GroupId);

        var key = $"{GroupKeyPrefix}{group.GroupId}";
        var groupJson = JsonSerializer.Serialize(group);

        await _db.StringSetAsync(key, groupJson);
        await _db.SetAddAsync("kkbot:groups:index", group.GroupId);

        _groups[group.GroupId] = group;

        _logger.LogInformation("Created coordination group {GroupId} ({Name}) with mode {Mode}", 
            group.GroupId, group.Name, group.Mode);

        return group;
    }

    public async Task<bool> JoinGroupAsync(string groupId, string agentId, CancellationToken ct = default)
    {
        var group = await GetGroupAsync(groupId, ct);
        if (group == null)
        {
            _logger.LogWarning("Attempted to join non-existent group {GroupId}", groupId);
            return false;
        }

        // Check if agent exists
        var agent = await _agentRegistry.GetAgentAsync(agentId, ct);
        if (agent == null)
        {
            _logger.LogWarning("Attempted to join group {GroupId} with non-existent agent {AgentId}", groupId, agentId);
            return false;
        }

        // Check policy requirements
        if (!MeetsGroupRequirements(agent, group))
        {
            _logger.LogWarning("Agent {AgentId} does not meet requirements for group {GroupId}", agentId, groupId);
            return false;
        }

        // Add to group
        var membersKey = $"{GroupMembersPrefix}{groupId}";
        await _db.SetAddAsync(membersKey, agentId);

        // Update local group
        if (_groups.TryGetValue(groupId, out var localGroup))
        {
            localGroup.MemberAgentIds.Add(agentId);
        }

        // Notify group members
        await NotifyGroupMembersAsync(groupId, new AgentMessage
        {
            Type = MessageType.AgentInfo,
            FromAgentId = agentId,
            PayloadType = nameof(AgentInfo),
            Payload = JsonSerializer.Serialize(await _agentRegistry.GetAgentAsync(agentId, ct)),
            Headers = new Dictionary<string, string> { ["groupId"] = groupId, ["action"] = "join" }
        }, ct);

        _logger.LogInformation("Agent {AgentId} joined group {GroupId}", agentId, groupId);
        return true;
    }

    public async Task<bool> LeaveGroupAsync(string groupId, string agentId, CancellationToken ct = default)
    {
        var group = await GetGroupAsync(groupId, ct);
        if (group == null) return false;

        var membersKey = $"{GroupMembersPrefix}{groupId}";
        await _db.SetRemoveAsync(membersKey, agentId);

        if (_groups.TryGetValue(groupId, out var localGroup))
        {
            localGroup.MemberAgentIds.Remove(agentId);
        }

        // If leader leaves, trigger new election
        if (group.LeaderId == agentId && group.Mode == GroupCoordinationMode.Hierarchical)
        {
            _ = ElectLeaderAsync(groupId, ct);
        }

        // Notify group members
        await NotifyGroupMembersAsync(groupId, new AgentMessage
        {
            Type = MessageType.AgentInfo,
            FromAgentId = agentId,
            Payload = agentId,
            Headers = new Dictionary<string, string> { ["groupId"] = groupId, ["action"] = "leave" }
        }, ct);

        _logger.LogInformation("Agent {AgentId} left group {GroupId}", agentId, groupId);
        return true;
    }

    public async Task<CoordinationGroup?> GetGroupAsync(string groupId, CancellationToken ct = default)
    {
        if (_groups.TryGetValue(groupId, out var cached))
        {
            // Refresh members from Redis
            var membersKey = $"{GroupMembersPrefix}{groupId}";
            var memberIds = await _db.SetMembersAsync(membersKey);
            cached.MemberAgentIds = memberIds.Select(m => m!).ToHashSet();
            return cached;
        }

        var key = $"{GroupKeyPrefix}{groupId}";
        var groupJson = await _db.StringGetAsync(key);

        if (groupJson.IsNullOrEmpty) return null;

        var group = JsonSerializer.Deserialize<CoordinationGroup>(groupJson!);
        if (group != null)
        {
            var membersKey = $"{GroupMembersPrefix}{groupId}";
            var memberIds = await _db.SetMembersAsync(membersKey);
            group.MemberAgentIds = memberIds.Select(m => m!).ToHashSet();
            _groups[groupId] = group;
        }

        return group;
    }

    public async Task<IReadOnlyList<CoordinationGroup>> GetGroupsForAgentAsync(string agentId, CancellationToken ct = default)
    {
        var allGroups = new List<CoordinationGroup>();
        
        var groupIds = await _db.SetMembersAsync("kkbot:groups:index");
        foreach (var id in groupIds)
        {
            var group = await GetGroupAsync(id!, ct);
            if (group != null && group.MemberAgentIds.Contains(agentId))
            {
                allGroups.Add(group);
            }
        }

        return allGroups.AsReadOnly();
    }

    public async Task<CoordinationDecision> CoordinateAsync(
        string groupId, 
        CoordinationTrigger trigger, 
        CancellationToken ct = default)
    {
        using var activity = KkbotActivitySources.Coordination.StartActivity("coordination.coordinate");
        activity?.SetTag("group.id", groupId);
        activity?.SetTag("trigger", trigger.ToString());

        var group = await GetGroupAsync(groupId, ct);
        if (group == null)
        {
            return new CoordinationDecision
            {
                Type = DecisionType.NoAction,
                Reasoning = "Group not found"
            };
        }

        // Get available agents
        var availableAgents = new List<AgentInfo>();
        foreach (var memberId in group.MemberAgentIds)
        {
            var agent = await _agentRegistry.GetAgentAsync(memberId, ct);
            if (agent != null && agent.Status == AgentStatus.Online)
            {
                availableAgents.Add(agent);
            }
        }

        // Get pending tasks for this group
        var pendingTasks = await GetGroupPendingTasksAsync(groupId, ct);

        var context = new CoordinationContext
        {
            GroupId = groupId,
            Trigger = trigger,
            AvailableAgents = availableAgents,
            PendingTasks = pendingTasks,
            GroupPolicy = group
        };

        // Use strategy to make decision
        var decision = await _defaultStrategy.DecideAsync(context, ct);

        // Execute decision actions
        await ExecuteDecisionActionsAsync(groupId, decision, ct);

        _metrics.IncrementHttpRequests(); // Reuse metric
        return decision;
    }

    public async Task<ConsensusResult> RunConsensusAsync(
        string groupId, 
        ConsensusProposal proposal, 
        CancellationToken ct = default)
    {
        using var activity = KkbotActivitySources.Coordination.StartActivity("coordination.run_consensus");
        activity?.SetTag("group.id", groupId);
        activity?.SetTag("proposal.id", proposal.ProposalId);

        var group = await GetGroupAsync(groupId, ct);
        if (group == null)
        {
            return new ConsensusResult { Outcome = ConsensusOutcome.InsufficientVoters };
        }

        // Get eligible voters
        var voters = new List<string>();
        foreach (var memberId in group.MemberAgentIds)
        {
            var agent = await _agentRegistry.GetAgentAsync(memberId, ct);
            if (agent != null && agent.Status == AgentStatus.Online)
            {
                voters.Add(memberId);
            }
        }

        if (voters.Count < proposal.Threshold.MinVoters)
        {
            return new ConsensusResult
            {
                ProposalId = proposal.ProposalId,
                Outcome = ConsensusOutcome.InsufficientVoters
            };
        }

        // Send consensus proposal to all voters
        var proposalMessage = new AgentMessage
        {
            Type = MessageType.ConsensusPropose,
            FromAgentId = "coordinator",
            ToAgentId = string.Empty, // Will be sent individually
            CorrelationId = proposal.ProposalId,
            PayloadType = nameof(ConsensusProposal),
            Payload = JsonSerializer.Serialize(proposal),
            Priority = 10
        };

        // Send to each voter
        foreach (var voterId in voters)
        {
            var msg = proposalMessage with { ToAgentId = voterId };
            await _messageBus.PublishAsync(msg, ct);
        }

        // Wait for votes
        var votes = new ConcurrentDictionary<string, ConsensusVote>();
        var votingCts = new CancellationTokenSource(proposal.VotingPeriod);

        // Subscribe to vote responses
        var responseChannel = $"{ConsensusChannelPrefix}{proposal.ProposalId}:responses";
        await _subscriber.SubscribeAsync(responseChannel, (ch, msg) => 
        {
            var vote = JsonSerializer.Deserialize<ConsensusVote>(msg!);
            if (vote != null && voters.Contains(vote.VoterId))
            {
                votes[vote.VoterId] = vote;
            }
        });

        // Wait for voting period or all votes received
        try
        {
            await Task.Delay(proposal.VotingPeriod, votingCts.Token);
        }
        catch (OperationCanceledException) { }

        await _subscriber.UnsubscribeAsync(responseChannel);

        // Tally votes
        var approveCount = votes.Values.Count(v => v.Approve);
        var rejectCount = votes.Values.Count(v => !v.Approve);
        var abstainCount = voters.Count - votes.Count;

        var outcome = approveCount >= rejectCount && 
                     (double)approveCount / voters.Count >= proposal.Threshold.RequiredRatio
            ? ConsensusOutcome.Approved
            : ConsensusOutcome.Rejected;

        var result = new ConsensusResult
        {
            ProposalId = proposal.ProposalId,
            Outcome = outcome,
            ApproveCount = approveCount,
            RejectCount = rejectCount,
            AbstainCount = abstainCount,
            DecidedData = proposal.Data
        };

        // Broadcast result
        await _messageBus.BroadcastAsync(new AgentMessage
        {
            Type = MessageType.ConsensusResult,
            FromAgentId = "coordinator",
            CorrelationId = proposal.ProposalId,
            PayloadType = nameof(ConsensusResult),
            Payload = JsonSerializer.Serialize(result)
        }, ct);

        _logger.LogInformation("Consensus {ProposalId} completed: {Outcome} ({Approve}/{Total})", 
            proposal.ProposalId, outcome, approveCount, voters.Count);

        return result;
    }

    public async Task<LeaderAnnouncement> ElectLeaderAsync(string groupId, CancellationToken ct = default)
    {
        using var activity = KkbotActivitySources.Coordination.StartActivity("coordination.elect_leader");
        activity?.SetTag("group.id", groupId);

        var group = await GetGroupAsync(groupId, ct);
        if (group == null)
        {
            return new LeaderAnnouncement { ElectionId = "failed" };
        }

        // Get eligible candidates
        var candidates = new List<(string AgentId, int Priority)>();
        foreach (var memberId in group.MemberAgentIds)
        {
            var agent = await _agentRegistry.GetAgentAsync(memberId, ct);
            if (agent != null && agent.Status == AgentStatus.Online)
            {
                var priority = CalculateLeaderPriority(agent);
                candidates.Add((memberId, priority));
            }
        }

        if (!candidates.Any())
        {
            return new LeaderAnnouncement { ElectionId = "no_candidates" };
        }

        // Sort by priority (highest first)
        candidates.Sort((a, b) => b.Priority.CompareTo(a.Priority));

        var leaderId = candidates.First().AgentId;
        var electionId = Guid.NewGuid().ToString("N");

        // Update group
        if (_groups.TryGetValue(groupId, out var localGroup))
        {
            localGroup.LeaderId = leaderId;
        }

        var announcement = new LeaderAnnouncement
        {
            ElectionId = electionId,
            LeaderId = leaderId,
            Term = 1, // In real implementation, increment term
            Metadata = new Dictionary<string, object>
            {
                ["candidates"] = candidates.Select(c => new { c.AgentId, c.Priority }).ToArray()
            }
        };

        // Notify group
        await NotifyGroupMembersAsync(groupId, new AgentMessage
        {
            Type = MessageType.LeaderAnnounce,
            FromAgentId = "coordinator",
            PayloadType = nameof(LeaderAnnouncement),
            Payload = JsonSerializer.Serialize(announcement),
            Headers = new Dictionary<string, string> { ["groupId"] = groupId }
        }, ct);

        _logger.LogInformation("Elected leader {LeaderId} for group {GroupId} (priority: {Priority})", 
            leaderId, groupId, candidates.First().Priority);

        return announcement;
    }

    public async Task SyncStateAsync(string groupId, StateSyncPayload state, CancellationToken ct = default)
    {
        // Broadcast state sync to group members
        await _messageBus.BroadcastAsync(new AgentMessage
        {
            Type = MessageType.StateSync,
            FromAgentId = "coordinator",
            PayloadType = nameof(StateSyncPayload),
            Payload = JsonSerializer.Serialize(state),
            Headers = new Dictionary<string, string> { ["groupId"] = groupId }
        }, ct);
    }

    private async Task ExecuteDecisionActionsAsync(string groupId, CoordinationDecision decision, CancellationToken ct)
    {
        foreach (var action in decision.Actions)
        {
            var agentId = action.Key;
            var actionData = action.Value;

            // Send task assignment or other action to agent
            await _messageBus.PublishAsync(new AgentMessage
            {
                Type = MessageType.DelegateTask,
                FromAgentId = "coordinator",
                ToAgentId = agentId,
                PayloadType = actionData.GetType().Name,
                Payload = JsonSerializer.Serialize(actionData),
                Headers = new Dictionary<string, string> { ["groupId"] = groupId }
            }, ct);
        }
    }

    private async Task NotifyGroupMembersAsync(string groupId, AgentMessage message, CancellationToken ct)
    {
        var group = await GetGroupAsync(groupId, ct);
        if (group == null) return;

        foreach (var memberId in group.MemberAgentIds)
        {
            var msg = message with { ToAgentId = memberId };
            await _messageBus.PublishAsync(msg, ct);
        }
    }

    private async Task<IReadOnlyList<AgentTask>> GetGroupPendingTasksAsync(string groupId, CancellationToken ct)
    {
        var tasksKey = $"{GroupTasksPrefix}{groupId}";
        var taskJsons = await _db.ListRangeAsync(tasksKey);
        
        var tasks = new List<AgentTask>();
        foreach (var json in taskJsons)
        {
            var task = JsonSerializer.Deserialize<AgentTask>(json!);
            if (task != null && task.Status == TaskStatus.Queued)
            {
                tasks.Add(task);
            }
        }

        return tasks.AsReadOnly();
    }

    private bool MeetsGroupRequirements(AgentInfo agent, CoordinationGroup group)
    {
        foreach (var requiredCap in group.Policy.RequiredCapabilities)
        {
            if (!agent.Capabilities.Skills.Contains(requiredCap) &&
                !agent.Capabilities.Tags.Contains(requiredCap))
            {
                return false;
            }
        }
        return true;
    }

    private int CalculateLeaderPriority(AgentInfo agent)
    {
        int priority = 0;

        // Base priority from capabilities
        priority += agent.Capabilities.Skills.Count * 5;
        priority += agent.Capabilities.Tags.Count * 2;

        // Resource capacity
        priority += (int)(agent.Capabilities.Capacity.CpuCores * 10);
        priority += (int)(agent.Capabilities.Capacity.MemoryBytes / (1024 * 1024 * 1024)); // GB

        // Online time / stability
        var uptime = DateTimeOffset.UtcNow - agent.LastHeartbeat;
        priority += (int)Math.Min(uptime.TotalHours, 100);

        return priority;
    }

    private async Task OnCoordinationEventReceived(RedisChannel channel, RedisValue message)
    {
        try
        {
            var coordEvent = JsonSerializer.Deserialize<CoordinationEvent>(message!);
            if (coordEvent == null) return;

            switch (coordEvent.Type)
            {
                case "agent_failed":
                    await HandleAgentFailedAsync(coordEvent.AgentId, ct: CancellationToken.None);
                    break;
                case "task_completed":
                    await HandleTaskCompletedAsync(coordEvent.TaskId, ct: CancellationToken.None);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process coordination event");
        }
    }

    private async Task HandleAgentFailedAsync(string agentId, CancellationToken ct)
    {
        // Find groups this agent was in
        var groupIds = await _db.SetMembersAsync("kkbot:groups:index");
        foreach (var id in groupIds)
        {
            var group = await GetGroupAsync(id!, ct);
            if (group?.MemberAgentIds.Contains(agentId) == true)
            {
                // Remove from group
                await LeaveGroupAsync(group.GroupId, agentId, ct);

                // If leader failed, elect new one
                if (group.LeaderId == agentId && group.Mode == GroupCoordinationMode.Hierarchical)
                {
                    await ElectLeaderAsync(group.GroupId, ct);
                }
            }
        }
    }

    private async Task HandleTaskCompletedAsync(string taskId, CancellationToken ct)
    {
        // In real implementation, update task status and trigger re-coordination
        _logger.LogDebug("Task {TaskId} completed", taskId);
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
/// Coordination event for internal communication.
/// </summary>
internal sealed record CoordinationEvent
{
    public string Type { get; init; } = string.Empty;
    public string AgentId { get; init; } = string.Empty;
    public string TaskId { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Options for coordination service.
/// </summary>
public sealed class CoordinationServiceOptions
{
    public TimeSpan DefaultTaskTimeout { get; set; } = TimeSpan.FromMinutes(5);
    public int MaxConcurrentCoordination { get; set; } = 10;
    public bool EnableAutoLeaderElection { get; set; } = true;
}