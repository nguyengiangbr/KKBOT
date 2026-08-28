// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using Serilog.Expressions;
using Serilog.Formatting.Compact;
using System.Diagnostics;

namespace KKBOT.Observability.Logging;

/// <summary>
/// Serilog configuration options.
/// </summary>
public sealed class SerilogOptions
{
    /// <summary>
    /// Minimum log level.
    /// </summary>
    public string MinimumLevel { get; set; } = "Information";

    /// <summary>
    /// Override levels for specific namespaces.
    /// </summary>
    public Dictionary<string, string> LevelOverrides { get; set; } = new()
    {
        ["Microsoft"] = "Warning",
        ["Microsoft.AspNetCore"] = "Warning",
        ["Microsoft.EntityFrameworkCore"] = "Warning",
        ["System.Net.Http"] = "Warning"
    };

    /// <summary>
    /// Enable console output with structured JSON.
    /// </summary>
    public bool EnableConsole { get; set; } = true;

    /// <summary>
    /// Enable file output with rolling intervals.
    /// </summary>
    public bool EnableFile { get; set; } = true;

    /// <summary>
    /// Log file path pattern.
    /// </summary>
    public string FilePath { get; set; } = "logs/kkbot-.log";

    /// <summary>
    /// Rolling interval for log files.
    /// </summary>
    public string RollingInterval { get; set; } = "Day";

    /// <summary>
    /// Retained file count limit.
    /// </summary>
    public int RetainedFileCountLimit { get; set; } = 31;

    /// <summary>
    /// Enable OpenTelemetry log export.
    /// </summary>
    public bool EnableOpenTelemetry { get; set; } = false;

    /// <summary>
    /// OTLP endpoint for logs.
    /// </summary>
    public string OtlpEndpoint { get; set; } = "http://localhost:4317";

    /// <summary>
    /// Enable structured JSON formatting.
    /// </summary>
    public bool UseJsonFormatter { get; set; } = true;
}

/// <summary>
/// Extension methods for configuring structured logging.
/// </summary>
public static class StructuredLoggingExtensions
{
    /// <summary>
    /// Adds structured Serilog logging with correlation IDs and OpenTelemetry integration.
    /// </summary>
    public static IHostBuilder UseKkbotStructuredLogging(
        this IHostBuilder hostBuilder,
        IConfiguration configuration,
        string? applicationName = null)
    {
        return hostBuilder.UseSerilog((context, services, loggerConfiguration) =>
        {
            var options = context.Configuration.GetSection("Serilog").Get<SerilogOptions>() ?? new SerilogOptions();
            var appName = applicationName ?? context.HostingEnvironment.ApplicationName ?? "KKBOT";

            // Base configuration
            loggerConfiguration
                .MinimumLevel.Is(Enum.Parse<LogEventLevel>(options.MinimumLevel, true))
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", appName)
                .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
                .Enrich.WithProperty("MachineName", System.Environment.MachineName)
                .Enrich.WithProperty("ProcessId", System.Diagnostics.Process.GetCurrentProcess().Id)
                .Enrich.WithProperty("ThreadId", System.Environment.CurrentManagedThreadId)
                .Enrich.With<CorrelationIdEnricher>()
                .Enrich.With<ActivityEnricher>();

            // Level overrides
            foreach (var overrideKvp in options.LevelOverrides)
            {
                if (Enum.TryParse<LogEventLevel>(overrideKvp.Value, true, out var level))
                {
                    loggerConfiguration.MinimumLevel.Override(overrideKvp.Key, level);
                }
            }

            // Console sink
            if (options.EnableConsole)
            {
                if (options.UseJsonFormatter)
                {
                    loggerConfiguration.WriteTo.Console(
                        formatter: new CompactJsonFormatter(),
                        restrictedToMinimumLevel: Enum.Parse<LogEventLevel>(options.MinimumLevel, true));
                }
                else
                {
                    loggerConfiguration.WriteTo.Console(
                        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj} {Properties:j}{NewLine}{Exception}",
                        restrictedToMinimumLevel: Enum.Parse<LogEventLevel>(options.MinimumLevel, true));
                }
            }

            // File sink
            if (options.EnableFile)
            {
                var rollingInterval = Enum.Parse<RollingInterval>(options.RollingInterval, true);
                
                if (options.UseJsonFormatter)
                {
                    loggerConfiguration.WriteTo.File(
                        formatter: new CompactJsonFormatter(),
                        path: options.FilePath,
                        rollingInterval: rollingInterval,
                        retainedFileCountLimit: options.RetainedFileCountLimit,
                        restrictedToMinimumLevel: Enum.Parse<LogEventLevel>(options.MinimumLevel, true),
                        buffered: true,
                        flushToDiskInterval: TimeSpan.FromSeconds(5));
                }
                else
                {
                    loggerConfiguration.WriteTo.File(
                        path: options.FilePath,
                        rollingInterval: rollingInterval,
                        retainedFileCountLimit: options.RetainedFileCountLimit,
                        restrictedToMinimumLevel: Enum.Parse<LogEventLevel>(options.MinimumLevel, true),
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj} {Properties:j}{NewLine}{Exception}",
                        buffered: true,
                        flushToDiskInterval: TimeSpan.FromSeconds(5));
                }
            }

            // OpenTelemetry log export is handled by OpenTelemetry logging integration
            // (see OpenTelemetryExtensions.AddKkbotOpenTelemetry -> AddLogging -> AddOpenTelemetry).

            // Filter out noisy health check logs
            loggerConfiguration.Filter.ByExcluding(logEvent =>
            {
                if (logEvent.Level < LogEventLevel.Warning) return false;
                
                var message = logEvent.RenderMessage();
                return message.Contains("/health") || message.Contains("/metrics") || message.Contains("/favicon.ico");
            });
        });
    }

    /// <summary>
    /// Adds correlation ID middleware and logging scope.
    /// </summary>
    public static IServiceCollection AddKkbotCorrelationId(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddTransient<ICorrelationIdProvider, CorrelationIdProvider>();
        services.AddTransient<CorrelationIdMiddleware>();
        return services;
    }
}

