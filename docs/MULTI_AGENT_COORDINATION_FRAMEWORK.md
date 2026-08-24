# Multi-Agent Coordination Framework - Technical Specification

## 1. Overview

### 1.1 Vision

Enable KKBOT GameAgents to operate as a **coordinated swarm** capable of:
- Dynamic task delegation based on agent capabilities
- Consensus-based decision making for critical operations
- Leader election for hierarchical coordination modes
- Real-time state synchronization across agents
- Fault tolerance through automatic failover

### 1.2 Scope

**In Scope:**
- Agent discovery and registry (Redis-backed)
- Message bus for inter-agent communication (pub/sub + request-response)
- Coordination groups with multiple modes (cooperative, hierarchical, competitive, swarm)
- Consensus protocol (Raft-inspired) for critical decisions
- Leader election (priority-based)
- Distributed locking (Redlock algorithm)
- State synchronization across agents
- gRPC service definitions for external integration
- Default coordination strategy with intelligent task assignment

**Out of Scope (Future):**
- Byzantine fault tolerance
- Cross-region coordination
- Advanced swarm behaviors (flocking, formation)
- Economic task allocation (auction-based)

---

## 2. Architecture

### 2.1 Component Diagram

```
+-------------------------------------------------------------+
|                    MULTI-AGENT COORDINATION FRAMEWORK       |
+-------------------------------------------------------------+
|                                                             |
|  +------------+    +------------+    +------------+         |
|  |   Agent A  |    |   Agent B  |    |   Agent C  |         |
|  | (GameAgent)|    | (GameAgent)|    | (GameAgent)|         |
|  +------+-----+    +------+-----+    +------+-----+         |
|         |                  |                  |              |
|         v                  v                  v              |
|  +----------------------------------------------------------+  |
|  |                    MESSAGE BUS (Redis Pub/Sub)           |  |
|  |  +--------------+  +--------------+  +--------------+    |  |
|  |  |  Pub/Sub     |  | Request/     |  | Broadcast    |    |  |
|  |  |  Channels    |  | Response     |  | Channel      |    |  |
|  |  +--------------+  +--------------+  +--------------+    |  |
|  +----------------------------------------------------------+  |
|         |                  |                  |              |
|         v                  v                  v              |
|  +----------------------------------------------------------+  |
|  |                      COORDINATION LAYER                  |  |
|  |  +----------------+  +----------------+  +-------------+  |  |
|  |  |   Agent        |  | Coordination   |  |  Consensus  |  |  |
|  |  |  Registry      |  |  Service       |  |  Engine     |  |  |
|  |  +----------------+  +----------------+  +-------------+  |  |
|  |  +----------------+  +----------------+  +-------------+  |  |
|  |  | Distributed    |  |  State         |  |  Leader     |  |  |
|  |  | Lock (Redlock) |  | Sync           |  |  Election   |  |  |
|  |  +----------------+  +----------------+  +-------------+  |  |
|  |  +----------------+  +----------------+                    |  |
|  |  | Coordination   |  |  Group         |                    |  |
|  |  | Strategies     |  | Management     |                    |  |
|  |  +----------------+  +----------------+                    |  |
|  +----------------------------------------------------------+  |
|                        |                                      |
|                        v                                      |
|  +----------------------------------------------------------+  |
|  |                      REDIS CLUSTER                        |  |
|  |  - Agent Registry    - Message Channels    - Distributed Locks  |
|  |  - Group Metadata    - Consensus State     - Pub/Sub Channels |
|  +----------------------------------------------------------+  |
|                                                             |
+-------------------------------------------------------------+
```

### 2.2 Core Abstractions

| Interface | Responsibility |
|-----------|----------------|
| `IAgentRegistry` | Agent registration, discovery, capability-based lookup |
| `IMessageBus` | Pub/sub, request-response, broadcast messaging |
| `ICoordinationService` | Group management, task coordination, consensus, leader election |
| `IDistributedLock` | Redlock-based distributed locking |
| `ICoordinationStrategy` | Pluggable coordination logic |

---

## 3. Communication Protocol

### 3.1 Message Types

| Category | Types |
|----------|-------|
| **Control** | Heartbeat, Register, Unregister, AgentInfo |
| **Task** | TaskRequest, TaskResponse, TaskProgress, TaskCancel |
| **Coordination** | DelegateTask, DelegateResponse, ConsensusPropose/Vote/Result, LeaderElection/Announce |
| **State Sync** | StateSync, StateDelta, SnapshotRequest/Response |
| **Events** | EventPublish, EventSubscribe, EventUnsubscribe |
| **Error** | Error, Ack, Nak |

### 3.2 Message Envelope

