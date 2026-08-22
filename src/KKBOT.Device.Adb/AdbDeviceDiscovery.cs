// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using KKBOT.Device.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Linq;
using System.Collections.Generic;
using System.Threading;

namespace KKBOT.Device.Adb;

/// <summary>
/// Discovers ADB devices on the system.
/// </summary>
public sealed class AdbDeviceDiscovery : IDeviceDiscovery
{
    private readonly AdbClientOptions _options;
    private readonly ILogger<AdbDeviceDiscovery> _logger;
    private readonly AdbCommandRunner _commandRunner;
    private readonly PeriodicTimer? _discoveryTimer;
    private readonly CancellationTokenSource _discoveryCts = new();

    public event Action<DeviceInfo>? DeviceConnected;
    public event Action<string>? DeviceDisconnected;

    private readonly HashSet<string> _knownDevices = new(StringComparer.OrdinalIgnoreCase);

    public AdbDeviceDiscovery(
        IOptions<AdbClientOptions> options,
        ILogger<AdbDeviceDiscovery> logger,
        AdbCommandRunner commandRunner)
    {
        _options = options.Value;
        _logger = logger;
        _commandRunner = commandRunner;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DeviceInfo>> DiscoverAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Discovering ADB devices");

        var result = await _commandRunner.RunAsync("devices -l", cancellationToken);
        
        if (result.ExitCode != 0)
        {
            _logger.LogWarning("ADB devices command failed with exit code {ExitCode}: {Error}", 
                result.ExitCode, result.ErrorOutput);
            return Array.Empty<DeviceInfo>();
        }

        var devices = ParseDevicesOutput(result.Output);
        var deviceInfos = new List<DeviceInfo>();

        foreach (var device in devices)
        {
            var info = await GetDeviceInfoAsync(device.Serial, cancellationToken);
            if (info != null)
            {
                deviceInfos.Add(info);
                
                // Raise event for new devices
                if (!_knownDevices.Contains(device.Serial))
                {
                    _knownDevices.Add(device.Serial);
                    DeviceConnected?.Invoke(info);
                }
            }
        }

        // Check for disconnected devices
        var currentDevices = devices.Select(d => d.Serial).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var disconnected = _knownDevices.Where(d => !currentDevices.Contains(d)).ToList();
        
        foreach (var disconnectedDevice in disconnected)
        {
            _knownDevices.Remove(disconnectedDevice);
            DeviceDisconnected?.Invoke(disconnectedDevice);
        }

        _logger.LogDebug("Discovered {Count} ADB devices", deviceInfos.Count);
        return deviceInfos;
    }

    /// <summary>
    /// Starts periodic device discovery.
    /// </summary>
    public void StartPeriodicDiscovery(TimeSpan interval)
    {
        _discoveryTimer = new PeriodicTimer(interval);
        _ = Task.Run(DiscoveryLoopAsync);
    }

    /// <summary>
    /// Stops periodic device discovery.
    /// </summary>
    public void StopPeriodicDiscovery()
    {
        _discoveryCts.Cancel();
    }

    private async Task DiscoveryLoopAsync()
    {
        try
        {
            while (await _discoveryTimer!.WaitForNextTickAsync(_discoveryCts.Token))
            {
                await DiscoverAsync(_discoveryCts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when stopping
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in device discovery loop");
        }
    }

    private static List<(string Serial, string Status, Dictionary<string, string> Properties)> ParseDevicesOutput(string output)
    {
        var devices = new List<(string Serial, string Status, Dictionary<string, string> Properties)>();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines.Skip(1)) // Skip header line
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                var serial = parts[0];
                var status = parts[1];
                var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                for (int i = 2; i < parts.Length; i++)
                {
                    var propParts = parts[i].Split(':', 2);
                    if (propParts.Length == 2)
                    {
                        properties[propParts[0]] = propParts[1];
                    }
                }

                devices.Add((serial, status, properties));
            }
        }

