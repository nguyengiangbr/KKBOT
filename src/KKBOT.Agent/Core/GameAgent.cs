// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KKBOT.Agent.Abstractions;
using KKBOT.AI.Abstractions;
using KKBOT.Observability;
using KKBOT.Observability.Tracing;

namespace KKBOT.Agent.Core;

/// <summary>
/// Main game agent implementation.
/// </summary>
public sealed class GameAgent : IGameAgent
{
    private readonly ILogger<GameAgent> _logger;
    private readonly GameAgentOptions _options;
    private readonly IAgentPlanner _planner;
    private readonly IToolExecutor _toolExecutor;
    private readonly IAgentExecutor _executor;
    private readonly IAgentMemory _memory;
    private readonly IAgentLearner? _learner;
    private readonly ISafetyGuardrails _safety;
    private readonly IMetricsRegistry _metrics;
    private readonly IAiService _aiService;
    
    private readonly ConcurrentDictionary<string, AgentState> _agentStates = new();
    private readonly SemaphoreSlim _executionLock = new(1, 1);

    public string AgentId { get; }
    public string Name { get; }
    public AgentType Type { get; }

    public GameAgent(
        string agentId,
        string name,
        AgentType type,
        ILogger<GameAgent> logger,
        IOptions<GameAgentOptions> options,
        IAgentPlanner planner,
        IToolExecutor toolExecutor,
        IAgentExecutor executor,
        IAgentMemory memory,
        ISafetyGuardrails safety,
        IMetricsRegistry metrics,
        IAiService aiService,
        IAgentLearner? learner = null)
    {
        AgentId = agentId;
        Name = name;
        Type = type;
        _logger = logger;
        _options = options.Value;
        _planner = planner;
        _toolExecutor = toolExecutor;
        _executor = executor;
        _memory = memory;
        _learner = learner;
        _safety = safety;
        _metrics = metrics;
        _aiService = aiService;

        _agentStates[agentId] = new AgentState
        {
            AgentId = agentId,
            Status = AgentStatus.Idle,
            WorkingMemory = new Dictionary<string, object>()
        };
    }

