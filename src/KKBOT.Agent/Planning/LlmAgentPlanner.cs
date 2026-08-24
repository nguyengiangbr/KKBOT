// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Ollama;
using KKBOT.Agent.Abstractions;
using KKBOT.AI.Abstractions;
using KKBOT.Observability;
using KKBOT.Observability.Tracing;

namespace KKBOT.Agent.Planning;

/// <summary>
/// LLM-powered agent planner using Semantic Kernel.
/// </summary>
public sealed class LlmAgentPlanner : IAgentPlanner
{
    private readonly ILogger<LlmAgentPlanner> _logger;
    private readonly LlmPlannerOptions _options;
    private readonly IChatCompletionService _chatService;
    private readonly Kernel _kernel;
    private readonly IMetricsRegistry _metrics;
    private readonly IAiService _aiService;

    public LlmAgentPlanner(
        ILogger<LlmAgentPlanner> logger,
        IOptions<LlmPlannerOptions> options,
        IAiService aiService,
        IMetricsRegistry metrics)
    {
        _logger = logger;
        _options = options.Value;
        _aiService = aiService;
        _metrics = metrics;

        // Initialize Semantic Kernel with Ollama
        var builder = Kernel.CreateBuilder();
        builder.AddOllamaChatCompletion(
            modelId: _options.Model,
            endpoint: new Uri(_options.BaseUrl));
        _kernel = builder.Build();
        _chatService = _kernel.GetRequiredService<IChatCompletionService>();
    }

    public async Task<Plan> CreatePlanAsync(GameGoal goal, AgentContext context, CancellationToken cancellationToken)
    {
        using var activity = KkbotActivitySources.AI.StartActivity("agent.create_plan", ActivityKind.Internal);
        activity?.SetTag(KkbotTags.ModuleId, "agent-planner");
        
        var startTime = DateTime.UtcNow;
        
        try
        {
            _logger.LogInformation("Creating plan for goal: {GoalId} - {Description}", goal.Id, goal.Description);

            // Build system prompt
            var systemPrompt = BuildSystemPrompt(context);
            
            // Build user prompt with goal and context
            var userPrompt = BuildUserPrompt(goal, context);

            // Get plan from LLM
            var planJson = await GetPlanFromLlmAsync(systemPrompt, userPrompt, cancellationToken);
            
            // Parse and validate plan
            var plan = ParsePlan(planJson, goal.Id);
            
            // Validate plan
            var validation = ValidatePlan(plan, context);
            if (!validation.IsValid)
            {
                _logger.LogWarning("Plan validation failed, attempting repair: {Errors}", string.Join(", ", validation.Errors));
                plan = await RepairPlanAsync(plan, validation.Errors, goal, context, cancellationToken);
            }

            _metrics.IncrementAiInferences();
            _metrics.RecordAiInferenceLatency((DateTime.UtcNow - startTime).TotalMilliseconds);

            activity?.SetStatus(ActivityStatusCode.Ok);
            return plan;
        }
        catch (Exception ex)
        {
            _metrics.IncrementAiInferencesFailed();
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Failed to create plan for goal {GoalId}", goal.Id);
            throw;
        }
    }

    public async Task<Plan> ReplanAsync(Plan currentPlan, PlanExecutionState state, CancellationToken cancellationToken)
    {
        using var activity = KkbotActivitySources.AI.StartActivity("agent.replan", ActivityKind.Internal);
        
        _logger.LogInformation("Replanning for plan {PlanId} at step {StepIndex}", currentPlan.PlanId, state.CurrentStepIndex);

        var replanPrompt = $@"
The current plan has encountered an issue and needs to be replanned.

ORIGINAL PLAN:
{JsonSerializer.Serialize(currentPlan, new JsonSerializerOptions { WriteIndented = true })}

EXECUTION STATE:
- Current Step Index: {state.CurrentStepIndex}
- Completed Steps: {string.Join(", ", state.StepStatuses.Where(s => s == PlanStepStatus.Completed).Select(s => s.ToString()))}
- Failed Step: {state.StepStatuses.FirstOrDefault(s => s == PlanStepStatus.Failed)?.ToString() ?? "none"}
- Failure Reason: {state.FailureReason ?? "unknown"}
- Step Results: {JsonSerializer.Serialize(state.StepResults)}

Please create a new plan that addresses the failure and continues from the current state.
Return only the JSON plan.";

        var systemPrompt = BuildSystemPrompt(new AgentContext());
        var planJson = await GetPlanFromLlmAsync(systemPrompt, replanPrompt, cancellationToken);
        var newPlan = ParsePlan(planJson, currentPlan.GoalId);
        newPlan.Metadata.Tags["replanned"] = true;
        newPlan.Metadata.Tags["originalPlanId"] = currentPlan.PlanId;

        return newPlan;
    }

