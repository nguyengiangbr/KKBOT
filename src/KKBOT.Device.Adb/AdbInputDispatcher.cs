// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using KKBOT.Device.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KKBOT.Device.Adb;

/// <summary>
/// Dispatches input commands to ADB devices.
/// </summary>
public sealed class AdbInputDispatcher : IAsyncDisposable
{
    private readonly string _deviceId;
    private readonly AdbClientOptions _options;
    private readonly ILogger _logger;
    private readonly AdbCommandRunner _commandRunner;
    private bool _disposed;

    public AdbInputDispatcher(
        string deviceId,
        AdbClientOptions options,
        ILogger logger,
        AdbCommandRunner commandRunner)
    {
        _deviceId = deviceId;
        _options = options;
        _logger = logger;
        _commandRunner = commandRunner;
    }

    /// <summary>
    /// Sends an input command to the device.
    /// </summary>
    public async Task<InputResult> SendAsync(InputCommand command, CancellationToken cancellationToken)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(AdbInputDispatcher));

        var startTime = DateTime.UtcNow;

        try
        {
            string adbCommand = command.Type switch
            {
                InputType.Tap => BuildTapCommand(command),
                InputType.Swipe => BuildSwipeCommand(command),
                InputType.Text => BuildTextCommand(command),
                InputType.Key => BuildKeyCommand(command),
                _ => throw new ArgumentOutOfRangeException(nameof(command.Type), command.Type, "Unknown input type")
            };

            _logger.LogDebug("Sending input to {DeviceId}: {Command}", _deviceId, adbCommand);

            var result = await _commandRunner.RunAsync(
                $"-s {_deviceId} {adbCommand}",
                cancellationToken,
                TimeSpan.FromSeconds(10));

            var duration = DateTime.UtcNow - startTime;

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Input command failed on {DeviceId}: {Error}", _deviceId, result.ErrorOutput);
                return new InputResult
                {
                    IsSuccess = false,
                    ErrorMessage = result.ErrorOutput,
                    Duration = duration
                };
            }

            _logger.LogDebug("Input command succeeded on {DeviceId} in {Duration}ms", _deviceId, duration.TotalMilliseconds);

            return new InputResult
            {
                IsSuccess = true,
                Duration = duration
            };
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "Input command threw exception on {DeviceId}", _deviceId);
            return new InputResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
                Duration = duration
            };
        }
    }

    /// <summary>
    /// Sends a tap at the specified coordinates.
    /// </summary>
    public Task<InputResult> TapAsync(int x, int y, CancellationToken cancellationToken)
    {
        var command = new InputCommand
        {
            Type = InputType.Tap,
            X = x,
            Y = y
        };
        return SendAsync(command, cancellationToken);
    }

    /// <summary>
    /// Sends a swipe from start to end coordinates.
    /// </summary>
    public Task<InputResult> SwipeAsync(int startX, int startY, int endX, int endY, int durationMs, CancellationToken cancellationToken)
    {
        var command = new InputCommand
        {
            Type = InputType.Swipe,
            X = startX,
            Y = startY,
            EndX = endX,
            EndY = endY,
            Duration = TimeSpan.FromMilliseconds(durationMs)
        };
        return SendAsync(command, cancellationToken);
    }

    /// <summary>
    /// Sends text input.
    /// </summary>
    public Task<InputResult> TextAsync(string text, CancellationToken cancellationToken)
    {
        var command = new InputCommand
        {
            Type = InputType.Text,
            Text = text
        };
        return SendAsync(command, cancellationToken);
    }

    /// <summary>
    /// Sends a key event.
    /// </summary>
    public Task<InputResult> KeyAsync(int keyCode, CancellationToken cancellationToken)
    {
        var command = new InputCommand
        {
            Type = InputType.Key,
            KeyCode = keyCode
        };
        return SendAsync(command, cancellationToken);
    }

    /// <summary>
    /// Sends multiple input commands in sequence.
    /// </summary>
    public async Task<IReadOnlyList<InputResult>> SendSequenceAsync(
        IEnumerable<InputCommand> commands,
        CancellationToken cancellationToken,
        TimeSpan? delayBetween = null)
    {
        var results = new List<InputResult>();
        var delay = delayBetween ?? TimeSpan.FromMilliseconds(100);

        foreach (var command in commands)
        {
            var result = await SendAsync(command, cancellationToken);
            results.Add(result);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Input sequence stopped due to failure: {Error}", result.ErrorMessage);
                break;
            }

            await Task.Delay(delay, cancellationToken);
        }

        return results;
    }

    private static string BuildTapCommand(InputCommand command)
    {
        return $"shell input tap {command.X} {command.Y}";
    }

    private static string BuildSwipeCommand(InputCommand command)
    {
        var durationMs = command.Duration?.TotalMilliseconds ?? 300;
        return $"shell input swipe {command.X} {command.Y} {command.EndX} {command.EndY} {(int)durationMs}";
    }

    private static string BuildTextCommand(InputCommand command)
    {
        // Escape special characters for ADB input text
        var text = command.Text?
            .Replace(" ", "%s")
            .Replace("'", "\\'")
            .Replace("\"", "\\\"")
            .Replace("&", "\\&")
            .Replace("|", "\\|")
            .Replace(";", "\\;")
            .Replace("<", "\\<")
            .Replace(">", "\\>")
            .Replace("(", "\\(")
            .Replace(")", "\\)")
            .Replace("$", "\\$")
            .Replace("`", "\\`") ?? string.Empty;

        return $"shell input text \"{text}\"";
    }

    private static string BuildKeyCommand(InputCommand command)
    {
        return $"shell input keyevent {command.KeyCode}";
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await Task.CompletedTask;
    }
}