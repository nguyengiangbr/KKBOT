// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using KKBOT.Device.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KKBOT.Device.Adb;

/// <summary>
/// Configuration options for ADB client.
/// </summary>
public class AdbClientOptions
{
    public string AdbPath { get; set; } = "adb";
    public string DefaultHost { get; set; } = "127.0.0.1";
    public int DefaultPort { get; set; } = 5037;
    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public int MaxRetries { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 500;
}

/// <summary>
/// Factory for creating ADB device sessions.
/// </summary>
public sealed class AdbClientFactory : IDeviceSessionFactory
{
    private readonly AdbClientOptions _options;
    private readonly ILogger<AdbClientFactory> _logger;
    private readonly AdbCommandRunner _commandRunner;

    public AdbClientFactory(
        IOptions<AdbClientOptions> options,
        ILogger<AdbClientFactory> logger,
        AdbCommandRunner commandRunner)
    {
        _options = options.Value;
        _logger = logger;
        _commandRunner = commandRunner;
    }

    /// <inheritdoc />
    public async Task<IDeviceSession> CreateAsync(string deviceId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating ADB session for device {DeviceId}", deviceId);

        // Verify device is connected
        var isConnected = await IsDeviceConnectedAsync(deviceId, cancellationToken);
        if (!isConnected)
        {
            _logger.LogError("Device {DeviceId} is not connected", deviceId);
            throw new InvalidOperationException($"Device {deviceId} is not connected");
        }

        var session = new AdbDeviceSession(deviceId, _options, _logger, _commandRunner);
        
        // Initialize the session
        await session.InitializeAsync(cancellationToken);
        
        _logger.LogInformation("Created ADB session for device {DeviceId}", deviceId);
        return session;
    }

    /// <summary>
    /// Checks if a device is connected via ADB.
    /// </summary>
    public async Task<bool> IsDeviceConnectedAsync(string deviceId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _commandRunner.RunAsync($"devices", cancellationToken);
            return result.Output.Contains(deviceId, StringComparison.OrdinalIgnoreCase) &&
                   result.Output.Contains("device", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check device connection for {DeviceId}", deviceId);
            return false;
        }
    }

    /// <summary>
    /// Gets the ADB server version.
    /// </summary>
    public async Task<string> GetAdbVersionAsync(CancellationToken cancellationToken)
    {
        var result = await _commandRunner.RunAsync("version", cancellationToken);
        return result.Output.Trim();
    }

    /// <summary>
    /// Starts the ADB server if not running.
    /// </summary>
    public async Task<bool> StartAdbServerAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _commandRunner.RunAsync("start-server", cancellationToken);
            return result.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start ADB server");
            return false;
        }
    }

    /// <summary>
    /// Kills the ADB server.
    /// </summary>
    public async Task<bool> KillAdbServerAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _commandRunner.RunAsync("kill-server", cancellationToken);
            return result.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to kill ADB server");
            return false;
        }
    }
}