// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using KKBOT.Agent.Abstractions;

namespace KKBOT.Agent.Coordination.Abstractions;

/// <summary>
/// Represents an agent in the coordination system.
/// </summary>
public sealed record AgentInfo
{
    public string AgentId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public AgentType Type { get; init; }
    public AgentStatus Status { get; init; } = AgentStatus.Offline;
    public AgentCapabilities Capabilities { get; init; } = new();
    public Dictionary<string, object> Metadata { get; init; } = new();
    public DateTimeOffset LastHeartbeat { get; init; } = DateTimeOffset.UtcNow;
    public string Endpoint { get; init; } = string.Empty; // gRPC endpoint
}

public enum AgentStatus
{
    Offline,
    Online,
    Busy,
    Degraded,
    Maintenance
}

/// <summary>
/// Agent capabilities for coordination.
/// </summary>
public sealed record AgentCapabilities
{
    public HashSet<string> Skills { get; init; } = new();
    public HashSet<string> SupportedTaskTypes { get; init; } = new();
    public ResourceCapacity Capacity { get; init; } = new();
    public HashSet<string> Tags { get; init; } = new(); // e.g., "region:us-east", "gpu:true"
    public int MaxConcurrentTasks { get; init; } = 1;
}

public sealed record ResourceCapacity
{
    public double CpuCores { get; init; }
    public long MemoryBytes { get; init; }
    public int GpuCount { get; init; }
    public long StorageBytes { get; init; }
}

/// <summary>
/// Message types for agent communication.
/// </summary>
public enum MessageType
{
    // Control messages
    Heartbeat,
    Register,
    Unregister,
    AgentInfo,
    
    // Task messages
    TaskRequest,
    TaskResponse,
    TaskProgress,
    TaskCancel,
    
    // Coordination messages
    DelegateTask,
    DelegateResponse,
    ConsensusPropose,
    ConsensusVote,
    ConsensusResult,
    LeaderElection,
    LeaderAnnounce,
    
    // State sync
    StateSync,
    StateDelta,
    SnapshotRequest,
    SnapshotResponse,
    
    // Events
    EventPublish,
    EventSubscribe,
    EventUnsubscribe,
    
    // Error
    Error,
    Ack,
    Nak
}

/// <summary>
/// Base message envelope for all inter-agent communication.
/// </summary>
public sealed record AgentMessage
{
    public string MessageId { get; init; } = Guid.NewGuid().ToString("N");
    public MessageType Type { get; init; }
    public string FromAgentId { get; init; } = string.Empty;
    public string ToAgentId { get; init; } = string.Empty; // Empty = broadcast
    public string CorrelationId { get; init; } = string.Empty;
    public string PayloadType { get; init; } = string.Empty;
    public string Payload { get; init; } = string.Empty; // JSON serialized
    public Dictionary<string, string> Headers { get; init; } = new();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public TimeSpan? Ttl { get; init; }
    public int Priority { get; init; } = 0; // Higher = more urgent
}

/// <summary>
/// Task delegation message payload.
/// </summary>
public sealed record TaskDelegationPayload
{
    public string TaskId { get; init; } = Guid.NewGuid().ToString("N");
    public string TaskType { get; init; } = string.Empty;
    public Dictionary<string, object> Parameters { get; init; } = new();
    public TaskConstraints Constraints { get; init; } = new();
    public string? RequiredSkill { get; init; }
    public string? PreferredAgentId { get; init; }
    public Dictionary<string, object> Context { get; init; } = new();
}

public sealed record TaskConstraints
{
    public TimeSpan? Timeout { get; init; }
    public ResourceRequirements? Resources { get; init; }
    public int MaxRetries { get; init; } = 2;
    public bool RequireConfirmation { get; init; } = false;
}

