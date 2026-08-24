# GameAgent Framework - Technical Specification

## 1. Overview

### 1.1 Vision

Transform KKBOT from **rule-based automation** to **goal-driven intelligent agents** that can:
- Understand high-level objectives in natural language
- Plan and execute complex multi-step workflows
- Learn from experience and adapt to game changes
- Collaborate with human operators

### 1.2 Scope

**In Scope:**
- Core `IGameAgent` abstraction and implementations
- LLM-powered planning and decision making
- Tool calling integration with existing KKBOT capabilities
- Visual Scripting "LLM Node" for hybrid workflows
- Learning framework (trajectory collection, reward modeling)
- Safety guardrails and human-in-the-loop controls

**Out of Scope (Future):**
- Multi-agent coordination (swarm intelligence)
- Full RL training pipeline
- Cross-game transfer learning

---

## 2. Architecture

### 2.1 High-Level Component Diagram

```
+-------------------------------------------------------------+
|                      GAME AGENT FRAMEWORK                   |
+-------------------------------------------------------------+
|                                                             |
|  +------------+    +------------+    +------------+         |
|  |  Planner   |    | Executor   |    |  Learner   |         |
|  | (LLM-based)|    | (Tool Call)|    | (RLHF/BC)  |         |
|  +----+-----+    +----+-----+    +----+-----+         |
|       |            |            |                      |
|       +------------+------------+----------------------+         |
|                        v                                     |
|  +----------------------------------------------------------+  |
|  |                      AGENT CORE                          |  |
|  |  +-----------+  +-----------+  +---------------+        |  |
|  |  |  Memory   |  |  Skills   |  |  Guardrails   |        |  |
|  |  | (Episodic/|  | (Learned/ |  | (Safety/     |        |  |
|  |  |  Semantic)|  |  Built-in)|  |  Policy)      |        |  |
|  |  +-----------+  +-----------+  +---------------+        |  |
|  +----------------------------------------------------------+  |
|                        |                                     |
|  +--------------------+--------------------+-----------------+  |
|  v                    v                    v                 v  |
| +-----------+  +----------------+  +--------------+           |
| |Perception |  |  Device        |  | Orchestration|           |
| | Pipeline  |  |  Control       |  |  (DAG/FSM)   |           |
| +-----------+  +----------------+  +--------------+           |
|                                                             |
+-------------------------------------------------------------+
```

### 2.2 Core Abstractions

#### 2.2.1 IGameAgent

```csharp
public interface IGameAgent
{
    string AgentId { get; }
    string Name { get; }
    AgentType Type { get; }
    
    Task<AgentDecision> DecideAsync(PerceptionContext context, GameGoal goal, CancellationToken ct);
    
    Task LearnAsync(Trajectory trajectory, RewardSignal reward, CancellationToken ct);
    
    Task<AgentCapability[]> GetCapabilitiesAsync();
    
    Task<AgentState> GetStateAsync();
    
    Task SetStateAsync(AgentState state);
    
    Task ConfigureAsync(AgentConfiguration config);
}

public enum AgentType
{
    Reactive,      // Direct perception to action mapping
    Planning,      // LLM-based planning with tool use
    Learning,      // RL/BC policy network
    Hybrid         // Combination with arbitration
}
```

#### 2.2.2 GameGoal

```csharp
public sealed record GameGoal
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Description { get; init; } = string.Empty;
    public GoalType Type { get; init; }
    public Dictionary<string, object> Parameters { get; init; } = new();
    public GoalConstraints Constraints { get; init; } = new();
    public SuccessCriteria SuccessCriteria { get; init; } = new();
    public TimeSpan? Timeout { get; init; }
    public int Priority { get; init; } = 0;
}

public enum GoalType
{
    Achieve,      // Reach a state (e.g., "reach level 60")
    Maintain,     // Keep a condition (e.g., "keep HP > 50%")
    Avoid,        // Avoid a state (e.g., "don't die")
    Optimize,     // Maximize metric (e.g., "maximize gold/hour")
    Explore,      // Discover (e.g., "find all teleport waypoints")
    Custom
}

public sealed record GoalConstraints
{
    public TimeSpan? MaxDuration { get; init; }
    public ResourceBudget? ResourceLimits { get; init; }
    public string[] AllowedActions { get; init; } = Array.Empty<string>();
    public string[] ForbiddenActions { get; init; } = Array.Empty<string>();
    public bool RequireApproval { get; init; } = false;
    public SafetyLevel SafetyLevel { get; init; } = SafetyLevel.Normal;
}

public enum SafetyLevel
{
    Permissive,   // Allow risky actions
    Normal,       // Standard safety checks
    Strict,       // Conservative, human approval for risks
    Supervised    // Every action requires approval
}
```

