// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Wrap;
using KKBOT.Observability;
using KKBOT.Observability.Metrics;

namespace KKBOT.Device.Adb;

/// <summary>
/// ADB command types for specific timeout and retry handling.
/// </summary>
public enum AdbCommandType
{
    Unknown,
    DeviceList,
    DeviceState,
    Screencap,
    Shell,
    Input,
    Install,
    Uninstall,
    Forward,
    Reverse,
    Logcat,
    Push,
    Pull
}

/// <summary>
/// Classification of ADB command errors for proper handling.
/// </summary>
public enum AdbErrorType
{
    None,
    Transient,           // Temporary issue, retry likely to succeed
    DeviceOffline,       // Device disconnected or unauthorized
    Permanent,           // Command syntax error, permission denied, etc.
    Timeout,             // Command timed out
    DaemonNotRunning,    // ADB server not running
    DeviceNotFound       // Specific device not found
}

/// <summary>
/// Result of an ADB command execution with error classification.
/// </summary>
public record AdbCommandResult
{
    public int ExitCode { get; init; }
    public string Output { get; init; } = string.Empty;
    public string ErrorOutput { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }
    public bool IsSuccess => ExitCode == 0;
    public AdbErrorType ErrorType { get; init; } = AdbErrorType.None;
    public string? ErrorCode { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
}

/// <summary>
/// Executes ADB commands with circuit breaker, error classification, and observability.
/// </summary>
public sealed class AdbCommandRunner : IAsyncDisposable
{
    private readonly AdbClientOptions _options;
    private readonly ILogger<AdbCommandRunner> _logger;
    private readonly IMetricsRegistry? _metricsRegistry;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly AsyncRetryPolicy<AdbCommandResult> _retryPolicy;
    private readonly AsyncCircuitBreakerPolicy<AdbCommandResult> _circuitBreakerPolicy;
    private readonly AsyncPolicyWrap<AdbCommandResult> _combinedPolicy;
    private bool _disposed;

    // Metrics
    private static readonly Counter<long> _commandsTotal;
    private static readonly Counter<long> _commandsFailed;
    private static readonly Histogram<double> _commandDurationMs;
    private static readonly Counter<long> _circuitBreakerTrips;
    private static readonly UpDownCounter<long> _activeCommands;

    static AdbCommandRunner()
    {
        var meter = new Meter("KKBOT.Device.Adb");
        _commandsTotal = meter.CreateCounter<long>("kkbot_adb_commands_total");
        _commandsFailed = meter.CreateCounter<long>("kkbot_adb_commands_failed_total");
        _commandDurationMs = meter.CreateHistogram<double>("kkbot_adb_command_duration_ms");
        _circuitBreakerTrips = meter.CreateCounter<long>("kkbot_adb_circuit_breaker_trips_total");
        _activeCommands = meter.CreateUpDownCounter<long>("kkbot_adb_active_commands");
    }

    public AdbCommandRunner(
        IOptions<AdbClientOptions> options,
        ILogger<AdbCommandRunner> logger,
        IMetricsRegistry? metricsRegistry = null)
    {
        _options = options.Value;
        _logger = logger;
        _metricsRegistry = metricsRegistry;

        // Retry policy for transient failures
        _retryPolicy = Policy<AdbCommandResult>
            .Handle<Exception>(ex => IsTransientException(ex))
            .OrResult(r => r.ErrorType == AdbErrorType.Transient || r.ErrorType == AdbErrorType.Timeout)
            .WaitAndRetryAsync(_options.MaxRetries,
                retryAttempt => TimeSpan.FromMilliseconds(_options.RetryDelayMs * retryAttempt), // Exponential backoff
                (outcome, timespan, retryAttempt, context) =>
                {
                    var correlationId = context.GetCorrelationId() ?? "unknown";
                    if (outcome.Exception != null)
                    {
                        _logger.LogWarning(outcome.Exception, "[{CorrelationId}] ADB command failed (transient), retry {Attempt}/{MaxRetries} after {Delay}ms", 
                            correlationId, retryAttempt, _options.MaxRetries, timespan.TotalMilliseconds);
                    }
                    else
                    {
                        _logger.LogWarning("[{CorrelationId}] ADB command returned transient error (exit {ExitCode}), retry {Attempt}/{MaxRetries} after {Delay}ms", 
                            correlationId, outcome.Result.ExitCode, retryAttempt, _options.MaxRetries, timespan.TotalMilliseconds);
                    }
                });

        // Circuit breaker for repeated failures (device offline, daemon issues)
        _circuitBreakerPolicy = Policy<AdbCommandResult>
            .Handle<Exception>(ex => IsCircuitBreakerException(ex))
            .OrResult(r => r.ErrorType == AdbErrorType.DeviceOffline || r.ErrorType == AdbErrorType.DaemonNotRunning)
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: _options.CircuitBreakerThreshold,
                durationOfBreak: _options.CircuitBreakerDuration,
                onBreak: (outcome, duration) =>
                {
                    _circuitBreakerTrips.Add(1, new KeyValuePair<string, object?>("reason", outcome.Exception?.GetType().Name ?? outcome.Result.ErrorType.ToString()));
                    _logger.LogError("ADB circuit breaker OPENED for {Duration}s due to: {Reason}", 
                        duration.TotalSeconds, outcome.Exception?.Message ?? outcome.Result.ErrorType.ToString());
                    _metricsRegistry?.IncrementHttpRequestsFailed(); // Reuse metric for circuit breaker trips
                },
                onReset: () =>
                {
                    _logger.LogInformation("ADB circuit breaker CLOSED - service recovered");
                },
                onHalfOpen: () =>
                {
                    _logger.LogWarning("ADB circuit breaker HALF-OPEN - testing service availability");
                });