public sealed record ResourceRequirements
{
    public double MinCpuCores { get; init; }
    public long MinMemoryBytes { get; init; }
    public int RequiredGpuCount { get; init; }
    public string[] RequiredTags { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Task response payload.
/// </summary>
public sealed record TaskResponsePayload
{
    public string TaskId { get; init; } = string.Empty;
    public bool Accepted { get; init; }
    public string? ErrorMessage { get; init; }
    public Dictionary<string, object> Result { get; init; } = new();
    public DateTimeOffset CompletedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Consensus protocol messages.
/// </summary>
public sealed record ConsensusProposal
{
    public string ProposalId { get; init; } = Guid.NewGuid().ToString("N");
    public string ProposerId { get; init; } = string.Empty;
    public ConsensusType Type { get; init; }
    public string Description { get; init; } = string.Empty;
    public Dictionary<string, object> Data { get; init; } = new();
    public TimeSpan VotingPeriod { get; init; } = TimeSpan.FromSeconds(30);
    public ConsensusThreshold Threshold { get; init; } = new();
}

public enum ConsensusType
{
    LeaderElection,
    TaskAssignment,
    ResourceAllocation,
    PolicyChange,
    EmergencyStop,
    Custom
}

public sealed record ConsensusThreshold
{
    public double RequiredRatio { get; init; } = 0.66; // 2/3 majority
    public int MinVoters { get; init; } = 3;
    public bool RequireUnanimous { get; init; } = false;
}

public sealed record ConsensusVote
{
    public string ProposalId { get; init; } = string.Empty;
    public string VoterId { get; init; } = string.Empty;
    public bool Approve { get; init; }
    public string? Reason { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record ConsensusResult
{
    public string ProposalId { get; init; } = string.Empty;
    public ConsensusOutcome Outcome { get; init; }
    public int ApproveCount { get; init; }
    public int RejectCount { get; init; }
    public int AbstainCount { get; init; }
    public Dictionary<string, object>? DecidedData { get; init; }
    public DateTimeOffset DecidedAt { get; init; } = DateTimeOffset.UtcNow;
}

public enum ConsensusOutcome
{
    Approved,
    Rejected,
    Timeout,
    InsufficientVoters
}

/// <summary>
/// Leader election payload.
/// </summary>
public sealed record LeaderElectionPayload
{
    public string ElectionId { get; init; } = Guid.NewGuid().ToString("N");
    public string CandidateId { get; init; } = string.Empty;
    public int Term { get; init; }
    public int Priority { get; init; } // Higher = more priority
    public Dictionary<string, object> PlatformInfo { get; init; } = new();
}

public sealed record LeaderAnnouncement
{
    public string ElectionId { get; init; } = string.Empty;
    public string LeaderId { get; init; } = string.Empty;
    public int Term { get; init; }
    public DateTimeOffset ElectedAt { get; init; } = DateTimeOffset.UtcNow;
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// State synchronization messages.
/// </summary>
public sealed record StateSyncPayload
{
    public string SyncId { get; init; } = Guid.NewGuid().ToString("N");
    public SyncType Type { get; init; }
    public string EntityId { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty;
    public string StateJson { get; init; } = string.Empty;
    public long Version { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

public enum SyncType
{
    Full,
    Delta,
    Snapshot,
    Invalidate
}

/// <summary>
/// Event system for pub/sub.
/// </summary>
public sealed record EventMessage
{
    public string EventId { get; init; } = Guid.NewGuid().ToString("N");
    public string EventType { get; init; } = string.Empty;
    public string SourceAgentId { get; init; } = string.Empty;
    public string Payload { get; init; } = string.Empty; // JSON
    public Dictionary<string, string> Tags { get; init; } = new();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public EventPriority Priority { get; init; } = EventPriority.Normal;
}

public enum EventPriority
{
    Low,
    Normal,
    High,
    Critical
}

public sealed record EventSubscription
{
    public string SubscriptionId { get; init; } = Guid.NewGuid().ToString("N");
    public string SubscriberId { get; init; } = string.Empty;
    public string[] EventTypes { get; init; } = Array.Empty<string>();
    public string[] TagFilters { get; init; } = Array.Empty<string>();
    public TimeSpan? ExpiresIn { get; init; }
}

/// <summary>
/// Coordination group for related agents.
/// </summary>
public sealed record CoordinationGroup
{
    public string GroupId { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public HashSet<string> MemberAgentIds { get; init; } = new();
    public GroupCoordinationMode Mode { get; init; } = GroupCoordinationMode.Cooperative;
    public string? LeaderId { get; init; }
    public GroupPolicy Policy { get; init; } = new();
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public enum GroupCoordinationMode
{
    Cooperative,    // Agents work together voluntarily
    Hierarchical,   // Leader assigns tasks
    Competitive,    // Agents compete for tasks
    Swarm           // Emergent behavior, no central control
}

public sealed record GroupPolicy
{
    public bool RequireConsensusForDecisions { get; init; } = false;
    public ConsensusThreshold DefaultThreshold { get; init; } = new();
    public TimeSpan TaskTimeout { get; init; } = TimeSpan.FromMinutes(5);
    public int MaxRetries { get; init; } = 3;
    public bool AllowDynamicMembership { get; init; } = true;
    public HashSet<string> RequiredCapabilities { get; init; } = new();
}

/// <summary>
/// Coordination strategy interface.
/// </summary>
public interface ICoordinationStrategy
{
    string StrategyName { get; }
    Task<CoordinationDecision> DecideAsync(CoordinationContext context, CancellationToken ct);
    Task<bool> CanHandleAsync(CoordinationContext context, CancellationToken ct);
}

public sealed record CoordinationContext
{
    public string GroupId { get; init; } = string.Empty;
    public CoordinationTrigger Trigger { get; init; }
    public IReadOnlyList<AgentInfo> AvailableAgents { get; init; } = Array.Empty<AgentInfo>();
    public IReadOnlyList<AgentTask> PendingTasks { get; init; } = Array.Empty<AgentTask>();
    public Dictionary<string, object> SharedState { get; init; } = new();
    public CoordinationGroup GroupPolicy { get; init; } = new();
}

public enum CoordinationTrigger
{
    TaskQueued,
    AgentJoined,
    AgentLeft,
    AgentFailed,
    ResourceChanged,
    DeadlineApproaching,
    Manual
}

public sealed record AgentTask
{
    public string TaskId { get; init; } = string.Empty;
    public string TaskType { get; init; } = string.Empty;
    public Dictionary<string, object> Parameters { get; init; } = new();
    public TaskPriority Priority { get; init; } = TaskPriority.Normal;
    public DateTimeOffset QueuedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? Deadline { get; init; }
    public string[] RequiredCapabilities { get; init; } = Array.Empty<string>();
    public string? AssignedAgentId { get; init; }
    public TaskStatus Status { get; init; } = TaskStatus.Queued;
}

public enum TaskPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

public enum TaskStatus
{
    Queued,
    Assigned,
    Running,
    Completed,
    Failed,
    Cancelled
}

public sealed record CoordinationDecision
{
    public DecisionType Type { get; init; }
    public Dictionary<string, object> Actions { get; init; } = new(); // agentId -> action
    public string Reasoning { get; init; } = string.Empty;
    public double Confidence { get; init; } = 1.0;
    public TimeSpan? ValidFor { get; init; }
}

public enum DecisionType
{
    AssignTask,
    RebalanceLoad,
    ElectLeader,
    ScaleGroup,
    ChangePolicy,
    EmergencyStop,
    NoAction
}

/// <summary>
/// Agent registry for discovery.
/// </summary>
public interface IAgentRegistry
{
    Task RegisterAgentAsync(AgentInfo agent, CancellationToken ct);
    Task UnregisterAgentAsync(string agentId, CancellationToken ct);
    Task<AgentInfo?> GetAgentAsync(string agentId, CancellationToken ct);
    Task<IReadOnlyList<AgentInfo>> GetAllAgentsAsync(CancellationToken ct);
    Task<IReadOnlyList<AgentInfo>> GetAgentsByCapabilityAsync(string capability, CancellationToken ct);
    Task<IReadOnlyList<AgentInfo>> GetAgentsByTagAsync(string tag, CancellationToken ct);
    Task<AgentInfo?> FindBestAgentForTaskAsync(AgentTask task, CancellationToken ct);
    Task UpdateAgentStatusAsync(string agentId, AgentStatus status, CancellationToken ct);
    Task UpdateAgentHeartbeatAsync(string agentId, CancellationToken ct);
}

/// <summary>
/// Message bus for inter-agent communication.
/// </summary>
public interface IMessageBus
{
    Task PublishAsync(AgentMessage message, CancellationToken ct);
    Task SubscribeAsync(string agentId, MessageType[] messageTypes, Func<AgentMessage, CancellationToken, Task> handler, CancellationToken ct);
    Task UnsubscribeAsync(string agentId, CancellationToken ct);
    Task<AgentMessage?> RequestResponseAsync(AgentMessage request, TimeSpan timeout, CancellationToken ct);
    Task BroadcastAsync(AgentMessage message, CancellationToken ct);
}

/// <summary>
/// Coordination service for managing agent groups.
/// </summary>
public interface ICoordinationService
{
    Task<CoordinationGroup> CreateGroupAsync(CoordinationGroup group, CancellationToken ct);
    Task<bool> JoinGroupAsync(string groupId, string agentId, CancellationToken ct);
    Task<bool> LeaveGroupAsync(string groupId, string agentId, CancellationToken ct);
    Task<CoordinationGroup?> GetGroupAsync(string groupId, CancellationToken ct);
    Task<IReadOnlyList<CoordinationGroup>> GetGroupsForAgentAsync(string agentId, CancellationToken ct);
    Task<CoordinationDecision> CoordinateAsync(string groupId, CoordinationTrigger trigger, CancellationToken ct);
    Task<ConsensusResult> RunConsensusAsync(string groupId, ConsensusProposal proposal, CancellationToken ct);
    Task<LeaderAnnouncement> ElectLeaderAsync(string groupId, CancellationToken ct);
    Task SyncStateAsync(string groupId, StateSyncPayload state, CancellationToken ct);
}

/// <summary>
/// Distributed lock for coordination.
/// </summary>
public interface IDistributedLock
{
    Task<bool> AcquireAsync(string resourceId, string ownerId, TimeSpan ttl, CancellationToken ct);
    Task<bool> ReleaseAsync(string resourceId, string ownerId, CancellationToken ct);
    Task<bool> ExtendAsync(string resourceId, string ownerId, TimeSpan ttl, CancellationToken ct);
    Task<LockInfo?> GetLockInfoAsync(string resourceId, CancellationToken ct);
}

public sealed record LockInfo
{
    public string ResourceId { get; init; } = string.Empty;
    public string OwnerId { get; init; } = string.Empty;
    public DateTimeOffset AcquiredAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
}