// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace KKBOT.Observability;

/// <summary>
/// OpenTelemetry configuration options.
/// </summary>
public sealed class OpenTelemetryOptions
{
    /// <summary>
    /// Service name for telemetry.
    /// </summary>
    public string ServiceName { get; set; } = "KKBOT";

    /// <summary>
    /// Service version.
    /// </summary>
    public string ServiceVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Deployment environment (development, staging, production).
    /// </summary>
    public string Environment { get; set; } = "development";

    /// <summary>
    /// Enable Jaeger exporter.
    /// </summary>
    public bool EnableJaeger { get; set; } = true;

    /// <summary>
    /// Jaeger agent host.
    /// </summary>
    public string JaegerHost { get; set; } = "localhost";

    /// <summary>
    /// Jaeger agent port.
    /// </summary>
    public int JaegerPort { get; set; } = 6831;

    /// <summary>
    /// Enable OTLP exporter (for Tempo, Grafana Cloud, etc.).
    /// </summary>
    public bool EnableOtlp { get; set; } = false;

    /// <summary>
    /// OTLP endpoint (e.g., http://tempo:4317).
    /// </summary>
    public string OtlpEndpoint { get; set; } = "http://localhost:4317";

    /// <summary>
    /// Enable Prometheus metrics exporter.
    /// </summary>
    public bool EnablePrometheus { get; set; } = true;

    /// <summary>
    /// Prometheus scrape endpoint port.
    /// </summary>
    public int PrometheusPort { get; set; } = 9464;

    /// <summary>
    /// Enable console exporter for development.
    /// </summary>
    public bool EnableConsoleExporter { get; set; } = false;

    /// <summary>
    /// Sampling ratio (0.0 to 1.0).
    /// </summary>
    public double SamplingRatio { get; set; } = 1.0;

    /// <summary>
    /// Additional resource attributes.
    /// </summary>
    public Dictionary<string, string> ResourceAttributes { get; set; } = new();
}

