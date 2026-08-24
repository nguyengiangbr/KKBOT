// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using KKBOT.Domain.Models.Perception;
using KKBOT.AI.Abstractions;
using KKBOT.Observability.Tracing;

namespace KKBOT.Agent.Abstractions;

/// <summary>
/// Represents the type of agent.
/// </summary>
public enum AgentType
{
    /// <summary>Direct perception to action mapping</summary>
    Reactive,
    /// <summary>LLM-based planning with tool use</summary>
    Planning,
    /// <summary>RL/BC policy network</summary>
    Learning,
    /// <summary>Combination with arbitration</summary>
    Hybrid
}

/// <summary>
/// Goal types for agent objectives.
/// </summary>
public enum GoalType
{
    /// <summary>Reach a specific state</summary>
    Achieve,
    /// <summary>Maintain a condition</summary>
    Maintain,
    /// <summary>Avoid a state</summary>
    Avoid,
    /// <summary>Optimize a metric</summary>
    Optimize,
    /// <summary>Explore/discover</summary>
    Explore,
    /// <summary>Custom goal type</summary>
    Custom
}

/// <summary>
/// Safety level for goal execution.
/// </summary>
public enum SafetyLevel
{
    /// <summary>Allow risky actions</summary>
    Permissive,
    /// <summary>Standard safety checks</summary>
    Normal,
    /// <summary>Conservative, human approval for risks</summary>
    Strict,
    /// <summary>Every action requires approval</summary>
    Supervised
}

/// <summary>
/// Action types the agent can take.
/// </summary>
public enum ActionType
{
    /// <summary>Call a KKBOT tool</summary>
    ToolCall,
    /// <summary>Wait for condition</summary>
    Wait,
    /// <summary>Delegate to another agent/skill</summary>
    Delegate,
    /// <summary>Request human input</summary>
    AskHuman,
    /// <summary>Do nothing</summary>
    NoOp,
    /// <summary>Abort current goal</summary>
    Abort
}

/// <summary>
/// Risk levels for actions.
/// </summary>
public enum RiskLevel
{
    None,
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// How to handle step failures.
/// </summary>
public enum FailureHandling
{
    /// <summary>Retry this step</summary>
    Retry,
    /// <summary>Skip to next step</summary>
    Skip,
    /// <summary>Request new plan</summary>
    Replan,
    /// <summary>Abort entire plan</summary>
    Abort,
    /// <summary>Request human intervention</summary>
    AskHuman
}

/// <summary>
/// Skill categories.
/// </summary>
public enum SkillCategory
{
    Navigation,
    Combat,
    ResourceGathering,
    Questing,
    Social,
    Custom
}

/// <summary>
/// Feedback types for learning.
/// </summary>
public enum FeedbackType
{
    /// <summary>Preference between two trajectories</summary>
    Preference,
    /// <summary>Correction to a trajectory</summary>
    Correction,
    /// <summary>Numeric rating</summary>
    Rating,
    /// <summary>Binary success/failure</summary> 
    Binary
}

/// <summary>
/// Safety actions for guardrails.
/// </summary>
public enum SafetyAction
{
    Allow,
    Warn,
    RequireApproval,
    Block,
    LogOnly
}

/// <summary>
/// Core agent interface.
/// </summary>
public interface IGameAgent
{
    string AgentId { get; }
    string Name { get; }
    AgentType Type { get; }
    
    /// <summary>
    /// Main decision loop: perceive -> decide -> act.
    /// </summary>
    Task<AgentDecision> DecideAsync(PerceptionContext context, GameGoal goal, CancellationToken cancellationToken);
    
    /// <summary>
    /// Learn from experience.
    /// </summary>
    Task LearnAsync(Trajectory trajectory, RewardSignal reward, CancellationToken cancellationToken);
    
    /// <summary>
    /// Get agent capabilities.
    /// </summary>
    Task<AgentCapability[]> GetCapabilitiesAsync();
    
    /// <summary>
    /// Get current agent state.
    /// </summary>
    Task<AgentState> GetStateAsync();
    
    /// <summary>
    /// Set agent state.
    /// </summary>
    Task SetStateAsync(AgentState state);
    
