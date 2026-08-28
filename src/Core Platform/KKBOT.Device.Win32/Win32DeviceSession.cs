// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Device.Abstractions;
using KKBOT.Device.Win32.Native;
using Microsoft.Extensions.Logging;

namespace KKBOT.Device.Win32;

/// <summary>
/// Win32 implementation of device session for capturing screen and sending input to Windows native windows.
/// </summary>
public sealed class Win32DeviceSession : IDeviceSession
{
    private readonly IntPtr _windowHandle;
    private readonly string _deviceId;
    private readonly ILogger<Win32DeviceSession> _logger;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="Win32DeviceSession"/> class.
    /// </summary>
    /// <param name="windowHandle">The window handle (HWND) to interact with.</param>
    /// <param name="deviceId">Unique identifier for this device session.</param>
    /// <param name="logger">Logger instance.</param>
    public Win32DeviceSession(IntPtr windowHandle, string deviceId, ILogger<Win32DeviceSession> logger)
    {
        _windowHandle = windowHandle == IntPtr.Zero ? throw new ArgumentException("Window handle cannot be zero.", nameof(windowHandle)) : windowHandle;
        _deviceId = string.IsNullOrWhiteSpace(deviceId) ? throw new ArgumentException("Device ID cannot be empty.", nameof(deviceId)) : deviceId;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string DeviceId => _deviceId;

    /// <inheritdoc />
    public Task<DeviceInfo> GetInfoAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Win32Api.IsWindow(_windowHandle))
        {
            return Task.FromResult(new DeviceInfo
            {
                DeviceId = _deviceId,
                Model = "Win32 Window",
                Manufacturer = "Microsoft",
                ScreenWidth = 0,
                ScreenHeight = 0,
                IsConnected = false
            });
        }

        Win32Api.GetWindowRect(_windowHandle, out var rect);
        Win32Api.GetClientRect(_windowHandle, out var clientRect);

