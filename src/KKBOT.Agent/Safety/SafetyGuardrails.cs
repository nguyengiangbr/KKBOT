// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using CEL.NET;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KKBOT.Agent.Abstractions;
using KKBOT.Observability;

namespace KKBOT.Agent.Safety;

/// <summary>
/// Safety guardrails engine using CEL expressions.
/// </summary>
public sealed class SafetyGuardrails : ISafetyGuardrails
{
    private readonly ILogger<SafetyGuardrails> _logger;
    private readonly SafetyGuardrailsOptions _options;
    private readonly ConcurrentDictionary<string, SafetyPolicy> _policies = new();
    private readonly ConcurrentDictionary<string, CompiledRule> _compiledRules = new();

    public SafetyGuardrails(
        ILogger<SafetyGuardrails> logger,
        IOptions<SafetyGuardrailsOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        
        RegisterBuiltInPolicies();
    }

    public async Task<SafetyCheckResult> CheckActionAsync(AgentAction action, AgentContext context)
    {
        foreach (var policy in _policies.Values.Where(p => p.IsActive).OrderByDescending(p => p.Priority))
        {
            foreach (var rule in policy.Rules)
            {
                if (!rule.ApplicableAgents.Any() || rule.ApplicableAgents.Contains(context.AgentState.AgentId))
                {
                    if (action.Risk.Level >= rule.MinRiskLevel)
                    {
                        var result = await EvaluateRuleAsync(rule, action, context);
                        if (result)
                        {
                            _logger.LogWarning("Safety rule triggered: {PolicyId}/{RuleId} - {Message}", 
                                policy.PolicyId, rule.RuleId, rule.Message);
                            
                            return new SafetyCheckResult
                            {
                                IsSafe = false,
                                Action = rule.Action,
                                Reason = rule.Message,
                                TriggeredRule = rule
                            };
                        }
                    }
                }
            }
        }

        return new SafetyCheckResult { IsSafe = true, Action = SafetyAction.Allow };
    }

    public async Task<SafetyCheckResult> CheckPlanAsync(Plan plan, AgentContext context)
    {
        foreach (var step in plan.Steps)
        {
            var result = await CheckActionAsync(step.Action, context);
            if (!result.IsSafe && result.Action >= SafetyAction.RequireApproval)
            {
                return result;
            }
        }
        return new SafetyCheckResult { IsSafe = true, Action = SafetyAction.Allow };
    }

    public Task<GuardrailViolation[]> GetActiveViolationsAsync()
    {
        // In real implementation, query from persistent store
        return Task.FromResult(Array.Empty<GuardrailViolation>());
    }

    public Task RegisterPolicyAsync(SafetyPolicy policy)
    {
        _policies[policy.PolicyId] = policy;
        
        // Pre-compile rules
        foreach (var rule in policy.Rules)
        {
            var key = $"{policy.PolicyId}/{rule.RuleId}";
            try
            {
                var compiled = CEL.Compile(rule.Condition);
                _compiledRules[key] = new CompiledRule(rule, compiled);
                _logger.LogDebug("Compiled safety rule: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to compile safety rule: {Key}", key);
            }
        }
        
        return Task.CompletedTask;
    }

    private async Task<bool> EvaluateRuleAsync(SafetyRule rule, AgentAction action, AgentContext context)
    {
        var key = $"{rule.Condition.GetHashCode()}";
        if (!_compiledRules.TryGetValue(key, out var compiled))
        {
            // Try to find by policy/rule
            key = $"{rule.Condition}";
            if (!_compiledRules.TryGetValue(key, out compiled))
            {
                return false;
            }
        }

        try
        {
            var env = BuildEvaluationEnvironment(action, context);
            var result = compiled.Program.Evaluate(env);
            return result.AsBoolean();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to evaluate safety rule: {RuleId}", rule.RuleId);
            return false;
        }
    }

    private Dictionary<string, object> BuildEvaluationEnvironment(AgentAction action, AgentContext context)
    {
        return new Dictionary<string, object>
        {
            ["action"] = new
            {
                type = action.Type.ToString(),
                toolName = action.ToolName,
                parameters = action.Parameters,
                riskLevel = action.Risk.Level.ToString(),
                requiredPermissions = action.RequiredPermissions
            },
            ["context"] = new
            {
                agentId = context.AgentState.AgentId,
                status = context.AgentState.Status.ToString(),
                currentGoal = context.AgentState.CurrentGoal?.Description ?? "",
                workingMemory = context.AgentState.WorkingMemory,
                perception = context.CurrentPerception != null ? new
                {
                    detections = context.CurrentPerception.Detections?.Select(d => new { d.DetectorName, d.Confidence, d.IsMatch }).ToArray()
                } : null,
                gameState = context.GameState?.StateData ?? new Dictionary<string, object>()
            },
            ["time"] = DateTimeOffset.UtcNow
        };
    }

