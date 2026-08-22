// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace KKBOT.Device.Simulator;

/// <summary>
/// Loads a simulation scenario from a JSON file.
/// </summary>
public sealed class ScenarioLoader
{
    private readonly ILogger<ScenarioLoader> _logger;

    public ScenarioLoader(ILogger<ScenarioLoader> logger)
    {
        _logger = logger;
    }

    public async Task<SimulationScenario?> LoadAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(path);
            var scenario = await JsonSerializer.DeserializeAsync<SimulationScenario>(stream, cancellationToken: cancellationToken);
            return scenario;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load simulation scenario from {Path}", path);
            return null;
        }
    }
}