    public async Task<AgentDecision> DecideAsync(PerceptionContext context, GameGoal goal, CancellationToken cancellationToken)
    {
        using var activity = KkbotActivitySources.AI.StartActivity("agent.decide", ActivityKind.Internal);
        activity?.SetTag(KkbotTags.ModuleId, "game-agent");
        activity?.SetTag(KkbotTags.InstanceId, AgentId);
        
        var startTime = DateTime.UtcNow;
        
        try
        {
            await _executionLock.WaitAsync(cancellationToken);
            
            var state = _agentStates.GetOrAdd(AgentId, _ => new AgentState { AgentId = AgentId, Status = AgentStatus.Idle });
            
            _logger.LogInformation("[{AgentId}] Deciding for goal: {GoalId} - {Description}", AgentId, goal.Id, goal.Description);

            // Update state
            state.Status = AgentStatus.Planning;
            state.CurrentGoal = goal;
            state.LastUpdated = DateTimeOffset.UtcNow;

            // Build agent context
            var agentContext = await BuildAgentContextAsync(context, state, cancellationToken);

            // Safety check on goal
            var goalSafety = await _safety.CheckPlanAsync(new Plan { Steps = Array.Empty<PlanStep>() }, agentContext);
            if (!goalSafety.IsSafe && goalSafety.Action == SafetyAction.Block)
            {
                throw new InvalidOperationException($"Goal blocked by safety: {goalSafety.Reason}");
            }

            // Create plan
            var plan = await _planner.CreatePlanAsync(goal, agentContext, cancellationToken);
            state.CurrentPlan = plan;
            state.CurrentStepIndex = 0;

            // Determine first action
            var nextStep = await _planner.NextStepAsync(plan, new PlanExecutionState
            {
                PlanId = plan.PlanId,
                CurrentStepIndex = 0,
                StepStatuses = plan.Steps.Select(_ => PlanStepStatus.Pending).ToArray()
            }, cancellationToken);

            if (nextStep == null)
            {
                return new AgentDecision
                {
                    Action = new AgentAction { Type = ActionType.NoOp },
                    Confidence = 1.0,
                    Reasoning = "No steps in plan or all dependencies unmet",
                    Plan = plan
                };
            }

            // Safety check on action
            var actionSafety = await _safety.CheckActionAsync(nextStep.Action, agentContext);
            if (!actionSafety.IsSafe)
            {
                if (actionSafety.Action == SafetyAction.Block)
                {
                    return new AgentDecision
                    {
                        Action = new AgentAction { Type = ActionType.Abort },
                        Confidence = 1.0,
                        Reasoning = $"Action blocked by safety: {actionSafety.Reason}",
                        Plan = plan,
                        Metadata = new Dictionary<string, object> { ["safety_blocked"] = true, ["safety_reason"] = actionSafety.Reason ?? "" }
                    };
                }
                else if (actionSafety.Action == SafetyAction.RequireApproval)
                {
                    return new AgentDecision
                    {
                        Action = new AgentAction { Type = ActionType.AskHuman },
                        Confidence = 0.5,
                        Reasoning = $"Action requires human approval: {actionSafety.Reason}",
                        Plan = plan,
                        Metadata = new Dictionary<string, object> { ["requires_approval"] = true, ["approval_reason"] = actionSafety.Reason ?? "" }
                    };
                }
            }

            var decision = new AgentDecision
            {
                Action = nextStep.Action,
                Confidence = CalculateConfidence(nextStep, agentContext),
                Reasoning = $"Executing step {nextStep.StepNumber}/{plan.Steps.Length}: {nextStep.Description}",
                Plan = plan,
                TraceId = activity?.TraceId.ToHexString(),
                SpanId = activity?.SpanId.ToHexString()
            };

            state.Status = AgentStatus.Executing;
            state.LastUpdated = DateTimeOffset.UtcNow;

            _metrics.IncrementAiInferences();
            _metrics.RecordAiInferenceLatency((DateTime.UtcNow - startTime).TotalMilliseconds);

            activity?.SetStatus(ActivityStatusCode.Ok);
            return decision;
        }
        catch (Exception ex)
        {
            _metrics.IncrementAiInferencesFailed();
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "[{AgentId}] Decision failed for goal {GoalId}", AgentId, goal.Id);
            throw;
        }
        finally
        {
            _executionLock.Release();
        }
    }

    public async Task LearnAsync(Trajectory trajectory, RewardSignal reward, CancellationToken cancellationToken)
    {
        if (_learner == null)
        {
            _logger.LogWarning("[{AgentId}] Learning requested but no learner configured", AgentId);
            return;
        }

        using var activity = KkbotActivitySources.AI.StartActivity("agent.learn", ActivityKind.Internal);
        
        try
        {
            _logger.LogInformation("[{AgentId}] Learning from trajectory {TrajectoryId} with reward {Reward}", 
                AgentId, trajectory.TrajectoryId, reward.Value);

            // Store episode in memory
            var episode = new Episode
            {
                Goal = new GameGoal { Id = trajectory.GoalId },
                Trajectory = trajectory,
                Outcome = reward,
                Context = new Dictionary<string, object> { ["agentId"] = AgentId }
            };
            await _memory.StoreEpisodeAsync(episode);

            // Learn
            var config = new LearningConfig
            {
                Algorithm = _options.LearningAlgorithm,
                BatchSize = _options.LearningBatchSize,
                LearningRate = _options.LearningRate,
                Epochs = _options.LearningEpochs
            };

            var result = await _learner.LearnFromDemonstrationsAsync(
                new[] { trajectory }, config, cancellationToken);

            if (result.Success && result.ExtractedSkills.Length > 0)
            {
                foreach (var skill in result.ExtractedSkills)
                {
                    await _memory.StoreSkillAsync(skill);
                    _logger.LogInformation("[{AgentId}] Learned new skill: {SkillName} ({SkillId})", AgentId, skill.Name, skill.SkillId);
                }
            }

            _metrics.IncrementAiInferences(); // Reuse metric for learning sessions
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{AgentId}] Learning failed", AgentId);
        }
        finally
        {
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
    }

    public async Task<AgentCapability[]> GetCapabilitiesAsync()
    {
        var builtIn = GetBuiltInCapabilities();
        var skills = await _memory.ListSkillsAsync();
        var learnedCapabilities = skills.Select(s => new AgentCapability
        {
            CapabilityId = s.SkillId,
            Name = s.Name,
            Description = s.Description,
            Type = CapabilityType.Learned,
            Parameters = s.Parameters.ToDictionary(p => p.Name, p => (object)p.DefaultValue ?? ""),
            Proficiency = s.SuccessRate
        }).ToArray();

        return builtIn.Concat(learnedCapabilities).ToArray();
    }

    public Task<AgentState> GetStateAsync()
    {
        _agentStates.TryGetValue(AgentId, out var state);
        return Task.FromResult(state ?? new AgentState { AgentId = AgentId, Status = AgentStatus.Idle });
    }

    public Task SetStateAsync(AgentState state)
    {
        _agentStates[AgentId] = state with { AgentId = AgentId, LastUpdated = DateTimeOffset.UtcNow };
        return Task.CompletedTask;
    }

    public async Task ConfigureAsync(AgentConfiguration config)
    {
        // Update planner options
        // Update safety policies
        // Update memory settings
        _logger.LogInformation("[{AgentId}] Configuration updated", AgentId);
    }

    private async Task<AgentContext> BuildAgentContextAsync(PerceptionContext perception, AgentState state, CancellationToken ct)
    {
        var capabilities = await GetCapabilitiesAsync();
        var tools = _toolExecutor.GetAvailableTools();

        return new AgentContext
        {
            CurrentPerception = perception,
            AgentState = state,
            GameState = new GameState 
            { 
                GameId = "current", 
                StateData = state.WorkingMemory.ToDictionary(k => k.Key, k => k.Value) 
            },
            AvailableCapabilities = capabilities,
            AvailableTools = tools,
            History = state.WorkingMemory.TryGetValue("history", out var hist) && hist is ConversationHistory h 
                ? h 
                : new ConversationHistory()
        };
    }

    private double CalculateConfidence(PlanStep step, AgentContext context)
    {
        var baseConfidence = 0.8;
        
        // Reduce confidence for high-risk actions
        if (step.Action.Risk.Level >= RiskLevel.High) baseConfidence -= 0.2;
        
        // Reduce if tool not available
        var tool = context.AvailableTools.FirstOrDefault(t => t.Name == step.Action.ToolName);
        if (tool == null) baseConfidence -= 0.3;
        
        // Increase for learned skills
        if (step.Action.ToolName == "execute_skill") baseConfidence += 0.1;
        
        return Math.Clamp(baseConfidence, 0.1, 1.0);
    }

    private AgentCapability[] GetBuiltInCapabilities()
    {
        return new[]
        {
            new AgentCapability { CapabilityId = "capture", Name = "Screen Capture", Type = CapabilityType.BuiltIn, Proficiency = 1.0 },
            new AgentCapability { CapabilityId = "detect", Name = "Template Detection", Type = CapabilityType.BuiltIn, Proficiency = 0.9 },
            new AgentCapability { CapabilityId = "input", Name = "Device Input", Type = CapabilityType.BuiltIn, Proficiency = 1.0 },
            new AgentCapability { CapabilityId = "ocr", Name = "Text Recognition", Type = CapabilityType.BuiltIn, Proficiency = 0.85 },
            new AgentCapability { CapabilityId = "vision", Name = "AI Vision", Type = CapabilityType.BuiltIn, Proficiency = 0.8 },
            new AgentCapability { CapabilityId = "wait", Name = "Condition Waiting", Type = CapabilityType.BuiltIn, Proficiency = 1.0 }
        };
    }
}

