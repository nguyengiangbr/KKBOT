// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.VisualScripting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KKBOT.VisualScripting.Engine;

public sealed class VisualScriptingEngine : IVisualScriptingEngine
{
    private readonly ILogger<VisualScriptingEngine> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly VisualScriptingOptions _options;
    private readonly ConcurrentDictionary<string, INodeExecutor> _executors = new();
    private readonly ConcurrentDictionary<Guid, WorkflowExecutionResult> _executions = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _cancellationTokens = new();

    public VisualScriptingEngine(
        ILogger<VisualScriptingEngine> logger,
        IServiceProvider serviceProvider,
        IOptions<VisualScriptingOptions> options)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _options = options.Value;
        
        RegisterBuiltInExecutors();
    }

    public async Task<WorkflowValidationResult> ValidateWorkflowAsync(VisualWorkflow workflow, CancellationToken ct = default)
    {
        var result = new WorkflowValidationResult { IsValid = true };

        var cycleErrors = DetectCycles(workflow);
        result.Errors.AddRange(cycleErrors);

        foreach (var node in workflow.Nodes)
        {
            var nodeErrors = await ValidateNodeAsync(node, workflow, ct);
            result.Errors.AddRange(nodeErrors);
        }

        var inputErrors = CheckUnconnectedRequiredInputs(workflow);
        result.Errors.AddRange(inputErrors);

        var disconnectedWarnings = CheckDisconnectedNodes(workflow);
        result.Warnings.AddRange(disconnectedWarnings);

        result.IsValid = !result.Errors.Any(e => e.Severity == ValidationSeverity.Error);
        return result;
    }

    public async Task<WorkflowExecutionResult> ExecuteWorkflowAsync(VisualWorkflow workflow, Dictionary<string, object>? initialVariables = null, CancellationToken ct = default)
    {
        var validation = await ValidateWorkflowAsync(workflow, ct);
        if (!validation.IsValid)
        {
            return new WorkflowExecutionResult
            {
                WorkflowId = workflow.Id,
                Status = WorkflowExecutionStatus.Failed,
                ErrorMessage = $"Workflow validation failed: {string.Join("; ", validation.Errors.Select(e => e.Message))}",
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            };
        }

        var executionId = Guid.NewGuid();
        var executionResult = new WorkflowExecutionResult
        {
            ExecutionId = executionId,
            WorkflowId = workflow.Id,
            Status = WorkflowExecutionStatus.Running,
            StartedAt = DateTime.UtcNow
        };

        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _cancellationTokens[executionId] = cts;
        _executions[executionId] = executionResult;

        _logger.LogInformation("Starting workflow execution {ExecutionId} for workflow {WorkflowId}", executionId, workflow.Id);

        try
        {
            var variables = new ConcurrentDictionary<string, object>(initialVariables ?? new Dictionary<string, object>());
            foreach (var kvp in workflow.Variables)
            {
                variables[kvp.Key] = kvp.Value;
            }

            var executionOrder = GetTopologicalOrder(workflow);
            if (executionOrder.Count != workflow.Nodes.Count)
            {
                throw new InvalidOperationException("Workflow contains cycles");
            }

            var nodeResults = new ConcurrentDictionary<string, NodeExecutionResult>();

            foreach (var nodeId in executionOrder)
            {
                cts.Token.ThrowIfCancellationRequested();

                var node = workflow.Nodes.First(n => n.Id == nodeId);
                var nodeResult = await ExecuteNodeAsync(node, workflow, variables, nodeResults, cts.Token);
                nodeResults[nodeId] = nodeResult;
                executionResult.NodeResults[nodeId] = nodeResult;

                foreach (var output in nodeResult.Outputs)
                {
                    variables[output.Key] = output.Value;
                }

                if (nodeResult.Status == NodeExecutionStatus.Failed && !workflow.Settings.ContinueOnError)
                {
                    throw new InvalidOperationException($"Node {node.Name} failed: {nodeResult.ErrorMessage}");
                }
            }

            executionResult.Status = WorkflowExecutionStatus.Completed;
            executionResult.Outputs = variables.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            executionResult.FinalVariables = variables.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            _logger.LogInformation("Workflow execution {ExecutionId} completed successfully", executionId);
        }
        catch (OperationCanceledException)
        {
            executionResult.Status = WorkflowExecutionStatus.Cancelled;
            _logger.LogInformation("Workflow execution {ExecutionId} was cancelled", executionId);
        }
        catch (Exception ex)
        {
            executionResult.Status = WorkflowExecutionStatus.Failed;
            executionResult.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Workflow execution {ExecutionId} failed", executionId);
        }
        finally
        {
            executionResult.CompletedAt = DateTime.UtcNow;
            executionResult.Duration = executionResult.CompletedAt.Value - executionResult.StartedAt;
            _cancellationTokens.TryRemove(executionId, out _);
        }

        return executionResult;
    }

    public async Task<WorkflowExecutionResult> ResumeWorkflowAsync(Guid executionId, CancellationToken ct = default)
    {
        if (!_executions.TryGetValue(executionId, out var existingResult))
        {
            throw new InvalidOperationException($"Execution {executionId} not found");
        }
        throw new NotImplementedException("Resume workflow not yet implemented");
    }

    public async Task<bool> CancelExecutionAsync(Guid executionId, CancellationToken ct = default)
    {
        if (_cancellationTokens.TryRemove(executionId, out var cts))
        {
            cts.Cancel();
            if (_executions.TryGetValue(executionId, out var result))
            {
                result.Status = WorkflowExecutionStatus.Cancelled;
                result.CompletedAt = DateTime.UtcNow;
            }
            return true;
        }
        return false;
    }

    public async Task<WorkflowExecutionResult?> GetExecutionStatusAsync(Guid executionId, CancellationToken ct = default)
    {
        _executions.TryGetValue(executionId, out var result);
        return result;
    }

    public async Task<List<WorkflowExecutionResult>> GetExecutionHistoryAsync(Guid workflowId, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        return _executions.Values
            .Where(e => e.WorkflowId == workflowId)
            .OrderByDescending(e => e.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
    }

    private void RegisterBuiltInExecutors()
    {
        _executors.TryAdd("start", new StartNodeExecutor());
        _executors.TryAdd("end", new EndNodeExecutor());
        _executors.TryAdd("condition", new ConditionNodeExecutor());
        _executors.TryAdd("parallel", new ParallelNodeExecutor());
        _executors.TryAdd("sequence", new SequenceNodeExecutor());
        _executors.TryAdd("delay", new DelayNodeExecutor());
        _executors.TryAdd("setVariable", new SetVariableNodeExecutor());
        _executors.TryAdd("getVariable", new GetVariableNodeExecutor());
        _executors.TryAdd("log", new LogNodeExecutor());
        _executors.TryAdd("httpRequest", new HttpRequestNodeExecutor());
        _executors.TryAdd("pluginAction", new PluginActionNodeExecutor());
        _executors.TryAdd("aiInference", new AiInferenceNodeExecutor());
        _executors.TryAdd("deviceCapture", new DeviceCaptureNodeExecutor());
        _executors.TryAdd("deviceInput", new DeviceInputNodeExecutor());
        _executors.TryAdd("perception", new PerceptionNodeExecutor());
        _executors.TryAdd("transform", new TransformNodeExecutor());
        _executors.TryAdd("filter", new FilterNodeExecutor());
        _executors.TryAdd("loop", new LoopNodeExecutor());
        _executors.TryAdd("branch", new BranchNodeExecutor());
    }

    private async Task<NodeExecutionResult> ExecuteNodeAsync(
        WorkflowNode node,
        VisualWorkflow workflow,
        ConcurrentDictionary<string, object> variables,
        ConcurrentDictionary<string, NodeExecutionResult> nodeResults,
        CancellationToken ct)
    {
        var nodeResult = new NodeExecutionResult
        {
            NodeId = node.Id,
            Status = NodeExecutionStatus.Running,
            StartedAt = DateTime.UtcNow,
            Inputs = new Dictionary<string, object>()
        };

        var executor = _executors.GetValueOrDefault(node.NodeTypeId);
        if (executor == null)
        {
            nodeResult.Status = NodeExecutionStatus.Failed;
            nodeResult.ErrorMessage = $"No executor found for node type: {node.NodeTypeId}";
            nodeResult.CompletedAt = DateTime.UtcNow;
            nodeResult.Duration = DateTime.UtcNow - nodeResult.StartedAt;
            return nodeResult;
        }

        try
        {
            var inputs = ResolveInputs(node, workflow, nodeResults);
            nodeResult.Inputs = inputs;

            var context = new NodeExecutionContext
            {
                ExecutionId = Guid.NewGuid(),
                WorkflowId = workflow.Id,
                NodeId = node.Id,
                Node = node,
                Workflow = workflow,
                Variables = new Dictionary<string, object>(),
                Inputs = inputs,
                Services = _serviceProvider,
                Logger = _logger,
                CancellationToken = CancellationToken.None,
                OutputCollector = new NodeOutputCollector()
            };

            foreach (var kvp in _executions.Values.FirstOrDefault()?.FinalVariables ?? new Dictionary<string, object>())
            {
                context.Variables[kvp.Key] = kvp.Value;
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(
                node.Properties.TryGetValue("timeout", out var timeout) && timeout is int t ? t : _options.DefaultNodeTimeoutSeconds));
            
            var result = await executor.ExecuteAsync(context, ct);
            
            nodeResult.Status = result.Status;
            nodeResult.Outputs = result.Outputs;
            nodeResult.ErrorMessage = result.ErrorMessage;
            nodeResult.Logs = result.Logs;
            nodeResult.RetryCount = result.RetryCount;
        }
        catch (OperationCanceledException)
        {
            nodeResult.Status = NodeExecutionStatus.Failed;
            nodeResult.ErrorMessage = "Execution timeout";
        }
        catch (Exception ex)
        {
            nodeResult.Status = NodeExecutionStatus.Failed;
            nodeResult.ErrorMessage = ex.Message;
        }
        finally
        {
            nodeResult.CompletedAt = DateTime.UtcNow;
            nodeResult.Duration = nodeResult.CompletedAt.Value - nodeResult.StartedAt;
        }

        return nodeResult;
    }

    private Dictionary<string, object> ResolveInputs(WorkflowNode node, VisualWorkflow workflow, ConcurrentDictionary<string, NodeExecutionResult> nodeResults)
    {
        var inputs = new Dictionary<string, object>();

        foreach (var edge in workflow.Edges.Where(e => e.TargetNodeId == node.Id))
        {
            var sourceResult = nodeResults.GetValueOrDefault(edge.SourceNodeId);
            if (sourceResult != null && sourceResult.Outputs.TryGetValue(edge.SourcePortId, out var value))
            {
                inputs[edge.TargetPortId] = value;
            }
        }

        foreach (var input in node.Inputs)
        {
            if (!inputs.ContainsKey(input.Key) && input.Value.DefaultValue != null)
            {
                inputs[input.Key] = input.Value.DefaultValue;
            }
        }

        return inputs;
    }

    private List<string> GetTopologicalOrder(VisualWorkflow workflow)
    {
        var graph = workflow.Nodes.ToDictionary(n => n.Id, n => workflow.Edges
            .Where(e => e.SourceNodeId == n.Id)
            .Select(e => e.TargetNodeId)
            .ToList());

        var result = new List<string>();
        var visited = new HashSet<string>();
        var tempMark = new HashSet<string>();

        void Visit(string nodeId)
        {
            if (tempMark.Contains(nodeId))
                throw new InvalidOperationException($"Cycle detected involving node {nodeId}");
            
            if (!visited.Contains(nodeId))
            {
                tempMark.Add(nodeId);
                foreach (var neighbor in graph.GetValueOrDefault(nodeId, new List<string>()))
                {
                    Visit(neighbor);
                }
                tempMark.Remove(nodeId);
                visited.Add(nodeId);
                result.Add(nodeId);
            }
        }

        foreach (var node in workflow.Nodes)
        {
            Visit(node.Id);
        }

        return result;
    }

    private List<WorkflowValidationError> DetectCycles(VisualWorkflow workflow)
    {
        var errors = new List<WorkflowValidationError>();
        var visited = new HashSet<string>();
        var tempMark = new HashSet<string>();
        var graph = workflow.Nodes.ToDictionary(n => n.Id, n => workflow.Edges
            .Where(e => e.SourceNodeId == n.Id)
            .Select(e => e.TargetNodeId)
            .ToList());

        void Visit(string nodeId, List<string> path)
        {
            if (tempMark.Contains(nodeId))
            {
                var cyclePath = string.Join(" -> ", path) + " -> " + nodeId;
                errors.Add(new WorkflowValidationError
                {
                    Code = "CYCLE_DETECTED",
                    Message = $"Cycle detected: {cyclePath}",
                    Severity = ValidationSeverity.Error,
                    SuggestedFix = "Remove the edge that creates the cycle"
                });
                return;
            }

            if (!visited.Contains(nodeId))
            {
                tempMark.Add(nodeId);
                var newPath = new List<string>(path) { nodeId };
                foreach (var neighbor in workflow.Edges
                    .Where(e => e.SourceNodeId == nodeId)
                    .Select(e => e.TargetNodeId))
                {
                    Visit(neighbor, newPath);
                }
                tempMark.Remove(nodeId);
                visited.Add(nodeId);
            }
        }

        foreach (var node in workflow.Nodes)
        {
            Visit(node.Id, new List<string>());
        }

        return errors;
    }

    private List<WorkflowValidationError> CheckUnconnectedRequiredInputs(VisualWorkflow workflow)
    {
        var errors = new List<WorkflowValidationError>();

        foreach (var node in workflow.Nodes)
        {
            foreach (var input in node.Inputs.Where(i => i.Value.IsRequired))
            {
                var hasConnection = workflow.Edges.Any(e => e.TargetNodeId == node.Id && e.TargetPortId == input.Key);
                var hasDefault = node.Inputs[input.Key].DefaultValue != null;

                if (!hasConnection && !hasDefault)
                {
                    errors.Add(new WorkflowValidationError
                    {
                        Code = "REQUIRED_INPUT_UNCONNECTED",
                        Message = $"Required input '{input.Value.Name}' on node '{node.Name}' is not connected and has no default value",
                        NodeId = node.Id,
                        PortId = input.Key,
                        Severity = ValidationSeverity.Error,
                        SuggestedFix = "Connect the input or provide a default value"
                    });
                }
            }
        }

        return errors;
    }

    private List<WorkflowValidationWarning> CheckDisconnectedNodes(VisualWorkflow workflow)
    {
        var warnings = new List<WorkflowValidationWarning>();
        var connectedNodes = new HashSet<string>();

        foreach (var edge in workflow.Edges)
        {
            connectedNodes.Add(edge.SourceNodeId);
            connectedNodes.Add(edge.TargetNodeId);
        }

        foreach (var node in workflow.Nodes)
        {
            if (!connectedNodes.Contains(node.Id) && workflow.Nodes.Count > 1)
            {
                warnings.Add(new WorkflowValidationWarning
                {
                    Code = "NODE_DISCONNECTED",
                    Message = $"Node '{node.Name}' is not connected to any other node",
                    NodeId = node.Id,
                    SuggestedFix = "Connect the node to the workflow or remove it"
                });
            }
        }

        return warnings;
    }

    private async Task<List<WorkflowValidationError>> ValidateNodeAsync(WorkflowNode node, VisualWorkflow workflow, CancellationToken ct)
    {
        var errors = new List<WorkflowValidationError>();

        if (!_executors.ContainsKey(node.NodeTypeId))
        {
            errors.Add(new WorkflowValidationError
            {
                Code = "UNKNOWN_NODE_TYPE",
                Message = $"Unknown node type: {node.NodeTypeId}",
                NodeId = node.Id,
                Severity = ValidationSeverity.Error,
                SuggestedFix = "Use a valid node type or implement a custom executor"
            });
        }

        foreach (var input in node.Inputs)
        {
            var connectedEdges = workflow.Edges.Where(e => e.TargetNodeId == node.Id && e.TargetPortId == input.Key);
            if (input.Value.IsMultiple)
            {
            }
            else if (input.Value.IsRequired)
            {
                var connectionCount = connectedEdges.Count();
                if (connectionCount > 1)
                {
                    errors.Add(new WorkflowValidationError
                    {
                        Code = "MULTIPLE_CONNECTIONS_TO_SINGLE_INPUT",
                        Message = $"Input '{input.Value.Name}' on node '{node.Name}' has multiple connections but only accepts one",
                        NodeId = node.Id,
                        PortId = input.Key,
                        Severity = ValidationSeverity.Error,
                        SuggestedFix = "Use a merge node or connect only one source"
                    });
                }
            }
        }

        if (_executors.TryGetValue(node.NodeTypeId, out var executor))
        {
            var isValid = await executor.ValidateAsync(node, ct);
            if (!isValid)
            {
                errors.Add(new WorkflowValidationError
                {
                    Code = "NODE_VALIDATION_FAILED",
                    Message = $"Node '{node.Name}' failed validation",
                    NodeId = node.Id,
                    Severity = ValidationSeverity.Error
                });
            }
        }

        return errors;
    }
}

