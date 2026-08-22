// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace KKBOT.Device.Abstractions;

/// <summary>
/// Represents a session with a connected device.
/// </summary>
public interface IDeviceSession : IAsyncDisposable
{
    /// <summary>
    /// Unique identifier of the device.
    /// </summary>
    string DeviceId { get; }

    /// <summary>
    /// Gets device information.
    /// </summary>
    Task<DeviceInfo> GetInfoAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Determines if the device is connected.
    /// </summary>
    Task<bool> IsConnectedAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Captures a frame from the device screen.
    /// </summary>
    Task<Frame> CaptureAsync(CaptureOptions options, CancellationToken cancellationToken);

    /// <summary>
    /// Sends an input command to the device.
    /// </summary>
    Task<InputResult> SendInputAsync(InputCommand command, CancellationToken cancellationToken);

    /// <summary>
    /// Launches an application on the device.
    /// </summary>
    Task LaunchApplicationAsync(ApplicationIdentity application, CancellationToken cancellationToken);

    /// <summary>
    /// Stops an application on the device.
    /// </summary>
    Task StopApplicationAsync(ApplicationIdentity application, CancellationToken cancellationToken);

    /// <summary>
    /// Performs a health check on the device.
    /// </summary>
    Task<HealthCheckResult> HealthCheckAsync(CancellationToken cancellationToken);
}