#### 2.2.3 AgentDecision & AgentAction

```csharp
public sealed record AgentDecision
{
    public string DecisionId { get; init; } = Guid.NewGuid().ToString("N");
    public AgentAction Action { get; init; } = null!;
    public double Confidence { get; init; }
    public string Reasoning { get; init; } = string.Empty;
    public Plan? Plan { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    
    // Trace context
    public string? TraceId { get; init; }
    public string? SpanId { get; init; }
}

public sealed record AgentAction
{
    public ActionType Type { get; init; } = ActionType.ToolCall;
    public string ToolName { get; init; } = string.Empty;
    public Dictionary<string, object> Parameters { get; init; } = new();
    public TimeSpan EstimatedDuration { get; init; }
    public RiskAssessment Risk { get; init; } = new();
    public string[] RequiredPermissions { get; init; } = Array.Empty<string>();
    public string? CorrelationId { get; init; }
}

public enum ActionType
{
    ToolCall,       // Call a KKBOT tool
    Wait,           // Wait for condition
    Delegate,       // Delegate to another agent/skill
    AskHuman,       // Request human input
    NoOp,           // Do nothing
    Abort           // Abort current goal
}
```

#### 2.2.4 Plan (Multi-step)

```csharp
public sealed record Plan
{
    public string PlanId { get; init; } = Guid.NewGuid().ToString("N");
    public string GoalId { get; init; } = string.Empty;
    public PlanStep[] Steps { get; init; } = Array.Empty<PlanStep>();
    public PlanMetadata Metadata { get; init; } = new();
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record PlanStep
{
    public int StepNumber { get; init; }
    public string StepId { get; init; } = Guid.NewGuid().ToString("N");
    public string Description { get; init; } = string.Empty;
    public AgentAction Action { get; init; } = null!;
    public string[] Dependencies { get; init; } = Array.Empty<string>();
    public SuccessCondition SuccessCondition { get; init; } = new();
    public FailureHandling FailureHandling { get; init; } = FailureHandling.Retry;
    public TimeSpan? Timeout { get; init; }
    public int MaxRetries { get; init; } = 2;
}

public sealed record SuccessCondition
{
    public string Type { get; init; } = string.Empty; // "tool_result", "perception", "state", "custom"
    public Dictionary<string, object> Parameters { get; init; } = new();
    public string? CustomExpression { get; init; } // CEL expression
}

public enum FailureHandling
{
    Retry,           // Retry this step
    Skip,            // Skip to next step
    Replan,          // Request new plan
    Abort,           // Abort entire plan
    AskHuman         // Request human intervention
}
```

---

## 3. LLM-Powered Planner

### 3.1 Planner Interface

```csharp
public interface IAgentPlanner
{
    Task<Plan> CreatePlanAsync(GameGoal goal, AgentContext context, CancellationToken ct);
    Task<Plan> ReplanAsync(Plan currentPlan, PlanExecutionState state, CancellationToken ct);
    Task<PlanStep?> NextStepAsync(Plan plan, PlanExecutionState state, CancellationToken ct);
}

public sealed record AgentContext
{
    public PerceptionContext CurrentPerception { get; init; } = null!;
    public AgentState AgentState { get; init; } = null!;
    public GameState GameState { get; init; } = null!;
    public IReadOnlyList<AgentCapability> AvailableCapabilities { get; init; } = Array.Empty<AgentCapability>();
    public IReadOnlyList<ToolDefinition> AvailableTools { get; init; } = Array.Empty<ToolDefinition>();
    public ConversationHistory History { get; init; } = new();
}
```