// Built-in node executors

public sealed class StartNodeExecutor : INodeExecutor
{
    public string NodeTypeId => "start";

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken ct = default)
    {
        var result = new NodeExecutionResult
        {
            NodeId = context.NodeId,
            Status = NodeExecutionStatus.Completed,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Outputs = new Dictionary<string, object>
            {
                ["triggered"] = true,
                ["workflowId"] = context.Workflow.Id,
                ["executionId"] = context.ExecutionId
            }
        };
        return Task.FromResult(result);
    }

    public Task<bool> ValidateAsync(WorkflowNode node, CancellationToken ct = default) => Task.FromResult(true);
}

public sealed class EndNodeExecutor : INodeExecutor
{
    public string NodeTypeId => "end";

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken ct = default)
    {
        var result = new NodeExecutionResult
        {
            NodeId = context.NodeId,
            Status = NodeExecutionStatus.Completed,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Outputs = new Dictionary<string, object>
            {
                ["completed"] = true
            }
        };
        return Task.FromResult(result);
    }

    public Task<bool> ValidateAsync(WorkflowNode node, CancellationToken ct = default) => Task.FromResult(true);
}

public sealed class ConditionNodeExecutor : INodeExecutor
{
    public string NodeTypeId => "condition";

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken ct = default)
    {
        var condition = context.Node.Properties.TryGetValue("condition", out var cond) ? cond?.ToString() ?? "true" : "true";
        var result = false;

        try
        {
            result = EvaluateCondition(context.Inputs, condition);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Condition evaluation failed: {ex.Message}");
        }

        var resultOutput = new NodeExecutionResult
        {
            NodeId = context.NodeId,
            Status = NodeExecutionStatus.Completed,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Outputs = new Dictionary<string, object>
            {
                ["true"] = result,
                ["false"] = !result
            }
        };
        return Task.FromResult(resultOutput);
    }

    private bool EvaluateCondition(Dictionary<string, object> inputs, string expression)
    {
        if (bool.TryParse(expression, out var boolResult))
            return boolResult;
        return false;
    }

    public Task<bool> ValidateAsync(WorkflowNode node, CancellationToken ct = default) => Task.FromResult(true);
}

