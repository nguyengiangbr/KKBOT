// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Polly;
using Polly.Retry;

namespace KKBOT.Device.Adb;

/// <summary>
/// Result of an ADB command execution.
/// </summary>
public record AdbCommandResult
{
    public int ExitCode { get; init; }
    public string Output { get; init; } = string.Empty;
    public string ErrorOutput { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }
    public bool IsSuccess => ExitCode == 0;
}

/// <summary>
/// Executes ADB commands and parses output.
/// </summary>
public sealed class AdbCommandRunner : IAsyncDisposable
{
    private readonly AsyncRetryPolicy<AdbCommandResult> _retryPolicy;

    private readonly AdbClientOptions _options;
    private readonly ILogger<AdbCommandRunner> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _disposed;

    public AdbCommandRunner(
        IOptions<AdbClientOptions> options,
        ILogger<AdbCommandRunner> logger)
    {
        _options = options.Value;
        _logger = logger;
        // Initialize Polly retry policy based on options
        _retryPolicy = Policy<AdbCommandResult>
            .Handle<Exception>()
            .OrResult(r => !r.IsSuccess)
            .WaitAndRetryAsync(_options.MaxRetries,
                retryAttempt => TimeSpan.FromMilliseconds(_options.RetryDelayMs),
                (outcome, timespan, retryAttempt, context) =>
                {
                    if (outcome.Exception != null)
                        _logger.LogWarning(outcome.Exception, "ADB command failed, retry {Attempt}/{MaxRetries} after {Delay}s", retryAttempt, _options.MaxRetries, timespan.TotalSeconds);
                    else
                        _logger.LogWarning("ADB command returned error (exit {ExitCode}), retry {Attempt}/{MaxRetries} after {Delay}s", outcome.Result.ExitCode, retryAttempt, _options.MaxRetries, timespan.TotalSeconds);
                });
    }

    /// <summary>
    /// Runs an ADB command and returns the result.
    /// </summary>
    public async Task<AdbCommandResult> RunAsync(string arguments, CancellationToken cancellationToken, TimeSpan? timeout = null)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(AdbCommandRunner));
        // Use retry policy for transient failures
        return await _retryPolicy.ExecuteAsync(ct => ExecuteCommandAsync(arguments, ct, timeout), cancellationToken);
    }

    private async Task<AdbCommandResult> ExecuteCommandAsync(string arguments, CancellationToken cancellationToken, TimeSpan? timeout)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(AdbCommandRunner));

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var startTime = DateTime.UtcNow;
            var effectiveTimeout = timeout ?? _options.CommandTimeout;

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _options.AdbPath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                }
            };

            _logger.LogDebug("Running ADB command: {AdbPath} {Arguments}", _options.AdbPath, arguments);

            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            var completedTask = await Task.WhenAny(
                process.WaitForExitAsync(cancellationToken),
                Task.Delay(effectiveTimeout, cancellationToken)
            );

            if (completedTask is Task<bool> waitTask && !waitTask.Result)
            {
                // Timeout
                try { process.Kill(true); } catch { }
                throw new TimeoutException($"ADB command timed out after {effectiveTimeout}: {arguments}");
            }

            var output = await outputTask;
            var errorOutput = await errorTask;

            var duration = DateTime.UtcNow - startTime;

            var result = new AdbCommandResult
            {
                ExitCode = process.ExitCode,
                Output = output ?? string.Empty,
                ErrorOutput = errorOutput ?? string.Empty,
                Duration = duration
            };

            if (!result.IsSuccess)
            {
                _logger.LogWarning("ADB command failed (exit code {ExitCode}): {Arguments}\nError: {ErrorOutput}",
                    result.ExitCode, arguments, result.ErrorOutput);
            }
            else
            {
                _logger.LogDebug("ADB command succeeded in {Duration}ms: {Arguments}", duration.TotalMilliseconds, arguments);
            }

            return result;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Runs an ADB command and returns only the output (throws on failure).
    /// </summary>
    public async Task<string> RunOutputAsync(string arguments, CancellationToken cancellationToken, TimeSpan? timeout = null)
    {
        var result = await RunAsync(arguments, cancellationToken, timeout);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"ADB command failed: {result.ErrorOutput}");
        }
        return result.Output;
    }

    /// <summary>
    /// Runs an ADB command that produces binary output (e.g., screencap).
    /// </summary>
    public async Task<byte[]> RunBinaryAsync(string arguments, CancellationToken cancellationToken, TimeSpan? timeout = null)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(AdbCommandRunner));
        return await _retryPolicy.ExecuteAsync(ct => ExecuteBinaryAsync(arguments, ct, timeout), cancellationToken);
    }

    private async Task<byte[]> ExecuteBinaryAsync(string arguments, CancellationToken cancellationToken, TimeSpan? timeout)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(AdbCommandRunner));

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var startTime = DateTime.UtcNow;
            var effectiveTimeout = timeout ?? _options.CommandTimeout;

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _options.AdbPath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            _logger.LogDebug("Running ADB binary command: {AdbPath} {Arguments}", _options.AdbPath, arguments);

            process.Start();

            using var memoryStream = new MemoryStream();
            var copyTask = process.StandardOutput.BaseStream.CopyToAsync(memoryStream, cancellationToken);

            var completedTask = await Task.WhenAny(
                process.WaitForExitAsync(cancellationToken),
                Task.Delay(effectiveTimeout, cancellationToken)
            );

            if (completedTask is Task<bool> waitTask && !waitTask.Result)
            {
                try { process.Kill(true); } catch { }
                throw new TimeoutException($"ADB binary command timed out after {effectiveTimeout}: {arguments}");
            }

            await copyTask;
            
            var errorOutput = await process.StandardError.ReadToEndAsync(cancellationToken);
            var duration = DateTime.UtcNow - startTime;

            if (process.ExitCode != 0)
            {
                _logger.LogWarning("ADB binary command failed (exit code {ExitCode}): {Arguments}\nError: {ErrorOutput}",
                    process.ExitCode, arguments, errorOutput);
                throw new InvalidOperationException($"ADB binary command failed: {errorOutput}");
            }

            _logger.LogDebug("ADB binary command succeeded in {Duration}ms, {Size} bytes", duration.TotalMilliseconds, memoryStream.Length);
            return memoryStream.ToArray();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Runs multiple ADB commands in sequence.
    /// </summary>
    public async Task<IReadOnlyList<AdbCommandResult>> RunBatchAsync(
        IEnumerable<string> arguments,
        CancellationToken cancellationToken,
        TimeSpan? timeout = null)
    {
        var results = new List<AdbCommandResult>();
        foreach (var arg in arguments)
        {
            var result = await RunAsync(arg, cancellationToken, timeout);
            results.Add(result);
            if (!result.IsSuccess)
            {
                _logger.LogWarning("Batch command failed, stopping: {Arguments}", arg);
                break;
            }
        }
        return results;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _semaphore.Dispose();
        await Task.CompletedTask;
    }
}