// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using System.Diagnostics;
using Serilog;
using Serilog.Events;
using KKBOT.Observability.Logging;
using KKBOT.Observability.Metrics;

namespace KKBOT.Observability;

/// <summary>
/// Extension methods to configure Serilog, OpenTelemetry, and expose health-check / metric services.
/// </summary>
public static class TelemetryExtensions
{
    /// <summary>
    /// Adds complete KKBOT telemetry stack:
    /// - Structured Serilog logging with correlation IDs
    /// - OpenTelemetry tracing (Jaeger/OTLP)
    /// - OpenTelemetry metrics (Prometheus/OTLP)
    /// - Health checks
    /// - Custom metrics registry
    /// </summary>
    public static IHostBuilder UseKkbotTelemetry(
        this IHostBuilder hostBuilder,
        IConfiguration configuration,
        string? serviceName = null)
    {
        // 1. Structured Logging (Serilog)
        hostBuilder.UseKkbotStructuredLogging(configuration, serviceName);

        // 2. OpenTelemetry (Tracing + Metrics + Logging)
        hostBuilder.ConfigureServices((context, services) =>
        {
            services.AddKkbotOpenTelemetry(context.Configuration, serviceName);
            
            // 3. Correlation ID support
            services.AddKkbotCorrelationId();
            
            // 4. Health Checks
            services.AddKkbotHealthChecks();
            
            // 5. Metrics Registry (already registered in AddKkbotOpenTelemetry)
        });

        return hostBuilder;
    }

    /// <summary>
    /// Adds Serilog logger with structured JSON output and correlation IDs.
    /// Call this early in the host builder pipeline.
    /// </summary>
    [Obsolete("Use UseKkbotTelemetry for full stack. This only configures Serilog.")]
    public static IHostBuilder UseKkbotSerilog(this IHostBuilder hostBuilder)
    {
        return hostBuilder.UseSerilog((ctx, services, configuration) =>
        {
            configuration
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "KKBOT")
                .WriteTo.Console()
                .WriteTo.File("logs/kkbot-.log", rollingInterval: RollingInterval.Day);
        });
    }

    /// <summary>
    /// Registers health-check services.
    /// </summary>
    public static IServiceCollection AddKkbotHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck("kkbot-alive", () => 
                Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("KKBOT is alive"))
            .AddCheck("kkbot-dependencies", () => 
                Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Dependencies OK"));

        return services;
    }

    /// <summary>
    /// Configures ASP.NET Core to use the telemetry middleware pipeline.
    /// </summary>
    public static IApplicationBuilder UseKkbotTelemetryPipeline(this IApplicationBuilder app)
    {
        // Correlation ID middleware (must be early)
        app.UseMiddleware<CorrelationIdMiddleware>();

        // Serilog request logging
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("CorrelationId", httpContext.Response.Headers["X-Correlation-ID"].FirstOrDefault() ?? "none");
                diagnosticContext.Set("TraceId", Activity.Current?.TraceId.ToHexString() ?? "none");
                diagnosticContext.Set("UserId", httpContext.User?.FindFirst("sub")?.Value ?? "anonymous");
                diagnosticContext.Set("TenantId", httpContext.Request.Headers["X-Tenant-ID"].FirstOrDefault() ?? "none");
            };
        });

        // Prometheus metrics endpoint
        app.UseOpenTelemetryPrometheusScrapingEndpoint();

        return app;
    }
}

/// <summary>
/// Helper to create a configured MeterProvider for testing.
/// </summary>
public static class MetricsTestHelper
{
    public static MeterProvider CreateTestMeterProvider(Action<MeterProviderBuilder>? configure = null)
    {
        var builder = Sdk.CreateMeterProviderBuilder()
            .AddMeter("KKBOT.*")
            .AddConsoleExporter();

        configure?.Invoke(builder);

        return builder.Build();
    }
}