### 3.2 System Prompt Template

```
You are an expert game automation agent for {game_name}. 

## Your Capabilities
{capabilities_list}

## Available Tools
{tools_schema}

## Current State
- Goal: {goal_description}
- Game State: {game_state_summary}
- Recent History: {recent_actions_summary}
- Perception: {perception_summary}

## Constraints
{safety_constraints}
{resource_constraints}
{action_constraints}

## Task
Create a detailed step-by-step plan to achieve the goal. Each step must:
1. Specify the exact tool to call with parameters
2. Define clear success conditions
3. Include failure handling strategy
4. Estimate duration and risk level

Output format: JSON matching Plan schema.
```

### 3.3 Tool Definitions (Function Calling)

```csharp
public sealed record ToolDefinition
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public JsonSchema ParametersSchema { get; init; } = new();
    public string[] RequiredPermissions { get; init; } = Array.Empty<string>();
    public RiskLevel RiskLevel { get; init; } = RiskLevel.Low;
    public bool IsAsync { get; init; } = true;
    public string Category { get; init; } = string.Empty;
}

// Built-in KKBOT Tools
public static class BuiltInTools
{
    public static readonly ToolDefinition CaptureScreen = new()
    {
        Name = "capture_screen",
        Description = "Capture current game screen",
        ParametersSchema = JsonSchema.FromType<CaptureScreenParams>(),
        RequiredPermissions = new[] { "DEVICE_READ" }
    };
    
    public static readonly ToolDefinition DetectTemplate = new()
    {
        Name = "detect_template",
        Description = "Find template on screen",
        ParametersSchema = JsonSchema.FromType<DetectTemplateParams>(),
        RequiredPermissions = new[] { "DEVICE_READ" }
    };
    
    public static readonly ToolDefinition SendInput = new()
    {
        Name = "send_input",
        Description = "Send tap/swipe/key input",
        ParametersSchema = JsonSchema.FromType<SendInputParams>(),
        RequiredPermissions = new[] { "INPUT_WRITE" },
        RiskLevel = RiskLevel.Medium
    };
    
    public static readonly ToolDefinition RunOcr = new()
    {
        Name = "run_ocr",
        Description = "Extract text from screen region",
        ParametersSchema = JsonSchema.FromType<RunOcrParams>(),
        RequiredPermissions = new[] { "DEVICE_READ" }
    };
    
    public static readonly ToolDefinition AiVision = new()
    {
        Name = "ai_vision",
        Description = "Ask vision model about screen",
        ParametersSchema = JsonSchema.FromType<AiVisionParams>(),
        RequiredPermissions = new[] { "DEVICE_READ", "AI_USE" }
    };
    
    public static readonly ToolDefinition ExecuteSkill = new()
    {
        Name = "execute_skill",
        Description = "Execute a learned skill",
        ParametersSchema = JsonSchema.FromType<ExecuteSkillParams>(),
        RequiredPermissions = new[] { "PLUGIN_EXECUTE" }
    };
    
    public static readonly ToolDefinition WaitForCondition = new()
    {
        Name = "wait_for",
        Description = "Wait for perception condition",
        ParametersSchema = JsonSchema.FromType<WaitForParams>(),
        RequiredPermissions = new[] { "DEVICE_READ" }
    };
}
```

---

## 4. Executor (Tool Calling Runtime)

### 4.1 Tool Executor

```csharp
public interface IToolExecutor
{
    Task<ToolResult> ExecuteAsync(string toolName, Dictionary<string, object> parameters, CancellationToken ct);
    IReadOnlyList<ToolDefinition> GetAvailableTools();
    Task<ToolDefinition?> GetToolDefinitionAsync(string toolName);
}

public sealed record ToolResult
{
    public bool Success { get; init; }
    public object? Result { get; init; }
    public string? Error { get; init; }
    public TimeSpan Duration { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();
}
```

### 4.2 Execution Pipeline