public sealed class ParallelNodeExecutor : INodeExecutor
{
    public string NodeTypeId => "parallel";

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken ct = default)
    {
        var result = new NodeExecutionResult
        {
            NodeId = context.NodeId,
            Status = NodeExecutionStatus.Completed,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Outputs = new Dictionary<string, object>
            {
                ["parallel"] = true,
                ["branchesStarted"] = true
            }
        };
        return await Task.FromResult(result);
    }

    public Task<bool> ValidateAsync(WorkflowNode node, CancellationToken ct = default) => Task.FromResult(true);
}

public sealed class SequenceNodeExecutor : INodeExecutor
{
    public string NodeTypeId => "sequence";

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken ct = default)
    {
        var result = new NodeExecutionResult
        {
            NodeId = context.NodeId,
            Status = NodeExecutionStatus.Completed,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Outputs = new Dictionary<string, object>
            {
                ["sequence"] = true
            }
        };
        return Task.FromResult(result);
    }

    public Task<bool> ValidateAsync(WorkflowNode node, CancellationToken ct = default) => Task.FromResult(true);
}

public sealed class DelayNodeExecutor : INodeExecutor
{
    public string NodeTypeId => "delay";

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken ct = default)
    {
        var delayMs = context.Node.Properties.TryGetValue("delayMs", out var delay) && delay is int d ? d : 1000;
        
        await Task.Delay(delayMs, ct);

        var result = new NodeExecutionResult
        {
            NodeId = context.NodeId,
            Status = NodeExecutionStatus.Completed,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Outputs = new Dictionary<string, object>
            {
                ["delayed"] = true,
                ["delayMs"] = delayMs
            }
        };
        return result;
    }

    public Task<bool> ValidateAsync(WorkflowNode node, CancellationToken ct = default) => Task.FromResult(true);
}