        // Combine retry and circuit breaker
        _combinedPolicy = _retryPolicy.WrapAsync(_circuitBreakerPolicy);
    }

    /// <summary>
    /// Runs an ADB command with full observability and error handling.
    /// </summary>
    public async Task<AdbCommandResult> RunAsync(string arguments, CancellationToken cancellationToken, TimeSpan? timeout = null)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(AdbCommandRunner));
        
        var correlationId = Activity.Current?.GetTagItem(KkbotTags.CorrelationId)?.ToString() 
            ?? Guid.NewGuid().ToString("N")[..8];
        var commandType = ClassifyCommandType(arguments);
        var effectiveTimeout = timeout ?? GetCommandTimeout(commandType);

        // Record metrics
        _activeCommands.Add(1);
        _commandsTotal.Add(1, new KeyValuePair<string, object?>("type", commandType.ToString()));
        _metricsRegistry?.IncrementHttpRequests(); // Reuse HTTP metric

        var context = new Context
        {
            ["correlationId"] = correlationId,
            ["commandType"] = commandType.ToString(),
            ["arguments"] = arguments
        };

        try
        {
            var result = await _combinedPolicy.ExecuteAsync(async (ctx, ct) => 
                await ExecuteCommandAsync(arguments, commandType, correlationId, effectiveTimeout, ct), 
                context, cancellationToken);

            // Record success metrics
            _commandDurationMs.Record(result.Duration.TotalMilliseconds, 
                new KeyValuePair<string, object?>("type", commandType.ToString()),
                new KeyValuePair<string, object?>("success", "true"));
            
            if (!result.IsSuccess)
            {
                _commandsFailed.Add(1, 
                    new KeyValuePair<string, object?>("type", commandType.ToString()),
                    new KeyValuePair<string, object?>("error_type", result.ErrorType.ToString()));
            }

            return result;
        }
        catch (BrokenCircuitException)
        {
            _commandDurationMs.Record(0, 
                new KeyValuePair<string, object?>("type", commandType.ToString()),
                new KeyValuePair<string, object?>("success", "false"),
                new KeyValuePair<string, object?>("circuit_open", "true"));
            _commandsFailed.Add(1, 
                new KeyValuePair<string, object?>("type", commandType.ToString()),
                new KeyValuePair<string, object?>("error_type", "circuit_open"));
            throw;
        }
        catch (Exception ex)
        {
            _commandDurationMs.Record(0,
                new KeyValuePair<string, object?>("type", commandType.ToString()),
                new KeyValuePair<string, object?>("success", "false"));
            _commandsFailed.Add(1,
                new KeyValuePair<string, object?>("type", commandType.ToString()),
                new KeyValuePair<string, object?>("error_type", ex.GetType().Name));
            throw;
        }
        finally
        {
            _activeCommands.Add(-1);
        }
    }

    private async Task<AdbCommandResult> ExecuteCommandAsync(
        string arguments, 
        AdbCommandType commandType,
        string correlationId,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(AdbCommandRunner));

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var startTime = DateTime.UtcNow;

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

            _logger.LogDebug("[{CorrelationId}] Running ADB command: {AdbPath} {Arguments} (type: {CommandType}, timeout: {Timeout}ms)",
                correlationId, _options.AdbPath, arguments, commandType, timeout.TotalMilliseconds);

            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            var waitTask = process.WaitForExitAsync(cancellationToken);
            var timeoutTask = Task.Delay(timeout, cancellationToken);

            var completedTask = await Task.WhenAny(waitTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                // Timeout
                try { process.Kill(true); } catch { }
                
                var duration = DateTime.UtcNow - startTime;
                _logger.LogWarning("[{CorrelationId}] ADB command timed out after {Timeout}ms: {Arguments}", 
                    correlationId, timeout.TotalMilliseconds, arguments);

                return new AdbCommandResult
                {
                    ExitCode = -1,
                    Output = string.Empty,
                    ErrorOutput = $"Command timed out after {timeout.TotalMilliseconds}ms",
                    Duration = duration,
                    ErrorType = AdbErrorType.Timeout,
                    ErrorCode = "ADB_TIMEOUT",
                    CorrelationId = correlationId
                };
            }

            var output = await outputTask;
            var errorOutput = await errorTask;

            var duration2 = DateTime.UtcNow - startTime;

            var errorType = ClassifyError(process.ExitCode, errorOutput ?? string.Empty, output ?? string.Empty);
            var errorCode = GetErrorCode(process.ExitCode, errorOutput ?? string.Empty);

            var result = new AdbCommandResult
            {
                ExitCode = process.ExitCode,
                Output = output ?? string.Empty,
                ErrorOutput = errorOutput ?? string.Empty,
                Duration = duration2,
                ErrorType = errorType,
                ErrorCode = errorCode,
                CorrelationId = correlationId
            };

            if (!result.IsSuccess)
            {
                _logger.LogWarning("[{CorrelationId}] ADB command failed (exit code {ExitCode}, type: {ErrorType}): {Arguments} Error: {ErrorOutput}",
                    correlationId, result.ExitCode, errorType, arguments, result.ErrorOutput);
            }
            else
            {
                _logger.LogDebug("[{CorrelationId}] ADB command succeeded in {Duration}ms: {Arguments}", 
                    correlationId, duration2.TotalMilliseconds, arguments);
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
        
        var correlationId = Activity.Current?.GetTagItem(KkbotTags.CorrelationId)?.ToString() 
            ?? Guid.NewGuid().ToString("N")[..8];
        var commandType = ClassifyCommandType(arguments);
        var effectiveTimeout = timeout ?? GetCommandTimeout(commandType);

        _activeCommands.Add(1);
        _commandsTotal.Add(1, new KeyValuePair<string, object?>("type", commandType.ToString()));

        try
        {
            var startTime = DateTime.UtcNow;

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

            _logger.LogDebug("[{CorrelationId}] Running ADB binary command: {AdbPath} {Arguments} (type: {CommandType})",
                correlationId, _options.AdbPath, arguments, commandType);

            process.Start();

            using var memoryStream = new MemoryStream();
            var copyTask = process.StandardOutput.BaseStream.CopyToAsync(memoryStream, cancellationToken);

            var waitTask = process.WaitForExitAsync(cancellationToken);
            var timeoutTask = Task.Delay(effectiveTimeout, cancellationToken);

            var completedTask = await Task.WhenAny(waitTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                try { process.Kill(true); } catch { }
                throw new TimeoutException($"ADB binary command timed out after {effectiveTimeout}: {arguments}");
            }

            await copyTask;
            
            var errorOutput = await process.StandardError.ReadToEndAsync(cancellationToken);
            var duration = DateTime.UtcNow - startTime;

            _commandDurationMs.Record(duration.TotalMilliseconds,
                new KeyValuePair<string, object?>("type", commandType.ToString()),
                new KeyValuePair<string, object?>("success", process.ExitCode == 0 ? "true" : "false"));

            if (process.ExitCode != 0)
            {
                _commandsFailed.Add(1,
                    new KeyValuePair<string, object?>("type", commandType.ToString()),
                    new KeyValuePair<string, object?>("error_type", "binary_failed"));
                throw new InvalidOperationException($"ADB binary command failed: {errorOutput}");
            }

            _logger.LogDebug("[{CorrelationId}] ADB binary command succeeded in {Duration}ms, {Size} bytes", 
                correlationId, duration.TotalMilliseconds, memoryStream.Length);
            return memoryStream.ToArray();
        }
        finally
        {
            _activeCommands.Add(-1);
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

    /// <summary>
    /// Gets the current circuit breaker state.
    /// </summary>
    public CircuitState GetCircuitState() => _circuitBreakerPolicy.CircuitState;

    /// <summary>
    /// Manually resets the circuit breaker.
    /// </summary>
    public void ResetCircuitBreaker() => _circuitBreakerPolicy.Reset();

    private AdbCommandType ClassifyCommandType(string arguments)
    {
        var args = arguments.TrimStart('-', ' ').ToLowerInvariant();
        
        if (args.StartsWith("devices")) return AdbCommandType.DeviceList;
        if (args.StartsWith("get-state")) return AdbCommandType.DeviceState;
        if (args.Contains("screencap")) return AdbCommandType.Screencap;
        if (args.StartsWith("shell")) return AdbCommandType.Shell;
        if (args.Contains("input ")) return AdbCommandType.Input;
        if (args.StartsWith("install")) return AdbCommandType.Install;
        if (args.StartsWith("uninstall")) return AdbCommandType.Uninstall;
        if (args.StartsWith("forward")) return AdbCommandType.Forward;
        if (args.StartsWith("reverse")) return AdbCommandType.Reverse;
        if (args.StartsWith("logcat")) return AdbCommandType.Logcat;
        if (args.StartsWith("push")) return AdbCommandType.Push;
        if (args.StartsWith("pull")) return AdbCommandType.Pull;
        
        return AdbCommandType.Unknown;
    }

    private TimeSpan GetCommandTimeout(AdbCommandType commandType)
    {
        if (_options.CommandTimeoutsMs.TryGetValue(commandType, out var ms))
            return TimeSpan.FromMilliseconds(ms);
        return _options.CommandTimeout;
    }

    private AdbErrorType ClassifyError(int exitCode, string errorOutput, string output)
    {
        var error = errorOutput.ToLowerInvariant();
        var outLower = output.ToLowerInvariant();

        if (exitCode == -1) return AdbErrorType.Timeout;

        // Device offline / disconnected
        if (error.Contains("device offline") || 
            error.Contains("device not found") ||
            error.Contains("no devices/emulators found") ||
            outLower.Contains("offline"))
            return AdbErrorType.DeviceOffline;

        // Daemon not running
        if (error.Contains("daemon not running") ||
            error.Contains("could not read ok from adb server") ||
            error.Contains("connection refused"))
            return AdbErrorType.DaemonNotRunning;

        // Device not found (specific serial)
        if (error.Contains("no such device") ||
            error.Contains("device unauthorized"))
            return AdbErrorType.DeviceNotFound;

        // Permanent errors (permission, syntax, etc.)
        if (exitCode != 0)
        {
            if (error.Contains("permission denied") ||
                error.Contains("read-only file system") ||
                error.Contains("no space left") ||
                error.Contains("invalid") ||
                error.Contains("syntax") ||
                error.Contains("unknown command"))
                return AdbErrorType.Permanent;
        }

        // Transient by default for non-zero exit codes
        return exitCode != 0 ? AdbErrorType.Transient : AdbErrorType.None;
    }

    private string GetErrorCode(int exitCode, string errorOutput)
    {
        var error = errorOutput.ToLowerInvariant();
        
        if (error.Contains("device offline")) return "DEVICE_OFFLINE";
        if (error.Contains("device not found") || error.Contains("no devices")) return "DEVICE_NOT_FOUND";
        if (error.Contains("device unauthorized")) return "DEVICE_UNAUTHORIZED";
        if (error.Contains("daemon not running")) return "DAEMON_NOT_RUNNING";
        if (error.Contains("permission denied")) return "PERMISSION_DENIED";
        if (error.Contains("no space left")) return "NO_SPACE";
        if (error.Contains("timeout")) return "TIMEOUT";
        if (error.Contains("connection refused")) return "CONNECTION_REFUSED";
        if (exitCode != 0) return $"EXIT_CODE_{exitCode}";
        
        return "UNKNOWN";
    }

    private static bool IsTransientException(Exception ex)
    {
        return ex is TimeoutException or
               IOException or
               UnauthorizedAccessException
               || (ex is InvalidOperationException ioe &&
                   (ioe.Message.Contains("timed out") || ioe.Message.Contains("connection")));
    }

    private static bool IsCircuitBreakerException(Exception ex)
    {
        return ex is TimeoutException or
               IOException or
               UnauthorizedAccessException or
               InvalidOperationException;
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

/// <summary>
/// Extension for getting correlation ID from Polly context.
/// </summary>
internal static class ContextExtensions
{
    public static string? GetCorrelationId(this Context context)
    {
        if (context.TryGetValue("correlationId", out var value))
        {
            return value?.ToString();
        }
        return null;
    }
}