// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Domain.Models;
using KKBOT.Orchestration.DynamicDag;
using KKBOT.Orchestration.EventSourcing;
using KKBOT.Orchestration.MultiTenancy;
using KKBOT.Orchestration.Saga;
using KKBOT.Plugins.Abstractions;
using KKBOT.Plugins.Loader;
using Microsoft.Extensions.Logging;

namespace KKBOT.Orchestration;

/// <summary>
/// Advanced orchestrator with Event Sourcing, Saga, Dynamic DAG, and Multi-tenancy support.
/// </summary>
public sealed class AdvancedInstanceOrchestrator
{
    private readonly PluginLoader _pluginLoader;
    private readonly IDynamicDagBuilder _dagBuilder;
    private readonly IEventStore _eventStore;
    private readonly ISagaOrchestrator _sagaOrchestrator;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<AdvancedInstanceOrchestrator> _logger;

    public AdvancedInstanceOrchestrator(
        PluginLoader pluginLoader,
        IDynamicDagBuilder dagBuilder,
        IEventStore eventStore,
        ISagaOrchestrator sagaOrchestrator,
        ITenantProvider tenantProvider,
        ILogger<AdvancedInstanceOrchestrator> logger)
    {
        _pluginLoader = pluginLoader;
        _dagBuilder = dagBuilder;
        _eventStore = eventStore;
        _sagaOrchestrator = sagaOrchestrator;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    /// <summary>
    /// Starts an instance with full orchestration pipeline.
    /// </summary>
    public async Task<InstanceRunResult> RunInstanceAsync(
        Instance instance,
        string pluginsPath,
        CancellationToken cancellationToken)
    {
        // 1. Resolve tenant
        var tenant = await _tenantProvider.GetTenantAsync(instance.Key ?? "default", cancellationToken);
        if (tenant == null)
        {
            _logger.LogWarning("Tenant {TenantId} not found, using default", instance.Key);
        }

        // 2. Load plugin
        var modules = await _pluginLoader.LoadAllPluginsAsync(cancellationToken);
        var module = modules.FirstOrDefault(m => string.Equals(m.Id?.ToString(), instance.Module?.Id.ToString(), StringComparison.OrdinalIgnoreCase));
        if (module == null)
        {
            return InstanceRunResult.Failed($"Plugin {instance.Module?.Id} not found");
        }

        // 3. Validate module
        var validation = await module.ValidateAsync(
            new ModuleContext(instance.Id, instance.DeviceId ?? Guid.Empty, Guid.Parse(module.Id)),
            cancellationToken);
        if (!validation.IsValid)
        {
            return InstanceRunResult.Failed($"Plugin validation failed: {string.Join(", ", validation.Errors.Select(e => e.Message))}");
        }

        // 4. Build dynamic DAG
        var dagContext = new DagBuildContext(
            instance.Id,
            instance.DeviceId ?? Guid.Empty,
            instance.ConfigOverride ?? "{}",
            new Dictionary<string, object>(),
            cancellationToken);

        var dag = await _dagBuilder.BuildDagAsync(module, dagContext, cancellationToken);
        _logger.LogInformation("Built DAG for instance {InstanceId}: {NodeCount} nodes, {EdgeCount} edges",
            instance.Id, dag.Nodes.Count, dag.Edges.Count);

        // 5. Store initial event
        await _eventStore.AppendAsync(new InstanceStartedEvent
        {
            AggregateId = instance.Id,
            InstanceName = instance.DisplayName,
            ModuleId = module.Id,
            DagId = dag.DagId,
            Version = 1
        }, cancellationToken);

        // 6. Execute DAG via topological order
        var executionResult = await ExecuteDagAsync(dag, instance, module, cancellationToken);

        // 6. Store completion event
        await _eventStore.AppendAsync(new InstanceCompletedEvent
        {
            AggregateId = instance.Id,
            Success = executionResult.Success,
            ErrorMessage = executionResult.ErrorMessage,
            Version = 2
        }, cancellationToken);

        return executionResult;
    }

    private async Task<InstanceRunResult> ExecuteDagAsync(
        KKBOT.Orchestration.DynamicDag.DynamicDag dag,
        Instance instance,
        IGameModule module,
        CancellationToken ct)
    {
        var nodeResults = new Dictionary<string, object?>();
        var executedNodes = new HashSet<string>();

        try
        {
            // Get topological order
            var orderedNodes = dag.GetTopologicalOrder();
            _logger.LogDebug("Executing DAG in topological order: {Order}", 
                string.Join(" -> ", orderedNodes.Select(n => n.NodeId)));

            foreach (var node in orderedNodes)
            {
                ct.ThrowIfCancellationRequested();

                // Check if all dependencies are satisfied
                var incomingEdges = dag.Edges.Where(e => e.To == node.NodeId).ToList();
                var canExecute = incomingEdges.All(e => executedNodes.Contains(e.From));

                if (!canExecute)
                {
                    _logger.LogWarning("Node {NodeId} dependencies not met, skipping", node.NodeId);
                    continue;
                }

                // Execute node based on type
                var result = await ExecuteNodeAsync(node, instance, module, dag, nodeResults, ct);
                nodeResults[node.NodeId] = result;
                executedNodes.Add(node.NodeId);

                // Log event
                await _eventStore.AppendAsync(new NodeExecutedEvent
                {
                    AggregateId = instance.Id,
                    NodeId = node.NodeId,
                    NodeType = node.NodeType,
                    Success = result != null,
                    Version = executedNodes.Count
                }, ct);
            }

            return InstanceRunResult.Succeeded();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DAG execution failed for instance {InstanceId}", instance.Id);
            return InstanceRunResult.Failed(ex.Message);
        }
    }

    private async Task<object?> ExecuteNodeAsync(
        DagNode node,
        Instance instance,
        IGameModule module,
        KKBOT.Orchestration.DynamicDag.DynamicDag dag,
        Dictionary<string, object?> nodeResults,
        CancellationToken ct)
    {
        _logger.LogDebug("Executing node {NodeId} ({NodeType})", node.NodeId, node.NodeType);

        return node.NodeType switch
        {
            "start" => await ExecuteStartNodeAsync(node, instance, ct),
            "state" => await ExecuteStateNodeAsync(node, instance, module, ct),
            "task" => await ExecuteTaskNodeAsync(node, instance, module, ct),
            "condition" => await ExecuteConditionNodeAsync(node, instance, module, dag, nodeResults, ct),
            "end" => await ExecuteEndNodeAsync(node, instance, ct),
            _ => throw new InvalidOperationException($"Unknown node type: {node.NodeType}")
        };
    }

    private Task<object?> ExecuteStartNodeAsync(DagNode node, Instance instance, CancellationToken ct)
    {
        _logger.LogInformation("Instance {InstanceId} started", instance.Id);
        return Task.FromResult<object?>(new { StartedAt = DateTime.UtcNow });
    }

    private async Task<object?> ExecuteStateNodeAsync(
        DagNode node,
        Instance instance,
        IGameModule module,
        CancellationToken ct)
    {
        var state = module.GetStates().FirstOrDefault(s => s.Id == node.StateId);
        if (state == null)
        {
            _logger.LogWarning("State {StateId} not found in module", node.StateId);
            return null;
        }

        // In a real implementation, this would:
        // 1. Capture frame from device
        // 2. Run perception pipeline
        // 3. Evaluate entry condition
        // 4. Execute state action if condition met
        // 5. Return next state decision

        _logger.LogDebug("Evaluating state {StateId} ({StateName})", state.Id, state.Name);

        // Simulate state evaluation
        await Task.Delay(100, ct);

        return new { StateId = state.Id, Entered = true, NextStates = state.NextStateIds };
    }

    private async Task<object?> ExecuteTaskNodeAsync(
        DagNode node,
        Instance instance,
        IGameModule module,
        CancellationToken ct)
    {
        var task = module.GetTasks().FirstOrDefault(t => t.Id == node.TaskId);
        if (task == null)
        {
            _logger.LogWarning("Task {TaskId} not found in module", node.TaskId);
            return null;
        }

        _logger.LogDebug("Executing task {TaskId} ({TaskName})", task.Id, task.Name);

        var parameters = node.Config != null
            ? node.Config.ToDictionary(kv => kv.Key, kv => (object?)kv.Value)
            : new Dictionary<string, object?>();
        var taskContext = new TaskContext(instance.Id, Guid.NewGuid(), parameters);
        var nextState = await task.ExecuteAsync(taskContext, ct);

        return new { TaskId = task.Id, Completed = true, NextStateId = nextState.Id };
    }

    private Task<object?> ExecuteConditionNodeAsync(
        DagNode node,
        Instance instance,
        IGameModule module,
        KKBOT.Orchestration.DynamicDag.DynamicDag dag,
        Dictionary<string, object?> nodeResults,
        CancellationToken ct)
    {
        // Evaluate condition expression
        // This would use a lightweight expression evaluator
        _logger.LogDebug("Evaluating condition node {NodeId}", node.NodeId);
        
        return Task.FromResult<object?>(new { Evaluated = true, Result = true });
    }

    private Task<object?> ExecuteEndNodeAsync(DagNode node, Instance instance, CancellationToken ct)
    {
        _logger.LogInformation("Instance {InstanceId} completed", instance.Id);
        return Task.FromResult<object?>(new { CompletedAt = DateTime.UtcNow });
    }
}

// Result types
public sealed record InstanceRunResult(bool Success, string? ErrorMessage = null, object? Data = null)
{
    public static InstanceRunResult Succeeded(object? data = null) => new(true, null, data);
    public static InstanceRunResult Failed(string error) => new(false, error);
}

// Events
public sealed record InstanceStartedEvent : EventBase, IOrchestrationEvent
{
    public string InstanceName { get; init; } = string.Empty;
    public string ModuleId { get; init; } = string.Empty;
    public Guid DagId { get; init; }
}

public sealed record InstanceCompletedEvent : EventBase, IOrchestrationEvent
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed record NodeExecutedEvent : EventBase, IOrchestrationEvent
{
    public string NodeId { get; init; } = string.Empty;
    public string NodeType { get; init; } = string.Empty;
    public bool Success { get; init; }
}
