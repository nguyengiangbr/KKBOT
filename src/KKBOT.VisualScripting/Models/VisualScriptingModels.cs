// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace KKBOT.VisualScripting;

/// <summary>
/// Represents a visual scripting workflow that can be executed by the engine.
/// </summary>
public sealed class VisualWorkflow
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Draft;
    public List<WorkflowNode> Nodes { get; set; } = new();
    public List<WorkflowEdge> Edges { get; set; } = new();
    public WorkflowSettings Settings { get; set; } = new();
    public Dictionary<string, object> Variables { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public enum WorkflowStatus
{
    Draft,
    Validating,
    Valid,
    Invalid,
    Running,
    Completed,
    Failed,
    Paused
}

public sealed class WorkflowSettings
{
    public bool EnableDebugging { get; set; } = true;
    public int MaxExecutionTimeSeconds { get; set; } = 300;
    public int MaxIterations { get; set; } = 1000;
    public bool ContinueOnError { get; set; } = false;
    public Dictionary<string, object> EnvironmentVariables { get; set; } = new();
    public ConcurrencySettings Concurrency { get; set; } = new();
}

public sealed class ConcurrencySettings
{
    public int MaxParallelNodes { get; set; } = 4;
    public bool AllowParallelExecution { get; set; } = true;
}

/// <summary>
/// Represents a node in the visual workflow graph.
/// </summary>
public sealed class WorkflowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string NodeTypeId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public NodePosition Position { get; set; } = new();
    public Dictionary<string, NodePort> Inputs { get; set; } = new();
    public Dictionary<string, NodePort> Outputs { get; set; } = new();
    public Dictionary<string, object> Properties { get; set; } = new();
    public NodeState State { get; set; } = NodeState.Idle;
    public string? ErrorMessage { get; set; }
    public DateTime? LastExecutedAt { get; set; }
    public TimeSpan? ExecutionTime { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public sealed class NodePosition
{
    public double X { get; set; }
    public double Y { get; set; }
}

public sealed class NodePort
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public PortDataType DataType { get; set; } = PortDataType.Any;
    public bool IsRequired { get; set; } = false;
    public bool IsMultiple { get; set; } = false;
    public object? DefaultValue { get; set; }
    public List<PortValidationRule> ValidationRules { get; set; } = new();
    public string? ConnectedEdgeId { get; set; }
}

public enum PortDataType
{
    Any,
    Boolean,
    Integer,
    Number,
    String,
    Object,
    Array,
    Frame,
    Image,
    Text,
    Json,
    Plugin,
    Instance,
    Device,
    Model,
    Dataset
}

public sealed class PortValidationRule
{
    public string RuleType { get; set; } = string.Empty; // regex, range, enum, custom
    public string Parameter { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}

public enum NodeState
{
    Idle,
    Waiting,
    Running,
    Completed,
    Failed,
    Skipped,
    Disabled
}

/// <summary>
/// Represents a connection between two nodes.
/// </summary>
public sealed class WorkflowEdge
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string SourceNodeId { get; set; } = string.Empty;
    public string SourcePortId { get; set; } = string.Empty;
    public string TargetNodeId { get; set; } = string.Empty;
    public string TargetPortId { get; set; } = string.Empty;
    public EdgeStyle Style { get; set; } = new();
    public EdgeCondition? Condition { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public sealed class EdgeStyle
{
    public string Color { get; set; } = "#1976D2";
    public double Width { get; set; } = 2;
    public EdgeLineStyle LineStyle { get; set; } = EdgeLineStyle.Solid;
    public bool Animated { get; set; } = false;
}

public enum EdgeLineStyle
{
    Solid,
    Dashed,
    Dotted
}

public sealed class EdgeCondition
{
    public string Expression { get; set; } = string.Empty; // JavaScript expression
    public Dictionary<string, object> Variables { get; set; } = new();
}

/// <summary>
/// Defines a node type that can be used in workflows.
/// </summary>
public sealed class NodeDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Color { get; set; } = "#1976D2";
    public List<NodePortDefinition> InputPorts { get; set; } = new();
    public List<NodePortDefinition> OutputPorts { get; set; } = new();
    public List<NodePropertyDefinition> Properties { get; set; } = new();
    public NodeExecutorType ExecutorType { get; set; } = NodeExecutorType.BuiltIn;
    public string? ExecutorAssembly { get; set; }
    public string? ExecutorTypeName { get; set; }
    public bool IsAsync { get; set; } = true;
    public int DefaultTimeoutSeconds { get; set; } = 30;
    public List<string> RequiredPermissions { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public sealed class NodePortDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public PortDataType DataType { get; set; } = PortDataType.Any;
    public bool IsRequired { get; set; } = false;
    public bool IsMultiple { get; set; } = false;
    public object? DefaultValue { get; set; }
    public List<PortValidationRule> ValidationRules { get; set; } = new();
}

public sealed class NodePropertyDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public PropertyDataType DataType { get; set; } = PropertyDataType.String;
    public bool IsRequired { get; set; } = false;
    public object? DefaultValue { get; set; }
    public List<PropertyValidationRule> ValidationRules { get; set; } = new();
    public PropertyUIHint UIHint { get; set; } = PropertyUIHint.Text;
    public List<PropertyOption> Options { get; set; } = new();
    public bool IsAdvanced { get; set; } = false;
    public string? DependsOn { get; set; }
    public string? VisibilityCondition { get; set; }
}

public enum PropertyDataType
{
    String,
    Integer,
    Number,
    Boolean,
    Json,
    Array,
    Enum,
    File,
    Color,
    DateTime,
    TimeSpan,
    Regex
}

public sealed class PropertyValidationRule
{
    public string RuleType { get; set; } = string.Empty;
    public string Parameter { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}

public enum PropertyUIHint
{
    Text,
    TextArea,
    Number,
    Checkbox,
    Select,
    MultiSelect,
    ColorPicker,
    DateTimePicker,
    TimeSpanPicker,
    JsonEditor,
    CodeEditor,
    FilePicker,
    DirectoryPicker,
    Slider,
    Toggle
}

public sealed class PropertyOption
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public enum NodeExecutorType
{
    BuiltIn,
    Plugin,
    External,
    JavaScript,
    Python,
    WASM
}

/// <summary>
/// Result of workflow execution.
/// </summary>
public sealed class WorkflowExecutionResult
{
    public Guid ExecutionId { get; set; } = Guid.NewGuid();
    public Guid WorkflowId { get; set; }
    public WorkflowExecutionStatus Status { get; set; } = WorkflowExecutionStatus.Pending;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public TimeSpan Duration { get; set; }
    public Dictionary<string, object> Outputs { get; set; } = new();
    public Dictionary<string, NodeExecutionResult> NodeResults { get; set; } = new();
    public List<ExecutionLogEntry> Logs { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object> FinalVariables { get; set; } = new();
}

public enum WorkflowExecutionStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled,
    Paused
}

public sealed class NodeExecutionResult
{
    public string NodeId { get; set; } = string.Empty;
    public NodeExecutionStatus Status { get; set; } = NodeExecutionStatus.Pending;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public TimeSpan Duration { get; set; }
    public Dictionary<string, object> Inputs { get; set; } = new();
    public Dictionary<string, object> Outputs { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public List<ExecutionLogEntry> Logs { get; set; } = new();
    public int RetryCount { get; set; } = 0;
}

public enum NodeExecutionStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Skipped,
    Retrying
}

public sealed class ExecutionLogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public LogLevel Level { get; set; } = LogLevel.Information;
    public string Message { get; set; } = string.Empty;
    public string? NodeId { get; set; }
    public string? Source { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
}

public enum LogLevel
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Critical
}

/// <summary>
/// Context passed to node executors during execution.
/// </summary>
public sealed class NodeExecutionContext
{
    public Guid ExecutionId { get; set; }
    public Guid WorkflowId { get; set; }
    public string NodeId { get; set; } = string.Empty;
    public WorkflowNode Node { get; set; } = null!;
    public VisualWorkflow Workflow { get; set; } = null!;
    public Dictionary<string, object> Variables { get; set; } = new();
    public Dictionary<string, object> Inputs { get; set; } = new();
    public IServiceProvider Services { get; set; } = null!;
    public ILogger Logger { get; set; } = null!;
    public CancellationToken CancellationToken { get; set; }
    public Dictionary<string, object> Services { get; set; } = new();
    public INodeOutputCollector OutputCollector { get; set; } = null!;
}

/// <summary>
/// Interface for collecting node outputs during execution.
/// </summary>
public interface INodeOutputCollector
{
    void SetOutput(string portId, object value);
    void SetOutputs(Dictionary<string, object> outputs);
    void AddLog(LogLevel level, string message, Dictionary<string, object>? data = null);
}

/// <summary>
/// Interface for executing workflow nodes.
/// </summary>
public interface INodeExecutor
{
    string NodeTypeId { get; }
    Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken ct = default);
    Task<bool> ValidateAsync(WorkflowNode node, CancellationToken ct = default);
}

/// <summary>
/// Interface for the visual scripting engine.
/// </summary>
public interface IVisualScriptingEngine
{
    Task<WorkflowValidationResult> ValidateWorkflowAsync(VisualWorkflow workflow, CancellationToken ct = default);
    Task<WorkflowExecutionResult> ExecuteWorkflowAsync(VisualWorkflow workflow, Dictionary<string, object>? initialVariables = null, CancellationToken ct = default);
    Task<WorkflowExecutionResult> ResumeWorkflowAsync(Guid executionId, CancellationToken ct = default);
    Task<bool> CancelExecutionAsync(Guid executionId, CancellationToken ct = default);
    Task<WorkflowExecutionResult?> GetExecutionStatusAsync(Guid executionId, CancellationToken ct = default);
    Task<List<WorkflowExecutionResult>> GetExecutionHistoryAsync(Guid workflowId, int page = 1, int pageSize = 20, CancellationToken ct = default);
}

public sealed class WorkflowValidationResult
{
    public bool IsValid { get; set; }
    public List<WorkflowValidationError> Errors { get; set; } = new();
    public List<WorkflowValidationWarning> Warnings { get; set; } = new();
    public Dictionary<string, object> Analysis { get; set; } = new();
}

public sealed class WorkflowValidationError
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? NodeId { get; set; }
    public string? PortId { get; set; }
    public ValidationSeverity Severity { get; set; } = ValidationSeverity.Error;
    public string? SuggestedFix { get; set; }
}

public sealed class WorkflowValidationWarning
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? NodeId { get; set; }
    public string? PortId { get; set; }
    public string? SuggestedFix { get; set; }
}

public enum ValidationSeverity
{
    Info,
    Warning,
    Error
}