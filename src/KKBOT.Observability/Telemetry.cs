// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace KKBOT.Observability;

/// <summary>
/// Extension methods to configure Serilog and expose simple health‑check / metric services.
/// </summary>
public static class TelemetryExtensions
{
    /// <summary>
    /// Adds Serilog logger with a rolling file sink and console output.
    /// Call this early in the host builder pipeline.
    /// </summary>
    public static IHostBuilder UseKkbotTelemetry(this IHostBuilder hostBuilder)
    {
        hostBuilder.UseSerilog((ctx, services, configuration) =>
        {
            configuration
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.File("logs/kkbot-.log", rollingInterval: RollingInterval.Day);
        });
        return hostBuilder;
    }

    /// <summary>
    /// Registers a simple health‑check service that reports the application is running.
    /// </summary>
    public static IServiceCollection AddKkbotHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck("kkbot-alive", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("KKBOT is alive"));
        return services;
    }
}