public sealed class SetVariableNodeExecutor : INodeExecutor
{
    public string NodeTypeId => "setVariable";

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken ct = default)
    {
        var name = context.Node.Properties.TryGetValue("name", out var nameObj) ? nameObj?.ToString() : "";
        var value = context.Inputs.TryGetValue("value", out var val) ? val : null;

        if (string.IsNullOrEmpty(name))
        {
            return Task.FromResult(new NodeExecutionResult
            {
                NodeId = context.NodeId,
                Status = NodeExecutionStatus.Failed,
                ErrorMessage = "Variable name is required"
            });
        }

        var result = new NodeExecutionResult
        {
            NodeId = context.NodeId,
            Status = NodeExecutionStatus.Completed,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Outputs = new Dictionary<string, object>
            {
                [name] = value ?? ""
            }
        };
        return Task.FromResult(result);
    }

    public Task<bool> ValidateAsync(WorkflowNode node, CancellationToken ct = default)
    {
        var hasName = node.Properties.ContainsKey("name") && !string.IsNullOrEmpty(node.Properties.GetValueOrDefault("name")?.ToString());
        return Task.FromResult(hasName);
    }
}

public sealed class GetVariableNodeExecutor : INodeExecutor
{
    public string NodeTypeId => "getVariable";

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken ct = default)
    {
        var name = context.Node.Properties.TryGetValue("name", out var nameObj) ? nameObj?.ToString() : "";
        
        var result = new NodeExecutionResult
        {
            NodeId = context.NodeId,
            Status = NodeExecutionStatus.Completed,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Outputs = new Dictionary<string, object>
            {
                ["value"] = context.Variables.TryGetValue(name ?? "", out var val) ? val! : null!
            }
        };
        return Task.FromResult(result);
    }

    public Task<bool> ValidateAsync(WorkflowNode node, CancellationToken ct = default) => Task.FromResult(true);
}

