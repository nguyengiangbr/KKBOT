// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using KKBOT.Marketplace.Server.Models;
using KKBOT.Marketplace.Server.Services;
using KKBOT.Marketplace.Server.Hubs;

namespace KKBOT.Marketplace.Server;

public static class MarketplaceServerExtensions
{
    public static IServiceCollection AddKkbotMarketplaceServer(this IServiceCollection services, IConfiguration configuration)
    {
        // Database
        services.AddDbContext<MarketplaceDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("MarketplaceDb") 
                ?? configuration.GetConnectionString("KkbotDb") 
                ?? "DataSource=marketplace.db";
            options.UseSqlite(connectionString);
        });

        // Storage
        var storageType = configuration["Marketplace:Storage:Type"] ?? "Local";
        if (storageType.Equals("Minio", StringComparison.OrdinalIgnoreCase))
        {
            services.Configure<StorageOptions>(configuration.GetSection("Marketplace:Storage:Minio"));
            services.AddSingleton<IStorageService, MinioStorageService>();
        }
        else
        {
            services.Configure<StorageOptions>(configuration.GetSection("Marketplace:Storage:Local"));
            services.AddSingleton<IStorageService, LocalStorageService>();
        }

        // Signing
        services.Configure<SigningOptions>(configuration.GetSection("Marketplace:Signing"));
        services.AddSingleton<ISigningService, SigningService>();

        // Marketplace Services
        services.AddScoped<IMarketplaceService, MarketplaceService>();

        // SignalR
        services.AddSignalR();

        // Health Checks
        services.AddHealthChecks()
            .AddDbContextCheck<MarketplaceDbContext>("marketplace-db")
            .AddCheck("marketplace-api", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Marketplace API is healthy"));

        return services;
    }

    public static WebApplication UseKkbotMarketplaceServer(this WebApplication app)
    {
        // Map SignalR Hub
        app.MapHub<MarketplaceHub>("/hubs/marketplace");

        // Health checks
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready")
        });

        return app;
    }
}