    /// <summary>
    /// Configure agent.
    /// </summary>
    Task ConfigureAsync(AgentConfiguration config);
}

/// <summary>
/// A goal for the agent to achieve.
/// </summary>
public sealed record GameGoal
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Description { get; init; } = string.Empty;
    public GoalType Type { get; init; } = GoalType.Achieve;
    public Dictionary<string, object> Parameters { get; init; } = new();
    public GoalConstraints Constraints { get; init; } = new();
    public SuccessCriteria SuccessCriteria { get; init; } = new();
    public TimeSpan? Timeout { get; init; }
    public int Priority { get; init; } = 0;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public string CreatedBy { get; init; } = string.Empty;
}

/// <summary>
/// Constraints on goal execution.
/// </summary>
public sealed record GoalConstraints
{
    public TimeSpan? MaxDuration { get; init; }
    public ResourceBudget? ResourceLimits { get; init; }
    public string[] AllowedActions { get; init; } = Array.Empty<string>();
    public string[] ForbiddenActions { get; init; } = Array.Empty<string>();
    public bool RequireApproval { get; init; } = false;
    public SafetyLevel SafetyLevel { get; init; } = SafetyLevel.Normal;
}

/// <summary>
/// Resource budget for goal execution.
/// </summary>
public sealed record ResourceBudget
{
    public TimeSpan? MaxTime { get; init; }
    public long? MaxGold { get; init; }
    public int? MaxActions { get; init; }
    public Dictionary<string, long> CustomResources { get; init; } = new();
}

/// <summary>
/// Criteria for goal success.
/// </summary>
public sealed record SuccessCriteria
{
    public string[] RequiredConditions { get; init; } = Array.Empty<string>();
    public Dictionary<string, object> TargetValues { get; init; } = new();
    public double MinConfidence { get; init; } = 0.8;
    public TimeSpan? MaxTimeToComplete { get; init; }
}

/// <summary>
/// Agent decision output.
/// </summary>
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

/// <summary>
/// An action the agent can take.
/// </summary>
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

/// <summary>
/// Risk assessment for an action.
/// </summary>
public sealed record RiskAssessment
{
    public RiskLevel Level { get; init; } = RiskLevel.Low;
    public string[] PotentialIssues { get; init; } = Array.Empty<string>();
    public string[] Mitigations { get; init; } = Array.Empty<string>();
    public bool RequiresHumanApproval { get; init; } = false;
}

/// <summary>
/// Multi-step plan.
/// </summary>
public sealed record Plan
{
    public string PlanId { get; init; } = Guid.NewGuid().ToString("N");
    public string GoalId { get; init; } = string.Empty;
    public PlanStep[] Steps { get; init; } = Array.Empty<PlanStep>();
    public PlanMetadata Metadata { get; init; } = new();
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public string CreatedBy { get; init; } = string.Empty;
}

/// <summary>
/// A single step in a plan.
/// </summary>
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

/// <summary>
/// Condition for step success.
/// </summary>
public sealed record SuccessCondition
{
    public string Type { get; init; } = string.Empty; // "tool_result", "perception", "state", "custom"
    public Dictionary<string, object> Parameters { get; init; } = new();
    public string? CustomExpression { get; init; } // CEL expression
}

/// <summary>
/// Plan metadata.
/// </summary>
public sealed record PlanMetadata
{
    public int EstimatedTotalSteps { get; init; }
    public TimeSpan EstimatedDuration { get; init; }
    public RiskLevel OverallRisk { get; init; } = RiskLevel.Low;
    public Dictionary<string, object> Tags { get; init; } = new();
}

/// <summary>
/// Agent state.
/// </summary>
public sealed record AgentState
{
    public string AgentId { get; init; } = string.Empty;
    public AgentStatus Status { get; init; } = AgentStatus.Idle;
    public GameGoal? CurrentGoal { get; init; }
    public Plan? CurrentPlan { get; init; }
    public int CurrentStepIndex { get; init; } = -1;
    public Dictionary<string, object> WorkingMemory { get; init; } = new();
    public DateTimeOffset LastUpdated { get; init; } = DateTimeOffset.UtcNow;
    public string? ErrorMessage { get; init; }
}

public enum AgentStatus
{
    Idle,
    Planning,
    Executing,
    Waiting,
    Learning,
    Paused,
    Error,
    Stopped
}

