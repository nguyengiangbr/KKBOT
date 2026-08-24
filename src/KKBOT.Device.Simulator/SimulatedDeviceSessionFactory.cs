// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using KKBOT.Device.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading;
using System.Threading.Tasks;

namespace KKBOT.Device.Simulator;

/// <summary>
/// Factory for creating simulated device sessions.
/// </summary>
public sealed class SimulatedDeviceSessionFactory : IDeviceSessionFactory
{
    private readonly SimulatorOptions _options;
    private readonly ILogger<SimulatedDeviceSessionFactory> _logger;

    public SimulatedDeviceSessionFactory(
        IOptions<SimulatorOptions> options,
        ILogger<SimulatedDeviceSessionFactory> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IDeviceSession> CreateAsync(string deviceId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating simulated device session for {DeviceId}", deviceId);

        var scenarioPath = _options.ScenarioPath ?? Path.Combine(AppContext.BaseDirectory, "scenarios", deviceId);
        
        var session = new SimulatedDeviceSession(scenarioPath, _logger);
        await session.InitializeAsync(cancellationToken);
        
        return session;
    }
}

/// <summary>
/// Configuration options for simulator.
/// </summary>
public sealed class SimulatorOptions
{
    public string ScenarioPath { get; set; } = "scenarios";
    public bool EnableReplay { get; set; } = true;
    public int DefaultFrameRate { get; set; } = 30;
}