        return devices;
    }

    private async Task<DeviceInfo?> GetDeviceInfoAsync(string serial, CancellationToken cancellationToken)
    {
        try
        {
            // Get model
            var modelResult = await _commandRunner.RunAsync($"-s {serial} shell getprop ro.product.model", cancellationToken);
            var model = modelResult.Output.Trim();

            // Get manufacturer
            var manufacturerResult = await _commandRunner.RunAsync($"-s {serial} shell getprop ro.product.manufacturer", cancellationToken);
            var manufacturer = manufacturerResult.Output.Trim();

            // Get Android version
            var versionResult = await _commandRunner.RunAsync($"-s {serial} shell getprop ro.build.version.release", cancellationToken);
            var androidVersion = versionResult.Output.Trim();

            // Get ABI
            var abiResult = await _commandRunner.RunAsync($"-s {serial} shell getprop ro.product.cpu.abi", cancellationToken);
            var abi = abiResult.Output.Trim();

            // Get screen resolution
            var wmResult = await _commandRunner.RunAsync($"-s {serial} shell wm size", cancellationToken);
            var (width, height) = ParseResolution(wmResult.Output);

            // Get screen density
            var densityResult = await _commandRunner.RunAsync($"-s {serial} shell wm density", cancellationToken);
            var density = ParseDensity(densityResult.Output);

            // Get battery info
            var batteryResult = await _commandRunner.RunAsync($"-s {serial} shell dumpsys battery", cancellationToken);
            var (batteryLevel, isCharging) = ParseBattery(batteryResult.Output);

            // Get storage info
            var storageResult = await _commandRunner.RunAsync($"-s {serial} shell df /data", cancellationToken);
            var (totalStorage, freeStorage) = ParseStorage(storageResult.Output);

            return new DeviceInfo
            {
                DeviceId = serial,
                Model = model,
                Manufacturer = manufacturer,
                AndroidVersion = androidVersion,
                Abi = abi,
                ScreenWidth = width,
                ScreenHeight = height,
                ScreenDensity = density,
                BatteryLevel = batteryLevel,
                IsCharging = isCharging,
                TotalStorage = totalStorage,
                FreeStorage = freeStorage,
                IsConnected = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get device info for {Serial}", serial);
            return null;
        }
    }

    private static (int Width, int Height) ParseResolution(string output)
    {
        try
        {
            // Output format: "Physical size: 1080x1920"
            var parts = output.Split(':', 2);
            if (parts.Length == 2)
            {
                var size = parts[1].Trim();
                var dims = size.Split('x');
                if (dims.Length == 2 &&
                    int.TryParse(dims[0], out var width) &&
                    int.TryParse(dims[1], out var height))
                {
                    return (width, height);
                }
            }
        }
        catch { }
        return (1280, 720); // Default
    }

    private static int ParseDensity(string output)
    {
        try
        {
            // Output format: "Physical density: 480"
            var parts = output.Split(':', 2);
            if (parts.Length == 2 && int.TryParse(parts[1].Trim(), out var density))
            {
                return density;
            }
        }
        catch { }
        return 240; // Default
    }

    private static (int? Level, bool? IsCharging) ParseBattery(string output)
    {
        try
        {
            int? level = null;
            bool? charging = null;

            foreach (var line in output.Split('\n'))
            {
                if (line.Contains("level:", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(':', 2);
                    if (parts.Length == 2 && int.TryParse(parts[1].Trim(), out var l))
                        level = l;
                }
                else if (line.Contains("status:", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(':', 2);
                    if (parts.Length == 2)
                        charging = parts[1].Trim().Equals("Charging", StringComparison.OrdinalIgnoreCase) ||
                                  parts[1].Trim().Equals("Full", StringComparison.OrdinalIgnoreCase);
                }
            }

            return (level, charging);
        }
        catch { }
        return (null, null);
    }

    private static (long? Total, long? Free) ParseStorage(string output)
    {
        try
        {
            // df output format:
            // Filesystem     1K-blocks      Used Available Use% Mounted on
            // /dev/block/...  12345678   1234567  11111111  10% /data
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (lines.Length >= 2)
            {
                var parts = lines[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4 &&
                    long.TryParse(parts[1], out var totalKb) &&
                    long.TryParse(parts[3], out var freeKb))
                {
                    return (totalKb * 1024, freeKb * 1024); // Convert KB to bytes
                }
            }
        }
        catch { }
        return (null, null);
    }
}