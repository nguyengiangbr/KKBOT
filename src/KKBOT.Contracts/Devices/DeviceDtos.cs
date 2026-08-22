// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace KKBOT.Contracts.Devices;

/// <summary>
/// Request to register a new device.
/// </summary>
public record RegisterDeviceRequest
{
    [Required, MaxLength(200)]
    public string Key { get; init; } = string.Empty;

    [Required, MaxLength(200)]
    public string DisplayName { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; init; }

    [Required]
    public string Type { get; init; } = "Adb";

    [MaxLength(100)]
    public string? AdbSerial { get; init; }

    [MaxLength(100)]
    public string? AdbHost { get; init; } = "127.0.0.1";

    public int AdbPort { get; init; } = 5037;

    [MaxLength(50)]
    public string? EmulatorType { get; init; }

    [MaxLength(50)]
    public string? EmulatorInstance { get; init; }

    public int ScreenWidth { get; init; } = 1280;

    public int ScreenHeight { get; init; } = 720;

    public string[]? Tags { get; init; }
}

/// <summary>
/// Device summary DTO.
/// </summary>
public record DeviceSummaryDto
{
    public Guid Id { get; init; }
    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public bool IsConnected { get; init; }
    public bool IsAvailable { get; init; }
    public bool IsHealthy { get; init; }
    public string? AdbSerial { get; init; }
    public int ScreenWidth { get; init; }
    public int ScreenHeight { get; init; }
    public string? CurrentInstanceName { get; init; }
    public DateTimeOffset? LastSeenAt { get; init; }
    public double? CpuUsagePercent { get; init; }
    public long? MemoryUsageMb { get; init; }
    public int? BatteryLevel { get; init; }
    public string[]? Tags { get; init; }
}

/// <summary>
/// Device detail DTO.
/// </summary>
public record DeviceDetailDto : DeviceSummaryDto
{
    public string? Description { get; init; }
    public string? AdbHost { get; init; }
    public int AdbPort { get; init; }
    public string? EmulatorType { get; init; }
    public string? EmulatorInstance { get; init; }
    public int ScreenDensity { get; init; }
    public bool IsReserved { get; init; }
    public Guid? CurrentInstanceId { get; init; }
    public DateTimeOffset? LastHealthCheckAt { get; init; }
    public bool? IsCharging { get; init; }
    public long? TotalStorageMb { get; init; }
    public long? FreeStorageMb { get; init; }
    public string? AndroidVersion { get; init; }
    public string? Model { get; init; }
    public string? Manufacturer { get; init; }
    public string? Abi { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// Device connection test result.
/// </summary>
public record DeviceConnectionTestResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public string? DeviceModel { get; init; }
    public string? AndroidVersion { get; init; }
    public int? ScreenWidth { get; init; }
    public int? ScreenHeight { get; init; }
    public int? BatteryLevel { get; init; }
    public TimeSpan? ConnectionLatency { get; init; }
}