/// <summary>
/// Provides correlation ID for the current request/context.
/// </summary>
public interface ICorrelationIdProvider
{
    string GetCorrelationId();
    void SetCorrelationId(string correlationId);
}

/// <summary>
/// Default correlation ID provider using HttpContext or Activity.
/// </summary>
public sealed class CorrelationIdProvider : ICorrelationIdProvider
{
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private string? _fallbackCorrelationId;

    public CorrelationIdProvider(IHttpContextAccessor? httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetCorrelationId()
    {
        // Priority 1: HttpContext header
        var httpContext = _httpContextAccessor?.HttpContext;
        if (httpContext != null)
        {
            if (httpContext.Request.Headers.TryGetValue("X-Correlation-ID", out var headerValue))
            {
                return headerValue.FirstOrDefault() ?? GenerateCorrelationId();
            }

            if (httpContext.Items.TryGetValue("X-Correlation-ID", out var itemValue))
            {
                return itemValue?.ToString() ?? GenerateCorrelationId();
            }
        }

        // Priority 2: Activity.Current trace ID (W3C TraceContext)
        var activity = Activity.Current;
        if (activity != null && activity.TraceId != default)
        {
            return activity.TraceId.ToHexString();
        }

        // Priority 3: Fallback
        return _fallbackCorrelationId ??= GenerateCorrelationId();
    }

    public void SetCorrelationId(string correlationId)
    {
        _fallbackCorrelationId = correlationId;
        
        var httpContext = _httpContextAccessor?.HttpContext;
        if (httpContext != null)
        {
            httpContext.Items["X-Correlation-ID"] = correlationId;
            httpContext.Response.Headers["X-Correlation-ID"] = correlationId;
        }
    }

    private static string GenerateCorrelationId() => ActivityTraceId.CreateRandom().ToHexString();
}

/// <summary>
/// Middleware to extract and propagate correlation IDs.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ICorrelationIdProvider correlationIdProvider)
    {
        var correlationId = correlationIdProvider.GetCorrelationId();
        correlationIdProvider.SetCorrelationId(correlationId);

        // Add to response headers
        context.Response.Headers["X-Correlation-ID"] = correlationId;

        // Add to logging scope
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["TraceId"] = Activity.Current?.TraceId.ToHexString() ?? "none",
            ["SpanId"] = Activity.Current?.SpanId.ToHexString() ?? "none"
        });

        _logger.LogDebug("Request started: {Method} {Path}", context.Request.Method, context.Request.Path);

        try
        {
            await _next(context);
            
            _logger.LogDebug("Request completed: {Method} {Path} - {StatusCode}", 
                context.Request.Method, context.Request.Path, context.Response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Request failed: {Method} {Path}", context.Request.Method, context.Request.Path);
            throw;
        }
    }
}

