// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using KKBOT.Enterprise.Data;
using KKBOT.Enterprise.Services;
using KKBOT.Enterprise.Abstractions;

namespace KKBOT.Enterprise;

public static class EnterpriseExtensions
{
    public static IServiceCollection AddKkbotEnterprise(this IServiceCollection services, IConfiguration configuration)
    {
        // Database
        services.AddDbContext<EnterpriseDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("EnterpriseDb") 
                ?? configuration.GetConnectionString("KkbotDb") 
                ?? "DataSource=enterprise.db";
            options.UseSqlite(connectionString);
        });

        // Core Services
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IDataRetentionService, DataRetentionService>();
        services.AddScoped<IDataSubjectRequestService, DataSubjectRequestService>();
        services.AddScoped<IDataBreachService, DataBreachService>();
        services.AddScoped<IComplianceService, ComplianceService>();
        services.AddScoped<IAdvancedRbacService, AdvancedRbacService>();
        services.AddScoped<IDataGovernanceService, DataGovernanceService>();
        services.AddScoped<IRiskManagementService, RiskManagementService>();
        services.AddScoped<IIncidentResponseService, IncidentResponseService>();
        services.AddScoped<IDataSubjectRequestService, DataSubjectRequestService>();
        services.AddScoped<IDataBreachService, DataBreachService>();
        services.AddScoped<IComplianceService, ComplianceService>();
        services.AddScoped<IAdvancedRbacService, AdvancedRbacService>();
        services.AddScoped<IDataGovernanceService, DataGovernanceService>();
        services.AddScoped<IRiskManagementService, RiskManagementService>();
        services.AddScoped<IIncidentResponseService, IncidentResponseService>();

        // Identity Provider
        services.AddScoped<IIdentityProviderService, IdentityProviderService>();
        services.AddScoped<ISaml2Service, Saml2Service>();

        return services;
    }
}

public static class EnterpriseExtensions
{
    public static WebApplication UseKkbotEnterprise(this WebApplication app)
    {
        app.MapHealthChecks("/health/enterprise");
        return app;
    }
}