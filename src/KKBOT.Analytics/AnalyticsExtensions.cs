// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ML;
using KKBOT.Analytics.Data;
using KKBOT.Analytics.Services;
using KKBOT.Analytics.Abstractions;

namespace KKBOT.Analytics;

public static class AnalyticsExtensions
{
    public static IServiceCollection AddKkbotAnalytics(this IServiceCollection services, IConfiguration configuration)
    {
        // Database
        services.AddDbContext<AnalyticsDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("AnalyticsDb") 
                ?? configuration.GetConnectionString("KkbotDb") 
                ?? "DataSource=analytics.db";
            options.UseSqlite(connectionString);
        });

        // ML.NET
        services.AddSingleton<MLContext>();

        // Core Services
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IDataSourceService, DataSourceService>();
        services.AddScoped<IMetricService, MetricService>();
        services.AddScoped<IAlertService, AlertService>();
        services.AddScoped<IAnomalyDetectionService, AnomalyDetectionService>();
        services.AddScoped<IForecastService, ForecastService>();
        services.AddScoped<IKpiService, KpiService>();
        services.AddScoped<ICohortAnalysisService, CohortAnalysisService>();
        services.AddScoped<IFunnelAnalysisService, FunnelAnalysisService>();
        services.AddScoped<IExportService, ExportService>();

        return services;
    }
}