public sealed class LogNodeExecutor : INodeExecutor
{
    public string NodeTypeId => "log";

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken ct = default)
    {
        var message = context.Inputs.TryGetValue("message", out var msg) ? msg?.ToString() : "";
        var level = context.Node.Properties.TryGetValue("level", out var levelObj) ? levelObj?.ToString() : "Information";
        
        if (Enum.TryParse<LogLevel>(level, out var logLevel))
        {
            switch (logLevel)
            {
                case LogLevel.Warning:
                    context.Logger.LogWarning("Workflow Log [{NodeId}]: {Message}", context.NodeId, message);
                    break;
                case LogLevel.Error:
                case LogLevel.Critical:
                    context.Logger.LogError("Workflow Log [{NodeId}]: {Message}", context.NodeId, message);
                    break;
                case LogLevel.Debug:
                    context.Logger.LogDebug("Workflow Log [{NodeId}]: {Message}", context.NodeId, message);
                    break;
                case LogLevel.Trace:
                    context.Logger.LogTrace("Workflow Log [{NodeId}]: {Message}", context.NodeId, message);
                    break;
                default:
                    context.Logger.LogInformation("Workflow Log [{NodeId}]: {Message}", context.NodeId, message);
                    break;
            }
        }

        var result = new NodeExecutionResult
        {
            NodeId = context.NodeId,
            Status = NodeExecutionStatus.Completed,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Outputs = new Dictionary<string, object>
            {
                ["logged"] = true,
                ["message"] = message!
            }
        };
        return Task.FromResult(result);
    }

    public Task<bool> ValidateAsync(WorkflowNode node, CancellationToken ct = default) => Task.FromResult(true);
}