```csharp
public sealed record AgentMessage
{
    public string MessageId { get; init; } = Guid.NewGuid().ToString("N");
    public MessageType Type { get; init; }
    public string FromAgentId { get; init; } = string.Empty;
    public string ToAgentId { get; init; } = string.Empty; // Empty = broadcast
    public string CorrelationId { get; init; } = string.Empty;
    public string PayloadType { get; init; } = string.Empty;
    public string Payload { get; init; } = string.Empty; // JSON
    public Dictionary<string, string> Headers { get; init; } = new();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public TimeSpan? Ttl { get; init; }
    public int Priority { get; init; } = 0;
}
```

### 3.3 Communication Patterns

| Pattern | Use Case | Implementation |
|---------|----------|----------------|
| **Pub/Sub** | Event notifications, state changes | Redis channels per agent + broadcast |
| **Request/Response** | Task delegation, queries | Correlation IDs + response channels |
| **Broadcast** | Group announcements, leader election | Single broadcast channel |
| **Streaming** | gRPC bidirectional for high-frequency | gRPC streams |

---

## 4. Coordination Patterns

### 4.1 Group Coordination Modes

| Mode | Description | Use Case |
|------|-------------|----------|
| **Cooperative** | Agents voluntarily share work | Distributed task processing |
| **Hierarchical** | Leader assigns tasks | Centralized control, clear authority |
| **Competitive** | Agents bid for tasks | Auction-based allocation |
| **Swarm** | Emergent behavior, no leader | Flocking, exploration |

### 4.2 Task Assignment Algorithm

```
1. Sort pending tasks by Priority desc, Deadline asc
2. For each task:
   a. Filter agents by RequiredCapabilities
   b. Score candidates: CapabilityMatch + Resources + Load + Affinity
   c. Assign to highest-scored agent
   d. Update agent load estimate
3. Return assignment decisions
```

### 4.3 Consensus Protocol (Raft-inspired)

```
1. Proposer sends ConsensusProposal to all voters
2. Voters evaluate and send ConsensusVote (Approve/Reject)
3. Coordinator tallies votes after VotingPeriod
4. Outcome determined by RequiredRatio (default 2/3) and MinVoters
5. Result broadcast to all group members
```

### 4.4 Leader Election (Priority-based)

```
1. Triggered on: Group creation, leader failure, manual trigger
2. All online members become candidates
3. Priority = Capabilities + Resources + Uptime
4. Highest priority wins (deterministic)
5. LeaderAnnouncement broadcast to group
```

---

## 5. Implementation Components

### 5.1 Projects Created

| Project | Purpose |
|---------|---------|
| `KKBOT.Agent.Coordination.Abstractions` | Interfaces, models, message types |
| `KKBOT.Agent.Coordination` | Redis implementations, coordination service, strategies, locking |
| `KKBOT.Agent.Coordination.Grpc` | gRPC service definitions (.proto) |

### 5.2 Key Implementations

| Component | Technology | Key Features |
|-----------|------------|--------------|
| `RedisAgentRegistry` | StackExchange.Redis | TTL-based expiry, capability/tag indexing, heartbeats |
| `RedisMessageBus` | Redis Pub/Sub | Pub/sub, request-response, broadcast, priority queues |
| `CoordinationService` | Redis + In-memory | Group mgmt, consensus, leader election, state sync |
| `RedisDistributedLock` | Redlock Algorithm | Lua scripts for atomicity, TTL, auto-renewal |
| `DefaultCoordinationStrategy` | C# | Task queuing, agent join/leave/failure handling, deadlines |

---

## 6. gRPC Service Definitions

### 6.1 Services

| Service | Methods |
|---------|---------|
| `AgentRegistry` | Register/Unregister/Get/List/Find agents, Watch (streaming) |
| `MessageBus` | Publish, RequestResponse, Broadcast, Subscribe (streaming) |
| `CoordinationService` | Group CRUD, Join/Leave, Coordinate, Consensus, LeaderElection, SyncState |
| `DistributedLockService` | Acquire/Release/Extend, GetInfo, Watch (streaming) |

### 6.2 Key Messages

- `AgentInfo`, `AgentCapabilities`, `AgentTask`
- `AgentMessage` (envelope), `ConsensusProposal/Vote/Result`
- `CoordinationGroup`, `GroupPolicy`, `LeaderAnnouncement`
- `LockInfo`, `TaskAssignmentAction`

---

## 7. DI Extensions & Hosted Service

### 7.1 Registration

```csharp
services.AddKkbotAgentCoordination(configuration);
// Registers: IAgentRegistry, IMessageBus, IDistributedLock, 
// ICoordinationService, ICoordinationStrategy
```

### 7.2 AgentCoordinationHostedService

- Auto-registers agent on startup
- Sends periodic heartbeats
- Subscribes to coordination messages
- Graceful shutdown (unregister, unsubscribe)

