// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KKBOT.Agent.Abstractions;
using KKBOT.AI.Abstractions;
using KKBOT.Observability;

namespace KKBOT.Agent.Learning;

/// <summary>
/// Behavioral cloning learner - learns from demonstration trajectories.
/// </summary>
public sealed class BehavioralCloningLearner : IAgentLearner
{
    private readonly ILogger<BehavioralCloningLearner> _logger;
    private readonly BcLearnerOptions _options;
    private readonly IMetricsRegistry _metrics;

    public BehavioralCloningLearner(
        ILogger<BehavioralCloningLearner> logger,
        IOptions<BcLearnerOptions> options,
        IMetricsRegistry metrics)
    {
        _logger = logger;
        _options = options.Value;
        _metrics = metrics;
    }

    public async Task<LearningResult> LearnFromDemonstrationsAsync(
        IReadOnlyList<Trajectory> demonstrations, 
        LearningConfig config, 
        CancellationToken cancellationToken)
    {
        using var activity = KkbotActivitySources.AI.StartActivity("agent.learn.bc", ActivityKind.Internal);
        
        var startTime = DateTime.UtcNow;
        
        try
        {
            _logger.LogInformation("Starting behavioral cloning with {Count} demonstrations", demonstrations.Count);

            if (demonstrations.Count < _options.MinTrajectoriesForTraining)
            {
                return new LearningResult
                {
                    Success = false,
                    Error = $"Insufficient demonstrations: {demonstrations.Count} < {_options.MinTrajectoriesForTraining}",
                    Duration = DateTime.UtcNow - startTime
                };
            }

            // Filter successful trajectories
            var successful = demonstrations.Where(t => t.Success).ToList();
            if (successful.Count < _options.MinTrajectoriesForTraining)
            {
                _logger.LogWarning("Only {Count} successful trajectories out of {Total}", successful.Count, demonstrations.Count);
            }

            // Extract state-action pairs
            var trainingData = ExtractTrainingData(successful);
            
            // In real implementation, train a neural network policy
            // For now, we extract skills from common patterns
            var skills = await ExtractSkillsFromTrajectoriesAsync(successful, cancellationToken);

            var metrics = new Dictionary<string, double>
            {
                ["trajectories_total"] = demonstrations.Count,
                ["trajectories_successful"] = successful.Count,
                ["training_samples"] = trainingData.Count,
                ["skills_extracted"] = skills.Length,
                ["avg_trajectory_length"] = demonstrations.Average(t => t.Steps.Length),
                ["avg_reward"] = demonstrations.Average(t => t.TotalReward)
            };

            _metrics.IncrementAiInferences(); // Reuse for learning sessions

            return new LearningResult
            {
                Success = true,
                ModelPath = _options.ModelOutputPath,
                Metrics = metrics,
                ExtractedSkills = skills,
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Behavioral cloning failed");
            return new LearningResult
            {
                Success = false,
                Error = ex.Message,
                Duration = DateTime.UtcNow - startTime
            };
        }
        finally
        {
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
    }

    public async Task<LearningResult> LearnFromFeedbackAsync(
        IReadOnlyList<FeedbackSample> feedback, 
        LearningConfig config, 
        CancellationToken cancellationToken)
    {
        using var activity = KkbotActivitySources.AI.StartActivity("agent.learn.rlhf", ActivityKind.Internal);
        
        try
        {
            _logger.LogInformation("Learning from {Count} feedback samples", feedback.Count);

            // Convert feedback to training data
            var trajectories = feedback.Select(f => f.Trajectory).ToList();
            
            // In real implementation: train reward model, then PPO
            // For now, delegate to BC with weighted samples
            var weightedTrajectories = feedback
                .Where(f => f.HumanReward.Value > 0)
                .Select(f => f.Trajectory)
                .ToList();

            if (weightedTrajectories.Count == 0)
            {
                return new LearningResult { Success = false, Error = "No positive feedback samples" };
            }

            return await LearnFromDemonstrationsAsync(weightedTrajectories, config, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RLHF learning failed");
            return new LearningResult { Success = false, Error = ex.Message };
        }
    }

    public async Task AdaptOnlineAsync(Trajectory trajectory, RewardSignal reward, CancellationToken cancellationToken)
    {
        // Online adaptation - in real implementation, update policy network
        // For now, just log and store
        _logger.LogDebug("Online adaptation: trajectory {TrajectoryId} reward {Reward}", 
            trajectory.TrajectoryId, reward.Value);
        
        await Task.CompletedTask;
    }

    public async Task<Skill[]> ExtractSkillsAsync(IReadOnlyList<Trajectory> trajectories, CancellationToken cancellationToken)
    {
        return await ExtractSkillsFromTrajectoriesAsync(trajectories, cancellationToken);
    }

    private List<(Dictionary<string, object> State, AgentAction Action)> ExtractTrainingData(List<Trajectory> trajectories)
    {
        var data = new List<(Dictionary<string, object>, AgentAction)>();

        foreach (var traj in trajectories)
        {
            foreach (var step in traj.Steps)
            {
                var state = new Dictionary<string, object>
                {
                    ["perception"] = step.Perception?.Detections?.Select(d => new { d.DetectorName, d.Confidence, d.IsMatch }).ToArray() ?? Array.Empty<object>(),
                    ["gameState"] = step.NextState?.WorkingMemory ?? new Dictionary<string, object>(),
                    ["stepNumber"] = step.StepNumber
                };

                data.Add((state, step.Action));
            }
        }

        return data;
    }

    private async Task<Skill[]> ExtractSkillsFromTrajectoriesAsync(List<Trajectory> trajectories, CancellationToken ct)
    {
        var skills = new List<Skill>();
        
        // Group trajectories by goal type
        var byGoal = trajectories.GroupBy(t => t.GoalId).ToList();
        
        foreach (var group in byGoal)
        {
            if (group.Count() < _options.MinTrajectoriesForSkill) continue;

            // Find common subsequences (simplified)
            var commonSteps = FindCommonSubsequences(group.SelectMany(t => t.Steps).ToList());
            
            foreach (var (subseq, frequency) in commonSteps)
            {
                if (frequency < _options.MinFrequencyForSkill) continue;

                var skill = CreateSkillFromSubsequence(group.Key, subseq, frequency, group.Count());
                skills.Add(skill);
            }
        }

        return skills.ToArray();
    }

    private List<(AgentAction[] Subsequence, int Frequency)> FindCommonSubsequences(List<TrajectoryStep> steps)
    {
        var subsequences = new Dictionary<string, (AgentAction[] Actions, int Count)>();
        var windowSize = _options.SkillWindowSize;

        for (int i = 0; i <= steps.Count - windowSize; i++)
        {
            var window = steps.Skip(i).Take(windowSize).Select(s => s.Action).ToArray();
            var key = string.Join("|", window.Select(a => $"{a.ToolName}:{string.Join(",", a.Parameters.Keys)}"));

            if (subsequences.TryGetValue(key, out var existing))
            {
                subsequences[key] = (existing.Actions, existing.Count + 1);
            }
            else
            {
                subsequences[key] = (window, 1);
            }
        }

        return subsequences.Values.Select(v => (v.Actions, v.Count)).ToList();
    }

    private Skill CreateSkillFromSubsequence(string goalId, AgentAction[] actions, int frequency, int totalTrajectories)
    {
        var skillId = Guid.NewGuid().ToString("N");
        var name = $"skill_{goalId}_{skillId[..8]}";
        
        // Create parameterized plan template
        var planSteps = actions.Select((a, i) => new PlanStep
        {
            StepNumber = i + 1,
            StepId = Guid.NewGuid().ToString("N"),
            Description = $"Execute {a.ToolName}",
            Action = a,
            Dependencies = i > 0 ? new[] { $"step-{i}" } : Array.Empty<string>(),
            SuccessCondition = new SuccessCondition { Type = "tool_result", Parameters = new Dictionary<string, object> { ["success"] = true } },
            FailureHandling = FailureHandling.Retry,
            Timeout = TimeSpan.FromMinutes(2)
        }).ToArray();

        var templatePlan = new Plan
        {
            PlanId = skillId,
            GoalId = goalId,
            Steps = planSteps,
            Metadata = new PlanMetadata
            {
                EstimatedTotalSteps = actions.Length,
                EstimatedDuration = TimeSpan.FromMinutes(actions.Length * 2),
                OverallRisk = actions.Max(a => a.Risk.Level),
                Tags = new Dictionary<string, object> { ["extracted"] = true, ["frequency"] = frequency }
            }
        };

        // Extract parameters from actions
        var parameters = actions
            .SelectMany(a => a.Parameters.Keys)
            .Distinct()
            .Select(k => new SkillParameter
            {
                Name = k,
                Type = "object",
                Description = $"Parameter for {k}",
                Required = true
            }).ToArray();

        return new Skill
        {
            SkillId = skillId,
            Name = name,
            Description = $"Extracted skill from goal {goalId} (frequency: {frequency}/{totalTrajectories})",
            Category = InferCategory(actions),
            Parameters = parameters,
            TemplatePlan = planSteps,
            SuccessRate = Math.Min(0.5 + (frequency * 0.05), 0.95),
            UsageCount = 0,
            LearnedFrom = "behavioral_cloning",
            Metadata = new Dictionary<string, object>
            {
                ["source_goal"] = goalId,
                ["frequency"] = frequency,
                ["total_trajectories"] = totalTrajectories,
                ["actions_count"] = actions.Length
            }
        };
    }

    private SkillCategory InferCategory(AgentAction[] actions)
    {
        var toolNames = actions.Select(a => a.ToolName).ToHashSet();
        
        if (toolNames.Contains("send_input") && toolNames.Any(t => t.Contains("combat") || t.Contains("attack")))
            return SkillCategory.Combat;
        if (toolNames.Contains("send_input") && toolNames.Any(t => t.Contains("move") || t.Contains("navigate")))
            return SkillCategory.Navigation;
        if (toolNames.Any(t => t.Contains("gather") || t.Contains("collect") || t.Contains("mine")))
            return SkillCategory.ResourceGathering;
        if (toolNames.Any(t => t.Contains("quest") || t.Contains("dialog")))
            return SkillCategory.Questing;
        if (toolNames.Any(t => t.Contains("chat") || t.Contains("trade") || t.Contains("party")))
            return SkillCategory.Social;
            
        return SkillCategory.Custom;
    }
}

/// <summary>
/// Options for BC learner.
/// </summary>
public sealed class BcLearnerOptions
{
    public int MinTrajectoriesForTraining { get; set; } = 10;
    public int MinTrajectoriesForSkill { get; set; } = 5;
    public int MinFrequencyForSkill { get; set; } = 3;
    public int SkillWindowSize { get; set; } = 3;
    public string ModelOutputPath { get; set; } = "models/bc_policy.onnx";
    public bool EnableSkillExtraction { get; set; } = true;
}