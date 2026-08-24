// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using KKBOT.Device.Abstractions;

namespace KKBOT.Device.Simulator;

/// <summary>
/// Parses simulation scenarios from JSON files with full DSL support.
/// </summary>
public sealed class ScenarioParser
{
    private readonly ILogger<ScenarioParser> _logger;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public ScenarioParser(ILogger<ScenarioParser> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Loads a scenario from a JSON file.
    /// </summary>
    public async Task<SimulationScenario?> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(path))
            {
                _logger.LogError("Scenario file not found: {Path}", path);
                return null;
            }

            await using var stream = File.OpenRead(path);
            var scenario = await JsonSerializer.DeserializeAsync<SimulationScenario>(stream, _jsonOptions, cancellationToken);
            
            if (scenario != null)
            {
                // Validate scenario
                var validation = ValidateScenario(scenario);
                if (!validation.IsValid)
                {
                    _logger.LogError("Scenario validation failed: {Errors}", string.Join(", ", validation.Errors));
                    return null;
                }

                _logger.LogInformation("Loaded scenario: {Name} ({Id}) with {StepCount} steps", 
                    scenario.Name, scenario.Id, scenario.Steps.Count);
            }

            return scenario;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse scenario JSON: {Path}", path);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading scenario: {Path}", path);
            return null;
        }
    }

    /// <summary>
    /// Loads a scenario from a JSON string.
    /// </summary>
    public SimulationScenario? Parse(string json)
    {
        try
        {
            var scenario = JsonSerializer.Deserialize<SimulationScenario>(json, _jsonOptions);
            if (scenario != null)
            {
                var validation = ValidateScenario(scenario);
                if (!validation.IsValid)
                {
                    _logger.LogError("Scenario validation failed: {Errors}", string.Join(", ", validation.Errors));
                    return null;
                }
            }
            return scenario;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse scenario JSON string");
            return null;
        }
    }

    /// <summary>
    /// Validates a scenario for correctness.
    /// </summary>
    public ScenarioValidationResult ValidateScenario(SimulationScenario scenario)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(scenario.Name))
            errors.Add("Scenario name is required");

        if (scenario.Steps == null || scenario.Steps.Count == 0)
            warnings.Add("Scenario has no steps");

        // Validate step IDs are unique
        var stepIds = new HashSet<string>();
        foreach (var step in scenario.Steps ?? Enumerable.Empty<ScenarioStep>())
        {
            if (!stepIds.Add(step.Id))
                errors.Add($"Duplicate step ID: {step.Id}");

            ValidateStep(step, errors, warnings);
        }

        // Check for unreachable steps (basic check)
        if (scenario.Steps != null)
        {
            for (int i = 0; i < scenario.Steps.Count - 1; i++)
            {
                if (scenario.Steps[i] is DelayStep delay && delay.Duration == TimeSpan.Zero && string.IsNullOrEmpty(delay.DurationVar))
                {
                    warnings.Add($"Step {scenario.Steps[i].Id} has zero delay");
                }
            }
        }

        return new ScenarioValidationResult(errors.Count == 0, errors, warnings);
    }

    private void ValidateStep(ScenarioStep step, List<string> errors, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(step.Id))
            errors.Add("Step ID is required");

        switch (step)
        {
            case CaptureStep capture:
                if (!string.IsNullOrEmpty(capture.SaveAs) && !IsValidVariableName(capture.SaveAs))
                    errors.Add($"Invalid variable name in CaptureStep: {capture.SaveAs}");
                break;

            case InputStep input:
                if (input.Type == InputType.Swipe && (input.EndX == 0 && input.EndY == 0))
                    warnings.Add($"Swipe input missing end coordinates: {input.Id}");
                if (input.Type == InputType.Text && string.IsNullOrEmpty(input.Text))
                    warnings.Add($"Text input missing text: {input.Id}");
                break;

            case AssertStep assert:
                if (string.IsNullOrWhiteSpace(assert.Expression) && 
                    assert.AssertType != AssertType.DeviceState &&
                    assert.AssertType != AssertType.VariableValue &&
                    string.IsNullOrEmpty(assert.TemplateName))
                    errors.Add($"AssertStep {assert.Id} missing assertion criteria");
                break;

            case IfStep ifStep:
                if (string.IsNullOrWhiteSpace(ifStep.Condition))
                    errors.Add($"IfStep {ifStep.Id} missing condition");
                foreach (var s in ifStep.ThenSteps ?? Enumerable.Empty<ScenarioStep>())
                    ValidateStep(s, errors, warnings);
                foreach (var s in ifStep.ElseSteps ?? Enumerable.Empty<ScenarioStep>())
                    ValidateStep(s, errors, warnings);
                break;

            case LoopStep loop:
                if (loop.LoopType == LoopType.Count && loop.Count <= 0)
                    errors.Add($"LoopStep {loop.Id} count must be > 0");
                if (loop.LoopType == LoopType.While && string.IsNullOrWhiteSpace(loop.WhileCondition))
                    errors.Add($"While loop {loop.Id} missing condition");
                if (loop.LoopType == LoopType.ForEach && string.IsNullOrWhiteSpace(loop.ForEachVar))
                    errors.Add($"ForEach loop {loop.Id} missing variable");
                foreach (var s in loop.Steps ?? Enumerable.Empty<ScenarioStep>())
                    ValidateStep(s, errors, warnings);
                break;

            case ParallelStep parallel:
                if (parallel.Steps == null || parallel.Steps.Count == 0)
                    warnings.Add($"ParallelStep {parallel.Id} has no steps");
                if (parallel.MaxParallelism <= 0)
                    errors.Add($"ParallelStep {parallel.Id} max parallelism must be > 0");
                foreach (var s in parallel.Steps ?? Enumerable.Empty<ScenarioStep>())
                    ValidateStep(s, errors, warnings);
                break;

            case CallStep call:
                if (string.IsNullOrWhiteSpace(call.ScenarioId))
                    errors.Add($"CallStep {call.Id} missing scenario ID");
                break;

            case SetVariableStep setVar:
                if (string.IsNullOrWhiteSpace(setVar.Name))
                    errors.Add($"SetVariableStep {setVar.Id} missing variable name");
                if (!IsValidVariableName(setVar.Name))
                    errors.Add($"Invalid variable name: {setVar.Name}");
                break;

            case WaitForStep waitFor:
                if (string.IsNullOrWhiteSpace(waitFor.Condition) && string.IsNullOrEmpty(waitFor.TemplateName))
                    errors.Add($"WaitForStep {waitFor.Id} missing condition or template");
                if (waitFor.MaxWaitTime <= TimeSpan.Zero)
                    errors.Add($"WaitForStep {waitFor.Id} max wait time must be > 0");
                if (waitFor.PollInterval <= TimeSpan.Zero)
                    errors.Add($"WaitForStep {waitFor.Id} poll interval must be > 0");
                break;
        }
    }

    private static bool IsValidVariableName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        if (!char.IsLetter(name[0]) && name[0] != '_') return false;
        return name.All(c => char.IsLetterOrDigit(c) || c == '_');
    }
}

/// <summary>
/// Result of scenario validation.
/// </summary>
public sealed record ScenarioValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);