---

## 8. Configuration

```json
{
  "Coordination": {
    "Registry": {
      "AgentTtlMinutes": 10,
      "HeartbeatTtlMinutes": 2
    },
    "MessageBus": {
      "EnableTracing": true,
      "DefaultTimeoutSeconds": 30
    },
    "Lock": {
      "DefaultTtlSeconds": 30,
      "MaxRetryAttempts": 3
    },
    "Service": {
      "DefaultTaskTimeout": "00:05:00",
      "MaxConcurrentCoordination": 10
    },
    "Strategy": {
      "DeadlineWarningThreshold": "00:05:00",
      "OverloadThreshold": 0.8
    },
    "Redis": {
      "ConnectionString": "localhost:6379"
    }
  }
}
```

---

## 9. Usage Examples

### 9.1 Register Agent & Join Group

```csharp
var agent = new AgentInfo
{
    AgentId = "agent-001",
    Name = "Farming Bot",
    Type = AgentType.Planning,
    Capabilities = new AgentCapabilities
    {
        Skills = new[] { "farming", "navigation", "combat" },
        Tags = new[] { "region:us-east", "gpu:true" },
        Capacity = new ResourceCapacity { CpuCores = 8, MemoryBytes = 16_000_000_000 }
    }
};

await agentRegistry.RegisterAgentAsync(agent);
await coordinationService.JoinGroupAsync("farming-squad", "agent-001");
```

### 9.2 Delegate Task

```csharp
await messageBus.PublishAsync(new AgentMessage
{
    Type = MessageType.DelegateTask,
    FromAgentId = "coordinator",
    ToAgentId = "agent-001",
    CorrelationId = Guid.NewGuid().ToString(),
    PayloadType = nameof(TaskDelegationPayload),
    Payload = JsonSerializer.Serialize(new TaskDelegationPayload
    {
        TaskId = "task-123",
        TaskType = "farm_gold",
        Parameters = new Dictionary<string, object> { ["location"] = "zone_5", ["duration"] = 3600 },
        RequiredSkill = "farming"
    })
});
```

### 9.3 Run Consensus

```csharp
var result = await coordinationService.RunConsensusAsync("farming-squad", new ConsensusProposal
{
    ProposalId = "prop-001",
    Type = ConsensusType.ResourceAllocation,
    Description = "Allocate GPU to agent-002 for training",
    Data = new Dictionary<string, object> { ["gpu_id"] = "gpu-001", ["duration_hours"] = 4 },
    VotingPeriod = TimeSpan.FromMinutes(2),
    Threshold = new ConsensusThreshold { RequiredRatio = 0.66, MinVoters = 3 }
});
```

### 9.4 Acquire Distributed Lock

```csharp
var acquired = await distributedLock.AcquireAsync("resource:gpu-001", "agent-002", TimeSpan.FromHours(2));
if (acquired)
{
    try { await TrainModelAsync(); }
    finally { await distributedLock.ReleaseAsync("resource:gpu-001", "agent-002"); }
}
```

---

## 10. Observability

### 10.1 Metrics

| Metric | Type | Description |
|--------|------|-------------|
| `kkbot_coord_agents_total` | Gauge | Registered agents |
| `kkbot_coord_groups_total` | Gauge | Active groups |
| `kkbot_coord_tasks_assigned_total` | Counter | Tasks assigned |
| `kkbot_coord_consensus_total` | Counter | Consensus runs |
| `kkbot_coord_lock_acquisitions_total` | Counter | Lock acquisitions |
| `kkbot_coord_message_latency_ms` | Histogram | Message latency |

### 10.2 Traces

- `coordination.register_agent`
- `coordination.coordinate`
- `coordination.run_consensus`
- `coordination.elect_leader`
- `messagebus.publish`
- `lock.acquire`

---

## 11. Testing Strategy

### 11.1 Unit Tests
- Strategy decision logic (task assignment, rebalancing)
- Consensus tallying logic
- Leader priority calculation
- Lock acquisition/release

### 11.2 Integration Tests
- Full group lifecycle (create -> join -> coordinate -> leave)
- Consensus with network partitions
- Leader failover
- Lock contention scenarios

### 11.3 Chaos Tests
- Agent crash during coordination
- Network partition during consensus
- Redis failover during operations

---

## 12. Future Extensions

| Extension | Description |
|-----------|-------------|
| **Byzantine Fault Tolerance** | PBFT for malicious agent protection |
| **Cross-Region Coordination** | Multi-datacenter consensus |
| **Economic Allocation** | Auction-based task bidding |
| **Swarm Behaviors** | Flocking, formation, gradient following |
| **ML-Based Strategy** | Learned coordination policies |

---

*Document Version: 1.0*  
*Author: KKBOT Team*  
*Status: Implemented*