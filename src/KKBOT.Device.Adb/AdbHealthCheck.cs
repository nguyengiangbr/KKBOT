// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using KKBOT.Device.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KKBOT.Device.Adb;

/// <summary>
/// Performs health checks on ADB devices.
/// </summary>
public sealed class AdbHealthCheck : IAsyncDisposable
{
    private readonly string _deviceId;
    private readonly AdbClientOptions _options;
    private readonly ILogger<AdbHealthCheck> _logger;
    private readonly AdbCommandRunner _commandRunner;
    private bool _disposed;

    // Cached properties
    public string? Model { get; private set; }
    public string? Manufacturer { get; private set; }
    public string? AndroidVersion { get; private set; }
    public string? Abi { get; private set; }
    public int ScreenWidth { get; private set; }
    public int ScreenHeight { get; private set; }
    public int ScreenDensity { get; private set; }
    public int? BatteryLevel { get; private set; }
    public bool? IsCharging { get; private set; }
    public long? TotalStorage { get; private set; }
    public long? FreeStorage { get; private set; }

    public AdbHealthCheck(
        string deviceId,
        AdbClientOptions options,
        ILogger<AdbHealthCheck> logger,
        AdbCommandRunner commandRunner)
    {
        _deviceId = deviceId;
        _options = options;
        _logger = logger;
        _commandRunner = commandRunner;
    }