/// <summary>
/// Extension methods for configuring OpenTelemetry.
/// </summary>
public static class OpenTelemetryExtensions
{
    /// <summary>
    /// Adds OpenTelemetry tracing, metrics, and logging to the application.
    /// </summary>
    public static IServiceCollection AddKkbotOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        string? serviceName = null)
    {
        var options = configuration.GetSection("OpenTelemetry").Get<OpenTelemetryOptions>() ?? new OpenTelemetryOptions();
        
        if (!string.IsNullOrEmpty(serviceName))
            options.ServiceName = serviceName;

        services.Configure<OpenTelemetryOptions>(configuration.GetSection("OpenTelemetry"));
        services.AddSingleton(options);

        // Configure OpenTelemetry SDK
        var builder = services.AddOpenTelemetry();

        // ===== TRACING =====
        builder.WithTracing(tracerProviderBuilder =>
        {
            tracerProviderBuilder
                .SetResourceBuilder(CreateResourceBuilder(options))
                .AddSource("KKBOT.*")
                .AddSource("KKBOT.Device.*")
                .AddSource("KKBOT.Perception.*")
                .AddSource("KKBOT.AI.*")
                .AddSource("KKBOT.Orchestration.*")
                .AddSource("KKBOT.Plugins.*")
                .AddSource("KKBOT.Workflow.*")
                .AddAspNetCoreInstrumentation(aspNetCoreOptions =>
                {
                    aspNetCoreOptions.RecordException = true;
                    aspNetCoreOptions.Filter = ctx => 
                    {
                        // Filter out health checks and metrics endpoints
                        var path = ctx.Request.Path.Value ?? "";
                        return !path.StartsWith("/health") && !path.StartsWith("/metrics");
                    };
                    aspNetCoreOptions.EnrichWithHttpRequest = (activity, request) =>
                    {
                        activity.SetTag("http.request.headers.x-tenant-id", request.Headers["X-Tenant-ID"].FirstOrDefault() ?? "");
                        activity.SetTag("http.request.headers.x-correlation-id", request.Headers["X-Correlation-ID"].FirstOrDefault() ?? "");
                    };
                    aspNetCoreOptions.EnrichWithHttpResponse = (activity, response) =>
                    {
                        activity.SetTag("http.response.headers.x-correlation-id", response.Headers["X-Correlation-ID"].FirstOrDefault() ?? "");
                    };
                })
                .AddHttpClientInstrumentation(httpClientOptions =>
                {
                    httpClientOptions.RecordException = true;
                    httpClientOptions.EnrichWithHttpRequestMessage = (activity, request) =>
                    {
                        activity.SetTag("http.client.request.headers.x-correlation-id", 
                            request.Headers.TryGetValues("X-Correlation-ID", out var vals) ? vals.FirstOrDefault() ?? "" : "");
                    };
                })
                .AddEntityFrameworkCoreInstrumentation(efOptions =>
                {
                    efOptions.SetDbStatementForText = true;
                    efOptions.EnrichWithIDbCommand = (activity, command) =>
                    {
                        activity.SetTag("db.operation", command.CommandText?.Split(' ')[0] ?? "");
                    };
                })
                .AddRuntimeInstrumentation()
                .SetSampler(new TraceIdRatioBasedSampler(options.SamplingRatio));

            // Exporters
            if (options.EnableJaeger)
            {
                tracerProviderBuilder.AddJaegerExporter(jaegerOptions =>
                {
                    jaegerOptions.AgentHost = options.JaegerHost;
                    jaegerOptions.AgentPort = options.JaegerPort;
                    jaegerOptions.ExportProcessorType = ExportProcessorType.Batch;
                    jaegerOptions.BatchExportProcessorOptions = new BatchExportProcessorOptions<Activity>
                    {
                        MaxQueueSize = 2048,
                        ScheduledDelayMilliseconds = 5000,
                        ExporterTimeoutMilliseconds = 30000,
                        MaxExportBatchSize = 512
                    };
                });
            }

            if (options.EnableOtlp)
            {
                tracerProviderBuilder.AddOtlpExporter(otlpOptions =>
                {
                    otlpOptions.Endpoint = new Uri(options.OtlpEndpoint);
                    otlpOptions.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                    otlpOptions.ExportProcessorType = ExportProcessorType.Batch;
                });
            }

            if (options.EnableConsoleExporter)
            {
                tracerProviderBuilder.AddConsoleExporter();
            }
        });

        // ===== METRICS =====
        builder.WithMetrics(metricsProviderBuilder =>
        {
            metricsProviderBuilder
                .SetResourceBuilder(CreateResourceBuilder(options))
                .AddMeter("KKBOT.*")
                .AddMeter("KKBOT.Device")
                .AddMeter("KKBOT.Perception")
                .AddMeter("KKBOT.AI")
                .AddMeter("KKBOT.Orchestration")
                .AddMeter("KKBOT.Plugins")
                .AddMeter("KKBOT.Workflow")
                .AddRuntimeInstrumentation()
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddPrometheusExporter(prometheusOptions =>
                {
                    prometheusOptions.ScrapeEndpointPath = "/metrics";
                    prometheusOptions.StartHttpListener = options.EnablePrometheus;
                });

            if (options.EnableConsoleExporter)
            {
                metricsProviderBuilder.AddConsoleExporter();
            }

            if (options.EnableOtlp)
            {
                metricsProviderBuilder.AddOtlpExporter(otlpOptions =>
                {
                    otlpOptions.Endpoint = new Uri(options.OtlpEndpoint);
                    otlpOptions.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                });
            }
        });

        // ===== LOGGING =====
        services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.AddOpenTelemetry(loggingOptions =>
            {
                loggingOptions.SetResourceBuilder(CreateResourceBuilder(options));
                loggingOptions.AddConsoleExporter();
                
                if (options.EnableOtlp)
                {
                    loggingOptions.AddOtlpExporter(otlpOptions =>
                    {
                        otlpOptions.Endpoint = new Uri(options.OtlpEndpoint);
                        otlpOptions.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                    });
                }

                loggingOptions.IncludeFormattedMessage = true;
                loggingOptions.IncludeScopes = true;
                loggingOptions.ParseStateValues = true;
            });
        });

        // Register custom metrics registry
        services.AddSingleton<MetricsRegistry>();
        services.AddSingleton<IMetricsRegistry>(sp => sp.GetRequiredService<MetricsRegistry>());

        return services;
    }

    /// <summary>
    /// Creates a resource builder with standard attributes.
    /// </summary>
    private static ResourceBuilder CreateResourceBuilder(OpenTelemetryOptions options)
    {
        var attributes = new Dictionary<string, object>
        {
            ["service.name"] = options.ServiceName,
            ["service.version"] = options.ServiceVersion,
            ["deployment.environment"] = options.Environment,
            ["service.namespace"] = "KKBOT",
        };

        foreach (var kvp in options.ResourceAttributes)
        {
            attributes[kvp.Key] = kvp.Value;
        }

        return ResourceBuilder.CreateDefault().AddAttributes(attributes);
    }

    /// <summary>
    /// Adds OpenTelemetry to the host builder (for Worker Services).
    /// </summary>
    public static IHostBuilder UseKkbotOpenTelemetry(this IHostBuilder hostBuilder, string? serviceName = null)
    {
        return hostBuilder.ConfigureServices((context, services) =>
        {
            services.AddKkbotOpenTelemetry(context.Configuration, serviceName);
        });
    }
}