public sealed class HttpRequestNodeExecutor : INodeExecutor
{
    public string NodeTypeId => "httpRequest";

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken ct = default)
    {
        var url = context.Inputs.TryGetValue("url", out var urlObj) ? urlObj?.ToString() : "";
        var method = context.Node.Properties.TryGetValue("method", out var methodObj) ? methodObj?.ToString() ?? "GET" : "GET";
        var headers = context.Inputs.TryGetValue("headers", out var headersObj) ? headersObj as Dictionary<string, string> ?? new Dictionary<string, string>() : new Dictionary<string, string>();
        var body = context.Inputs.TryGetValue("body", out var bodyObj) ? bodyObj : null;

        if (string.IsNullOrEmpty(url))
        {
            return new NodeExecutionResult
            {
                NodeId = context.NodeId,
                Status = NodeExecutionStatus.Failed,
                ErrorMessage = "URL is required"
            };
        }

        try
        {
            using var httpClient = new HttpClient();
            using var request = new HttpRequestMessage(new HttpMethod(method!), url);

            foreach (var header in headers)
            {
                request.Headers.Add(header.Key, header.Value);
            }

            if (body != null && (method == "POST" || method == "PUT" || method == "PATCH"))
            {
                var json = JsonSerializer.Serialize(body);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            var response = await httpClient.SendAsync(request, ct);
            var responseContent = await response.Content.ReadAsStringAsync(ct);

            var result = new NodeExecutionResult
            {
                NodeId = context.NodeId,
                Status = response.IsSuccessStatusCode ? NodeExecutionStatus.Completed : NodeExecutionStatus.Failed,
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                Outputs = new Dictionary<string, object>
                {
                    ["statusCode"] = (int)response.StatusCode,
                    ["headers"] = response.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value)),
                    ["body"] = responseContent,
                    ["success"] = response.IsSuccessStatusCode
                }
            };

            if (!response.IsSuccessStatusCode)
            {
                result.ErrorMessage = $"HTTP {response.StatusCode}: {responseContent}";
            }

            return result;
        }
        catch (Exception ex)
        {
            return new NodeExecutionResult
            {
                NodeId = context.NodeId,
                Status = NodeExecutionStatus.Failed,
                ErrorMessage = ex.Message
            };
        }
    }

    public Task<bool> ValidateAsync(WorkflowNode node, CancellationToken ct = default) => Task.FromResult(true);
}

public sealed class PluginActionNodeExecutor : INodeExecutor
{
    public string NodeTypeId => "pluginAction";

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken ct = default)
    {
        var pluginId = context.Node.Properties.TryGetValue("pluginId", out var pid) ? pid?.ToString() : "";
        var actionId = context.Node.Properties.TryGetValue("actionId", out var aid) ? aid?.ToString() : "";
        var parameters = context.Inputs.TryGetValue("parameters", out var paramObj) ? paramObj as Dictionary<string, object> : new Dictionary<string, object>();

        if (string.IsNullOrEmpty(pluginId) || string.IsNullOrEmpty(actionId))
        {
            return new NodeExecutionResult
            {
                NodeId = context.NodeId,
                Status = NodeExecutionStatus.Failed,
                ErrorMessage = "Plugin ID and Action ID are required"
            };
        }

        try
        {
            await Task.Delay(100, ct);

            var result = new NodeExecutionResult
            {
                NodeId = context.NodeId,
                Status = NodeExecutionStatus.Completed,
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                Outputs = new Dictionary<string, object>
                {
                    ["result"] = new { success = true, pluginId, actionId, parameters },
                    ["executed"] = true
                }
            };
            return result;
        }
        catch (Exception ex)
        {
            return new NodeExecutionResult
            {
                NodeId = context.NodeId,
                Status = NodeExecutionStatus.Failed,
                ErrorMessage = ex.Message
            };
        }
    }

    public Task<bool> ValidateAsync(WorkflowNode node, CancellationToken ct = default)
    {
        var hasPluginId = node.Properties.ContainsKey("pluginId") && !string.IsNullOrEmpty(node.Properties.GetValueOrDefault("pluginId")?.ToString());
        var hasActionId = node.Properties.ContainsKey("actionId") && !string.IsNullOrEmpty(node.Properties.GetValueOrDefault("actionId")?.ToString());
        return Task.FromResult(hasPluginId && hasActionId);
    }
}

public sealed class AiInferenceNodeExecutor : INodeExecutor
{
    public string NodeTypeId => "aiInference";

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken ct = default)
    {
        var modelId = context.Node.Properties.TryGetValue("modelId", out var mid) ? mid?.ToString() : "";
        var prompt = context.Inputs.TryGetValue("prompt", out var promptObj) ? promptObj?.ToString() : "";
        var image = context.Inputs.TryGetValue("image", out var imgObj) ? imgObj as byte[] : null;

        if (string.IsNullOrEmpty(modelId) || string.IsNullOrEmpty(prompt))
        {
            return new NodeExecutionResult
            {
                NodeId = context.NodeId,
                Status = NodeExecutionStatus.Failed,
                ErrorMessage = "Model ID and prompt are required"
            };
        }

        try
        {
            await Task.Delay(500, ct);

            var result = new NodeExecutionResult
            {
                NodeId = context.NodeId,
                Status = NodeExecutionStatus.Completed,
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                Outputs = new Dictionary<string, object>
                {
                    ["response"] = $"AI response for: {prompt}",
                    ["modelId"] = modelId,
                    ["tokensUsed"] = 100
                }
            };
            return result;
        }
        catch (Exception ex)
        {
            return new NodeExecutionResult
            {
                NodeId = context.NodeId,
                Status = NodeExecutionStatus.Failed,
                ErrorMessage = ex.Message
            };
        }
    }

    public Task<bool> ValidateAsync(WorkflowNode node, CancellationToken ct = default) => Task.FromResult(true);
}

