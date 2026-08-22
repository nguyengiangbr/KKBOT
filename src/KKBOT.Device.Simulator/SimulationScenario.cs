// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using KKBOT.Device.Abstractions;

namespace KKBOT.Device.Simulator;

/// <summary>
/// Represents a simple simulation scenario.
/// The file is JSON (or YAML) where each step describes an action.
/// </summary>
public sealed class SimulationScenario
{
    public List<ScenarioStep> Steps { get; set; } = new();
}

public sealed class ScenarioStep
{
    /// <summary>
    /// Action to perform: "capture", "input", "delay".
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Optional delay before executing this step (milliseconds).
    /// </summary>
    public int? DelayMs { get; set; }

    /// <summary>
    /// Optional input command (used when Action=="input").
    /// </summary>
    public InputCommand? Input { get; set; }
}