        return Task.FromResult(new DeviceInfo
        {
            DeviceId = _deviceId,
            Model = "Win32 Window",
            Manufacturer = "Microsoft",
            ScreenWidth = clientRect.Width,
            ScreenHeight = clientRect.Height,
            ScreenDensity = 96,
            IsConnected = true
        });
    }

    /// <inheritdoc />
    public Task<bool> IsConnectedAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Win32Api.IsWindow(_windowHandle) && Win32Api.IsWindowVisible(_windowHandle));
    }

    /// <inheritdoc />
    public async Task<Frame> CaptureAsync(CaptureOptions options, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!Win32Api.IsWindow(_windowHandle))
            {
                return CreateEmptyFrame("Window not found");
            }

            // Get client rectangle
            Win32Api.GetClientRect(_windowHandle, out var clientRect);
            var width = clientRect.Width;
            var height = clientRect.Height;

            if (width <= 0 || height <= 0)
            {
                return CreateEmptyFrame("Invalid window dimensions");
            }

            // Apply region of interest if specified
            var roi = options.RegionOfInterest;
            var captureRect = roi != null
                ? new System.Drawing.Rectangle(roi.X, roi.Y, roi.Width, roi.Height)
                : new System.Drawing.Rectangle(0, 0, width, height);

            captureRect.Width = Math.Min(captureRect.Width, width - captureRect.X);
            captureRect.Height = Math.Min(captureRect.Height, height - captureRect.Y);

            if (captureRect.Width <= 0 || captureRect.Height <= 0)
            {
                return CreateEmptyFrame("Invalid capture region");
            }

            // Capture using PrintWindow (captures hidden windows too)
            var frameData = await CaptureWindowAsync(captureRect, cancellationToken);
            
            stopwatch.Stop();

            return new Frame
            {
                FrameId = Guid.NewGuid(),
                Timestamp = DateTimeOffset.UtcNow,
                Width = captureRect.Width,
                Height = captureRect.Height,
                Format = options.Format,
                Data = frameData,
                CaptureLatencyMs = stopwatch.Elapsed.TotalMilliseconds,
                Metadata = new Dictionary<string, object?>
                {
                    ["WindowHandle"] = _windowHandle.ToString(),
                    ["CaptureMethod"] = "PrintWindow",
                    ["ClientRect"] = $"{clientRect.Width}x{clientRect.Height}"
                }
            };
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return CreateEmptyFrame("Operation cancelled");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error capturing window {Handle}", _windowHandle);
            return CreateEmptyFrame($"Capture error: {ex.Message}");
        }
    }

    private async Task<byte[]> CaptureWindowAsync(System.Drawing.Rectangle captureRect, CancellationToken cancellationToken)
    {
        // Try PrintWindow first (works for hidden/minimized windows)
        var printWindowData = TryPrintWindowCapture(captureRect);
        if (printWindowData != null && printWindowData.Length > 0)
        {
            return printWindowData;
        }

        // Fallback to BitBlt if PrintWindow fails
        return await Task.Run(() => BitBltCapture(captureRect, cancellationToken), cancellationToken);
    }

    private byte[]? TryPrintWindowCapture(System.Drawing.Rectangle captureRect)
    {
        IntPtr hWndDC = IntPtr.Zero;
        IntPtr hMemDC = IntPtr.Zero;
        IntPtr hBitmap = IntPtr.Zero;

        try
        {
            var width = captureRect.Width;
            var height = captureRect.Height;

            // Get window DC
            hWndDC = Win32Api.GetWindowDC(_windowHandle);
            if (hWndDC == IntPtr.Zero)
            {
                return null;
            }

            // Create compatible memory DC
            hMemDC = Win32Api.CreateCompatibleDC(hWndDC);
            if (hMemDC == IntPtr.Zero)
            {
                return null;
            }

            // Create compatible bitmap
            hBitmap = Win32Api.CreateCompatibleBitmap(hWndDC, width, height);
            if (hBitmap == IntPtr.Zero)
            {
                return null;
            }

            // Select bitmap into memory DC
            var hOldBitmap = Win32Api.SelectObject(hMemDC, hBitmap);
            if (hOldBitmap == IntPtr.Zero)
            {
                return null;
            }

            // Use PrintWindow to capture (PW_RENDERFULLCONTENT = 0x02 captures full content including hidden)
            var result = Win32Api.PrintWindow(_windowHandle, hMemDC, Win32Api.PW_RENDERFULLCONTENT);
            if (!result)
            {
                // Try without PW_RENDERFULLCONTENT
                result = Win32Api.PrintWindow(_windowHandle, hMemDC, 0);
            }

            if (!result)
            {
                return null;
            }

            // Convert bitmap to byte array
            return BitmapToBytes(hBitmap, width, height);
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            // CRITICAL: Clean up all unmanaged resources in finally block
            if (hBitmap != IntPtr.Zero)
            {
                Win32Api.DeleteObject(hBitmap);
            }
            if (hMemDC != IntPtr.Zero)
            {
                Win32Api.DeleteDC(hMemDC);
            }
            if (hWndDC != IntPtr.Zero)
            {
                Win32Api.ReleaseDC(_windowHandle, hWndDC);
            }
        }
    }

    private byte[] BitBltCapture(System.Drawing.Rectangle captureRect, CancellationToken cancellationToken)
    {
        IntPtr hWndDC = IntPtr.Zero;
        IntPtr hMemDC = IntPtr.Zero;
        IntPtr hBitmap = IntPtr.Zero;

        try
        {
            var width = captureRect.Width;
            var height = captureRect.Height;

            // Get window DC
            hWndDC = Win32Api.GetWindowDC(_windowHandle);
            if (hWndDC == IntPtr.Zero)
            {
                return CreateEmptyFrameData("Failed to get window DC");
            }

            // Create compatible memory DC
            hMemDC = Win32Api.CreateCompatibleDC(hWndDC);
            if (hMemDC == IntPtr.Zero)
            {
                return CreateEmptyFrameData("Failed to create compatible DC");
            }

            // Create compatible bitmap
            hBitmap = Win32Api.CreateCompatibleBitmap(hWndDC, width, height);
            if (hBitmap == IntPtr.Zero)
            {
                return CreateEmptyFrameData("Failed to create compatible bitmap");
            }

            // Select bitmap into memory DC
            var hOldBitmap = Win32Api.SelectObject(hMemDC, hBitmap);
            if (hOldBitmap == IntPtr.Zero)
            {
                return CreateEmptyFrameData("Failed to select bitmap");
            }

            // BitBlt from window DC to memory DC
            var result = Win32Api.BitBlt(
                hMemDC,
                0, 0,
                width, height,
                hWndDC,
                captureRect.X, captureRect.Y,
                Win32Api.SRCCOPY | Win32Api.CAPTUREBLT);

            if (!result)
            {
                return CreateEmptyFrameData("BitBlt failed");
            }

            // Convert bitmap to byte array
            return BitmapToBytes(hBitmap, width, height);
        }
        catch (Exception)
        {
            return CreateEmptyFrameData("BitBlt capture failed");
        }
        finally
        {
            // CRITICAL: Clean up all unmanaged resources in finally block
            if (hBitmap != IntPtr.Zero)
            {
                Win32Api.DeleteObject(hBitmap);
            }
            if (hMemDC != IntPtr.Zero)
            {
                Win32Api.DeleteDC(hMemDC);
            }
            if (hWndDC != IntPtr.Zero)
            {
                Win32Api.ReleaseDC(_windowHandle, hWndDC);
            }
        }
    }

    private byte[] BitmapToBytes(IntPtr hBitmap, int width, int height)
    {
        try
        {
            var bmiHeader = new Win32Api.BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<Win32Api.BITMAPINFOHEADER>(),
                biWidth = width,
                biHeight = -height, // Negative for top-down DIB
                biPlanes = 1,
                biBitCount = 32,
                biCompression = 0, // BI_RGB
                biSizeImage = 0,
                biXPelsPerMeter = 0,
                biYPelsPerMeter = 0,
                biClrUsed = 0,
                biClrImportant = 0
            };

            var bitmapInfo = new Win32Api.BITMAPINFO
            {
                bmiHeader = bmiHeader
            };

            var bufferSize = width * height * 4; // 32-bit = 4 bytes per pixel
            var buffer = new byte[bufferSize];
            var bufferHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);

            try
            {
                var hBitmapPtr = hBitmap;
                var bitmapInfoPtr = Marshal.AllocHGlobal(Marshal.SizeOf<Win32Api.BITMAPINFO>());
                try
                {
                    Marshal.StructureToPtr(bitmapInfo, bitmapInfoPtr, false);

                    // Get DIBits to retrieve pixel data
                    var result = Win32Api.GetDIBits(
                        IntPtr.Zero, // We'll use a temporary DC
                        hBitmapPtr,
                        0,
                        (uint)height,
                        bufferHandle.AddrOfPinnedObject(),
                        ref bitmapInfo,
                        0); // DIB_RGB_COLORS

                    if (!result)
                    {
                        // Try with a compatible DC
                        var hDC = Win32Api.GetDC(IntPtr.Zero);
                        if (hDC != IntPtr.Zero)
                        {
                            try
                            {
                                result = Win32Api.GetDIBits(
                                    hDC,
                                    hBitmapPtr,
                                    0,
                                    (uint)height,
                                    bufferHandle.AddrOfPinnedObject(),
                                    ref bitmapInfo,
                                    0);
                            }
                            finally
                            {
                                Win32Api.ReleaseDC(IntPtr.Zero, hDC);
                            }
                        }
                    }

                    if (!result)
                    {
                        // Last resort: use System.Drawing to convert
                        return ConvertBitmapUsingGDI(hBitmap, width, height);
                    }

                    return buffer;
                }
                finally
                {
                    bufferHandle.Free();
                }
            }
            catch (Exception)
            {
                return CreateEmptyFrameData("Failed to convert bitmap to bytes");
            }
        }
        catch (Exception)
        {
            return CreateEmptyFrameData("BitmapToBytes failed");
        }
    }

    private byte[] ConvertBitmapUsingGDI(IntPtr hBitmap, int width, int height)
    {
        try
        {
            // Use System.Drawing to create bitmap from HANDLE and convert to bytes
            using var bitmap = System.Drawing.Image.FromHbitmap(hBitmap);
            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Bmp);
            return ms.ToArray();
        }
        catch (Exception)
        {
            return CreateEmptyFrameData("GDI conversion failed");
        }
    }

    private byte[] CreateEmptyFrameData(string message)
    {
        _logger.LogWarning("Capture failed: {Message}", message);
        return Array.Empty<byte>();
    }

    private Frame CreateEmptyFrame(string message)
    {
        _logger.LogWarning("Capture failed: {Message}", message);
        return new Frame
        {
            FrameId = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            Width = 0,
            Height = 0,
            Format = "raw",
            Data = Array.Empty<byte>(),
            CaptureLatencyMs = 0,
            Metadata = new Dictionary<string, object?> { ["Error"] = message }
        };
    }

    /// <inheritdoc />
    public async Task<InputResult> SendInputAsync(InputCommand command, CancellationToken cancellationToken)
    {
        var dispatcher = new Win32InputDispatcher(_windowHandle, _logger);
        var dispatchResult = await dispatcher.DispatchAsync(command, cancellationToken);

        return new InputResult
        {
            IsSuccess = dispatchResult.Success,
            ErrorMessage = dispatchResult.Success ? null : dispatchResult.Details,
            Duration = TimeSpan.Zero // Win32InputDispatcher doesn't measure duration currently
        };
    }

    /// <inheritdoc />
    public async Task LaunchApplicationAsync(ApplicationIdentity application, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = application.PackageName,
                Arguments = application.Arguments ?? string.Empty,
                UseShellExecute = true
            };

            var process = Process.Start(startInfo);
            if (process == null)
            {
                _logger.LogWarning("Failed to launch application: {PackageName}", application.PackageName);
                return;
            }

            // Wait a bit for the window to appear
            await Task.Delay(1000, cancellationToken);

            // Try to find the new window and update handle if needed
            // This is a simplified implementation - in production you might want more robust window finding
            _logger.LogInformation("Launched application: {PackageName}", application.PackageName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch application {PackageName}", application.PackageName);
        }
    }

    /// <inheritdoc />
    public Task StopApplicationAsync(ApplicationIdentity application, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var processes = Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(application.PackageName));
            foreach (var process in processes)
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(5000))
                {
                    process.Kill();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop application {PackageName}", application.PackageName);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> HealthCheckAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var isWindowValid = Win32Api.IsWindow(_windowHandle);
        var isVisible = isWindowValid && Win32Api.IsWindowVisible(_windowHandle);

        return Task.FromResult(new HealthCheckResult
        {
            IsHealthy = isWindowValid,
            IsConnected = isWindowValid,
            ErrorMessage = isWindowValid ? null : "Window handle is invalid"
        });
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // No persistent unmanaged resources to clean up for the session itself
        // (Capture resources are cleaned up in finally blocks)

        _logger.LogDebug("Win32DeviceSession {DeviceId} disposed", _deviceId);
        await Task.CompletedTask;
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().Wait();
    }
}