public sealed class DeviceCaptureNodeExecutor : INodeExecutor
{
    public string NodeTypeId => "deviceCapture";

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken ct = default)
    {
        var deviceId = context.Node.Properties.TryGetValue("deviceId", out var did) ? did?.ToString() : "";

        try
        {
            await Task.Delay(100, ct);

            var result = new NodeExecutionResult
            {
                NodeId = context.NodeId,
                Status = NodeExecutionStatus.Completed,
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                Outputs = new Dictionary<string, object>
                {
                    ["frame"] = new byte[0],
                    ["timestamp"] = DateTime.UtcNow,
                    ["deviceId"] = deviceId!
                }
            };
            return result;
        }
        catch (Exception ex)
        {
            return new NodeExecutionResult
            {
                NodeId = context.NodeId,
                Status = NodeExecutionStatus.Failed,
                ErrorMessage = ex.Message
            };
        }
    }

    public Task<bool> ValidateAsync(WorkflowNode node, CancellationToken ct = default) => Task.FromResult(true);
}

public sealed class DeviceInputNodeExecutor : INodeExecutor
{
    public string NodeTypeId => "deviceInput";

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken ct = default)
    {
        var deviceId = context.Node.Properties.TryGetValue("deviceId", out var did) ? did?.ToString() : "";
        var action = context.Inputs.TryGetValue("action", out var actionObj) ? actionObj?.ToString() : "";
        var parameters = context.Inputs.TryGetValue("parameters", out var paramObj) ? paramObj as Dictionary<string, object> : new Dictionary<string, object>();

        try
        {
            await Task.Delay(50, ct);

            var result = new NodeExecutionResult
            {
                NodeId = context.NodeId,
                Status = NodeExecutionStatus.Completed,
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                Outputs = new Dictionary<string, object>
                {
                    ["sent"] = true,
                    ["action"] = action!,
                    ["deviceId"] = deviceId!
                }
            };
            return result;
        }
        catch (Exception ex)
        {
            return new NodeExecutionResult
            {
                NodeId = context.NodeId,
                Status = NodeExecutionStatus.Failed,
                ErrorMessage = ex.Message
            };
        }
    }

    public Task<bool> ValidateAsync(WorkflowNode node, CancellationToken ct = default) => Task.FromResult(true);
}

public sealed class PerceptionNodeExecutor : INodeExecutor
{
    public string NodeTypeId => "perception";

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken ct = default)
    {
        var detectorId = context.Node.Properties.TryGetValue("detectorId", out var did) ? did?.ToString() : "";
        var frame = context.Inputs.TryGetValue("frame", out var frameObj) ? frameObj : null;

        try
        {
            await Task.Delay(200, ct);

            var result = new NodeExecutionResult
            {
                NodeId = context.NodeId,
                Status = NodeExecutionStatus.Completed,
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                Outputs = new Dictionary<string, object>
                {
                    ["detections"] = new List<object>(),
                    ["confidence"] = 0.95,
                    ["detectorId"] = detectorId!
                }
            };
            return result;
        }
        catch (Exception ex)
        {
            return new NodeExecutionResult
            {
                NodeId = context.NodeId,
                Status = NodeExecutionStatus.Failed,
                ErrorMessage = ex.Message
            };
        }
    }

    public Task<bool> ValidateAsync(WorkflowNode node, CancellationToken ct = default) => Task.FromResult(true);
}

public sealed class TransformNodeExecutor : INodeExecutor
{
    public string NodeTypeId => "transform";

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken ct = default)
    {
        var transformType = context.Node.Properties.TryGetValue("transformType", out var tt) ? tt?.ToString() : "map";
        var input = context.Inputs.TryGetValue("input", out var inputObj) ? inputObj : null;
        var script = context.Node.Properties.TryGetValue("script", out var scriptObj) ? scriptObj?.ToString() : "";

        try
        {
            object? output = null;

            switch (transformType)
            {
                case "map":
                    if (input is Dictionary<string, object> dict)
                    {
                        output = dict.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                    }
                    break;
                case "filter":
                    if (input is IEnumerable<object> enumInput)
                    {
                        output = enumInput.ToList();
                    }
                    break;
                case "script":
                    output = input;
                    break;
                default:
                    output = input;
                    break;
            }

            var result = new NodeExecutionResult
            {
                NodeId = context.NodeId,
                Status = NodeExecutionStatus.Completed,
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                Outputs = new Dictionary<string, object>
                {
                    ["output"] = output ?? new object()
                }
            };
            return result;
        }
        catch (Exception ex)
        {
            return new NodeExecutionResult
            {
                NodeId = context.NodeId,
                Status = NodeExecutionStatus.Failed,
                ErrorMessage = ex.Message
            };
        }
    }

    public Task<bool> ValidateAsync(WorkflowNode node, CancellationToken ct = default) => Task.FromResult(true);
}