    public async Task<PlanStep?> NextStepAsync(Plan plan, PlanExecutionState state, CancellationToken cancellationToken)
    {
        if (state.CurrentStepIndex >= plan.Steps.Length)
            return null;

        var nextStep = plan.Steps[state.CurrentStepIndex];
        
        // Check dependencies
        if (nextStep.Dependencies.Length > 0)
        {
            var depsMet = nextStep.Dependencies.All(depId => 
                state.StepStatuses.Any(s => s == PlanStepStatus.Completed && plan.Steps.Any(ps => ps.StepId == depId)));
            
            if (!depsMet)
                return null; // Wait for dependencies
        }

        return nextStep;
    }

    private string BuildSystemPrompt(AgentContext context)
    {
        var toolsJson = JsonSerializer.Serialize(context.AvailableTools, new JsonSerializerOptions { WriteIndented = true });
        var capabilitiesJson = JsonSerializer.Serialize(context.AvailableCapabilities, new JsonSerializerOptions { WriteIndented = true });

        return $@"
You are an expert game automation agent. Your task is to create detailed, executable plans to achieve game objectives.

## AVAILABLE TOOLS
{toolsJson}

## AVAILABLE CAPABILITIES
{capabilitiesJson}

## PLANNING RULES
1. Each step must specify an exact tool name from the available tools list
2. Parameters must match the tool's JSON schema exactly
3. Include clear success conditions for each step
4. Specify failure handling: retry, skip, replan, abort, or ask_human
5. Estimate duration and risk level for each step
6. Max {_options.MaxPlanSteps} steps per plan
7. Consider safety constraints and required permissions
8. Use dependencies to enable parallel execution where possible

## OUTPUT FORMAT
Return ONLY a JSON object matching this schema:
{{
  ""planId"": ""string"",
  ""goalId"": ""string"",
  ""steps"": [
    {{
      ""stepNumber"": 1,
      ""stepId"": ""string"",
      ""description"": ""string"",
      ""action"": {{
        ""type"": ""ToolCall"",
        ""toolName"": ""string"",
        ""parameters"": {{}},
        ""estimatedDuration"": ""00:00:30"",
        ""risk"": {{ ""level"": ""Low"", ""potentialIssues"": [], ""mitigations"": [], ""requiresHumanApproval"": false }},
        ""requiredPermissions"": []
      }},
      ""dependencies"": [],
      ""successCondition"": {{ ""type"": ""tool_result"", ""parameters"": {{ ""success"": true }} }},
      ""failureHandling"": ""Retry"",
      ""timeout"": ""00:05:00"",
      ""maxRetries"": 2
    }}
  ],
  ""metadata"": {{
    ""estimatedTotalSteps"": 1,
    ""estimatedDuration"": ""00:05:00"",
    ""overallRisk"": ""Low"",
    ""tags"": {{}}
  }}
}}";
    }