```csharp
public sealed class AgentExecutor : IAgentExecutor
{
    private readonly IToolExecutor _toolExecutor;
    private readonly ILogger<AgentExecutor> _logger;
    private readonly IMetricsRegistry _metrics;
    private readonly ISafetyGuardrails _safety;
    
    public async Task<ExecutionResult> ExecuteStepAsync(PlanStep step, AgentContext context, CancellationToken ct)
    {
        // 1. Pre-execution validation
        var validation = await ValidateStepAsync(step, context, ct);
        if (!validation.IsValid)
            return ExecutionResult.Failed(validation.Error);
        
        // 2. Permission check
        if (!await CheckPermissionsAsync(step.Action.RequiredPermissions, ct))
            return ExecutionResult.Failed("Insufficient permissions");
        
        // 3. Safety check
        var safetyCheck = await _safety.CheckActionAsync(step.Action, context);
        if (!safetyCheck.IsSafe)
        {
            if (safetyCheck.Action == SafetyAction.Block)
                return ExecutionResult.Failed($"Action blocked by safety: {safetyCheck.Reason}");
            else if (safetyCheck.Action == SafetyAction.RequireApproval)
                return ExecutionResult.Failed($"Action requires human approval: {safetyCheck.Reason}");
        }
        
        // 4. Execute tool
        var toolResult = await _toolExecutor.ExecuteAsync(step.Action.ToolName, step.Action.Parameters, ct);
        
        // 5. Post-execution validation
        var success = await ValidateSuccessAsync(step.SuccessCondition, toolResult, context, ct);
        
        return success && toolResult.Success 
            ? ExecutionResult.SuccessResult(toolResult, DateTime.UtcNow - startTime, step.StepNumber)
            : ExecutionResult.Failed($"Step failed: {toolResult.Error}");
    }
}
```

---

## 5. Memory System

### 5.1 Memory Types

```csharp
public interface IAgentMemory
{
    // Episodic memory - specific experiences
    Task StoreEpisodeAsync(Episode episode);
    Task<IReadOnlyList<Episode>> RetrieveEpisodesAsync(EpisodeQuery query, int limit = 10);
    
    // Semantic memory - facts and knowledge
    Task StoreFactAsync(SemanticFact fact);
    Task<IReadOnlyList<SemanticFact>> QueryFactsAsync(string query, int limit = 10);
    
    // Working memory - current context
    Task UpdateWorkingMemoryAsync(string key, object value);
    Task<T?> GetWorkingMemoryAsync<T>(string key);
    
    // Skill memory - learned procedures
    Task StoreSkillAsync(Skill skill);
    Task<Skill?> GetSkillAsync(string skillId);
    Task<IReadOnlyList<Skill>> ListSkillsAsync(SkillCategory? category = null);
}

public sealed record Episode
{
    public string EpisodeId { get; init; } = Guid.NewGuid().ToString("N");
    public GameGoal Goal { get; init; } = null!;
    public Trajectory Trajectory { get; init; } = null!;
    public RewardSignal Outcome { get; init; } = null!;
    public Dictionary<string, object> Context { get; init; } = new();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record Trajectory
{
    public TrajectoryStep[] Steps { get; init; } = Array.Empty<TrajectoryStep>();
    public TimeSpan TotalDuration { get; init; }
    public bool Success { get; init; }
}

public sealed record TrajectoryStep
{
    public PerceptionContext Perception { get; init; } = null!;
    public AgentAction Action { get; init; } = null!;
    public ToolResult Result { get; init; } = null!;
    public AgentState NextState { get; init; } = null!;
    public double Reward { get; init; }
}
```

### 5.2 Vector Store Integration
- Use **PGVector** (PostgreSQL) or **Qdrant** for embeddings
- Embed episodes, facts, skills for semantic retrieval
- Hybrid search: vector + keyword + metadata filters

---

## 6. Learning Framework

### 6.1 Learning Interfaces

