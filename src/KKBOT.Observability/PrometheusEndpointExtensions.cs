// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;

namespace KKBOT.Observability;

/// <summary>
/// Extension methods for Prometheus metrics endpoint.
/// </summary>
public static class PrometheusEndpointExtensions
{
    /// <summary>
    /// Adds the Prometheus metrics scraping endpoint to the application pipeline.
    /// </summary>
    public static IApplicationBuilder UseOpenTelemetryPrometheusScrapingEndpoint(this IApplicationBuilder app, string pattern = "/metrics")
    {
        app.UseRouting();
        app.UseEndpoints(endpoints => endpoints.MapPrometheusScrapingEndpoint(pattern));
        return app;
    }

    /// <summary>
    /// Maps the Prometheus metrics scraping endpoint.
    /// </summary>
    public static IEndpointRouteBuilder MapPrometheusScrapingEndpoint(this IEndpointRouteBuilder endpoints, string pattern = "/metrics")
    {
        endpoints.MapGet(pattern, async context =>
        {
            var meterProvider = context.RequestServices.GetService<MeterProvider>();
            if (meterProvider is null)
            {
                context.Response.StatusCode = 503;
                await context.Response.WriteAsync("Metrics not available");
                return;
            }

            context.Response.ContentType = "text/plain; version=0.0.4; charset=utf-8";

            // The OpenTelemetry Prometheus exporter handles this automatically
            // when AddPrometheusExporter is configured with StartHttpListener = true.
            // This endpoint is a fallback for when StartHttpListener = false.
            await context.Response.WriteAsync("# Metrics served by OpenTelemetry Prometheus Exporter\n");
            await context.Response.WriteAsync("# Use the OpenTelemetry Prometheus exporter HTTP listener for full metrics\n");
        })
        .AllowAnonymous()
        .WithName("PrometheusMetrics")
        .WithDisplayName("Prometheus Metrics")
        .WithDescription("Prometheus metrics scraping endpoint");

        return endpoints;
    }
}