    private void RegisterBuiltInPolicies()
    {
        // Device Safety Policy
        RegisterPolicyAsync(new SafetyPolicy
        {
            PolicyId = "device-safety",
            Name = "Device Safety",
            Priority = 100,
            Description = "Prevent device damage and ensure stable operation",
            Rules = new[]
            {
                new SafetyRule
                {
                    RuleId = "max-apm",
                    Condition = "action.toolName == 'send_input' && context.workingMemory.apm > 60",
                    Action = SafetyAction.Block,
                    Message = "Actions per minute exceeded safe limit (60 APM)",
                    MinRiskLevel = RiskLevel.Medium
                },
                new SafetyRule
                {
                    RuleId = "no-input-during-dialog",
                    Condition = "context.perception.detections.exists(d, d.detectorName == 'system_dialog' && d.confidence > 0.8) && action.toolName == 'send_input'",
                    Action = SafetyAction.Block,
                    Message = "System dialog detected, blocking input",
                    MinRiskLevel = RiskLevel.High
                },
                new SafetyRule
                {
                    RuleId = "device-overheat",
                    Condition = "context.gameState.cpu_temp > 85 && action.riskLevel in ['High', 'Critical']",
                    Action = SafetyAction.RequireApproval,
                    Message = "Device temperature critical, high-risk actions require approval",
                    MinRiskLevel = RiskLevel.High
                }
            }
        }).Wait();

        // Game ToS Compliance Policy
        RegisterPolicyAsync(new SafetyPolicy
        {
            PolicyId = "tos-compliance",
            Name = "Game ToS Compliance",
            Priority = 200,
            Description = "Ensure compliance with game Terms of Service",
            Rules = new[]
            {
                new SafetyRule
                {
                    RuleId = "no-memory-read",
                    Condition = "action.toolName in ['read_memory', 'inject_code', 'hook_function']",
                    Action = SafetyAction.Block,
                    Message = "Memory reading/code injection prohibited by ToS",
                    MinRiskLevel = RiskLevel.Critical
                },
                new SafetyRule
                {
                    RuleId = "no-packet-manipulation",
                    Condition = "action.toolName in ['send_packet', 'modify_packet', 'intercept_traffic']",
                    Action = SafetyAction.Block,
                    Message = "Packet manipulation prohibited by ToS",
                    MinRiskLevel = RiskLevel.Critical
                },
                new SafetyRule
                {
                    RuleId = "human-like-delays",
                    Condition = "action.toolName == 'send_input' && action.parameters.duration_ms < 50",
                    Action = SafetyAction.Warn,
                    Message = "Input delays too short, may appear bot-like",
                    MinRiskLevel = RiskLevel.Low
                }
            }
        }).Wait();

        // Resource Protection Policy
        RegisterPolicyAsync(new SafetyPolicy
        {
            PolicyId = "resource-protection",
            Name = "Resource Protection",
            Priority = 50,
            Description = "Protect valuable in-game resources",
            Rules = new[]
            {
                new SafetyRule
                {
                    RuleId = "max-gold-spend",
                    Condition = "action.toolName == 'send_input' && context.workingMemory.gold_spent_hour > 100000",
                    Action = SafetyAction.RequireApproval,
                    Message = "Gold spend rate exceeds 100k/hour limit",
                    MinRiskLevel = RiskLevel.Medium
                },
                new SafetyRule
                {
                    RuleId = "no-sell-valuables",
                    Condition = "action.toolName == 'send_input' && context.perception.detections.exists(d, d.detectorName in ['legendary_item', 'unique_item'] && d.confidence > 0.9)",
                    Action = SafetyAction.Block,
                    Message = "Valuable item detected, blocking sell/trade actions",
                    MinRiskLevel = RiskLevel.High
                }
            }
        }).Wait();

        // Progress Protection Policy
        RegisterPolicyAsync(new SafetyPolicy
        {
            PolicyId = "progress-protection",
            Name = "Progress Protection",
            Priority = 150,
            Description = "Prevent actions that could lose game progress",
            Rules = new[]
            {
                new SafetyRule
                {
                    RuleId = "hardcore-death-prevention",
                    Condition = "context.gameState.hardcore == true && context.gameState.hp_percent < 20 && action.riskLevel in ['High', 'Critical']",
                    Action = SafetyAction.RequireApproval,
                    Message = "Hardcore character at low HP, high-risk actions need approval",
                    MinRiskLevel = RiskLevel.High
                },
                new SafetyRule
                {
                    RuleId = "no-delete-quest-items",
                    Condition = "action.toolName == 'send_input' && context.workingMemory.quest_items.exists(i, i.required == true)",
                    Action = SafetyAction.Block,
                    Message = "Quest-required items detected, preventing disposal",
                    MinRiskLevel = RiskLevel.High
                }
            }
        }).Wait();

        // Social Safety Policy
        RegisterPolicyAsync(new SafetyPolicy
        {
            PolicyId = "social-safety",
            Name = "Social Safety",
            Priority = 75,
            Description = "Prevent disruptive social behavior",
            Rules = new[]
            {
                new SafetyRule
                {
                    RuleId = "no-chat-spam",
                    Condition = "action.toolName == 'send_input' && action.parameters.type == 'text' && context.workingMemory.messages_sent_minute > 5",
                    Action = SafetyAction.Block,
                    Message = "Chat rate limit exceeded (5 messages/minute)",
                    MinRiskLevel = RiskLevel.Medium
                },
                new SafetyRule
                {
                    RuleId = "respect-party-privacy",
                    Condition = "context.gameState.in_party == true && action.toolName == 'send_input' && action.parameters.text.contains('@')",
                    Action = SafetyAction.Warn,
                    Message = "Mentions in party chat may be disruptive",
                    MinRiskLevel = RiskLevel.Low
                }
            }
        }).Wait();
    }

    private sealed class CompiledRule
    {
        public SafetyRule Rule { get; }
        public CEL.CompiledProgram Program { get; }

        public CompiledRule(SafetyRule rule, CEL.CompiledProgram program)
        {
            Rule = rule;
            Program = program;
        }
    }
}

/// <summary>
/// Safety guardrails options.
/// </summary>
public sealed class SafetyGuardrailsOptions
{
    public bool EnableBuiltInPolicies { get; set; } = true;
    public string PolicyStoragePath { get; set; } = "safety-policies";
    public TimeSpan EvaluationTimeout { get; set; } = TimeSpan.FromSeconds(5);
}