```csharp
public interface IAgentLearner
{
    // Behavioral Cloning from demonstrations
    Task<LearningResult> LearnFromDemonstrationsAsync(
        IReadOnlyList<Trajectory> demonstrations, 
        LearningConfig config,
        CancellationToken ct);
    
    // RLHF from human feedback
    Task<LearningResult> LearnFromFeedbackAsync(
        IReadOnlyList<FeedbackSample> feedback,
        LearningConfig config,
        CancellationToken ct);
    
    // Online adaptation
    Task AdaptOnlineAsync(Trajectory trajectory, RewardSignal reward, CancellationToken ct);
    
    // Skill extraction
    Task<Skill[]> ExtractSkillsAsync(IReadOnlyList<Trajectory> trajectories, CancellationToken ct);
}

public sealed record FeedbackSample
{
    public Trajectory Trajectory { get; init; } = null!;
    public RewardSignal HumanReward { get; init; } = null!;
    public string? HumanComment { get; init; }
    public FeedbackType Type { get; init; } = FeedbackType.Binary;
}
```

### 6.2 Skill Representation

```csharp
public sealed record Skill
{
    public string SkillId { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public SkillCategory Category { get; init; }
    public SkillParameter[] Parameters { get; init; } = Array.Empty<SkillParameter>();
    public Plan TemplatePlan { get; init; } = null!;
    public SkillPrecondition[] Preconditions { get; init; } = Array.Empty<SkillPrecondition>();
    public SkillPostcondition[] Postconditions { get; init; } = Array.Empty<SkillPostcondition>();
    public double SuccessRate { get; init; }
    public int UsageCount { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public string LearnedFrom { get; init; } = string.Empty;  // "demonstration", "rl", "manual"
    public Dictionary<string, object> Metadata { get; init; } = new();
}

public enum SkillCategory
{
    Navigation,
    Combat,
    ResourceGathering,
    Questing,
    Social,
    Custom
}
```

---

## 7. Safety & Guardrails

### 7.1 Safety Layer

```csharp
public interface ISafetyGuardrails
{
    Task<SafetyCheckResult> CheckActionAsync(AgentAction action, AgentContext context);
    Task<SafetyCheckResult> CheckPlanAsync(Plan plan, AgentContext context);
    Task<GuardrailViolation[]> GetActiveViolationsAsync();
    Task RegisterPolicyAsync(SafetyPolicy policy);
}

public sealed record SafetyPolicy
{
    public string PolicyId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public SafetyRule[] Rules { get; init; } = Array.Empty<SafetyRule>();
    public bool IsActive { get; init; } = true;
    public int Priority { get; init; } = 0;
    public string Description { get; init; } = string.Empty;
}

public sealed record SafetyRule
{
    public string RuleId { get; init; } = Guid.NewGuid().ToString("N");
    public string Condition { get; init; } = string.Empty; // CEL expression
    public SafetyAction Action { get; init; } = SafetyAction.Block;
    public string Message { get; init; } = string.Empty;
    public RiskLevel MinRiskLevel { get; init; } = RiskLevel.Low;
    public string[] ApplicableAgents { get; init; } = Array.Empty<string>(); // Empty = all
}

public enum SafetyAction
{
    Allow,
    Warn,
    RequireApproval,
    Block,
    LogOnly
}
```

### 7.2 Built-in Safety Policies

| Policy | Rules |
|--------|-------|
| **Device Safety** | No inputs during critical system dialogs, max 10 APM |
| **Game ToS Compliance** | No memory reading, no packet injection, human-like delays |
| **Resource Protection** | Max gold spend/hour, no selling valuable items |
| **Progress Protection** | No actions that could lose progress (hardcore mode) |
| **Social Safety** | No chat spam, no trade scams, respect party/guild rules |

---

## 8. Visual Scripting Integration

### 8.1 LLM Node

```csharp
// New node type for Visual Scripting
public sealed class LlmAgentNode : WorkflowNode
{
    public override string NodeTypeId => "llm-agent";
    
    public LlmAgentConfiguration Configuration { get; set; } = new();
    
    public sealed class LlmAgentConfiguration
    {
        public string AgentId { get; set; } = string.Empty;
        public string GoalTemplate { get; set; } = string.Empty;  // With variable interpolation
        public Dictionary<string, string> InputMappings { get; set; } = new();  // Port -> Goal param
        public Dictionary<string, string> OutputMappings { get; set; } = new(); // Result -> Port
        public bool WaitForCompletion { get; set; } = true;
        public TimeSpan? Timeout { get; set; }
        public bool StreamResults { get; set; } = false;
    }
}
```