/// <summary>
/// Agent executor implementation.
/// </summary>
public sealed class AgentExecutor : IAgentExecutor
{
    private readonly ILogger<AgentExecutor> _logger;
    private readonly IToolExecutor _toolExecutor;
    private readonly ISafetyGuardrails _safety;
    private readonly IMetricsRegistry _metrics;

    public AgentExecutor(
        ILogger<AgentExecutor> logger,
        IToolExecutor toolExecutor,
        ISafetyGuardrails safety,
        IMetricsRegistry metrics)
    {
        _logger = logger;
        _toolExecutor = toolExecutor;
        _safety = safety;
        _metrics = metrics;
    }

    public async Task<ExecutionResult> ExecuteStepAsync(PlanStep step, AgentContext context, CancellationToken cancellationToken)
    {
        using var activity = KkbotActivitySources.AI.StartActivity($"agent.execute_step.{step.StepNumber}", ActivityKind.Internal);
        
        var startTime = DateTime.UtcNow;
        
        try
        {
            _logger.LogInformation("Executing step {StepNumber}: {Description}", step.StepNumber, step.Description);

            // Pre-execution safety check
            var safetyCheck = await _safety.CheckActionAsync(step.Action, context);
            if (!safetyCheck.IsSafe && safetyCheck.Action == SafetyAction.Block)
            {
                return ExecutionResult.Failed($"Step blocked by safety: {safetyCheck.Reason}");
            }

            // Execute with retries
            Exception? lastException = null;
            for (int attempt = 0; attempt <= step.MaxRetries; attempt++)
            {
                try
                {
                    var result = await _toolExecutor.ExecuteAsync(step.Action.ToolName, step.Action.Parameters, cancellationToken);
                    
                    // Validate success condition
                    var success = await ValidateSuccessConditionAsync(step.SuccessCondition, result, context, cancellationToken);
                    
                    if (success && result.Success)
                    {
                        _logger.LogInformation("Step {StepNumber} completed successfully", step.StepNumber);
                        return ExecutionResult.SuccessResult(result, DateTime.UtcNow - startTime, step.StepNumber);
                    }
                    
                    if (!result.Success)
                    {
                        lastException = new InvalidOperationException(result.Error ?? "Tool failed");
                        _logger.LogWarning("Step {StepNumber} failed (attempt {Attempt}/{MaxRetries}): {Error}", 
                            step.StepNumber, attempt + 1, step.MaxRetries + 1, result.Error);
                    }
                    else
                    {
                        _logger.LogWarning("Step {StepNumber} success condition not met (attempt {Attempt}/{MaxRetries})", 
                            step.StepNumber, attempt + 1, step.MaxRetries + 1);
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogWarning(ex, "Step {StepNumber} threw exception (attempt {Attempt}/{MaxRetries})", 
                        step.StepNumber, attempt + 1, step.MaxRetries + 1);
                }

                if (attempt < step.MaxRetries)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt) * 1000), cancellationToken);
                }
            }

            return ExecutionResult.Failed($"Step failed after {step.MaxRetries + 1} attempts: {lastException?.Message}");
        }
        finally
        {
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
    }

    public async Task<ExecutionResult[]> ExecutePlanAsync(Plan plan, AgentContext context, CancellationToken cancellationToken)
    {
        var results = new List<ExecutionResult>();
        var stepStatuses = new Dictionary<string, PlanStepStatus>();
        
        // Initialize all steps as pending
        foreach (var step in plan.Steps)
        {
            stepStatuses[step.StepId] = PlanStepStatus.Pending;
        }

        for (int i = 0; i < plan.Steps.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var step = plan.Steps[i];
            
            // Check dependencies
            if (step.Dependencies.Length > 0)
            {
                var depsMet = step.Dependencies.All(depId => stepStatuses.GetValueOrDefault(depId) == PlanStepStatus.Completed);
                if (!depsMet)
                {
                    _logger.LogWarning("Step {StepNumber} dependencies not met, skipping", step.StepNumber);
                    results.Add(ExecutionResult.Failed("Dependencies not met"));
                    stepStatuses[step.StepId] = PlanStepStatus.Skipped;
                    continue;
                }
            }

            stepStatuses[step.StepId] = PlanStepStatus.Running;
            var result = await ExecuteStepAsync(step, context, cancellationToken);
            results.Add(result);
            
            if (result.Success)
            {
                stepStatuses[step.StepId] = PlanStepStatus.Completed;
            }
            else
            {
                stepStatuses[step.StepId] = PlanStepStatus.Failed;
                
                // Handle failure based on step's failure handling
                switch (step.FailureHandling)
                {
                    case FailureHandling.Abort:
                        _logger.LogError("Step {StepNumber} failed, aborting plan", step.StepNumber);
                        return results.ToArray();
                    case FailureHandling.Replan:
                        _logger.LogWarning("Step {StepNumber} failed, replanning needed", step.StepNumber);
                        // In real implementation, trigger replan
                        return results.ToArray();
                    case FailureHandling.AskHuman:
                        _logger.LogWarning("Step {StepNumber} failed, human intervention needed", step.StepNumber);
                        return results.ToArray();
                    case FailureHandling.Skip:
                        _logger.LogWarning("Step {StepNumber} failed, skipping", step.StepNumber);
                        continue;
                    case FailureHandling.Retry:
                    default:
                        // Already handled by retries in ExecuteStepAsync
                        break;
                }
            }
        }

        return results.ToArray();
    }

    private async Task<bool> ValidateSuccessConditionAsync(SuccessCondition condition, ToolResult result, AgentContext context, CancellationToken ct)
    {
        if (!result.Success) return false;

        return condition.Type switch
        {
            "tool_result" => result.Success && (condition.Parameters.GetValueOrDefault("success") as bool? ?? true) == result.Success,
            "perception" => await ValidatePerceptionConditionAsync(condition, context, ct),
            "state" => ValidateStateCondition(condition, context),
            "custom" => EvaluateCustomCondition(condition, context),
            _ => true
        };
    }

    private Task<bool> ValidatePerceptionConditionAsync(SuccessCondition condition, AgentContext context, CancellationToken ct)
    {
        // In real implementation, run perception and check conditions
        return Task.FromResult(true);
    }

    private bool ValidateStateCondition(SuccessCondition condition, AgentContext context)
    {
        // Check game state conditions
        return true;
    }

    private bool EvaluateCustomCondition(SuccessCondition condition, AgentContext context)
    {
        // Evaluate CEL expression
        return true;
    }
}

/// <summary>
/// Game agent options.
/// </summary>
public sealed class GameAgentOptions
{
    public string LearningAlgorithm { get; set; } = "bc";
    public int LearningBatchSize { get; set; } = 32;
    public double LearningRate { get; set; } = 0.001;
    public int LearningEpochs { get; set; } = 10;
    public TimeSpan DefaultDecisionTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public bool EnableLearning { get; set; } = true;
    public double DefaultConfidenceThreshold { get; set; } = 0.7;
}