/// <summary>
/// Agent configuration.
/// </summary>
public sealed record AgentConfiguration
{
    public string PlannerModel { get; init; } = "llama3.1:8b";
    public double PlannerTemperature { get; init; } = 0.3;
    public int MaxPlanSteps { get; init; } = 20;
    public int ReplanThreshold { get; init; } = 3;
    public TimeSpan DefaultStepTimeout { get; init; } = TimeSpan.FromMinutes(5);
    public bool EnableStreaming { get; init; } = false;
    public SafetyLevel DefaultSafetyLevel { get; init; } = SafetyLevel.Normal;
    public Dictionary<string, object> CustomSettings { get; init; } = new();
}

/// <summary>
/// Agent capabilities.
/// </summary>
public sealed record AgentCapability
{
    public string CapabilityId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public CapabilityType Type { get; init; }
    public Dictionary<string, object> Parameters { get; init; } = new();
    public double Proficiency { get; init; } = 1.0; // 0.0 - 1.0
}

public enum CapabilityType
{
    BuiltIn,      // Native KKBOT capability
    Learned,      // Learned skill
    Plugin,       // From game plugin
    Composite     // Combination of capabilities
}

/// <summary>
/// Agent context for planning.
/// </summary>
public sealed record AgentContext
{
    public PerceptionContext CurrentPerception { get; init; } = null!;
    public AgentState AgentState { get; init; } = null!;
    public GameState GameState { get; init; } = null!;
    public IReadOnlyList<AgentCapability> AvailableCapabilities { get; init; } = Array.Empty<AgentCapability>();
    public IReadOnlyList<ToolDefinition> AvailableTools { get; init; } = Array.Empty<ToolDefinition>();
    public ConversationHistory History { get; init; } = new();
    public Dictionary<string, object> ContextData { get; init; } = new();
}

