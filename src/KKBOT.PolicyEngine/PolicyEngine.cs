// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KKBOT.PolicyEngine;

/// <summary>
/// Policy engine for self-correction and automated decision making.
/// </summary>
public sealed class PolicyEngine
{
    private readonly ILogger<PolicyEngine> _logger;
    private readonly PolicyEngineOptions _options;
    private readonly List<Policy> _policies = new();
    private readonly object _lock = new();

    public PolicyEngine(
        ILogger<PolicyEngine> logger,
        IOptions<PolicyEngineOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        RegisterDefaultPolicies();
    }

    /// <summary>
    /// Registers a policy.
    /// </summary>
    public void RegisterPolicy(Policy policy)
    {
        lock (_lock)
        {
            _policies.Add(policy);
            _policies.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }
    }

    /// <summary>
    /// Evaluates all policies against the given context.
    /// </summary>
    public async Task<PolicyEvaluationResult> EvaluateAsync(PolicyContext context, CancellationToken cancellationToken = default)
    {
        var results = new List<PolicyResult>();
        var triggeredPolicies = new List<Policy>();

        foreach (var policy in _policies)
        {
            if (!policy.Enabled) continue;
            
            try
            {
                var matches = await policy.Condition.EvaluateAsync(context, cancellationToken);
                if (matches)
                {
                    triggeredPolicies.Add(policy);
                    var result = await policy.ExecuteAsync(context, cancellationToken);
                    results.Add(result);
                    
                    _logger.LogInformation("Policy '{PolicyName}' triggered for {ContextKey}", policy.Name, context.InstanceId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Policy '{PolicyName}' evaluation failed", policy.Name);
                results.Add(PolicyResult.Failure(policy.Name, ex.Message));
            }
        }

        return new PolicyEvaluationResult
        {
            ContextKey = context.InstanceId,
            TriggeredPolicies = triggeredPolicies,
            Results = results,
            EvaluatedAt = DateTimeOffset.UtcNow
        };
    }

    private void RegisterDefaultPolicies()
    {
        // Policy: Auto-restart on crash
        _policies.Add(new Policy
        {
            Name = "AutoRestartOnCrash",
            Description = "Automatically restart instance after unexpected termination",
            Priority = 100,
            Enabled = true,
            Condition = new PolicyCondition
            {
                EvaluateAsync = async (ctx, ct) => 
                {
                    return ctx.LastExitCode != 0 && ctx.RestartCount < ctx.MaxRestarts;
                }
            },
            ExecuteAsync = async (ctx, ct) => 
            {
                await Task.Delay(5000); // Wait before restart
                return PolicyResult.Success("AutoRestartOnCrash", "Instance scheduled for restart");
            }
        });

        // Policy: Throttle on high CPU
        _policies.Add(new Policy
        {
            Name = "ThrottleOnHighCPU",
            Description = "Reduce instance count when CPU usage is high",
            Priority = 90,
            Enabled = true,
            Condition = new PolicyCondition
            {
                EvaluateAsync = async (ctx, ct) => 
                {
                    return ctx.CpuUsagePercent > 85 && ctx.ActiveInstances > 1;
                }
            },
            ExecuteAsync = async (ctx, ct) => 
            {
                var toStop = Math.Max(1, ctx.ActiveInstances / 2);
                return PolicyResult.Success("ThrottleOnHighCPU", "Throttling " + toStop + " instances due to high CPU");
            }
        });

        // Policy: Alert on memory leak
        _policies.Add(new Policy
        {
            Name = "AlertOnMemoryLeak",
            Description = "Alert when memory growth exceeds threshold",
            Priority = 80,
            Enabled = true,
            Condition = new PolicyCondition
            {
                EvaluateAsync = async (ctx, ct) => 
                {
                    return ctx.MemoryGrowthRateMBPerHour > 100;
                }
            },
            ExecuteAsync = async (ctx, ct) => 
            {
                return PolicyResult.Success("AlertOnMemoryLeak", "Memory leak alert: " + ctx.MemoryGrowthRateMBPerHour + " MB/hr growth");
            }
        });

        // Policy: License validation retry
        _policies.Add(new Policy
        {
            Name = "LicenseValidationRetry",
            Description = "Retry license validation with exponential backoff",
            Priority = 70,
            Enabled = true,
            Condition = new PolicyCondition
            {
                EvaluateAsync = async (ctx, ct) => 
                {
                    return ctx.LastLicenseValidationFailed && ctx.LicenseRetryCount < 3;
                }
            },
            ExecuteAsync = async (ctx, ct) => 
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, ctx.LicenseRetryCount) * 30);
                await Task.Delay(delay);
                return PolicyResult.Success("LicenseValidationRetry", "License validation retry scheduled in " + delay.TotalSeconds + "s");
            }
        });

