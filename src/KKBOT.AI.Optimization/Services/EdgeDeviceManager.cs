// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.AI.Optimization.Abstractions;
using KKBOT.AI.Optimization.Models;
using Microsoft.Extensions.Logging;

namespace KKBOT.AI.Optimization.Services;

public sealed class EdgeDeviceManager : IEdgeDeviceManager
{
    private readonly ConcurrentDictionary<string, EdgeDeviceSpec> _devices = new();
    private readonly ILogger<EdgeDeviceManager> _logger;
    private readonly string _storagePath;

    public EdgeDeviceManager(ILogger<EdgeDeviceManager> logger, string? storagePath = null)
    {
        _logger = logger;
        _storagePath = storagePath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "devices");
        Directory.CreateDirectory(_storagePath);
        LoadDevices();
    }

    public async Task<EdgeDeviceSpec> RegisterDeviceAsync(EdgeDeviceSpec device, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(device.DeviceId))
            device.DeviceId = Guid.NewGuid().ToString("N");

        if (_devices.ContainsKey(device.DeviceId))
            throw new InvalidOperationException($"Device {device.DeviceId} already registered");

        device.Capabilities ??= new Dictionary<string, object>();
        device.SupportedPrecisions ??= GetDefaultPrecisions(device.Platform).ToList();
        device.SupportedFrameworks ??= GetDefaultFrameworks(device.Platform).ToList();

        _devices[device.DeviceId] = device;
        await SaveDevicesAsync(ct);

        _logger.LogInformation("Registered device {DeviceId} ({Name})", device.DeviceId, device.Name);
        return device;
    }

    public async Task<EdgeDeviceSpec?> GetDeviceAsync(string deviceId, CancellationToken ct = default)
    {
        _devices.TryGetValue(deviceId, out var device);
        return device;
    }

    public async Task<List<EdgeDeviceSpec>> ListDevicesAsync(DeviceFilter filter, CancellationToken ct = default)
    {
        var query = _devices.Values.AsEnumerable();

        if (filter.Platform.HasValue)
            query = query.Where(d => d.Platform == filter.Platform.Value);

        if (!string.IsNullOrWhiteSpace(filter.Architecture))
            query = query.Where(d => d.Architecture.Equals(filter.Architecture, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(filter.OS))
            query = query.Where(d => d.OS.Equals(filter.OS, StringComparison.OrdinalIgnoreCase));

        if (filter.IsOnline.HasValue)
            query = query.Where(d => d.Capabilities.TryGetValue("isOnline", out var online) && 
                bool.TryParse(online.ToString(), out var isOnline) && isOnline == filter.IsOnline.Value);

        return query
            .OrderByDescending(d => d.Capabilities.GetValueOrDefault("lastSeen", DateTime.MinValue))
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToList();
    }

    public async Task<EdgeDeviceSpec> UpdateDeviceAsync(string deviceId, EdgeDeviceSpec device, CancellationToken ct = default)
    {
        if (!_devices.ContainsKey(deviceId))
            throw new InvalidOperationException($"Device {deviceId} not found");

        device.DeviceId = deviceId; // Ensure ID doesn't change
        device.Capabilities["lastUpdated"] = DateTime.UtcNow;
        _devices[deviceId] = device;
        await SaveDevicesAsync(ct);
        return device;
    }

    public async Task<bool> DeleteDeviceAsync(string deviceId, CancellationToken ct = default)
    {
        var removed = _devices.TryRemove(deviceId, out _);
        if (removed)
            await SaveDevicesAsync(ct);
        return removed;
    }

    public async Task<DeviceHealth> GetDeviceHealthAsync(string deviceId, CancellationToken ct = default)
    {
        if (!_devices.TryGetValue(deviceId, out var device))
            return new DeviceHealth { DeviceId = deviceId, IsOnline = false };

        // In a real implementation, this would query the actual device
        // For now, return simulated health data
        var random = new Random();
        return new DeviceHealth
        {
            DeviceId = deviceId,
            IsOnline = true,
            LastHeartbeat = DateTime.UtcNow,
            CpuUsagePercent = Math.Round(20 + new Random().NextDouble() * 60, 1),
            MemoryUsagePercent = Math.Round(30 + new Random().NextDouble() * 50, 1),
            GpuUsagePercent = Math.Round(new Random().NextDouble() * 80, 1),
            TemperatureCelsius = Math.Round(35 + new Random().NextDouble() * 30, 1),
            PowerWatts = Math.Round(10 + new Random().NextDouble() * 40, 1),
            AvailableMemoryMB = 2048,
            TotalMemoryMB = 8192,
            DiskUsagePercent = Math.Round(new Random().NextDouble() * 50, 1),
            ActiveDeployments = new List<DeploymentStatus> { DeploymentStatus.Running, DeploymentStatus.Running },
            Alerts = new List<string>()
        };
    }

    public async Task<bool> SendCommandAsync(string deviceId, string command, Dictionary<string, object> parameters, CancellationToken ct = default)
    {
        if (!_devices.ContainsKey(deviceId))
            return false;

        // In a real implementation, this would send a command to the device
        // via MQTT, HTTP, gRPC, etc.
        _logger.LogInformation("Sent command {Command} to device {DeviceId}", command, deviceId);
        return true;
    }

    private void LoadDevices()
    {
        var file = Path.Combine(_storagePath, "devices.json");
        if (File.Exists(file))
        {
            try
            {
                var json = File.ReadAllText(file);
                var devices = JsonSerializer.Deserialize<List<EdgeDeviceSpec>>(json);
                if (devices != null)
                {
                    foreach (var device in devices)
                        _devices[device.DeviceId] = device;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load devices");
            }
        }
    }

    private async Task SaveDevicesAsync(CancellationToken ct)
    {
        try
        {
            var file = Path.Combine(_storagePath, "devices.json");
            var json = JsonSerializer.Serialize(_devices.Values.ToList(), new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(file, json, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save devices");
        }
    }

    private static string[] GetDefaultPrecisions(TargetPlatform platform)
    {
        return platform switch
        {
            TargetPlatform.NvidiaGpu => new[] { "fp32", "fp16", "int8" },
            TargetPlatform.NvidiaJetson => new[] { "fp32", "fp16", "int8" },
            TargetPlatform.AmdGpu => new[] { "fp32", "fp16" },
            TargetPlatform.IntelGpu => new[] { "fp32", "fp16", "int8" },
            TargetPlatform.AppleNeuralEngine => new[] { "fp32", "fp16" },
            TargetPlatform.RaspberryPi => new[] { "fp32", "int8" },
            TargetPlatform.CoralTpu => new[] { "int8" },
            TargetPlatform.QualcommHexagon => new[] { "fp32", "fp16", "int8" },
            TargetPlatform.GenericArm64 => new[] { "fp32", "fp16", "int8" },
            TargetPlatform.GenericX64 => new[] { "fp32", "fp16", "int8" },
            TargetPlatform.WebAssembly => new[] { "fp32" },
            _ => new[] { "fp32" }
        };
    }

    private static string[] GetDefaultFrameworks(TargetPlatform platform)
    {
        return platform switch
        {
            TargetPlatform.NvidiaGpu => new[] { "ONNX Runtime", "TensorRT", "PyTorch", "TensorFlow" },
            TargetPlatform.NvidiaJetson => new[] { "ONNX Runtime", "TensorRT", "TensorFlow" },
            TargetPlatform.AmdGpu => new[] { "ONNX Runtime", "ROCm", "PyTorch" },
            TargetPlatform.IntelGpu => new[] { "ONNX Runtime", "OpenVINO", "oneDNN" },
            TargetPlatform.AppleNeuralEngine => new[] { "CoreML", "ONNX Runtime" },
            TargetPlatform.RaspberryPi => new[] { "TFLite", "ONNX Runtime", "OpenCV" },
            TargetPlatform.CoralTpu => new[] { "TFLite", "Edge TPU Runtime" },
            TargetPlatform.QualcommHexagon => new[] { "ONNX Runtime", "SNPE", "TFLite" },
            TargetPlatform.GenericArm64 => new[] { "ONNX Runtime", "TFLite" },
            TargetPlatform.GenericX64 => new[] { "ONNX Runtime", "TensorRT", "OpenVINO" },
            TargetPlatform.WebAssembly => new[] { "ONNX Runtime Web", "TensorFlow.js" },
            _ => new[] { "ONNX Runtime" }
        };
    }
}