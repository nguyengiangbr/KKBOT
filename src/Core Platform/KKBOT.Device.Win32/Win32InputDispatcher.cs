// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Device.Abstractions;
using KKBOT.Device.Win32.Native;
using Microsoft.Extensions.Logging;

namespace KKBOT.Device.Win32;

/// <summary>
/// Win32 implementation of input dispatcher for sending hidden input commands to a window.
/// Uses PostMessage/SendMessage to send input without requiring the window to be in foreground.
/// </summary>
public sealed class Win32InputDispatcher : IInputDispatcher
{
    private readonly IntPtr _windowHandle;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="Win32InputDispatcher"/> class.
    /// </summary>
    /// <param name="windowHandle">The window handle (HWND) to send input to.</param>
    /// <param name="logger">Logger instance.</param>
    public Win32InputDispatcher(IntPtr windowHandle, ILogger logger)
    {
        _windowHandle = windowHandle == IntPtr.Zero ? throw new ArgumentException("Window handle cannot be zero.", nameof(windowHandle)) : windowHandle;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<DispatchResult> DispatchAsync(InputCommand command, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!Win32Api.IsWindow(_windowHandle))
            {
                return Task.FromResult(new DispatchResult
                {
                    Success = false,
                    Details = "Target window is not valid."
                });
            }

            var success = command.Type switch
            {
                InputType.Tap => SendTapAsync(command, cancellationToken),
                InputType.Swipe => SendSwipeAsync(command, cancellationToken),
                InputType.Key => SendKeyAsync(command, cancellationToken),
                InputType.Text => SendTextAsync(command, cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(command.Type), command.Type, "Unsupported input type.")
            };

            stopwatch.Stop();

            return Task.FromResult(new DispatchResult
            {
                Success = success,
                Details = success ? "Input dispatched successfully." : "Failed to dispatch input."
            });
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return Task.FromResult(new DispatchResult
            {
                Success = false,
                Details = "Operation was cancelled."
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error dispatching input command {CommandType}", command.Type);
            return Task.FromResult(new DispatchResult
            {
                Success = false,
                Details = $"Exception: {ex.Message}"
            });
        }
    }

    private bool SendTapAsync(InputCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var x = command.X;
            var y = command.Y;
            var lParam = Win32Api.MakeLParam(x, y);

            // Send WM_LBUTTONDOWN
            var downResult = Win32Api.PostMessage(_windowHandle, Win32Api.WM_LBUTTONDOWN, Win32Api.MK_LBUTTON, lParam);
            if (downResult == IntPtr.Zero)
            {
                _logger.LogWarning("PostMessage WM_LBUTTONDOWN failed for window {Handle}", _windowHandle);
                return false;
            }

            // Small delay between down and up
            Thread.Sleep(10);

            cancellationToken.ThrowIfCancellationRequested();

            // Send WM_LBUTTONUP
            var upResult = Win32Api.PostMessage(_windowHandle, Win32Api.WM_LBUTTONUP, IntPtr.Zero, lParam);
            if (upResult == IntPtr.Zero)
            {
                _logger.LogWarning("PostMessage WM_LBUTTONUP failed for window {Handle}", _windowHandle);
                return false;
            }

            _logger.LogDebug("Tap sent to window {Handle} at ({X}, {Y})", _windowHandle, x, y);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending tap to window {Handle}", _windowHandle);
            return false;
        }
    }

