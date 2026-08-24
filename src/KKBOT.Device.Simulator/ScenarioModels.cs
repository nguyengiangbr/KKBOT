// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using KKBOT.Device.Abstractions;

namespace KKBOT.Device.Simulator;

/// <summary>
/// Represents a complete simulation scenario with advanced features.
/// </summary>
public sealed class SimulationScenario
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = "2.0";
    public Dictionary<string, object> Variables { get; set; } = new();
    public List<ScenarioStep> Steps { get; set; } = new();
    public ScenarioConfig Config { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Configuration for scenario execution.
/// </summary>
public sealed class ScenarioConfig
{
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public bool ContinueOnError { get; set; } = false;
    public int MaxRetries { get; set; } = 0;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
    public bool CaptureScreenshots { get; set; } = true;
    public string ScreenshotPath { get; set; } = "scenarios/screenshots";
    public Dictionary<string, string> DeviceOverrides { get; set; } = new();
}

/// <summary>
/// Base class for all scenario steps.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(CaptureStep), "capture")]
[JsonDerivedType(typeof(InputStep), "input")]
[JsonDerivedType(typeof(DelayStep), "delay")]
[JsonDerivedType(typeof(AssertStep), "assert")]
[JsonDerivedType(typeof(IfStep), "if")]
[JsonDerivedType(typeof(LoopStep), "loop")]
[JsonDerivedType(typeof(ParallelStep), "parallel")]
[JsonDerivedType(typeof(CallStep), "call")]
[JsonDerivedType(typeof(SetVariableStep), "set")]
[JsonDerivedType(typeof(WaitForStep), "waitFor")]
public abstract class ScenarioStep
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TimeSpan? Timeout { get; set; }
    public bool ContinueOnError { get; set; } = false;
    public int RetryCount { get; set; } = 0;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Captures a frame from the device.
/// </summary>
public sealed class CaptureStep : ScenarioStep
{
    public Rectangle? Region { get; set; }
    public string Format { get; set; } = "png";
    public int Quality { get; set; } = 90;
    public string SaveAs { get; set; } = string.Empty; // Variable name to save frame data
}

/// <summary>
/// Sends an input command to the device.
/// </summary>
public sealed class InputStep : ScenarioStep
{
    public InputType Type { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int EndX { get; set; }
    public int EndY { get; set; }
    public string? Text { get; set; }
    public TimeSpan? Duration { get; set; }
    public int KeyCode { get; set; }
    
    // Support for variable interpolation
    public string? XVar { get; set; }
    public string? YVar { get; set; }
}

/// <summary>
/// Delays execution for a specified time.
/// </summary>
public sealed class DelayStep : ScenarioStep
{
    public TimeSpan Duration { get; set; }
    public string? DurationVar { get; set; } // Variable containing duration
}

/// <summary>
/// Asserts a condition on the captured frame or device state.
/// </summary>
public sealed class AssertStep : ScenarioStep
{
    public AssertType AssertType { get; set; }
    public string Expression { get; set; } = string.Empty; // CEL expression or simple condition
    public string? TemplateName { get; set; } // For template matching assertion
    public double Threshold { get; set; } = 0.85;
    public Rectangle? Region { get; set; }
    public string? ExpectedText { get; set; } // For OCR text assertion
    public string? Variable { get; set; } // Variable to assert on
    public object? ExpectedValue { get; set; }
    public AssertOperator Operator { get; set; } = AssertOperator.Equals;
}

/// <summary>
/// Types of assertions.
/// </summary>
public enum AssertType
{
    TemplateMatch,    // Template matching on frame
    OcrText,          // OCR text recognition
    PixelColor,       // Pixel color at coordinates
    VariableValue,    // Variable value comparison
    DeviceState,      // Device connection/state
    Custom            // Custom CEL expression
}

/// <summary>
/// Assertion operators.
/// </summary>
public enum AssertOperator
{
    Equals,
    NotEquals,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    Contains,
    NotContains,
    Matches,          // Regex match
    Exists,
    NotExists
}

/// <summary>
/// Conditional branching step.
/// </summary>
public sealed class IfStep : ScenarioStep
{
    public string Condition { get; set; } = string.Empty; // CEL expression or variable name
    public List<ScenarioStep> ThenSteps { get; set; } = new();
    public List<ScenarioStep> ElseSteps { get; set; } = new();
}

/// <summary>
/// Loop step for repeating steps.
/// </summary>
public sealed class LoopStep : ScenarioStep
{
    public LoopType LoopType { get; set; } = LoopType.Count;
    public int Count { get; set; } = 1;
    public string? CountVar { get; set; } // Variable containing count
    public string? WhileCondition { get; set; } // For while loops
    public string? ForEachVar { get; set; } // Variable containing array to iterate
    public string IteratorVariable { get; set; } = "item"; // Name of iterator variable
    public List<ScenarioStep> Steps { get; set; } = new();
    public TimeSpan MaxDuration { get; set; } = TimeSpan.FromMinutes(5); // Safety timeout
}

/// <summary>
/// Loop types.
/// </summary>
public enum LoopType
{
    Count,      // Repeat N times
    While,      // While condition is true
    ForEach,    // Iterate over array
    Until       // Repeat until condition is true
}

/// <summary>
/// Parallel execution of steps.
/// </summary>
public sealed class ParallelStep : ScenarioStep
{
    public List<ScenarioStep> Steps { get; set; } = new();
    public int MaxParallelism { get; set; } = 4;
    public bool WaitForAll { get; set; } = true;
}

/// <summary>
/// Calls another scenario (sub-scenario).
/// </summary>
public sealed class CallStep : ScenarioStep
{
    public string ScenarioId { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public string ReturnVariable { get; set; } = string.Empty;
}

/// <summary>
/// Sets a variable value.
/// </summary>
public sealed class SetVariableStep : ScenarioStep
{
    public string Name { get; set; } = string.Empty;
    public object? Value { get; set; }
    public string? Expression { get; set; } // CEL expression to evaluate
}

/// <summary>
/// Waits for a condition to become true.
/// </summary>
public sealed class WaitForStep : ScenarioStep
{
    public string Condition { get; set; } = string.Empty; // CEL expression
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(500);
    public TimeSpan MaxWaitTime { get; set; } = TimeSpan.FromSeconds(30);
    public string? TemplateName { get; set; } // Wait for template match
    public double TemplateThreshold { get; set; } = 0.85;
    public Rectangle? Region { get; set; }
}

/// <summary>
/// Result of scenario execution.
/// </summary>
public sealed class ScenarioExecutionResult
{
    public string ScenarioId { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public TimeSpan Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : TimeSpan.Zero;
    public List<StepExecutionResult> StepResults { get; set; } = new();
    public Dictionary<string, object> Variables { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public string? FailedStepId { get; set; }
    public List<string> Screenshots { get; set; } = new();
}

/// <summary>
/// Result of a single step execution.
/// </summary>
public sealed class StepExecutionResult
{
    public string StepId { get; set; } = string.Empty;
    public string StepType { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public TimeSpan Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : TimeSpan.Zero;
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object> Output { get; set; } = new();
    public List<string> Screenshots { get; set; } = new();
    public int RetryAttempt { get; set; }
}