### 8.2 Node Ports

- **Inputs**: goal_params (JSON), context (PerceptionContext), config (AgentConfiguration)
- **Outputs**: decision (AgentDecision), plan (Plan), result (ExecutionResult), error

---

## 9. API Endpoints

### 9.1 Agent Management

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | /api/v1/agents | Create agent |
| GET | /api/v1/agents | List agents |
| GET | /api/v1/agents/{id} | Get agent |
| PUT | /api/v1/agents/{id} | Update agent |
| DELETE | /api/v1/agents/{id} | Delete agent |
| POST | /api/v1/agents/{id}/configure | Configure agent |
| GET | /api/v1/agents/{id}/state | Get agent state |
| PUT | /api/v1/agents/{id}/state | Set agent state |

### 9.2 Goal Execution

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | /api/v1/agents/{id}/goals | Submit goal (async) |
| GET | /api/v1/agents/{id}/goals/{goalId} | Get goal status |
| DELETE | /api/v1/agents/{id}/goals/{goalId} | Cancel goal |
| GET | /api/v1/agents/{id}/goals/{goalId}/trajectory | Get execution trajectory |
| WS | /api/v1/agents/{id}/goals/{goalId}/stream | Stream decisions (SSE) |

### 9.3 Learning

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | /api/v1/agents/{id}/learn/demonstrations | Learn from demos |
| POST | /api/v1/agents/{id}/learn/feedback | Learn from feedback |
| GET | /api/v1/agents/{id}/skills | List learned skills |
| POST | /api/v1/agents/{id}/skills | Create skill manually |

### 9.4 Memory & Analytics

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | /api/v1/agents/{id}/memory/episodes | Query episodes |
| GET | /api/v1/agents/{id}/memory/facts | Query facts |
| GET | /api/v1/agents/{id}/analytics/performance | Performance metrics |
| GET | /api/v1/agents/{id}/analytics/learning | Learning curves |

---

## 10. Implementation Phases

### Phase 1: Core Framework (Week 1-2)
- [ ] `IGameAgent`, `GameGoal`, `AgentDecision`, `AgentAction`, `Plan`
- [ ] `IAgentPlanner` with basic LLM integration (Ollama)
- [ ] `IToolExecutor` with built-in KKBOT tools
- [ ] `IAgentExecutor` with validation pipeline
- [ ] Basic `IAgentMemory` (in-memory + EF Core persistence)

### Phase 2: Planner Intelligence (Week 3-4)
- [ ] Advanced system prompt engineering
- [ ] Plan validation and repair
- [ ] Replanning on failure
- [ ] Multi-goal handling (priority queue)
- [ ] Streaming decisions via SSE

### Phase 3: Learning & Skills (Week 5-6)
- [ ] Behavioral cloning from trajectories
- [ ] Skill extraction and parameterization
- [ ] Skill library management
- [ ] Human feedback collection UI

### Phase 4: Safety & Production (Week 7-8)
- [ ] Safety guardrails engine
- [ ] Human-in-the-loop approval flow
- [ ] Comprehensive logging and audit
- [ ] Performance optimization
- [ ] Integration tests

---

## 11. Dependencies

### 11.1 New Packages

```xml
<PackageReference Include="Microsoft.SemanticKernel" Version="1.0.0" />
<PackageReference Include="Microsoft.SemanticKernel.Planners" Version="1.0.0" />
<PackageReference Include="pgvector" Version="0.2.0" />  <!-- For vector memory -->
<PackageReference Include="CEL.NET" Version="1.0.0" />   <!-- For safety rules -->
```

### 11.2 Existing KKBOT Dependencies

- `KKBOT.AI.Abstractions` - `IAiService`, `IAiVisionService`
- `KKBOT.Perception.Abstractions` - `IPerceptionPipeline`
- `KKBOT.Device.Abstractions` - `IDeviceSession`
- `KKBOT.Orchestration` - `DagScheduler`, `InstanceOrchestrator`
- `KKBOT.Plugins.Abstractions` - `IGameModule` (for skills)
- `KKBOT.VisualScripting` - `WorkflowNode`, `VisualScriptingEngine`
- `KKBOT.Observability` - Metrics, Tracing, Logging

