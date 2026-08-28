// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using KKBOT.Device.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace KKBOT.Device.Adb;

/// <summary>
/// ADB implementation of device session.
/// </summary>
public sealed class AdbDeviceSession : IDeviceSession
{
    private readonly string _deviceId;
    private readonly AdbClientOptions _options;
    private readonly ILogger _logger;
    private readonly AdbCommandRunner _commandRunner;
    private readonly AdbScreenshotProvider _screenshotProvider;
    private readonly AdbInputDispatcher _inputDispatcher;
    private readonly AdbHealthCheck _healthCheck;
    private bool _disposed;

    public string DeviceId => _deviceId;

    public AdbDeviceSession(
        string deviceId,
        AdbClientOptions options,
        ILogger logger,
        AdbCommandRunner commandRunner)
    {
        _deviceId = deviceId;
        _options = options;
        _logger = logger;
        _commandRunner = commandRunner;
        _screenshotProvider = new AdbScreenshotProvider(_deviceId, _options, _logger, _commandRunner);
        _inputDispatcher = new AdbInputDispatcher(_deviceId, _options, _logger, _commandRunner);
        _healthCheck = new AdbHealthCheck(_deviceId, _options, _logger, _commandRunner);
    }

    /// <summary>
    /// Initializes the session.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Initializing ADB session for {DeviceId}", _deviceId);
        
        // Verify connection
        var connected = await IsConnectedAsync(cancellationToken);
        if (!connected)
        {
            throw new InvalidOperationException($"Device {_deviceId} is not connected");
        }

        // Wake up device if needed
        await WakeDeviceAsync(cancellationToken);
        
        _logger.LogInformation("ADB session initialized for {DeviceId}", _deviceId);
    }

    /// <inheritdoc />
    public async Task<DeviceInfo> GetInfoAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Getting device info for {DeviceId}", _deviceId);

        // Use the health check to get detailed info
        var health = await _healthCheck.CheckAsync(cancellationToken);
        
        return new DeviceInfo
        {
            DeviceId = _deviceId,
            Model = health.Model ?? string.Empty,
            Manufacturer = health.Manufacturer ?? string.Empty,
            AndroidVersion = health.AndroidVersion ?? string.Empty,
            Abi = health.Abi ?? string.Empty,
            ScreenWidth = health.ScreenWidth,
            ScreenHeight = health.ScreenHeight,
            ScreenDensity = health.ScreenDensity,
            BatteryLevel = health.BatteryLevel,
            IsCharging = health.IsCharging,
            TotalStorage = health.TotalStorage,
            FreeStorage = health.FreeStorage,
            IsConnected = health.IsConnected
        };
    }

    /// <inheritdoc />
    public async Task<bool> IsConnectedAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _commandRunner.RunAsync($"-s {_deviceId} get-state", cancellationToken);
            return result.Output.Trim().Equals("device", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<Frame> CaptureAsync(CaptureOptions options, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Capturing frame from {DeviceId}", _deviceId);
        return await _screenshotProvider.CaptureAsync(options, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<InputResult> SendInputAsync(InputCommand command, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Sending input to {DeviceId}: {Type}", _deviceId, command.Type);
        return await _inputDispatcher.SendAsync(command, cancellationToken);
    }

    /// <inheritdoc />
    public async Task LaunchApplicationAsync(ApplicationIdentity application, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Launching application {PackageName} on {DeviceId}", application.PackageName, _deviceId);

        var command = $"shell monkey -p {application.PackageName} -c android.intent.category.LAUNCHER 1";
        if (!string.IsNullOrEmpty(application.ActivityName))
        {
            command = $"shell am start -n {application.PackageName}/{application.ActivityName}";
            if (!string.IsNullOrEmpty(application.Arguments))
            {
                command += $" -e {application.Arguments}";
            }
        }

        var result = await _commandRunner.RunAsync($"-s {_deviceId} {command}", cancellationToken);
        
        if (result.ExitCode != 0)
        {
            _logger.LogError("Failed to launch application: {Error}", result.ErrorOutput);
            throw new InvalidOperationException($"Failed to launch application: {result.ErrorOutput}");
        }

        // Wait for app to start
        await Task.Delay(1000, cancellationToken);
    }

    /// <inheritdoc />
    public async Task StopApplicationAsync(ApplicationIdentity application, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping application {PackageName} on {DeviceId}", application.PackageName, _deviceId);

        var command = $"shell am force-stop {application.PackageName}";
        var result = await _commandRunner.RunAsync($"-s {_deviceId} {command}", cancellationToken);
        
        if (result.ExitCode != 0)
        {
            _logger.LogWarning("Force stop returned non-zero exit code: {Error}", result.ErrorOutput);
        }
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> HealthCheckAsync(CancellationToken cancellationToken)
    {
        return await _healthCheck.CheckAsync(cancellationToken);
    }

    /// <summary>
    /// Wakes up the device screen if it's off.
    /// </summary>
    private async Task WakeDeviceAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Check if screen is on
            var result = await _commandRunner.RunAsync($"-s {_deviceId} shell dumpsys power", cancellationToken);
            if (result.Output.Contains("mScreenOn=true", StringComparison.OrdinalIgnoreCase))
            {
                return; // Already on
            }

            // Wake up
            await _commandRunner.RunAsync($"-s {_deviceId} shell input keyevent KEYCODE_WAKEUP", cancellationToken);
            await Task.Delay(500, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to wake device {DeviceId}", _deviceId);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _logger.LogDebug("Disposing ADB session for {DeviceId}", _deviceId);
        
        await _screenshotProvider.DisposeAsync();
        await _inputDispatcher.DisposeAsync();
        await _healthCheck.DisposeAsync();
        
        _logger.LogInformation("ADB session disposed for {DeviceId}", _deviceId);
    }
}