    private string BuildUserPrompt(GameGoal goal, AgentContext context)
    {
        var perceptionSummary = context.CurrentPerception != null 
            ? $"Detected: {string.Join(", ", context.CurrentPerception.Detections?.Select(d => $"{d.DetectorName} ({d.Confidence:P})") ?? Array.Empty<string>())}"
            : "No perception data";

        var gameStateJson = JsonSerializer.Serialize(context.GameState?.StateData ?? new Dictionary<string, object>());

        return $@"
## GOAL
- ID: {goal.Id}
- Type: {goal.Type}
- Description: {goal.Description}
- Parameters: {JsonSerializer.Serialize(goal.Parameters)}
- Constraints: {JsonSerializer.Serialize(goal.Constraints)}
- Success Criteria: {JsonSerializer.Serialize(goal.SuccessCriteria)}

## CURRENT CONTEXT
- Agent Status: {context.AgentState.Status}
- Current Step: {context.AgentState.CurrentStepIndex}
- Perception: {perceptionSummary}
- Game State: {gameStateJson}
- Working Memory: {JsonSerializer.Serialize(context.AgentState.WorkingMemory)}

## RECENT HISTORY
{string.Join("
", context.History.Turns.TakeLast(10).Select(t => $"{t.Role}: {t.Content}"))}

Create a plan to achieve this goal.";
    }

    private async Task<string> GetPlanFromLlmAsync(string systemPrompt, string userPrompt, CancellationToken ct)
    {
        var history = new ChatHistory();
        history.AddSystemMessage(systemPrompt);
        history.AddUserMessage(userPrompt);

        var settings = new OllamaPromptExecutionSettings
        {
            Temperature = _options.Temperature,
            MaxTokens = _options.MaxTokens,
            TopP = 0.9,
            FrequencyPenalty = 0.1,
            PresencePenalty = 0.1
        };

        var response = await _chatService.GetChatMessageContentAsync(history, settings, kernel: _kernel, cancellationToken: ct);
        return response.Content ?? throw new InvalidOperationException("Empty response from LLM");
    }

    private Plan ParsePlan(string json, string goalId)
    {
        // Extract JSON from potential markdown code blocks
        var cleanJson = ExtractJson(json);
        
        var plan = JsonSerializer.Deserialize<Plan>(cleanJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        }) ?? throw new InvalidOperationException("Failed to parse plan JSON");

        plan.GoalId = goalId;
        return plan;
    }

    private string ExtractJson(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start)
            return text.Substring(start, end - start + 1);
        return text;
    }

    private PlanValidationResult ValidatePlan(Plan plan, AgentContext context)
    {
        var errors = new List<string>();

        if (plan.Steps.Length == 0)
            errors.Add("Plan has no steps");

        if (plan.Steps.Length > _options.MaxPlanSteps)
            errors.Add($"Plan exceeds max steps ({_options.MaxPlanSteps})");

        var stepIds = new HashSet<string>();
        foreach (var step in plan.Steps)
        {
            if (!stepIds.Add(step.StepId))
                errors.Add($"Duplicate step ID: {step.StepId}");

            // Validate tool exists
            var tool = context.AvailableTools.FirstOrDefault(t => t.Name == step.Action.ToolName);
            if (tool == null)
                errors.Add($"Step {step.StepNumber}: Unknown tool '{step.Action.ToolName}'");
            else
            {
                // Validate permissions
                var missingPerms = step.Action.RequiredPermissions.Except(context.AgentState.WorkingMemory.Keys.Select(k => k.ToString())).ToArray();
                // Note: Actual permission check happens at execution time
            }
        }

        // Check for circular dependencies
        if (HasCircularDependencies(plan.Steps))
            errors.Add("Plan has circular dependencies");

        return new PlanValidationResult(errors.Count == 0, errors);
    }

    private bool HasCircularDependencies(PlanStep[] steps)
    {
        var graph = steps.ToDictionary(s => s.StepId, s => s.Dependencies);
        var visited = new HashSet<string>();
        var recStack = new HashSet<string>();

        bool HasCycle(string node)
        {
            if (!graph.ContainsKey(node)) return false;
            if (recStack.Contains(node)) return true;
            if (visited.Contains(node)) return false;

            visited.Add(node);
            recStack.Add(node);

            foreach (var dep in graph[node])
            {
                if (HasCycle(dep)) return true;
            }

            recStack.Remove(node);
            return false;
        }

        return steps.Any(s => HasCycle(s.StepId));
    }

    private async Task<Plan> RepairPlanAsync(Plan plan, IReadOnlyList<string> errors, GameGoal goal, AgentContext context, CancellationToken ct)
    {
        var repairPrompt = $@"
The following plan has validation errors:
{string.Join("
", errors.Select(e => $"- {e}"))}

Original Plan:
{JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true })}

Please fix the plan and return corrected JSON only.";

        var systemPrompt = BuildSystemPrompt(context);
        var planJson = await GetPlanFromLlmAsync(systemPrompt, repairPrompt, ct);
        return ParsePlan(planJson, goal.Id);
    }

    private sealed record PlanValidationResult(bool IsValid, IReadOnlyList<string> Errors);
}

/// <summary>
/// Options for LLM planner.
/// </summary>
public sealed class LlmPlannerOptions
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "llama3.1:8b";
    public double Temperature { get; set; } = 0.3;
    public int MaxTokens { get; set; } = 4096;
    public int MaxPlanSteps { get; set; } = 20;
    public int ReplanThreshold { get; set; } = 3;
}