        // Policy: Graceful degradation on perception failure
        _policies.Add(new Policy
        {
            Name = "DegradeOnPerceptionFailure",
            Description = "Switch to fallback perception when primary fails",
            Priority = 60,
            Enabled = true,
            Condition = new PolicyCondition
            {
                EvaluateAsync = async (ctx, ct) => 
                {
                    return ctx.PerceptionFailureRate > 0.5;
                }
            },
            ExecuteAsync = async (ctx, ct) => 
            {
                return PolicyResult.Success("DegradeOnPerceptionFailure", "Switched to fallback perception mode");
            }
        });
    }
}

/// <summary>
/// Policy definition.
/// </summary>
public sealed class Policy
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int Priority { get; init; } = 0;
    public bool Enabled { get; init; } = true;
    public PolicyCondition Condition { get; init; } = new();
    public Func<PolicyContext, CancellationToken, Task<PolicyResult>> ExecuteAsync { get; init; } = default!;
}

/// <summary>
/// Policy condition evaluation.
/// </summary>
public sealed class PolicyCondition
{
    public Func<PolicyContext, CancellationToken, Task<bool>> EvaluateAsync { get; init; } = default!;
}

/// <summary>
/// Policy execution result.
/// </summary>
public sealed class PolicyResult
{
    public string PolicyName { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public Exception? Exception { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();

    public static PolicyResult Success(string policyName, string message, Dictionary<string, object>? metadata = null)
    {
        return new PolicyResult
        {
            PolicyName = policyName,
            Success = true,
            Message = message,
            Metadata = metadata ?? new Dictionary<string, object>()
        };
    }

    public static PolicyResult Failure(string policyName, string message, Exception? exception = null, Dictionary<string, object>? metadata = null)
    {
        return new PolicyResult
        {
            PolicyName = policyName,
            Success = false,
            Message = message,
            Exception = exception,
            Metadata = metadata ?? new Dictionary<string, object>()
        };
    }
}

/// <summary>
/// Policy evaluation result.
/// </summary>
public sealed class PolicyEvaluationResult
{
    public string ContextKey { get; init; } = string.Empty;
    public List<Policy> TriggeredPolicies { get; init; } = new();
    public List<PolicyResult> Results { get; init; } = new();
    public DateTimeOffset EvaluatedAt { get; init; } = DateTimeOffset.UtcNow;
    public bool HasTriggered => TriggeredPolicies.Count > 0;
}

/// <summary>
/// Context for policy evaluation.
/// </summary>
public sealed class PolicyContext
{
    public string InstanceId { get; init; } = string.Empty;
    public int LastExitCode { get; init; } = 0;
    public int RestartCount { get; init; } = 0;
    public int MaxRestarts { get; init; } = 5;
    public double CpuUsagePercent { get; init; } = 0;
    public int ActiveInstances { get; init; } = 0;
    public double MemoryGrowthRateMBPerHour { get; init; } = 0;
    public bool LastLicenseValidationFailed { get; init; } = false;
    public int LicenseRetryCount { get; init; } = 0;
    public double PerceptionFailureRate { get; init; } = 0;
    public Dictionary<string, object> CustomData { get; init; } = new();
}

/// <summary>
/// Options for policy engine.
/// </summary>
public sealed class PolicyEngineOptions
{
    public bool Enabled { get; set; } = true;
    public int MaxPoliciesPerEvaluation { get; set; } = 100;
    public TimeSpan EvaluationTimeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Extension methods for registering policy engine.
/// </summary>
public static class PolicyEngineExtensions
{
    public static IServiceCollection AddPolicyEngine(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PolicyEngineOptions>(configuration.GetSection("PolicyEngine"));
        services.AddSingleton<PolicyEngine>();
        services.AddHostedService(sp => sp.GetRequiredService<PolicyEngine>());
        return services;
    }
}