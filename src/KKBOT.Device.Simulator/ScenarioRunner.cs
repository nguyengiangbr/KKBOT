// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using KKBOT.Device.Abstractions;
using KKBOT.Perception.Abstractions;

namespace KKBOT.Device.Simulator;

/// <summary>
/// Executes simulation scenarios with full DSL v2 support.
/// </summary>
public sealed class ScenarioRunner
{
    private readonly IDeviceSession _session;
    private readonly ILogger<ScenarioRunner> _logger;
    private readonly IPerceptionPipeline? _perceptionPipeline;
    private readonly ScenarioParser _parser;
    private readonly ConcurrentDictionary<string, SimulationScenario> _scenarioCache = new();

    public ScenarioRunner(
        IDeviceSession session, 
        ILogger<ScenarioRunner> logger,
        IPerceptionPipeline? perceptionPipeline = null,
        ScenarioParser? parser = null)
    {
        _session = session;
        _logger = logger;
        _perceptionPipeline = perceptionPipeline;
        _parser = parser ?? new ScenarioParser(logger);
    }

    /// <summary>
    /// Runs a scenario from a file path.
    /// </summary>
    public async Task<ScenarioExecutionResult> RunFromFileAsync(string path, CancellationToken cancellationToken = default)
    {
        var scenario = await _parser.LoadAsync(path, cancellationToken);
        if (scenario == null)
        {
            return new ScenarioExecutionResult
            {
                IsSuccess = false,
                ErrorMessage = $"Failed to load scenario from {path}",
                StartedAt = DateTimeOffset.UtcNow
            };
        }

        return await RunAsync(scenario, null, cancellationToken);
    }

