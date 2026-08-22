// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Plugins.Abstractions;

namespace KKBOT.Orchestration.DynamicDag;

public interface IDynamicDagBuilder
{
    Task<DynamicDag> BuildDagAsync(IGameModule module, DagBuildContext context, CancellationToken ct);
    Task<DynamicDag> BuildDagFromStatesAsync(IReadOnlyList<IStateDefinition> states, DagBuildContext context, CancellationToken ct);
}

public sealed record DagBuildContext(
    Guid InstanceId,
    Guid DeviceId,
    string ModuleConfig,
    Dictionary<string, object> Parameters,
    CancellationToken CancellationToken
);

public sealed class DynamicDag
{
    public Guid DagId { get; } = Guid.NewGuid();
    public List<DagNode> Nodes { get; } = new();
    public List<DagEdge> Edges { get; } = new();
    public Dictionary<string, object> Metadata { get; } = new();

    public DynamicDag AddNode(DagNode node)
    {
        Nodes.Add(node);
        return this;
    }

    public DynamicDag AddEdge(string fromNodeId, string toNodeId, string? condition = null)
    {
        Edges.Add(new DagEdge(fromNodeId, toNodeId, condition));
        return this;
    }

    public IReadOnlyList<DagNode> GetTopologicalOrder()
    {
        // Simple topological sort
        var visited = new HashSet<string>();
        var temp = new HashSet<string>();
        var result = new List<DagNode>();

        void Visit(string nodeId)
        {
            if (temp.Contains(nodeId))
                throw new InvalidOperationException($"Cycle detected in DAG at node {nodeId}");
            
            if (!visited.Contains(nodeId))
            {
                temp.Add(nodeId);
                var edges = Edges.Where(e => e.From == nodeId).ToList();
                foreach (var edge in edges)
                    Visit(edge.To);
                temp.Remove(nodeId);
                visited.Add(nodeId);
                
                var node = Nodes.First(n => n.NodeId == nodeId);
                result.Add(node);
            }
        }

        foreach (var node in Nodes)
            Visit(node.NodeId);

        result.Reverse();
        return result;
    }
}

public sealed record DagNode(
    string NodeId,
    string NodeType,      // "task", "state", "condition", "parallel", "join"
    string Name,
    string? TaskId = null,
    string? StateId = null,
    Dictionary<string, object>? Config = null,
    bool IsParallel = false
);

public sealed record DagEdge(
    string From,
    string To,
    string? Condition = null,  // Expression to evaluate
    double Weight = 1.0
);

// State machine to DAG converter
public interface IStateMachineToDagConverter
{
    DynamicDag Convert(IReadOnlyList<IStateDefinition> states, DagBuildContext context);
}

public sealed class StateMachineToDagConverter : IStateMachineToDagConverter
{
    public DynamicDag Convert(IReadOnlyList<IStateDefinition> states, DagBuildContext context)
    {
        var dag = new DynamicDag();
        
        // Add start node
        dag.AddNode(new DagNode("start", "start", "Start", Config: new() { ["initial"] = true }));

        // Convert each state to a node
        foreach (var state in states)
        {
            var nodeId = $"state_{state.Id}";
            dag.AddNode(new DagNode(
                nodeId,
                "state",
                state.Name,
                StateId: state.Id,
                Config: new Dictionary<string, object>
                {
                    ["category"] = state.Category,
                    ["timeout"] = state.Timeout.TotalMilliseconds,
                    ["retryPolicy"] = state.RetryPolicy,
                    ["evidencePolicy"] = state.EvidencePolicy,
                    ["entryCondition"] = state.EntryCondition != null
                }
            ));
        }

        // Add edges based on NextStateIds
        foreach (var state in states)
        {
            var fromNodeId = $"state_{state.Id}";
            
            if (state.NextStateIds.Count == 0)
            {
                // Terminal state
                dag.AddEdge(fromNodeId, "end", null);
            }
            else if (state.NextStateIds.Count == 1)
            {
                // Single transition
                var toNodeId = $"state_{state.NextStateIds[0]}";
                dag.AddEdge(fromNodeId, toNodeId, "EntryCondition");
            }
            else
            {
                // Multiple possible transitions - add decision node
                var decisionId = $"decision_{state.Id}";
                dag.AddNode(new DagNode(decisionId, "condition", $"Decision: {state.Name}", IsParallel: false));
                dag.AddEdge(fromNodeId, decisionId, null);
                
                foreach (var nextStateId in state.NextStateIds)
                {
                    var toNodeId = $"state_{nextStateId}";
                    dag.AddEdge(decisionId, toNodeId, $"NextState == '{nextStateId}'");
                }
            }
        }

        // Add end node
        dag.AddNode(new DagNode("end", "end", "End", Config: new() { ["terminal"] = true }));

        dag.Metadata["generatedFrom"] = "StateMachine";
        dag.Metadata["stateCount"] = states.Count;
        dag.Metadata["generatedAt"] = DateTime.UtcNow;

        return dag;
    }
}

// Task-based DAG builder
public sealed class TaskDagBuilder : IDynamicDagBuilder
{
    private readonly IStateMachineToDagConverter _stateConverter;

    public TaskDagBuilder(IStateMachineToDagConverter stateConverter)
    {
        _stateConverter = stateConverter;
    }

    public async Task<DynamicDag> BuildDagAsync(IGameModule module, DagBuildContext context, CancellationToken ct)
    {
        // Build from states (primary)
        var stateDag = _stateConverter.Convert(module.GetStates(), context);
        
        // Enhance with tasks
        foreach (var task in module.GetTasks())
        {
            var taskNodeId = $"task_{task.Id}";
            stateDag.AddNode(new DagNode(
                taskNodeId,
                "task",
                task.Name,
                TaskId: task.Id,
                Config: new Dictionary<string, object>
                {
                    ["type"] = task.Type.ToString(),
                    ["timeout"] = task.DefaultTimeout.TotalMilliseconds,
                    ["retryPolicy"] = task.DefaultRetryPolicy,
                    ["requiresApproval"] = task.RequiresApproval
                }
            ));

            // Connect task to relevant states (simplified)
            var firstState = module.GetStates().FirstOrDefault();
            if (firstState != null)
            {
                stateDag.AddEdge($"state_{firstState.Id}", taskNodeId, null);
            }
        }

        stateDag.Metadata["moduleId"] = module.Id;
        stateDag.Metadata["moduleVersion"] = module.Version;
        stateDag.Metadata["taskCount"] = module.GetTasks().Count;

        return stateDag;
    }

    public async Task<DynamicDag> BuildDagFromStatesAsync(IReadOnlyList<IStateDefinition> states, DagBuildContext context, CancellationToken ct)
    {
        return _stateConverter.Convert(states, context);
    }
}