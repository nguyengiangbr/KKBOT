using KKBOT.Device.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace KKBOT.Device.Simulator;

/// <summary>
/// Utility helpers for unit tests involving simulated devices.
/// </summary>
public static class TestFixtures
{
    public static IDeviceSession CreateSession(string scenarioPath)
    {
        var logger = NullLoggerFactory.Instance.CreateLogger<SimulatedDeviceSession>();
        var session = new SimulatedDeviceSession(scenarioPath, logger);
        return session;
    }
}