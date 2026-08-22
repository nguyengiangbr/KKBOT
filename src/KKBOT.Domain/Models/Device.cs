// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KKBOT.Domain.Enums;

namespace KKBOT.Domain.Models;

/// <summary>
/// Represents a device (physical or virtual) that can run instances.
/// </summary>
public class Device : Entity
{
    /// <summary>
    /// Unique key for the device (e.g., ADB serial, emulator name).
    /// </summary>
    [MaxLength(200)]
    [Required]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Display name.
    /// </summary>
    [MaxLength(200)]
    [Required]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Description.
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Type of device.
    /// </summary>
    [Required]
    public DeviceType Type { get; set; } = DeviceType.Adb;

    /// <summary>
    /// ADB serial number (for ADB devices).
    /// </summary>
    [MaxLength(100)]
    public string? AdbSerial { get; set; }

    /// <summary>
    /// ADB host address.
    /// </summary>
    [MaxLength(100)]
    public string? AdbHost { get; set; } = "127.0.0.1";

    /// <summary>
    /// ADB port.
    /// </summary>
    public int AdbPort { get; set; } = 5037;

    /// <summary>
    /// Emulator type (LDPlayer, BlueStacks, MuMu, Nox, etc.).
    /// </summary>
    [MaxLength(50)]
    public string? EmulatorType { get; set; }

    /// <summary>
    /// Emulator instance index/name.
    /// </summary>
    [MaxLength(50)]
    public string? EmulatorInstance { get; set; }

    /// <summary>
    /// Screen width in pixels.
    /// </summary>
    public int ScreenWidth { get; set; } = 1280;

    /// <summary>
    /// Screen height in pixels.
    /// </summary>
    public int ScreenHeight { get; set; } = 720;

    /// <summary>
    /// Screen density (DPI).
    /// </summary>
    public int ScreenDensity { get; set; } = 240;

    /// <summary>
    /// Whether the device is currently connected.
    /// </summary>
    public bool IsConnected { get; set; } = false;

    /// <summary>
    /// Whether the device is available for use.
    /// </summary>
    public bool IsAvailable { get; set; } = true;

    /// <summary>
    /// Whether the device is reserved for exclusive use.
    /// </summary>
    public bool IsReserved { get; set; } = false;

    /// <summary>
    /// Current instance using this device.
    /// </summary>
    public Guid? CurrentInstanceId { get; set; }

    /// <summary>
    /// Navigation property to current instance.
    /// </summary>
    [ForeignKey(nameof(CurrentInstanceId))]
    public virtual Instance? CurrentInstance { get; set; }

    /// <summary>
    /// Last seen timestamp.
    /// </summary>
    public DateTimeOffset? LastSeenAt { get; set; }

    /// <summary>
    /// Last health check timestamp.
    /// </summary>
    public DateTimeOffset? LastHealthCheckAt { get; set; }

    /// <summary>
    /// Health check status.
    /// </summary>
    public bool IsHealthy { get; set; } = true;

    /// <summary>
    /// Battery level (0-100, for physical devices).
    /// </summary>
    public int? BatteryLevel { get; set; }

    /// <summary>
    /// Whether device is charging.
    /// </summary>
    public bool? IsCharging { get; set; }

    /// <summary>
    /// CPU usage percentage.
    /// </summary>
    public double? CpuUsagePercent { get; set; }

    /// <summary>
    /// Memory usage in MB.
    /// </summary>
    public long? MemoryUsageMb { get; set; }

    /// <summary>
    /// Total storage in MB.
    /// </summary>
    public long? TotalStorageMb { get; set; }

    /// <summary>
    /// Free storage in MB.
    /// </summary>
    public long? FreeStorageMb { get; set; }

    /// <summary>
    /// Android version (for Android devices).
    /// </summary>
    [MaxLength(50)]
    public string? AndroidVersion { get; set; }

    /// <summary>
    /// Device model.
    /// </summary>
    [MaxLength(100)]
    public string? Model { get; set; }

    /// <summary>
    /// Device manufacturer.
    /// </summary>
    [MaxLength(100)]
    public string? Manufacturer { get; set; }

    /// <summary>
    /// ABI (armeabi-v7a, arm64-v8a, x86, x86_64).
    /// </summary>
    [MaxLength(50)]
    public string? Abi { get; set; }

    /// <summary>
    /// Configuration JSON for device-specific settings.
    /// </summary>
    [Column(TypeName = "json")]
    public string? Config { get; set; }

    /// <summary>
    /// Tags for grouping/filtering.
    /// </summary>
    [MaxLength(500)]
    public string? Tags { get; set; }

    /// <summary>
    /// Navigation property for instances using this device.
    /// </summary>
    public virtual ICollection<Instance> Instances { get; set; } = new List<Instance>();

    /// <summary>
    /// Gets the ADB connection string.
    /// </summary>
    public string GetAdbConnectionString()
    {
        if (Type == DeviceType.Adb && !string.IsNullOrEmpty(AdbSerial))
        {
            return $"{AdbHost}:{AdbPort}/{AdbSerial}";
        }
        return string.Empty;
    }

    /// <summary>
    /// Updates device info from ADB.
    /// </summary>
    public void UpdateFromAdb(string model, string manufacturer, string androidVersion, string abi, int width, int height, int density)
    {
        Model = model;
        Manufacturer = manufacturer;
        AndroidVersion = androidVersion;
        Abi = abi;
        ScreenWidth = width;
        ScreenHeight = height;
        ScreenDensity = density;
        LastSeenAt = DateTimeOffset.UtcNow;
        Touch();
    }

    /// <summary>
    /// Marks the device as connected.
    /// </summary>
    public void MarkConnected()
    {
        IsConnected = true;
        LastSeenAt = DateTimeOffset.UtcNow;
        Touch();
    }

    /// <summary>
    /// Marks the device as disconnected.
    /// </summary>
    public void MarkDisconnected()
    {
        IsConnected = false;
        IsAvailable = false;
        CurrentInstanceId = null;
        Touch();
    }

    /// <summary>
    /// Updates health metrics.
    /// </summary>
    public void UpdateHealth(bool healthy, double? cpu = null, long? memoryMb = null, int? battery = null, bool? charging = null)
    {
        IsHealthy = healthy;
        LastHealthCheckAt = DateTimeOffset.UtcNow;
        if (cpu.HasValue) CpuUsagePercent = cpu.Value;
        if (memoryMb.HasValue) MemoryUsageMb = memoryMb.Value;
        if (battery.HasValue) BatteryLevel = battery.Value;
        if (charging.HasValue) IsCharging = charging.Value;
        Touch();
    }

    /// <summary>
    /// Checks if the device can accept a new instance.
    /// </summary>
    public bool CanAcceptInstance()
        => IsAvailable && IsConnected && IsHealthy && !IsReserved && CurrentInstanceId == null;

    /// <summary>
    /// Assigns an instance to this device.
    /// </summary>
    public void AssignInstance(Guid instanceId)
    {
        CurrentInstanceId = instanceId;
        IsAvailable = false;
        Touch();
    }

    /// <summary>
    /// Releases the current instance.
    /// </summary>
    public void ReleaseInstance()
    {
        CurrentInstanceId = null;
        IsAvailable = true;
        Touch();
    }
}