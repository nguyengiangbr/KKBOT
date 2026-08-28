// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using KKBOT.Device.Abstractions;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using System.Threading;

namespace KKBOT.Device.Simulator;

/// <summary>
/// Represents a simulated device session that replays frames from a pre-recorded scenario.
/// Implements the full IDeviceSession contract with placeholder behaviours.
/// </summary>
public sealed class SimulatedDeviceSession : IDeviceSession
{
    private readonly string _scenarioPath;
    private readonly ILogger _logger;
    private readonly ReplayFrameProvider _frameProvider;
    private readonly FakeInputDispatcher _inputDispatcher;
    private readonly ConcurrentQueue<Frame> _frameQueue = new();
    private bool _initialized;
    private CancellationTokenSource? _cts;

    public SimulatedDeviceSession(string scenarioPath, ILogger logger)
    {
        _scenarioPath = scenarioPath;
        _logger = logger;
        _frameProvider = new ReplayFrameProvider(scenarioPath, logger);
        _inputDispatcher = new FakeInputDispatcher(logger);
    }

    public string DeviceId => Path.GetFileNameWithoutExtension(_scenarioPath);

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_initialized) return;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        await foreach (var frame in _frameProvider.GetFramesAsync(_cts.Token))
        {
            _frameQueue.Enqueue(frame);
        }
        _initialized = true;
        _logger.LogInformation("Simulated device session '{DeviceId}' initialized with {Count} frames", DeviceId, _frameQueue.Count);
    }

    public async Task<DeviceInfo> GetInfoAsync(CancellationToken cancellationToken)
    {
        // Return minimal mock info for the simulated device
        await Task.CompletedTask;
        return new DeviceInfo
        {
            DeviceId = DeviceId,
            Model = "Simulated Device",
            Manufacturer = "KKBOT",
            AndroidVersion = "0",
            Abi = "simulation",
            ScreenWidth = 1280,
            ScreenHeight = 720,
            ScreenDensity = 160,
            BatteryLevel = 100,
            IsCharging = true,
            TotalStorage = 64L * 1024 * 1024 * 1024, // 64 GB
            FreeStorage = 32L * 1024 * 1024 * 1024,
            IsConnected = true
        };
    }

    public Task<bool> IsConnectedAsync(CancellationToken cancellationToken) => Task.FromResult(true);

    public async Task<Frame> CaptureAsync(CaptureOptions options, CancellationToken cancellationToken)
    {
        // Options are ignored in the simulator – we just return the next queued frame.
        return await CaptureFrameAsync(cancellationToken);
    }

    public async Task<Frame> CaptureFrameAsync(CancellationToken cancellationToken)
    {
        if (!_initialized) throw new InvalidOperationException("Session not initialized");
        // Simulate capture delay
        await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        if (_frameQueue.TryDequeue(out var frame))
            return frame;
        return new Frame { Timestamp = DateTimeOffset.UtcNow, Data = Array.Empty<byte>() };
    }

    public async Task<InputResult> SendInputAsync(InputCommand command, CancellationToken cancellationToken)
    {
        // Forward to the fake dispatcher which simply logs the action.
        var dispatchResult = await _inputDispatcher.DispatchAsync(command, cancellationToken);
        return new InputResult
        {
            IsSuccess = dispatchResult.Success,
            ErrorMessage = dispatchResult.Success ? null : dispatchResult.Details,
            Duration = TimeSpan.Zero
        };
    }

    public async Task LaunchApplicationAsync(ApplicationIdentity application, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[Simulator] LaunchApplication called for {PackageName} on {DeviceId}", application.PackageName, DeviceId);
        await Task.CompletedTask;
    }

    public async Task StopApplicationAsync(ApplicationIdentity application, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[Simulator] StopApplication called for {PackageName} on {DeviceId}", application.PackageName, DeviceId);
        await Task.CompletedTask;
    }

    public Task<HealthCheckResult> HealthCheckAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(new HealthCheckResult { IsHealthy = true, Details = "Simulated device always healthy" });
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
