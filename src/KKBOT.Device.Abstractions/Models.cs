// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace KKBOT.Device.Abstractions;

/// <summary>
/// Information about a connected device.
/// </summary>
public record DeviceInfo
{
    public string DeviceId { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string Manufacturer { get; init; } = string.Empty;
    public string AndroidVersion { get; init; } = string.Empty;
    public int ScreenWidth { get; init; }
    public int ScreenHeight { get; init; }
    public int ScreenDensity { get; init; }
    public string Abi { get; init; } = string.Empty;
    public int? BatteryLevel { get; init; }
    public bool? IsCharging { get; init; }
    public long? TotalStorage { get; init; }
    public long? FreeStorage { get; init; }
    public bool IsConnected { get; init; }
}

/// <summary>
/// A captured frame from a device.
/// </summary>
public record Frame
{
    public Guid FrameId { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public int Width { get; init; }
    public int Height { get; init; }
    public string Format { get; init; } = "raw";
    public byte[] Data { get; init; } = Array.Empty<byte>();
    public double CaptureLatencyMs { get; init; }
    public Dictionary<string, object?>? Metadata { get; init; }
}

/// <summary>
/// Options for capturing a frame.
/// </summary>
public record CaptureOptions
{
    public Rectangle? RegionOfInterest { get; init; }
    public string Format { get; init; } = "png";
    public int Quality { get; init; } = 90;
    public TimeSpan? Timeout { get; init; } = TimeSpan.FromSeconds(5);
}

/// <summary>
/// An input command to send to the device.
/// </summary>
public record InputCommand
{
    public InputType Type { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int EndX { get; init; }
    public int EndY { get; init; }
    public string? Text { get; init; }
    public TimeSpan? Duration { get; init; }
    public int KeyCode { get; init; }
}

/// <summary>
/// Supported input types.
/// </summary>
public enum InputType
{
    Tap,
    Swipe,
    Text,
    Key
}

/// <summary>
/// A rectangle region.
/// </summary>
public record Rectangle
{
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
}

/// <summary>
/// Result of an input operation.
/// </summary>
public record InputResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// Identity of an application to launch/stop.
/// </summary>
public record ApplicationIdentity
{
    public string PackageName { get; init; } = string.Empty;
    public string? ActivityName { get; init; }
    public string? Arguments { get; init; }
}

/// <summary>
/// Result of a health check operation.
/// </summary>
public record HealthCheckResult
{
    public bool IsHealthy { get; set; }
    public bool IsConnected { get; set; }
    public double? CpuUsage { get; set; }
    public long? MemoryUsage { get; set; }
    public int? BatteryLevel { get; set; }
    public bool? IsCharging { get; set; }
    public double? LatencyMs { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Details { get; set; }

    // Detailed device information
    public string? Model { get; set; }
    public string? Manufacturer { get; set; }
    public string? AndroidVersion { get; set; }
    public string? Abi { get; set; }
    public int ScreenWidth { get; set; }
    public int ScreenHeight { get; set; }
    public int ScreenDensity { get; set; }
    public long? TotalStorage { get; set; }
    public long? FreeStorage { get; set; }
}