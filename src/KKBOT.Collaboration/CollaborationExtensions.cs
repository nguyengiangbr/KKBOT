// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using KKBOT.Collaboration.Data;
using KKBOT.Collaboration.Services;
using KKBOT.Collaboration.Abstractions;

namespace KKBOT.Collaboration;

public static class CollaborationExtensions
{
    public static IServiceCollection AddKkbotCollaboration(this IServiceCollection services, IConfiguration configuration)
    {
        // Database
        services.AddDbContext<CollaborationDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("CollaborationDb") 
                ?? configuration.GetConnectionString("KkbotDb") 
                ?? "DataSource=collaboration.db";
            options.UseSqlite(connectionString);
        });

        // Core Services
        services.AddScoped<ICollaborationSessionService, CollaborationSessionService>();
        services.AddScoped<IParticipantService, ParticipantService>();
        services.AddScoped<IPresenceService, PresenceService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IAnnotationService, AnnotationService>();
        services.AddScoped<IRecordingService, RecordingService>();

        // SignalR
        services.AddSignalR();

        // Health Checks
        services.AddHealthChecks()
            .AddDbContextCheck<CollaborationDbContext>("collaboration-db")
            .AddCheck("collaboration-api", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Collaboration service is healthy"));

        return services;
    }
}

public static class CollaborationHubExtensions
{
    public static WebApplication UseKkbotCollaboration(this WebApplication app)
    {
        app.MapHub<KKBOT.Collaboration.Hubs.CollaborationHub>("/hubs/collaboration");
        app.MapHealthChecks("/health/collaboration");
        return app;
    }
}