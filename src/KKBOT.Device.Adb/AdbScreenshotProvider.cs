// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using KKBOT.Device.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenCvSharp;

namespace KKBOT.Device.Adb;

/// <summary>
/// Captures screenshots from ADB devices.
/// </summary>
public sealed class AdbScreenshotProvider : IAsyncDisposable
{
    private readonly string _deviceId;
    private readonly AdbClientOptions _options;
    private readonly ILogger<AdbScreenshotProvider> _logger;
    private readonly AdbCommandRunner _commandRunner;
    private bool _disposed;

    public AdbScreenshotProvider(
        string deviceId,
        AdbClientOptions options,
        ILogger<AdbScreenshotProvider> logger,
        AdbCommandRunner commandRunner)
    {
        _deviceId = deviceId;
        _options = options;
        _logger = logger;
        _commandRunner = commandRunner;
    }

    /// <summary>
    /// Captures a frame from the device screen.
    /// </summary>
    public async Task<Frame> CaptureAsync(CaptureOptions options, CancellationToken cancellationToken)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(AdbScreenshotProvider));

        var startTime = DateTime.UtcNow;
        var timeout = options.Timeout ?? _options.CommandTimeout;

        try
        // Capture raw PNG data using exec-out for better performance
        {
            var pngData = await _commandRunner.RunBinaryAsync(
                $"-s {_deviceId} exec-out screencap -p",
                cancellationToken,
                timeout);

            if (pngData.Length == 0)
            {
                throw new InvalidOperationException("Empty screenshot data received");
            }

            // Decode PNG to get dimensions
            using var mat = Cv2.ImDecode(pngData, ImreadModes.Color);
            if (mat.Empty())
            {
                throw new InvalidOperationException("Failed to decode screenshot PNG");
            }

            var frame = new Frame
            {
                Width = mat.Width,
                Height = mat.Height,
                Format = options.Format.ToLowerInvariant(),
                Data = pngData,
                CaptureLatencyMs = (DateTime.UtcNow - startTime).TotalMilliseconds,
                Metadata = new Dictionary<string, object?>
                {
                    ["deviceId"] = _deviceId,
                    ["captureMethod"] = "exec-out screencap",
                    ["originalFormat"] = "png"
                }
            };

            // Apply ROI if specified
            if (options.RegionOfInterest.HasValue)
            {
                frame = ApplyRoi(frame, options.RegionOfInterest.Value);
            }

            _logger.LogDebug("Captured frame {FrameId} from {DeviceId}: {Width}x{Height} in {Latency}ms",
                frame.FrameId, _deviceId, frame.Width, frame.Height, frame.CaptureLatencyMs);

            return frame;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture screenshot from {DeviceId}", _deviceId);
            throw;
        }
    }

    /// <summary>
    /// Captures a frame and returns it as an OpenCV Mat for processing.
    /// </summary>
    public async Task<Mat> CaptureMatAsync(CaptureOptions options, CancellationToken cancellationToken)
    {
        var frame = await CaptureAsync(options, cancellationToken);
        return Cv2.ImDecode(frame.Data, ImreadModes.Color);
    }

    /// <summary>
    /// Captures a frame as a byte array in the specified format.
    /// </summary>
    public async Task<byte[]> CaptureBytesAsync(CaptureOptions options, CancellationToken cancellationToken)
    {
        var frame = await CaptureAsync(options, cancellationToken);
        
        if (options.Format.Equals("raw", StringComparison.OrdinalIgnoreCase))
        {
            // Convert PNG to raw bytes
            using var mat = Cv2.ImDecode(frame.Data, ImreadModes.Color);
            return mat.ToBytes();
        }
        
        return frame.Data;
    }

    private Frame ApplyRoi(Frame frame, Rectangle roi)
    {
        using var mat = Cv2.ImDecode(frame.Data, ImreadModes.Color);
        
        var rect = new OpenCvSharp.Rect(roi.X, roi.Y, roi.Width, roi.Height);
        rect = rect.Intersect(new OpenCvSharp.Rect(0, 0, mat.Width, mat.Height));
        
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            _logger.LogWarning("Invalid ROI for frame {FrameId}: {Roi}", frame.FrameId, roi);
            return frame;
        }

        using var roiMat = new Mat(mat, rect);
        var roiData = roiMat.ToBytes(".png");

        return frame with
        {
            Width = rect.Width,
            Height = rect.Height,
            Data = roiData,
            Metadata = new Dictionary<string, object?>(frame.Metadata ?? new())
            {
                ["roi"] = roi,
                ["originalWidth"] = frame.Width,
                ["originalHeight"] = frame.Height
            }
        };
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await Task.CompletedTask;
    }
}