---

## 12. Configuration

```json
{
  "GameAgent": {
    "DefaultPlanner": "llm",
    "DefaultExecutor": "standard",
    "DefaultMemory": "efcore",
    "DefaultLearner": "bc",
    "Planner": {
      "Model": "llama3.1:8b",
      "Temperature": 0.3,
      "MaxTokens": 4096,
      "SystemPromptTemplate": "prompts/planner.md",
      "MaxPlanSteps": 20,
      "ReplanThreshold": 3
    },
    "Executor": {
      "MaxConcurrentTools": 3,
      "DefaultTimeout": "00:05:00",
      "RetryPolicy": {
        "MaxRetries": 2,
        "BaseDelayMs": 1000
      }
    },
    "Memory": {
      "EpisodicRetentionDays": 30,
      "SemanticRetentionDays": 365,
      "WorkingMemoryTtlMinutes": 60,
      "VectorDimensions": 768,
      "SimilarityThreshold": 0.75
    },
    "Safety": {
      "DefaultLevel": "Normal",
      "RequireApprovalFor": ["purchase", "trade", "delete"],
      "MaxActionsPerMinute": 60,
      "HumanApprovalTimeout": "00:02:00"
    },
    "Learning": {
      "Enabled": true,
      "MinTrajectoriesForSkill": 10,
      "SkillConfidenceThreshold": 0.8,
      "OnlineAdaptationRate": 0.01
    }
  }
}
```

---

## 13. Testing Strategy

### 13.1 Unit Tests
- Planner: Prompt formatting, plan parsing, replanning logic
- Executor: Tool execution, validation, error handling
- Memory: Storage, retrieval, vector search
- Safety: Rule evaluation, policy precedence

### 13.2 Integration Tests
- Full goal execution loop
- Multi-step plan with failures
- Skill learning from demonstrations
- Visual Scripting LLM node

### 13.3 Simulation Tests
- Scenario runner with agent
- Adversarial testing (unexpected game states)
- Long-running stability tests

---

## 14. Monitoring & Observability

### 14.1 Key Metrics

| Metric | Type | Description |
|--------|------|-------------|
| `kkbot_agent_decisions_total` | Counter | Decisions by agent/type/result |
| `kkbot_agent_decision_latency_ms` | Histogram | Decision latency |
| `kkbot_agent_plan_steps` | Histogram | Steps per plan |
| `kkbot_agent_tool_calls_total` | Counter | Tool calls by tool/result |
| `kkbot_agent_learning_sessions` | Counter | Learning sessions |
| `kkbot_agent_skill_usage` | Counter | Skill executions |
| `kkbot_agent_safety_violations` | Counter | Guardrail triggers |

### 14.2 Traces

- `agent.decide` - Full decision loop
- `agent.plan` - Planning phase
- `agent.execute` - Tool execution
- `agent.learn` - Learning phase

---

## 15. Security Considerations

1. **Prompt Injection Defense**
   - Sanitize all user inputs in prompts
   - Use structured output parsing (not regex)
   - Limit tool descriptions to prevent confusion

2. **Tool Permission Model**
   - Each tool declares required permissions
   - Agent inherits operator's permissions
   - Runtime validation before execution

3. **Action Approval**
   - High-risk actions require human approval
   - Approval workflow with timeout
   - Audit trail for all approved actions

4. **Data Privacy**
   - No PII in trajectories/memory
   - Encrypt sensitive game data
   - Configurable retention policies

---

## 16. Future Extensions

| Extension | Description |
|-----------|-------------|
| **Multi-Agent** | Agent teams with communication protocols |
| **Meta-Learning** | Learn to learn across games |
| **Sim2Real** | Train in simulator, deploy to real game |
| **Explainability** | Decision explanations for audit |
| **Federated Learning** | Learn across devices without sharing data |

---

*Document Version: 1.0*  
*Author: KKBOT Team*  
*Status: Implemented*