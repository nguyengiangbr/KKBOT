// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using KKBOT.Orchestration.DynamicDag;
using KKBOT.Orchestration.EventSourcing;
using KKBOT.Orchestration.MultiTenancy;
using KKBOT.Orchestration.Saga;
using KKBOT.Persistence.EfCore;

namespace KKBOT.Orchestration;

public static class OrchestrationExtensions
{
    public static IServiceCollection AddKkbotOrchestration(this IServiceCollection services, IConfiguration configuration)
    {
        // Core orchestration services
        services.AddSingleton<EventStore>();
        services.AddSingleton<DagScheduler>();
        services.AddSingleton<PluginLoader>();
        services.AddSingleton<InstanceOrchestrator>();

        // Advanced orchestration
        services.AddSingleton<IDynamicDagBuilder, TaskDagBuilder>();
        services.AddSingleton<IStateMachineToDagConverter, StateMachineToDagConverter>();
        services.AddSingleton<IEventStore, EventStore>();
        services.AddSingleton<ISagaOrchestrator, SagaOrchestrator>();
        services.AddSingleton<ISagaRepository, InMemorySagaRepository>();

        // Event Store DbContext
        services.AddDbContext<EventStoreDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("KkbotEventStore") 
                ?? configuration.GetConnectionString("KkbotDb") 
                ?? "DataSource=kkbot-events.db";
            options.UseSqlite(connectionString);
        });

        // Multi-tenancy
        services.AddSingleton<ITenantProvider, InMemoryTenantProvider>();
        services.AddSingleton<ITenantResolver, HeaderTenantResolver>();

        // Register tenant-aware repositories
        // services.AddScoped<ITenantScopedRepository<Instance>, EfInstanceRepository>();
        // services.AddScoped<ITenantScopedRepository<Job>, EfJobRepository>();

        return services;
    }
}

// In-memory implementations for development
public sealed class InMemorySagaRepository : ISagaRepository
{
    private readonly Dictionary<Guid, ISaga> _sagas = new();

    public Task<ISaga> GetAsync(Guid sagaId, CancellationToken ct)
    {
        _sagas.TryGetValue(sagaId, out var saga);
        return Task.FromResult(saga!);
    }

    public Task SaveAsync(ISaga saga, CancellationToken ct)
    {
        _sagas[saga.SagaId] = saga;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ISaga>> GetByStatusAsync(SagaStatus status, CancellationToken ct)
    {
        return Task.FromResult(_sagas.Values.Where(s => s.Status == status).ToList().AsReadOnly());
    }
}

public sealed class InMemoryTenantProvider : ITenantProvider
{
    private readonly Dictionary<string, TenantContext> _tenants = new();

    public InMemoryTenantProvider()
    {
        // Default tenant
        _tenants["default"] = new TenantContext
        {
            TenantId = "default",
            TenantName = "Default Tenant",
            Settings = new TenantSettings()
        };
    }

    public Task<TenantContext?> GetTenantAsync(string tenantId, CancellationToken ct)
    {
        _tenants.TryGetValue(tenantId, out var tenant);
        return Task.FromResult(tenant);
    }

    public Task<IReadOnlyList<TenantContext>> GetAllTenantsAsync(CancellationToken ct)
    {
        return Task.FromResult(_tenants.Values.ToList().AsReadOnly());
    }

    public Task<TenantContext> CreateTenantAsync(string tenantId, string tenantName, TenantSettings? settings = null, CancellationToken ct = default)
    {
        var tenant = new TenantContext
        {
            TenantId = tenantId,
            TenantName = tenantName,
            Settings = settings ?? new TenantSettings()
        };
        _tenants[tenantId] = tenant;
        return Task.FromResult(tenant);
    }

    public Task UpdateTenantAsync(TenantContext tenant, CancellationToken ct)
    {
        _tenants[tenant.TenantId] = tenant;
        return Task.CompletedTask;
    }

    public Task DeleteTenantAsync(string tenantId, CancellationToken ct)
    {
        _tenants.Remove(tenantId);
        return Task.CompletedTask;
    }
}

public sealed class HeaderTenantResolver : ITenantResolver
{
    private const string TenantHeader = "X-Tenant-ID";

    public string? ResolveTenantId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(TenantHeader, out var tenantId))
            return tenantId.FirstOrDefault();
        return null;
    }

    public string? ResolveTenantId(string? explicitTenantId = null)
    {
        return explicitTenantId;
    }
}