/// <summary>
/// Game state snapshot.
/// </summary>
public sealed record GameState
{
    public string GameId { get; init; } = string.Empty;
    public Dictionary<string, object> StateData { get; init; } = new();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Conversation history for context.
/// </summary>
public sealed record ConversationHistory
{
    public ConversationTurn[] Turns { get; init; } = Array.Empty<ConversationTurn>();
    public int MaxTurns { get; init; } = 20;
}

public sealed record ConversationTurn
{
    public string Role { get; init; } = string.Empty; // "user", "assistant", "system", "tool"
    public string Content { get; init; } = string.Empty;
    public Dictionary<string, object>? ToolCalls { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Tool definition for function calling.
/// </summary>
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

/// <summary>
/// JSON Schema for tool parameters.
/// </summary>
public sealed record JsonSchema
{
    public string Type { get; init; } = "object";
    public Dictionary<string, JsonSchemaProperty> Properties { get; init; } = new();
    public string[] Required { get; init; } = Array.Empty<string>();
    public Dictionary<string, object>? AdditionalProperties { get; init; }
}

public sealed record JsonSchemaProperty
{
    public string Type { get; init; } = "string";
    public string Description { get; init; } = string.Empty;
    public string[]? Enum { get; init; }
    public object? Default { get; init; }
    public JsonSchema? Items { get; init; } // For arrays
}

/// <summary>
/// Tool execution result.
/// </summary>
public sealed record ToolResult
{
    public bool Success { get; init; }
    public object? Result { get; init; }
    public string? Error { get; init; }
    public TimeSpan Duration { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// Execution result for a plan step.
/// </summary>
public sealed record ExecutionResult
{
    public bool Success { get; init; }
    public ToolResult? ToolResult { get; init; }
    public TimeSpan Duration { get; init; }
    public int StepNumber { get; init; }
    public string? Error { get; init; }
    
    public static ExecutionResult Failed(string error) => new()
    {
        Success = false,
        Error = error,
        Duration = TimeSpan.Zero
    };
    
    public static ExecutionResult SuccessResult(ToolResult result, TimeSpan duration, int stepNumber) => new()
    {
        Success = true,
        ToolResult = result,
        Duration = duration,
        StepNumber = stepNumber
    };
}

/// <summary>
/// Plan execution state.
/// </summary>
public sealed record PlanExecutionState
{
    public string PlanId { get; init; } = string.Empty;
    public int CurrentStepIndex { get; init; } = 0;
    public PlanStepStatus[] StepStatuses { get; init; } = Array.Empty<PlanStepStatus>();
    public Dictionary<string, object> StepResults { get; init; } = new();
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; init; }
    public bool IsCompleted { get; init; }
    public bool IsFailed { get; init; }
    public string? FailureReason { get; init; }
}

public enum PlanStepStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Skipped,
    Retrying
}

/// <summary>
/// Trajectory for learning.
/// </summary>
public sealed record Trajectory
{
    public string TrajectoryId { get; init; } = Guid.NewGuid().ToString("N");
    public string GoalId { get; init; } = string.Empty;
    public string AgentId { get; init; } = string.Empty;
    public TrajectoryStep[] Steps { get; init; } = Array.Empty<TrajectoryStep>();
    public TimeSpan TotalDuration { get; init; }
    public bool Success { get; init; }
    public double TotalReward { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record TrajectoryStep
{
    public int StepNumber { get; init; }
    public PerceptionContext Perception { get; init; } = null!;
    public AgentAction Action { get; init; } = null!;
    public ToolResult Result { get; init; } = null!;
    public AgentState NextState { get; init; } = null!;
    public double Reward { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Reward signal for learning.
/// </summary>
public sealed record RewardSignal
{
    public double Value { get; init; }
    public RewardSource Source { get; init; } = RewardSource.Environment;
    public string? Description { get; init; }
    public Dictionary<string, double> Components { get; init; } = new();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

public enum RewardSource
{
    Environment,
    Human,
    SelfSupervised,
    Intrinsic
}

/// <summary>
/// Feedback sample for RLHF.
/// </summary>
public sealed record FeedbackSample
{
    public Trajectory Trajectory { get; init; } = null!;
    public RewardSignal HumanReward { get; init; } = null!;
    public string? HumanComment { get; init; }
    public FeedbackType Type { get; init; } = FeedbackType.Binary;
    public string AnnotatorId { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Episode for episodic memory.
/// </summary>
public sealed record Episode
{
    public string EpisodeId { get; init; } = Guid.NewGuid().ToString("N");
    public GameGoal Goal { get; init; } = null!;
    public Trajectory Trajectory { get; init; } = null!;
    public RewardSignal Outcome { get; init; } = null!;
    public Dictionary<string, object> Context { get; init; } = new();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Query for episode retrieval.
/// </summary>
public sealed record EpisodeQuery
{
    public string? GoalId { get; init; }
    public string? AgentId { get; init; }
    public DateTimeOffset? FromDate { get; init; }
    public DateTimeOffset? ToDate { get; init; }
    public bool? SuccessOnly { get; init; }
    public double? MinReward { get; init; }
    public string[] Tags { get; init; } = Array.Empty<string>();
    public string? SemanticQuery { get; init; } // For vector search
    public int Limit { get; init; } = 10;
}

/// <summary>
/// Semantic fact for semantic memory.
/// </summary>
public sealed record SemanticFact
{
    public string FactId { get; init; } = Guid.NewGuid().ToString("N");
    public string Subject { get; init; } = string.Empty;
    public string Predicate { get; init; } = string.Empty;
    public string Object { get; init; } = string.Empty;
    public double Confidence { get; init; } = 1.0;
    public string Source { get; init; } = string.Empty;
    public Dictionary<string, object> Metadata { get; init; } = new();
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAt { get; init; }
}

/// <summary>
/// Learned skill.
/// </summary>
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
    public string LearnedFrom { get; init; } = string.Empty;
    public Dictionary<string, object> Metadata { get; init; } = new();
}

public sealed record SkillParameter
{
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public object? DefaultValue { get; init; }
    public bool Required { get; init; } = true;
    public object? Validation { get; init; } // JSON Schema for validation
}

public sealed record SkillPrecondition
{
    public string Type { get; init; } = string.Empty; // "perception", "state", "resource", "custom"
    public Dictionary<string, object> Parameters { get; init; } = new();
    public string? CustomExpression { get; init; }
}

public sealed record SkillPostcondition
{
    public string Type { get; init; } = string.Empty;
    public Dictionary<string, object> Parameters { get; init; } = new();
    public string? CustomExpression { get; init; }
}

/// <summary>
/// Learning configuration.
/// </summary>
public sealed record LearningConfig
{
    public string Algorithm { get; init; } = "bc"; // bc, rlhf, ppo, sac
    public int BatchSize { get; init; } = 32;
    public double LearningRate { get; init; } = 0.001;
    public int Epochs { get; init; } = 10;
    public Dictionary<string, object> Hyperparameters { get; init; } = new();
    public string? CheckpointPath { get; init; }
    public bool EnableLogging { get; init; } = true;
}

/// <summary>
/// Learning result.
/// </summary>
public sealed record LearningResult
{
    public bool Success { get; init; }
    public string? ModelPath { get; init; }
    public Dictionary<string, double> Metrics { get; init; } = new();
    public Skill[] ExtractedSkills { get; init; } = Array.Empty<Skill>();
    public string? Error { get; init; }
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// Safety policy for guardrails.
/// </summary>
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

/// <summary>
/// Safety check result.
/// </summary>
public sealed record SafetyCheckResult
{
    public bool IsSafe { get; init; }
    public SafetyAction Action { get; init; } = SafetyAction.Allow;
    public string? Reason { get; init; }
    public SafetyRule? TriggeredRule { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// Guardrail violation.
/// </summary>
public sealed record GuardrailViolation
{
    public string ViolationId { get; init; } = Guid.NewGuid().ToString("N");
    public string AgentId { get; init; } = string.Empty;
    public string PolicyId { get; init; } = string.Empty;
    public string RuleId { get; init; } = string.Empty;
    public AgentAction Action { get; init; } = null!;
    public SafetyAction ActionTaken { get; init; }
    public string Message { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public bool Resolved { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }
}

/// <summary>
/// Learning interfaces.
/// </summary>
public interface IAgentPlanner
{
    Task<Plan> CreatePlanAsync(GameGoal goal, AgentContext context, CancellationToken cancellationToken);
    Task<Plan> ReplanAsync(Plan currentPlan, PlanExecutionState state, CancellationToken cancellationToken);
    Task<PlanStep?> NextStepAsync(Plan plan, PlanExecutionState state, CancellationToken cancellationToken);
}

public interface IToolExecutor
{
    Task<ToolResult> ExecuteAsync(string toolName, Dictionary<string, object> parameters, CancellationToken cancellationToken);
    IReadOnlyList<ToolDefinition> GetAvailableTools();
    Task<ToolDefinition?> GetToolDefinitionAsync(string toolName);
}

public interface IAgentExecutor
{
    Task<ExecutionResult> ExecuteStepAsync(PlanStep step, AgentContext context, CancellationToken cancellationToken);
    Task<ExecutionResult[]> ExecutePlanAsync(Plan plan, AgentContext context, CancellationToken cancellationToken);
}

public interface IAgentMemory
{
    Task StoreEpisodeAsync(Episode episode);
    Task<IReadOnlyList<Episode>> RetrieveEpisodesAsync(EpisodeQuery query, int limit = 10);
    Task StoreFactAsync(SemanticFact fact);
    Task<IReadOnlyList<SemanticFact>> QueryFactsAsync(string query, int limit = 10);
    Task UpdateWorkingMemoryAsync(string key, object value);
    Task<T?> GetWorkingMemoryAsync<T>(string key);
    Task StoreSkillAsync(Skill skill);
    Task<Skill?> GetSkillAsync(string skillId);
    Task<IReadOnlyList<Skill>> ListSkillsAsync(SkillCategory? category = null);
}

public interface IAgentLearner
{
    Task<LearningResult> LearnFromDemonstrationsAsync(IReadOnlyList<Trajectory> demonstrations, LearningConfig config, CancellationToken cancellationToken);
    Task<LearningResult> LearnFromFeedbackAsync(IReadOnlyList<FeedbackSample> feedback, LearningConfig config, CancellationToken cancellationToken);
    Task AdaptOnlineAsync(Trajectory trajectory, RewardSignal reward, CancellationToken cancellationToken);
    Task<Skill[]> ExtractSkillsAsync(IReadOnlyList<Trajectory> trajectories, CancellationToken cancellationToken);
}

public interface ISafetyGuardrails
{
    Task<SafetyCheckResult> CheckActionAsync(AgentAction action, AgentContext context);
    Task<SafetyCheckResult> CheckPlanAsync(Plan plan, AgentContext context);
    Task<GuardrailViolation[]> GetActiveViolationsAsync();
    Task RegisterPolicyAsync(SafetyPolicy policy);
}

public interface IAgentFactory
{
    Task<IGameAgent> CreateAgentAsync(AgentType type, AgentConfiguration config, CancellationToken cancellationToken);
    IReadOnlyList<AgentType> GetSupportedTypes();
}