    /// <summary>
    /// Performs a comprehensive health check.
    /// </summary>
    public async Task<HealthCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(AdbHealthCheck));

        var startTime = DateTime.UtcNow;
        var result = new HealthCheckResult { IsHealthy = false };

        try
        {
            // Check connection state
            var stateResult = await _commandRunner.RunAsync(
                $"-s {_deviceId} get-state",
                cancellationToken,
                TimeSpan.FromSeconds(5));

            var isConnected = stateResult.Output.Trim().Equals("device", StringComparison.OrdinalIgnoreCase);
            result.IsConnected = isConnected;

            if (!isConnected)
            {
                result.ErrorMessage = $"Device state: {stateResult.Output.Trim()}";
                result.LatencyMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
                return result;
            }

            // Get detailed device info (cached after first call)
            if (string.IsNullOrEmpty(Model))
            {
                await RefreshDeviceInfoAsync(cancellationToken);
            }

            // Get current health metrics
            await RefreshHealthMetricsAsync(cancellationToken);

            result.IsHealthy = true;
            result.CpuUsage = await GetCpuUsageAsync(cancellationToken);
            result.MemoryUsage = await GetMemoryUsageAsync(cancellationToken);
            result.BatteryLevel = BatteryLevel;
            result.IsCharging = IsCharging;
            result.LatencyMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed for {DeviceId}", _deviceId);
            result.ErrorMessage = ex.Message;
            result.LatencyMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
            return result;
        }
    }

    /// <summary>
    /// Performs a quick connectivity check only.
    /// </summary>
    public async Task<bool> IsConnectedAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _commandRunner.RunAsync(
                $"-s {_deviceId} get-state",
                cancellationToken,
                TimeSpan.FromSeconds(3));
            return result.Output.Trim().Equals("device", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Refreshes static device information.
    /// </summary>
    public async Task RefreshDeviceInfoAsync(CancellationToken cancellationToken)
    {
        var tasks = new List<Task>
        {
            GetPropAsync("ro.product.model", v => Model = v),
            GetPropAsync("ro.product.manufacturer", v => Manufacturer = v),
            GetPropAsync("ro.build.version.release", v => AndroidVersion = v),
            GetPropAsync("ro.product.cpu.abi", v => Abi = v),
            GetResolutionAsync(),
            GetDensityAsync()
        };

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Refreshes dynamic health metrics.
    /// </summary>
    public async Task RefreshHealthMetricsAsync(CancellationToken cancellationToken)
    {
        var tasks = new List<Task>
        {
            GetBatteryAsync(),
            GetStorageAsync()
        };

        await Task.WhenAll(tasks);
    }

    private async Task GetPropAsync(string prop, Action<string> setter)
    {
        try
        {
            var result = await _commandRunner.RunAsync($"-s {_deviceId} shell getprop {prop}", CancellationToken.None);
            setter(result.Output.Trim());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get property {Property}", prop);
        }
    }

    private async Task GetResolutionAsync()
    {
        try
        {
            var result = await _commandRunner.RunAsync($"-s {_deviceId} shell wm size", CancellationToken.None);
            var (width, height) = ParseResolution(result.Output);
            ScreenWidth = width;
            ScreenHeight = height;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get screen resolution");
        }
    }

    private async Task GetDensityAsync()
    {
        try
        {
            var result = await _commandRunner.RunAsync($"-s {_deviceId} shell wm density", CancellationToken.None);
            ScreenDensity = ParseDensity(result.Output);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get screen density");
        }
    }

    private async Task GetBatteryAsync()
    {
        try
        {
            var result = await _commandRunner.RunAsync($"-s {_deviceId} shell dumpsys battery", CancellationToken.None);
            var (level, charging) = ParseBattery(result.Output);
            BatteryLevel = level;
            IsCharging = charging;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get battery info");
        }
    }

    private async Task GetStorageAsync()
    {
        try
        {
            var result = await _commandRunner.RunAsync($"-s {_deviceId} shell df /data", CancellationToken.None);
            var (total, free) = ParseStorage(result.Output);
            TotalStorage = total;
            FreeStorage = free;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get storage info");
        }
    }

    private async Task<double?> GetCpuUsageAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Read /proc/stat for CPU usage
            var result = await _commandRunner.RunAsync($"-s {_deviceId} shell cat /proc/stat", cancellationToken);
            return ParseCpuUsage(result.Output);
        }
        catch
        {
            return null;
        }
    }

    private async Task<long?> GetMemoryUsageAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _commandRunner.RunAsync($"-s {_deviceId} shell cat /proc/meminfo", cancellationToken);
            return ParseMemoryUsage(result.Output);
        }
        catch
        {
            return null;
        }
    }

    private static (int Width, int Height) ParseResolution(string output)
    {
        try
        {
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
        return (0, 0);
    }

    private static int ParseDensity(string output)
    {
        try
        {
            var parts = output.Split(':', 2);
            if (parts.Length == 2 && int.TryParse(parts[1].Trim(), out var density))
            {
                return density;
            }
        }
        catch { }
        return 0;
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
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (lines.Length >= 2)
            {
                var parts = lines[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4 &&
                    long.TryParse(parts[1], out var totalKb) &&
                    long.TryParse(parts[3], out var freeKb))
                {
                    return (totalKb * 1024, freeKb * 1024);
                }
            }
        }
        catch { }
        return (null, null);
    }

    private static double? ParseCpuUsage(string output)
    {
        try
        {
            // Parse /proc/stat format: cpu  1234 567 890 12345 ...
            var lines = output.Split('\n');
            var cpuLine = lines.FirstOrDefault(l => l.StartsWith("cpu "));
            if (cpuLine != null)
            {
                var parts = cpuLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 8)
                {
                    var user = long.Parse(parts[1]);
                    var nice = long.Parse(parts[2]);
                    var system = long.Parse(parts[3]);
                    var idle = long.Parse(parts[4]);
                    var iowait = long.Parse(parts[5]);
                    var irq = long.Parse(parts[6]);
                    var softirq = long.Parse(parts[7]);

                    var total = user + nice + system + idle + iowait + irq + softirq;
                    var nonIdle = user + nice + system + irq + softirq;

                    // This is a snapshot, would need previous reading for delta
                    // Returning null for now - implement proper delta tracking if needed
                    return (double)nonIdle / total * 100;
                }
            }
        }
        catch { }
        return null;
    }

    private static long? ParseMemoryUsage(string output)
    {
        try
        {
            long? memTotal = null;
            long? memAvailable = null;

            foreach (var line in output.Split('\n'))
            {
                if (line.StartsWith("MemTotal:", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && long.TryParse(parts[1], out var v))
                        memTotal = v * 1024; // Convert KB to bytes
                }
                else if (line.StartsWith("MemAvailable:", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && long.TryParse(parts[1], out var v))
                        memAvailable = v * 1024;
                }
            }

            if (memTotal.HasValue && memAvailable.HasValue)
            {
                return memTotal.Value - memAvailable.Value;
            }
        }
        catch { }
        return null;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await Task.CompletedTask;
    }
}