    private bool SendSwipeAsync(InputCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var startX = command.X;
            var startY = command.Y;
            var endX = command.EndX;
            var endY = command.EndY;
            var duration = command.Duration ?? TimeSpan.FromMilliseconds(500);

            var startLParam = Win32Api.MakeLParam(startX, startY);
            var endLParam = Win32Api.MakeLParam(endX, endY);

            // Send WM_LBUTTONDOWN at start position
            var downResult = Win32Api.PostMessage(_windowHandle, Win32Api.WM_LBUTTONDOWN, Win32Api.MK_LBUTTON, startLParam);
            if (downResult == IntPtr.Zero)
            {
                _logger.LogWarning("PostMessage WM_LBUTTONDOWN failed for swipe");
                return false;
            }

            // Calculate intermediate points for smooth swipe
            var steps = Math.Max(5, (int)(duration.TotalMilliseconds / 20));
            var deltaX = (endX - startX) / (double)steps;
            var deltaY = (endY - startY) / (double)steps;

            var stepDelay = duration.TotalMilliseconds / steps;

            for (int i = 1; i <= steps; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var currentX = (int)Math.Round(startX + deltaX * i);
                var currentY = (int)Math.Round(startY + deltaY * i);
                var moveLParam = Win32Api.MakeLParam(currentX, currentY);

                var moveResult = Win32Api.PostMessage(_windowHandle, Win32Api.WM_MOUSEMOVE, IntPtr.Zero, moveLParam);
                if (moveResult == IntPtr.Zero)
                {
                    _logger.LogWarning("PostMessage WM_MOUSEMOVE failed at step {Step}", i);
                }

                Thread.Sleep((int)stepDelay);
            }

            // Send WM_LBUTTONUP at end position
            var upResult = Win32Api.PostMessage(_windowHandle, Win32Api.WM_LBUTTONUP, IntPtr.Zero, endLParam);
            if (upResult == IntPtr.Zero)
            {
                _logger.LogWarning("PostMessage WM_LBUTTONUP failed for swipe");
                return false;
            }

            _logger.LogDebug("Swipe sent from ({StartX}, {StartY}) to ({EndX}, {EndY})", startX, startY, endX, endY);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending swipe");
            return false;
        }
    }

    private bool SendKeyAsync(InputCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var keyCode = command.KeyCode;
            if (keyCode == 0)
            {
                _logger.LogWarning("KeyCode is 0 for key input");
                return false;
            }

            var vKey = (uint)keyCode;
            var scanCode = Win32Api.MapVirtualKey(vKey, 0); // MAPVK_VK_TO_VSC = 0

            // Send WM_KEYDOWN
            var downWParam = new IntPtr(keyCode);
            var downLParam = (IntPtr)(1 | (scanCode << 16)); // repeat count = 1
            var downResult = Win32Api.PostMessage(_windowHandle, Win32Api.WM_KEYDOWN, downWParam, downLParam);
            if (downResult == IntPtr.Zero)
            {
                _logger.LogWarning("PostMessage WM_KEYDOWN failed for key {KeyCode}", keyCode);
                return false;
            }

            // Small delay
            Thread.Sleep(10);

            cancellationToken.ThrowIfCancellationRequested();

            // Send WM_KEYUP
            var upLParam = (IntPtr)(1 | (scanCode << 16) | (1 << 30) | (1 << 31)); // repeat count = 1, previous key state = 1, transition state = 1
            var upResult = Win32Api.PostMessage(_windowHandle, Win32Api.WM_KEYUP, new IntPtr(keyCode), upLParam);
            if (upResult == IntPtr.Zero)
            {
                _logger.LogWarning("PostMessage WM_KEYUP failed for key {KeyCode}", keyCode);
                return false;
            }

            _logger.LogDebug("Key {KeyCode} sent to window {Handle}", keyCode, _windowHandle);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending key");
            return false;
        }
    }

    private bool SendTextAsync(InputCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var text = command.Text ?? string.Empty;
            if (string.IsNullOrEmpty(text))
            {
                _logger.LogWarning("Text is empty for text input");
                return false;
            }

            // Use WM_SETTEXT for direct text setting (most reliable for hidden input)
            var result = Win32Api.SendMessage(_windowHandle, Win32Api.WM_SETTEXT, IntPtr.Zero, text);
            if (result == IntPtr.Zero)
            {
                // Fallback: send character by character using WM_CHAR
                _logger.LogDebug("WM_SETTEXT failed, falling back to WM_CHAR for each character");
                return SendTextCharByChar(text, cancellationToken);
            }

            _logger.LogDebug("Text sent via WM_SETTEXT to window {Handle}: {Text}", _windowHandle, text.Length > 50 ? text[..50] + "..." : text);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending text");
            return false;
        }
    }

    private bool SendTextCharByChar(string text, CancellationToken cancellationToken)
    {
        try
        {
            foreach (var c in text)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Use WM_CHAR for character input
                var result = Win32Api.PostMessage(_windowHandle, Win32Api.WM_CHAR, new IntPtr(c), IntPtr.Zero);
                if (result == IntPtr.Zero)
                {
                    _logger.LogWarning("PostMessage WM_CHAR failed for character {Char}", c);
                    return false;
                }

                // Small delay between characters
                Thread.Sleep(5);
            }

            _logger.LogDebug("Text sent char by char to window {Handle}: {Length} chars", _windowHandle, text.Length);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending text char by char");
            return false;
        }
    }
}