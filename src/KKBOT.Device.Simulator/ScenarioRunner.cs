using KKBOT.Device.Abstractions;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace KKBOT.Device.Simulator;

/// <summary>
/// Executes a defined scenario using a simulated device session.
/// </summary>
public sealed class ScenarioRunner
{
    private readonly IDeviceSession _session;
    private readonly ILogger<ScenarioRunner> _logger;

    public ScenarioRunner(IDeviceSession session, ILogger<ScenarioRunner> logger)
    {
        _session = session;
        _logger = logger;
    }

    public async Task RunAsync(SimulationScenario scenario, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting scenario on simulated device {DeviceId}", _session.DeviceId);
        foreach (var step in scenario.Steps)
        {
            if (cancellationToken.IsCancellationRequested) break;
            if (step.DelayMs.HasValue && step.DelayMs.Value > 0)
            {
                await Task.Delay(step.DelayMs.Value, cancellationToken);
            }
            switch (step.Action?.ToLowerInvariant())
            {
                case "capture":
                    var frame = await _session.CaptureFrameAsync(cancellationToken);
                    _logger.LogDebug("Captured simulated frame at {Timestamp}, size {Size} bytes", frame.Timestamp, frame.Data.Length);
                    break;
                case "input":
                    if (step.Input != null)
                    {
                        var result = await _session.SendInputAsync(step.Input, cancellationToken);
                        _logger.LogDebug("Sent simulated input: {Type}, success={Success}", step.Input.Type, result.IsSuccess);
                    }
                    break;
                case "delay":
                    // Already handled above
                    break;
                default:
                    _logger.LogWarning("Unknown scenario step action: {Action}", step.Action);
                    break;
            }
        }
        _logger.LogInformation("Scenario completed for device {DeviceId}", _session.DeviceId);
    }
}