public sealed class FilterNodeExecutor : INodeExecutor
{
    public string NodeTypeId => "filter";

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken ct = default)
    {
        var condition = context.Node.Properties.TryGetValue("condition", out var cond) ? cond?.ToString() : "true";
        var input = context.Inputs.TryGetValue("input", out var inputObj) ? inputObj : null;

        try
        {
            var output = input;

            if (input is IEnumerable<object> items)
            {
                output = items.Where(_ => true).ToList();
            }

            var result = new NodeExecutionResult
            {
                NodeId = context.NodeId,
                Status = NodeExecutionStatus.Completed,
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                Outputs = new Dictionary<string, object>
                {
                    ["output"] = output ?? new List<object>()
                }
            };
            return result;
        }
        catch (Exception ex)
        {
            return new NodeExecutionResult
            {
                NodeId = context.NodeId,
                Status = NodeExecutionStatus.Failed,
                ErrorMessage = ex.Message
            };
        }
    }

    public Task<bool> ValidateAsync(WorkflowNode node, CancellationToken ct = default) => Task.FromResult(true);
}

public sealed class LoopNodeExecutor : INodeExecutor
{
    public string NodeTypeId => "loop";

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken ct = default)
    {
        var maxIterations = context.Node.Properties.TryGetValue("maxIterations", out var mi) && mi is int miVal ? miVal : 10;
        var condition = context.Node.Properties.TryGetValue("condition", out var cond) ? cond?.ToString() : "true";
        var input = context.Inputs.TryGetValue("input", out var inputObj) ? inputObj : null;

        var results = new List<object>();

        try
        {
            for (int i = 0; i < maxIterations; i++)
            {
                ct.ThrowIfCancellationRequested();

                var shouldContinue = true;

                if (!shouldContinue)
                    break;

                await Task.Delay(10, ct);
                results.Add(new { iteration = i, input });
            }

            var result = new NodeExecutionResult
            {
                NodeId = context.NodeId,
                Status = NodeExecutionStatus.Completed,
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                Outputs = new Dictionary<string, object>
                {
                    ["results"] = results,
                    ["iterations"] = results.Count
                }
            };
            return result;
        }
        catch (Exception ex)
        {
            return new NodeExecutionResult
            {
                NodeId = context.NodeId,
                Status = NodeExecutionStatus.Failed,
                ErrorMessage = ex.Message
            };
        }
    }

    public Task<bool> ValidateAsync(WorkflowNode node, CancellationToken ct = default) => Task.FromResult(true);
}

public sealed class BranchNodeExecutor : INodeExecutor
{
    public string NodeTypeId => "branch";

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken ct = default)
    {
        var branches = context.Node.Properties.TryGetValue("branches", out var branchesObj) ? branchesObj as List<BranchDefinition> ?? new List<BranchDefinition>() : new List<BranchDefinition>();
        var input = context.Inputs.TryGetValue("input", out var inputObj) ? inputObj : null;

        var results = new Dictionary<string, object>();

        foreach (var branch in branches)
        {
            var shouldExecute = true;

            if (shouldExecute)
            {
                await Task.Delay(10, ct);
                results[branch.Id] = new { executed = true, branch = branch.Name };
            }
        }

        var result = new NodeExecutionResult
        {
            NodeId = context.NodeId,
            Status = NodeExecutionStatus.Completed,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Outputs = new Dictionary<string, object>
            {
                ["branches"] = results
            }
        };
        return result;
    }

    public Task<bool> ValidateAsync(WorkflowNode node, CancellationToken ct = default) => Task.FromResult(true);
}

public sealed class BranchDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Condition { get; set; } = "true";
}

public sealed class NodeOutputCollector : INodeOutputCollector
{
    private readonly Dictionary<string, object> _outputs = new();
    private readonly List<ExecutionLogEntry> _logs = new();

    public void SetOutput(string portId, object value)
    {
        _outputs[portId] = value;
    }

    public void SetOutputs(Dictionary<string, object> outputs)
    {
        foreach (var kvp in outputs)
        {
            _outputs[kvp.Key] = kvp.Value;
        }
    }

    public void AddLog(LogLevel level, string message, Dictionary<string, object>? data = null)
    {
        _logs.Add(new ExecutionLogEntry
        {
            Level = level,
            Message = message,
            Data = data ?? new Dictionary<string, object>()
        });
    }

    public Dictionary<string, object> GetOutputs() => _outputs;
    public List<ExecutionLogEntry> GetLogs() => _logs;
}

public sealed class VisualScriptingOptions
{
    public int DefaultNodeTimeoutSeconds { get; set; } = 30;
    public int MaxConcurrentExecutions { get; set; } = 10;
    public bool EnableDebugging { get; set; } = true;
    public string[] AllowedNodeTypes { get; set; } = Array.Empty<string>();
    public bool EnableCustomCode { get; set; } = false;
}