// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace KKBOT.Domain.Enums;

/// <summary>
/// Represents the type of device.
/// </summary>
public enum DeviceType
{
    /// <summary>
    /// Physical Android device connected via ADB.
    /// </summary>
    Adb = 0,

    /// <summary>
    /// Android emulator (LDPlayer, BlueStacks, MuMu, Nox, etc.).
    /// </summary>
    Emulator = 1,

    /// <summary>
    /// Simulated device for testing (replay, scenario).
    /// </summary>
    Simulator = 2,

    /// <summary>
    /// iOS device (future support).
    /// </summary>
    Ios = 3,

    /// <summary>
    /// Windows application (future support).
    /// </summary>
    Windows = 4
}

/// <summary>
/// Extension methods for DeviceType.
/// </summary>
public static class DeviceTypeExtensions
{
    /// <summary>
    /// Determines if the device type requires ADB.
    /// </summary>
    public static bool RequiresAdb(this DeviceType type)
        => type is DeviceType.Adb or DeviceType.Emulator;

    /// <summary>
    /// Determines if the device type is a simulator.
    /// </summary>
    public static bool IsSimulator(this DeviceType type)
        => type == DeviceType.Simulator;

    /// <summary>
    /// Gets the display name for the device type.
    /// </summary>
    public static string GetDisplayName(this DeviceType type)
        => type switch
        {
            DeviceType.Adb => "ADB Device",
            DeviceType.Emulator => "Android Emulator",
            DeviceType.Simulator => "Simulator",
            DeviceType.Ios => "iOS Device",
            DeviceType.Windows => "Windows App",
            _ => type.ToString()
        };
}