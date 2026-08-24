    private async Task<CoordinationDecision> HandleAgentLeftAsync(CoordinationContext context, CancellationToken ct)
    {
        var actions = new Dictionary<string, object>();
        var reasoning = new List<string>();

        var remainingAgents = context.AvailableAgents
            .Where(a => a.Status == AgentStatus.Online)
            .ToList();

        if (!remainingAgents.Any())
        {
            reasoning.Add("No remaining agents available");
            return new CoordinationDecision
            {
                Type = DecisionType.NoAction,
                Reasoning = string.Join("; ", reasoning),
                Confidence = 0.1
            };
        }

        var tasksToReassign = context.PendingTasks
            .Where(t => t.Status == TaskStatus.Queued)
            .OrderByDescending(t => t.Priority)
            .ToList();

        foreach (var task in tasksToReassign)
        {
            var bestAgent = FindBestAgent(task, remainingAgents);
            if (bestAgent != null)
            {
                actions[bestAgent.AgentId] = new TaskAssignmentAction
                {
                    TaskId = task.TaskId,
                    TaskType = task.TaskType,
                    Parameters = task.Parameters,
                    Priority = task.Priority
                };
                reasoning.Add($"Reassigned task {task.TaskId} to {bestAgent.Name}");
            }
        }

        return new CoordinationDecision
        {
            Type = DecisionType.RebalanceLoad,
            Actions = actions,
            Reasoning = string.Join("; ", reasoning),
            Confidence = 0.85
        };
    }

    private async Task<CoordinationDecision> HandleAgentFailedAsync(CoordinationContext context, CancellationToken ct)
    {
        var actions = new Dictionary<string, object>();
        var reasoning = new List<string>();

        reasoning.Add("Agent failure detected - initiating emergency rebalancing");

        var remainingAgents = context.AvailableAgents
            .Where(a => a.Status == AgentStatus.Online)
            .ToList();

        if (!remainingAgents.Any())
        {
            reasoning.Add("CRITICAL: No healthy agents remaining!");
            return new CoordinationDecision
            {
                Type = DecisionType.EmergencyStop,
                Reasoning = string.Join("; ", reasoning),
                Confidence = 1.0
            };
        }

        var criticalTasks = context.PendingTasks
            .Where(t => t.Priority >= TaskPriority.High && t.Status == TaskStatus.Queued)
            .OrderByDescending(t => t.Priority)
            .ToList();

        foreach (var task in criticalTasks)
        {
            var bestAgent = FindBestAgent(task, remainingAgents);
            if (bestAgent != null)
            {
                actions[bestAgent.AgentId] = new TaskAssignmentAction
                {
                    TaskId = task.TaskId,
                    TaskType = task.TaskType,
                    Parameters = task.Parameters,
                    Priority = TaskPriority.Critical
                };
                reasoning.Add($"Emergency reassignment: {task.TaskId} to {bestAgent.Name}");
            }
        }

        return new CoordinationDecision
        {
            Type = DecisionType.RebalanceLoad,
            Actions = actions,
            Reasoning = string.Join("; ", reasoning),
            Confidence = 0.95
        };
    }

    private async Task<CoordinationDecision> HandleResourceChangedAsync(CoordinationContext context, CancellationToken ct)
    {
        var actions = new Dictionary<string, object>();
        var reasoning = new List<string>();

        reasoning.Add("Resource change detected - evaluating load distribution");

        var overloadedAgents = context.AvailableAgents
            .Where(a => IsAgentOverloaded(a))
            .ToList();

        if (overloadedAgents.Any())
        {
            reasoning.Add($"Found {overloadedAgents.Count} overloaded agents - redistributing load");
            
            var underloadedAgents = context.AvailableAgents
                .Where(a => !IsAgentOverloaded(a) && a.Status == AgentStatus.Online)
                .OrderBy(a => GetAgentLoad(a))
                .ToList();

            reasoning.Add($"Identified {underloadedAgents.Count} underloaded agents for redistribution");
        }

        return new CoordinationDecision
        {
            Type = DecisionType.RebalanceLoad,
            Actions = actions,
            Reasoning = string.Join("; ", reasoning),
            Confidence = 0.7
        };
    }

    private async Task<CoordinationDecision> HandleDeadlineApproachingAsync(CoordinationContext context, CancellationToken ct)
    {
        var actions = new Dictionary<string, object>();
        var reasoning = new List<string>();

        var urgentTasks = context.PendingTasks
            .Where(t => t.Deadline.HasValue && 
                       t.Deadline.Value - DateTimeOffset.UtcNow < _options.DeadlineWarningThreshold)
            .OrderBy(t => t.Deadline)
            .ToList();

        if (!urgentTasks.Any())
        {
            return new CoordinationDecision { Type = DecisionType.NoAction };
        }

        reasoning.Add($"Found {urgentTasks.Count} tasks with approaching deadlines");

        foreach (var task in urgentTasks)
        {
            var fastestAgent = context.AvailableAgents
                .Where(a => a.Status == AgentStatus.Online && CanAgentHandleTask(a, task))
                .OrderBy(a => GetEstimatedCompletionTime(a, task))
                .FirstOrDefault();

            if (fastestAgent != null)
            {
                actions[fastestAgent.AgentId] = new TaskAssignmentAction
                {
                    TaskId = task.TaskId,
                    TaskType = task.TaskType,
                    Parameters = task.Parameters,
                    Priority = TaskPriority.Critical,
                    Deadline = task.Deadline
                };
                reasoning.Add($"Escalated task {task.TaskId} to {fastestAgent.Name} (deadline: {task.Deadline})");
            }
        }

        return new CoordinationDecision
        {
            Type = DecisionType.AssignTask,
            Actions = actions,
            Reasoning = string.Join("; ", reasoning),
            Confidence = 0.9
        };
    }

    private async Task<CoordinationDecision> HandleManualTriggerAsync(CoordinationContext context, CancellationToken ct)
    {
        return new CoordinationDecision
        {
            Type = DecisionType.NoAction,
            Reasoning = "Manual trigger received - awaiting specific instructions",
            Confidence = 0.5
        };
    }

    private AgentInfo? FindBestAgent(AgentTask task, IReadOnlyList<AgentInfo> availableAgents)
    {
        var candidates = availableAgents
            .Where(a => a.Status == AgentStatus.Online && CanAgentHandleTask(a, task))
            .ToList();

        if (!candidates.Any()) return null;

        var scored = candidates.Select(a => new
        {
            Agent = a,
            Score = CalculateAgentScore(a, task)
        })
        .OrderByDescending(x => x.Score)
        .ToList();

        return scored.First().Agent;
    }

    private bool CanAgentHandleTask(AgentInfo agent, AgentTask task)
    {
        foreach (var cap in task.RequiredCapabilities)
        {
            if (cap.StartsWith("tag:"))
            {
                if (!agent.Capabilities.Tags.Contains(cap.Substring(4)))
                    return false;
            }
            else if (!agent.Capabilities.Skills.Contains(cap) &&
                     !agent.Capabilities.SupportedTaskTypes.Contains(cap))
            {
                return false;
            }
        }
        return true;
    }

    private double CalculateAgentScore(AgentInfo agent, AgentTask task)
    {
        double score = 0;

        foreach (var cap in task.RequiredCapabilities)
        {
            if (agent.Capabilities.Skills.Contains(cap))
                score += 20;
            if (agent.Capabilities.Tags.Contains(cap))
                score += 15;
            if (agent.Capabilities.SupportedTaskTypes.Contains(cap))
                score += 10;
        }

        score += agent.Capabilities.Capacity.CpuCores * 2;
        score += (double)agent.Capabilities.Capacity.MemoryBytes / (1024 * 1024 * 1024) * 5;

        var currentLoad = GetAgentLoad(agent);
        score += (1.0 - currentLoad) * 30;

        return score;
    }

    private double GetAgentLoad(AgentInfo agent)
    {
        return 0.3;
    }

    private bool IsAgentOverloaded(AgentInfo agent)
    {
        return GetAgentLoad(agent) > 0.8;
    }

    private TimeSpan GetEstimatedCompletionTime(AgentInfo agent, AgentTask task)
    {
        var baseTime = TimeSpan.FromMinutes(5);
        var capabilityFactor = agent.Capabilities.Skills.Intersect(task.RequiredCapabilities).Any() ? 0.5 : 1.5;
        return TimeSpan.FromTicks((long)(baseTime.Ticks * capabilityFactor));
    }
}

/// <summary>
/// Action for assigning a task to an agent.
/// </summary>
public sealed record TaskAssignmentAction
{
    public string TaskId { get; init; } = string.Empty;
    public string TaskType { get; init; } = string.Empty;
    public Dictionary<string, object> Parameters { get; init; } = new();
    public TaskPriority Priority { get; init; } = TaskPriority.Normal;
    public DateTimeOffset? Deadline { get; init; }
}

/// <summary>
/// Options for default strategy.
/// </summary>
public sealed class DefaultStrategyOptions
{
    public TimeSpan DeadlineWarningThreshold { get; set; } = TimeSpan.FromMinutes(5);
    public double OverloadThreshold { get; set; } = 0.8;
    public bool EnablePredictiveScaling { get; set; } = false;
}