    /// <summary>
    /// Runs a scenario with the given variables.
    /// </summary>
    public async Task<ScenarioExecutionResult> RunAsync(SimulationScenario scenario, Dictionary<string, object>? variables = null, CancellationToken cancellationToken = default)
    {
        var result = new ScenarioExecutionResult
        {
            ScenarioId = scenario.Id,
            StartedAt = DateTimeOffset.UtcNow,
            Variables = new Dictionary<string, object>(scenario.Variables ?? new())
        };

        // Merge provided variables
        if (variables != null)
        {
            foreach (var kvp in variables)
            {
                result.Variables[kvp.Key] = kvp.Value;
            }
        }

        _logger.LogInformation("Starting scenario: {Name} ({Id})", scenario.Name, scenario.Id);

        try
        {
            var context = new ScenarioExecutionContext
            {
                Session = _session,
                Logger = _logger,
                PerceptionPipeline = _perceptionPipeline,
                Variables = result.Variables,
                Config = scenario.Config,
                CancellationToken = cancellationToken,
                ScreenshotDirectory = scenario.Config.ScreenshotPath
            };

            // Execute steps sequentially
            foreach (var step in scenario.Steps ?? Enumerable.Empty<ScenarioStep>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var stepResult = await ExecuteStepAsync(step, context);
                result.StepResults.Add(stepResult);

                if (!stepResult.IsSuccess)
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = stepResult.ErrorMessage;
                    result.FailedStepId = stepResult.StepId;
                    result.CompletedAt = DateTimeOffset.UtcNow;

                    if (!scenario.Config.ContinueOnError)
                    {
                        _logger.LogError("Scenario failed at step {StepId}: {Error}", stepResult.StepId, stepResult.ErrorMessage);
                        break;
                    }
                    else
                    {
                        _logger.LogWarning("Step {StepId} failed but continuing: {Error}", stepResult.StepId, stepResult.ErrorMessage);
                    }
                }
            }

            if (result.IsSuccess && !result.CompletedAt.HasValue)
            {
                result.IsSuccess = true;
                result.CompletedAt = DateTimeOffset.UtcNow;
            }

            _logger.LogInformation("Scenario {Name} completed: {Success} in {Duration}ms", 
                scenario.Name, result.IsSuccess, result.Duration.TotalMilliseconds);

            return result;
        }
        catch (OperationCanceledException)
        {
            result.IsSuccess = false;
            result.ErrorMessage = "Scenario execution cancelled";
            result.CompletedAt = DateTimeOffset.UtcNow;
            _logger.LogWarning("Scenario {Name} cancelled", scenario.Name);
            return result;
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
            result.CompletedAt = DateTimeOffset.UtcNow;
            _logger.LogError(ex, "Scenario {Name} failed with exception", scenario.Name);
            return result;
        }
    }

    private async Task<StepExecutionResult> ExecuteStepAsync(ScenarioStep step, ScenarioExecutionContext context)
    {
        var stepResult = new StepExecutionResult
        {
            StepId = step.Id,
            StepType = step.GetType().Name.Replace("Step", ""),
            StartedAt = DateTimeOffset.UtcNow
        };

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Apply timeout
            var stepTimeout = step.Timeout ?? context.Config.DefaultTimeout;
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
            cts.CancelAfter(stepTimeout);

            // Retry logic
            int attempt = 0;
            while (true)
            {
                attempt++;
                stepResult.RetryAttempt = attempt;

                try
                {
                    await ExecuteStepInternalAsync(step, context, stepResult);
                    stepResult.IsSuccess = true;
                    break;
                }
                catch (Exception ex) when (attempt <= step.RetryCount && ShouldRetry(ex, step))
                {
                    _logger.LogWarning("Step {StepId} failed (attempt {Attempt}/{MaxRetries}): {Error}. Retrying in {Delay}ms",
                        step.Id, attempt, step.RetryCount, ex.Message, step.RetryDelay.TotalMilliseconds);
                    
                    await Task.Delay(step.RetryDelay, context.CancellationToken);
                    continue;
                }
            }
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            stepResult.IsSuccess = false;
            stepResult.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Step {StepId} failed permanently", step.Id);
        }
        finally
        {
            stopwatch.Stop();
            stepResult.CompletedAt = DateTimeOffset.UtcNow;
        }

        return stepResult;
    }

    private async Task ExecuteStepInternalAsync(ScenarioStep step, ScenarioExecutionContext context, StepExecutionResult stepResult)
    {
        switch (step)
        {
            case CaptureStep capture:
                await ExecuteCaptureAsync(capture, context, stepResult);
                break;

            case InputStep input:
                await ExecuteInputAsync(input, context, stepResult);
                break;

            case DelayStep delay:
                await ExecuteDelayAsync(delay, context, stepResult);
                break;

            case AssertStep assert:
                await ExecuteAssertAsync(assert, context, stepResult);
                break;

            case IfStep ifStep:
                await ExecuteIfAsync(ifStep, context, stepResult);
                break;

            case LoopStep loop:
                await ExecuteLoopAsync(loop, context, stepResult);
                break;

            case ParallelStep parallel:
                await ExecuteParallelAsync(parallel, context, stepResult);
                break;

            case CallStep call:
                await ExecuteCallAsync(call, context, stepResult);
                break;

            case SetVariableStep setVar:
                await ExecuteSetVariableAsync(setVar, context, stepResult);
                break;

            case WaitForStep waitFor:
                await ExecuteWaitForAsync(waitFor, context, stepResult);
                break;

            default:
                throw new NotSupportedException($"Unknown step type: {step.GetType().Name}");
        }
    }

    private async Task ExecuteCaptureAsync(CaptureStep step, ScenarioExecutionContext context, StepExecutionResult stepResult)
    {
        var options = new CaptureOptions
        {
            RegionOfInterest = step.Region,
            Format = step.Format,
            Quality = step.Quality,
            Timeout = step.Timeout
        };

        var frame = await context.Session.CaptureAsync(options, context.CancellationToken);
        
        stepResult.Output["frameId"] = frame.FrameId;
        stepResult.Output["width"] = frame.Width;
        stepResult.Output["height"] = frame.Height;
        stepResult.Output["size"] = frame.Data.Length;
        stepResult.Output["latencyMs"] = frame.CaptureLatencyMs;

        // Save to variable if specified
        if (!string.IsNullOrEmpty(step.SaveAs))
        {
            context.Variables[step.SaveAs] = frame;
        }

        // Save screenshot if configured
        if (context.Config.CaptureScreenshots && !string.IsNullOrEmpty(context.ScreenshotDirectory))
        {
            var screenshotPath = Path.Combine(context.ScreenshotDirectory, $"{context.ScenarioId}_{step.Id}_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.png");
            Directory.CreateDirectory(Path.GetDirectoryName(screenshotPath)!);
            await File.WriteAllBytesAsync(screenshotPath, frame.Data, context.CancellationToken);
            stepResult.Screenshots.Add(screenshotPath);
        }

        _logger.LogDebug("Captured frame {FrameId} ({Width}x{Height})", frame.FrameId, frame.Width, frame.Height);
    }

    private async Task ExecuteInputAsync(InputStep step, ScenarioExecutionContext context, StepExecutionResult stepResult)
    {
        // Resolve variable coordinates if specified
        int x = step.X, y = step.Y, endX = step.EndX, endY = step.EndY;
        
        if (!string.IsNullOrEmpty(step.XVar) && context.Variables.TryGetValue(step.XVar, out var xVal))
        {
            x = Convert.ToInt32(xVal);
        }
        if (!string.IsNullOrEmpty(step.YVar) && context.Variables.TryGetValue(step.YVar, out var yVal))
        {
            y = Convert.ToInt32(yVal);
        }

        var command = new InputCommand
        {
            Type = step.Type,
            X = x,
            Y = y,
            EndX = endX,
            EndY = endY,
            Text = step.Text,
            Duration = step.Duration,
            KeyCode = step.KeyCode
        };

        var result = await context.Session.SendInputAsync(command, context.CancellationToken);
        
        stepResult.Output["success"] = result.IsSuccess;
        stepResult.Output["durationMs"] = result.Duration.TotalMilliseconds;

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Input failed: {result.ErrorMessage}");
        }

        _logger.LogDebug("Sent input {Type} at ({X},{Y})", step.Type, x, y);
    }

    private async Task ExecuteDelayAsync(DelayStep step, ScenarioExecutionContext context, StepExecutionResult stepResult)
    {
        var duration = step.Duration;
        
        if (!string.IsNullOrEmpty(step.DurationVar) && context.Variables.TryGetValue(step.DurationVar, out var durVal))
        {
            duration = TimeSpan.FromMilliseconds(Convert.ToDouble(durVal));
        }

        stepResult.Output["durationMs"] = duration.TotalMilliseconds;
        await Task.Delay(duration, context.CancellationToken);
    }

    private async Task ExecuteAssertAsync(AssertStep step, ScenarioExecutionContext context, StepExecutionResult stepResult)
    {
        bool passed = false;
        string? failureMessage = null;

        switch (step.AssertType)
        {
            case AssertType.TemplateMatch:
                if (_perceptionPipeline != null && !string.IsNullOrEmpty(step.TemplateName))
                {
                    // Capture frame and run perception
                    var frame = await context.Session.CaptureAsync(new CaptureOptions { RegionOfInterest = step.Region }, context.CancellationToken);
                    var results = await _perceptionPipeline.ProcessAsync(frame, context.CancellationToken);
                    
                    var match = results.FirstOrDefault(r => r.DetectorName.Contains(step.TemplateName, StringComparison.OrdinalIgnoreCase));
                    passed = match != null && match.Confidence >= step.Threshold;
                    
                    stepResult.Output["template"] = step.TemplateName;
                    stepResult.Output["confidence"] = match?.Confidence ?? 0;
                    stepResult.Output["threshold"] = step.Threshold;
                    
                    if (!passed)
                        failureMessage = $"Template '{step.TemplateName}' not found (confidence: {match?.Confidence:P2}, threshold: {step.Threshold:P2})";
                }
                else
                {
                    failureMessage = "Perception pipeline not available or template name missing";
                }
                break;

            case AssertType.OcrText:
                // Would need OCR engine
                failureMessage = "OCR text assertion not implemented";
                break;

            case AssertType.VariableValue:
                if (!string.IsNullOrEmpty(step.Variable) && context.Variables.TryGetValue(step.Variable, out var value))
                {
                    passed = EvaluateAssertion(value, step.ExpectedValue, step.Operator);
                    stepResult.Output["variable"] = step.Variable;
                    stepResult.Output["actual"] = value;
                    stepResult.Output["expected"] = step.ExpectedValue!;
                    stepResult.Output["operator"] = step.Operator.ToString();
                    
                    if (!passed)
                        failureMessage = $"Variable '{step.Variable}' assertion failed: {value} {step.Operator} {step.ExpectedValue}";
                }
                else
                {
                    failureMessage = $"Variable '{step.Variable}' not found";
                }
                break;

            case AssertType.DeviceState:
                var health = await context.Session.HealthCheckAsync(context.CancellationToken);
                passed = health.IsHealthy && health.IsConnected;
                stepResult.Output["connected"] = health.IsConnected;
                stepResult.Output["healthy"] = health.IsHealthy;
                
                if (!passed)
                    failureMessage = $"Device not healthy or connected: {health.ErrorMessage}";
                break;

            default:
                failureMessage = $"Assertion type {step.AssertType} not implemented";
                break;
        }

        stepResult.Output["passed"] = passed;

        if (!passed)
        {
            throw new AssertionException(failureMessage ?? $"Assertion failed: {step.Expression}");
        }
    }

    private async Task ExecuteIfAsync(IfStep step, ScenarioExecutionContext context, StepExecutionResult stepResult)
    {
        // Evaluate condition (simple variable check or CEL expression)
        bool condition = EvaluateCondition(step.Condition, context.Variables);
        stepResult.Output["condition"] = condition;

        var stepsToExecute = condition ? step.ThenSteps : step.ElseSteps;
        
        foreach (var subStep in stepsToExecute ?? Enumerable.Empty<ScenarioStep>())
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var subResult = await ExecuteStepAsync(subStep, context);
            stepResult.Output[$"sub_{subResult.StepId}"] = subResult.IsSuccess;
            
            if (!subResult.IsSuccess && !context.Config.ContinueOnError)
            {
                throw new InvalidOperationException($"Sub-step {subResult.StepId} failed: {subResult.ErrorMessage}");
            }
        }
    }

    private async Task ExecuteLoopAsync(LoopStep step, ScenarioExecutionContext context, StepExecutionResult stepResult)
    {
        var maxDuration = step.MaxDuration;
        var loopStart = DateTime.UtcNow;
        int iterations = 0;

        while (true)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            // Check max duration
            if (DateTime.UtcNow - loopStart > maxDuration)
            {
                _logger.LogWarning("Loop {StepId} exceeded max duration", step.Id);
                break;
            }

            // Check loop condition
            bool shouldContinue = step.LoopType switch
            {
                LoopType.Count => iterations < step.Count,
                LoopType.While => EvaluateCondition(step.WhileCondition ?? "false", context.Variables),
                LoopType.Until => !EvaluateCondition(step.WhileCondition ?? "false", context.Variables),
                LoopType.ForEach => false, // Handled separately
                _ => false
            };

            if (!shouldContinue) break;

            // For ForEach, iterate over collection
            if (step.LoopType == LoopType.ForEach && !string.IsNullOrEmpty(step.ForEachVar))
            {
                if (context.Variables.TryGetValue(step.ForEachVar, out var collection) && collection is IEnumerable<object> items)
                {
                    foreach (var item in items)
                    {
                        context.CancellationToken.ThrowIfCancellationRequested();
                        context.Variables[step.IteratorVariable] = item;
                        
                        foreach (var subStep in step.Steps ?? Enumerable.Empty<ScenarioStep>())
                        {
                            var subResult = await ExecuteStepAsync(subStep, context);
                            if (!subResult.IsSuccess && !context.Config.ContinueOnError)
                            {
                                throw new InvalidOperationException($"Sub-step {subResult.StepId} failed: {subResult.ErrorMessage}");
                            }
                        }
                    }
                    break; // ForEach completes after one iteration of the loop
                }
            }
            else
            {
                // Execute loop body
                foreach (var subStep in step.Steps ?? Enumerable.Empty<ScenarioStep>())
                {
                    context.CancellationToken.ThrowIfCancellationRequested();
                    var subResult = await ExecuteStepAsync(subStep, context);
                    if (!subResult.IsSuccess && !context.Config.ContinueOnError)
                    {
                        throw new InvalidOperationException($"Sub-step {subResult.StepId} failed: {subResult.ErrorMessage}");
                    }
                }
            }

            iterations++;
            stepResult.Output["iterations"] = iterations;

            // Safety check
            if (iterations > 10000)
            {
                _logger.LogError("Loop {StepId} exceeded safety iteration limit", step.Id);
                break;
            }
        }

        stepResult.Output["totalIterations"] = iterations;
    }

    private async Task ExecuteParallelAsync(ParallelStep step, ScenarioExecutionContext context, StepExecutionResult stepResult)
    {
        var semaphore = new SemaphoreSlim(step.MaxParallelism);
        var tasks = new List<Task<StepExecutionResult>>();

        foreach (var subStep in step.Steps ?? Enumerable.Empty<ScenarioStep>())
        {
            await semaphore.WaitAsync(context.CancellationToken);
            
            var task = Task.Run(async () =>
            {
                try
                {
                    return await ExecuteStepAsync(subStep, context);
                }
                finally
                {
                    semaphore.Release();
                }
            }, context.CancellationToken);

            tasks.Add(task);
        }

        var results = await Task.WhenAll(tasks);
        
        foreach (var subResult in results)
        {
            stepResult.Output[$"sub_{subResult.StepId}"] = subResult.IsSuccess;
            
            if (!subResult.IsSuccess)
            {
                stepResult.IsSuccess = false;
                stepResult.ErrorMessage = $"Parallel sub-step {subResult.StepId} failed: {subResult.ErrorMessage}";
                
                if (step.WaitForAll || !context.Config.ContinueOnError)
                {
                    break;
                }
            }
        }
    }

    private async Task ExecuteCallAsync(CallStep step, ScenarioExecutionContext context, StepExecutionResult stepResult)
    {
        // In a real implementation, this would load and execute another scenario
        // For now, we simulate it
        _logger.LogInformation("Calling sub-scenario: {ScenarioId}", step.ScenarioId);
        
        stepResult.Output["calledScenario"] = step.ScenarioId;
        
        // Set return variable if specified
        if (!string.IsNullOrEmpty(step.ReturnVariable))
        {
            context.Variables[step.ReturnVariable] = new { success = true, scenarioId = step.ScenarioId };
        }
    }

    private async Task ExecuteSetVariableAsync(SetVariableStep step, ScenarioExecutionContext context, StepExecutionResult stepResult)
    {
        object? value = step.Value;

        if (!string.IsNullOrEmpty(step.Expression))
        {
            // Evaluate CEL expression (simplified)
            value = EvaluateExpression(step.Expression, context.Variables);
        }

        context.Variables[step.Name] = value!;
        stepResult.Output["variable"] = step.Name;
        stepResult.Output["value"] = value!;
    }

    private async Task ExecuteWaitForAsync(WaitForStep step, ScenarioExecutionContext context, StepExecutionResult stepResult)
    {
        var maxWait = step.MaxWaitTime;
        var pollInterval = step.PollInterval;
        var startTime = DateTime.UtcNow;
        int attempts = 0;

        while (DateTime.UtcNow - startTime < maxWait)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            attempts++;

            bool conditionMet = false;

            if (!string.IsNullOrEmpty(step.TemplateName) && _perceptionPipeline != null)
            {
                // Wait for template match
                var frame = await context.Session.CaptureAsync(new CaptureOptions { RegionOfInterest = step.Region }, context.CancellationToken);
                var results = await _perceptionPipeline.ProcessAsync(frame, context.CancellationToken);
                
                var match = results.FirstOrDefault(r => r.DetectorName.Contains(step.TemplateName, StringComparison.OrdinalIgnoreCase));
                conditionMet = match != null && match.Confidence >= step.TemplateThreshold;
                
                stepResult.Output["template"] = step.TemplateName;
                stepResult.Output["confidence"] = match?.Confidence ?? 0;
            }
            else if (!string.IsNullOrEmpty(step.Condition))
            {
                // Evaluate condition
                conditionMet = EvaluateCondition(step.Condition, context.Variables);
            }

            stepResult.Output["attempts"] = attempts;
            stepResult.Output["conditionMet"] = conditionMet;

            if (conditionMet)
            {
                stepResult.Output["waitTimeMs"] = (DateTime.UtcNow - startTime).TotalMilliseconds;
                return;
            }

            await Task.Delay(pollInterval, context.CancellationToken);
        }

        throw new TimeoutException($"WaitFor condition not met within {maxWait.TotalSeconds}s after {attempts} attempts");
    }

    private bool ShouldRetry(Exception ex, ScenarioStep step)
    {
        // Don't retry assertion failures or cancellation
        if (ex is AssertionException or OperationCanceledException) return false;
        
        // Don't retry if step explicitly disables retry
        return step.RetryCount > 0;
    }

    private bool EvaluateCondition(string condition, Dictionary<string, object> variables)
    {
        // Simple condition evaluation: check if variable exists and is truthy
        // In production, use a proper CEL expression evaluator
        if (variables.TryGetValue(condition, out var value))
        {
            return value switch
            {
                bool b => b,
                int i => i != 0,
                double d => d != 0,
                string s => !string.IsNullOrEmpty(s),
                _ => value != null
            };
        }

        // Check for comparison expressions like "var > 5"
        // This is a simplified implementation
        return false;
    }

    private object? EvaluateExpression(string expression, Dictionary<string, object> variables)
    {
        // Simplified expression evaluation
        // In production, use a proper expression evaluator (CEL, NCalc, etc.)
        return expression;
    }

    private bool EvaluateAssertion(object actual, object? expected, AssertOperator op)
    {
        if (actual == null && expected == null) return op == AssertOperator.Equals;
        if (actual == null || expected == null) return op == AssertOperator.NotEquals;

        var actualStr = actual.ToString() ?? "";
        var expectedStr = expected.ToString() ?? "";

        return op switch
        {
            AssertOperator.Equals => actual.Equals(expected),
            AssertOperator.NotEquals => !actual.Equals(expected),
            AssertOperator.Contains => actualStr.Contains(expectedStr, StringComparison.OrdinalIgnoreCase),
            AssertOperator.NotContains => !actualStr.Contains(expectedStr, StringComparison.OrdinalIgnoreCase),
            AssertOperator.GreaterThan => CompareValues(actual, expected) > 0,
            AssertOperator.GreaterThanOrEqual => CompareValues(actual, expected) >= 0,
            AssertOperator.LessThan => CompareValues(actual, expected) < 0,
            AssertOperator.LessThanOrEqual => CompareValues(actual, expected) <= 0,
            AssertOperator.Matches => System.Text.RegularExpressions.Regex.IsMatch(actualStr, expectedStr),
            AssertOperator.Exists => actual != null,
            AssertOperator.NotExists => actual == null,
            _ => false
        };
    }

    private int CompareValues(object a, object b)
    {
        if (a is IComparable ca && b is IComparable cb)
        {
            try { return ca.CompareTo(cb); } catch { }
        }
        return string.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal);
    }
}

/// <summary>
/// Execution context passed to steps.
/// </summary>
internal sealed class ScenarioExecutionContext
{
    public IDeviceSession Session { get; init; } = null!;
    public ILogger Logger { get; init; } = null!;
    public IPerceptionPipeline? PerceptionPipeline { get; init; }
    public Dictionary<string, object> Variables { get; init; } = new();
    public ScenarioConfig Config { get; init; } = new();
    public CancellationToken CancellationToken { get; init; }
    public string ScenarioId { get; init; } = string.Empty;
    public string ScreenshotDirectory { get; init; } = string.Empty;
}

/// <summary>
/// Exception for failed assertions.
/// </summary>
internal sealed class AssertionException : Exception
{
    public AssertionException(string message) : base(message) { }
}