/// <summary>
/// Enriches log events with correlation ID from provider.
/// </summary>
public sealed class CorrelationIdEnricher : ILogEventEnricher
{
    private readonly ICorrelationIdProvider _correlationIdProvider;

    public CorrelationIdEnricher() : this(new CorrelationIdProvider(null))
    {
    }

    public CorrelationIdEnricher(ICorrelationIdProvider correlationIdProvider)
    {
        _correlationIdProvider = correlationIdProvider;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var correlationId = _correlationIdProvider.GetCorrelationId();
        if (!string.IsNullOrEmpty(correlationId))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CorrelationId", correlationId));
        }
    }
}

/// <summary>
/// Enriches log events with Activity context (TraceId, SpanId).
/// </summary>
public sealed class ActivityEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var activity = Activity.Current;
        if (activity != null)
        {
            if (activity.TraceId != default)
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceId", activity.TraceId.ToHexString()));
            }
            if (activity.SpanId != default)
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SpanId", activity.SpanId.ToHexString()));
            }
            if (activity.ParentSpanId != default)
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ParentSpanId", activity.ParentSpanId.ToHexString()));
            }
            
            // Add all activity tags as properties
            foreach (var tag in activity.Tags)
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(tag.Key, tag.Value));
            }
        }
    }
}

/// <summary>
/// Extension methods for logging with structured context.
/// </summary>
public static class StructuredLoggingHelpers
{
    public static IDisposable? BeginInstanceScope(this Microsoft.Extensions.Logging.ILogger logger, Guid instanceId, string? instanceName = null)
    {
        return logger.BeginScope(new Dictionary<string, object>
        {
            [KkbotTags.InstanceId] = instanceId,
            [KkbotTags.InstanceName] = instanceName ?? ""
        });
    }

    public static IDisposable? BeginDeviceScope(this Microsoft.Extensions.Logging.ILogger logger, string deviceId, string deviceType)
    {
        return logger.BeginScope(new Dictionary<string, object>
        {
            [KkbotTags.DeviceId] = deviceId,
            [KkbotTags.DeviceType] = deviceType
        });
    }

    public static IDisposable? BeginTaskScope(this Microsoft.Extensions.Logging.ILogger logger, string taskId, string taskType, Guid jobRunId)
    {
        return logger.BeginScope(new Dictionary<string, object>
        {
            [KkbotTags.TaskId] = taskId,
            [KkbotTags.TaskType] = taskType,
            [KkbotTags.JobRunId] = jobRunId
        });
    }

    public static IDisposable? BeginPluginScope(this Microsoft.Extensions.Logging.ILogger logger, string pluginId, string moduleId)
    {
        return logger.BeginScope(new Dictionary<string, object>
        {
            [KkbotTags.PluginId] = pluginId,
            [KkbotTags.ModuleId] = moduleId
        });
    }

    public static void LogPerceptionResult(this Microsoft.Extensions.Logging.ILogger logger, string detectorId, double confidence, bool isMatch, TimeSpan latency)
    {
        logger.LogInformation("Perception result: Detector={DetectorId}, Confidence={Confidence:P2}, Match={IsMatch}, Latency={LatencyMs}ms",
            detectorId, confidence, isMatch, latency.TotalMilliseconds);
    }

    public static void LogAIInference(this Microsoft.Extensions.Logging.ILogger logger, string modelId, string modelType, int tokens, TimeSpan latency, bool success)
    {
        logger.LogInformation("AI Inference: Model={ModelId}, Type={ModelType}, Tokens={Tokens}, Latency={LatencyMs}ms, Success={Success}",
            modelId, modelType, tokens, latency.TotalMilliseconds, success);
    }

    public static void LogDeviceCapture(this Microsoft.Extensions.Logging.ILogger logger, string deviceId, int width, int height, TimeSpan latency, bool success)
    {
        logger.LogInformation("Device Capture: Device={DeviceId}, Resolution={Width}x{Height}, Latency={LatencyMs}ms, Success={Success}",
            deviceId, width, height, latency.TotalMilliseconds, success);
    }

    public static void LogRecoveryAttempt(this Microsoft.Extensions.Logging.ILogger logger, Guid instanceId, string level, int attempt, bool success, string? errorCode = null)
    {
        logger.LogWarning("Recovery Attempt: Instance={InstanceId}, Level={Level}, Attempt={Attempt}, Success={Success}, ErrorCode={ErrorCode}",
            instanceId, level, attempt, success, errorCode ?? "none");
    }
}