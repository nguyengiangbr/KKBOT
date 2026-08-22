using KKBOT.Device.Abstractions;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Threading;

namespace KKBOT.Device.Simulator;

/// <summary>
/// Provides mechanisms to inject failures into simulated sessions for testing.
/// </summary>
public static class FailureInjection
{
    public static Task SimulateTimeoutAsync(IDeviceSession session, TimeSpan timeout, CancellationToken ct)
    {
        // Simply delay beyond the timeout to trigger caller's timeout handling.
        return Task.Delay(timeout + TimeSpan.FromSeconds(1), ct);
    }

    public static Task SimulateCorruptFrameAsync(IDeviceSession session, CancellationToken ct)
    {
        // This is a placeholder – a real implementation would replace the next frame with corrupted data.
        return Task.CompletedTask;
    }
}