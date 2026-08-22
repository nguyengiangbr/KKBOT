// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace KKBOT.Device.Abstractions;

/// <summary>
/// Discovers available devices on the system.
/// </summary>
public interface IDeviceDiscovery
{
    /// <summary>
    /// Discovers connected devices.
    /// </summary>
    Task<IReadOnlyList<DeviceInfo>> DiscoverAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Event raised when a device connects.
    /// </summary>
    event Action<DeviceInfo>? DeviceConnected;

    /// <summary>
    /// Event raised when a device disconnects.
    /// </summary>
    event Action<string>? DeviceDisconnected;
}

/// <summary>
/// Factory for creating device sessions.
/// </summary>
public interface IDeviceSessionFactory
{
    /// <summary>
    /// Creates a session for the specified device.
    /// </summary>
    Task<IDeviceSession> CreateAsync(string deviceId, CancellationToken cancellationToken);
}