/// <summary>
/// Activity source for KKBOT components.
/// </summary>
public static class KkbotActivitySources
{
    public static readonly ActivitySource Device = new("KKBOT.Device");
    public static readonly ActivitySource Perception = new("KKBOT.Perception");
    public static readonly ActivitySource AI = new("KKBOT.AI");
    public static readonly ActivitySource Orchestration = new("KKBOT.Orchestration");
    public static readonly ActivitySource Plugins = new("KKBOT.Plugins");
    public static readonly ActivitySource Workflow = new("KKBOT.Workflow");
    public static readonly ActivitySource Marketplace = new("KKBOT.Marketplace");
    public static readonly ActivitySource Collaboration = new("KKBOT.Collaboration");
}

/// <summary>
/// Activity tags for consistent telemetry.
/// </summary>
public static class KkbotTags
{
    public const string InstanceId = "kkbot.instance.id";
    public const string InstanceName = "kkbot.instance.name";
    public const string DeviceId = "kkbot.device.id";
    public const string DeviceType = "kkbot.device.type";
    public const string ModuleId = "kkbot.module.id";
    public const string ModuleVersion = "kkbot.module.version";
    public const string TaskId = "kkbot.task.id";
    public const string TaskType = "kkbot.task.type";
    public const string JobId = "kkbot.job.id";
    public const string JobRunId = "kkbot.jobrun.id";
    public const string PluginId = "kkbot.plugin.id";
    public const string DetectorId = "kkbot.detector.id";
    public const string DetectorConfidence = "kkbot.detector.confidence";
    public const string ModelId = "kkbot.model.id";
    public const string ModelType = "kkbot.model.type";
    public const string InferenceLatencyMs = "kkbot.inference.latency_ms";
    public const string FrameWidth = "kkbot.frame.width";
    public const string FrameHeight = "kkbot.frame.height";
    public const string CaptureLatencyMs = "kkbot.capture.latency_ms";
    public const string InputType = "kkbot.input.type";
    public const string InputLatencyMs = "kkbot.input.latency_ms";
    public const string RecoveryLevel = "kkbot.recovery.level";
    public const string RecoveryAttempt = "kkbot.recovery.attempt";
    public const string ErrorCode = "kkbot.error.code";
    public const string TenantId = "kkbot.tenant.id";
    public const string UserId = "kkbot.user.id";
    public const string CorrelationId = "kkbot.correlation.id";
}

/// <summary>
/// Helper for creating activities with standard tags.
/// </summary>
public static class ActivityExtensions
{
    public static Activity? StartActivity(this ActivitySource source, string name, ActivityKind kind = ActivityKind.Internal, 
        Dictionary<string, object?>? tags = null, ActivityContext parentContext = default)
    {
        var activity = source.StartActivity(name, kind, parentContext);
        if (activity == null) return null;

        if (tags != null)
        {
            foreach (var kvp in tags)
            {
                if (kvp.Value != null)
                    activity.SetTag(kvp.Key, kvp.Value);
            }
        }

        // Add correlation ID from current context if available
        var correlationId = Activity.Current?.GetTagItem(KkbotTags.CorrelationId)?.ToString();
        if (!string.IsEmpty(correlationId))
        {
            activity.SetTag(KkbotTags.CorrelationId, correlationId);
        }

        return activity;
    }

    public static void SetInstanceContext(this Activity activity, Guid instanceId, string? instanceName = null)
    {
        activity.SetTag(KkbotTags.InstanceId, instanceId);
        if (!string.IsNullOrEmpty(instanceName))
            activity.SetTag(KkbotTags.InstanceName, instanceName);
    }

    public static void SetDeviceContext(this Activity activity, string deviceId, string deviceType)
    {
        activity.SetTag(KkbotTags.DeviceId, deviceId);
        activity.SetTag(KkbotTags.DeviceType, deviceType);
    }

    public static void SetError(this Activity activity, string errorCode, string? errorMessage = null)
    {
        activity.SetStatus(ActivityStatusCode.Error, errorMessage);
        activity.SetTag(KkbotTags.ErrorCode, errorCode);
        if (!string.IsNullOrEmpty(errorMessage))
            activity.SetTag("error.message", errorMessage);
    }
}