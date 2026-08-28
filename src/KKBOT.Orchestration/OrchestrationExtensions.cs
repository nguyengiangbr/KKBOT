// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using KKBOT.Orchestration.DynamicDag;
using KKBOT.Orchestration.EventSourcing;
using KKBOT.Orchestration.MultiTenancy;
using KKBOT.Orchestration.Projections;
using KKBOT.Orchestration.Saga;
using KKBOT.Persistence.EfCore;
using KKBOT.Plugins.Loader;
using KKBOT.Observability;

namespace KKBOT.Orchestration;

public static class OrchestrationExtensions
{
    public static IServiceCollection AddKkbotOrchestration(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EventStoreOptions>(configuration.GetSection("EventStore"));

        services.AddSingleton<EventStore>();
        services.AddSingleton<IEventStore>(sp => sp.GetRequiredService<EventStore>());
        services.AddSingleton<DagScheduler>();
        services.AddSingleton<PluginLoader>();
        services.AddSingleton<InstanceOrchestrator>();

        services.AddSingleton<EventUpcaster>();
        services.AddSingleton<UpcastPipeline>();
        services.AddSingleton<EventVersionRegistry>();

        services.AddSingleton<InstanceStateProjection>();
        services.AddSingleton<JobExecutionProjection>();
        services.AddSingleton<RecoveryProjection>();
        services.AddSingleton<MetricsProjection>();
        services.AddSingleton<OrchestrationHealthProjection>();

        services.AddHostedService<ProjectionRegistrationService>();

        services.AddSingleton<IDynamicDagBuilder, TaskDagBuilder>();
        services.AddSingleton<IStateMachineToDagConverter, StateMachineToDagConverter>();
        services.AddSingleton<ISagaOrchestrator, SagaOrchestrator>();
        services.AddSingleton<ISagaRepository, InMemorySagaRepository>();

        services.AddDbContext<EventStoreDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("KkbotEventStore") 
                ?? configuration.GetConnectionString("KkbotDb") 
                ?? "DataSource=kkbot-events.db";
            options.UseSqlite(connectionString);
        });

        services.AddSingleton<ITenantProvider, InMemoryTenantProvider>();
        services.AddSingleton<ITenantResolver, HeaderTenantResolver>();

        return services;
    }
}

public sealed class ProjectionRegistrationService : BackgroundService
{
    private readonly IEventStore _eventStore;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ProjectionRegistrationService> _logger;

    public ProjectionRegistrationService(IEventStore eventStore, IServiceProvider serviceProvider, ILogger<ProjectionRegistrationService> logger)
    {
        _eventStore = eventStore;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await ProjectionRegistry.RegisterAllAsync(_eventStore, _serviceProvider, stoppingToken);
            _logger.LogInformation("All projections registered successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register projections");
        }
    }
}

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
        return Task.FromResult<IReadOnlyList<ISaga>>(_sagas.Values.Where(s => s.Status == status).ToList().AsReadOnly());
    }
}

public sealed class InMemoryTenantProvider : ITenantProvider
{
    private readonly Dictionary<string, TenantContext> _tenants = new();

    public InMemoryTenantProvider()
    {
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
        return Task.FromResult<IReadOnlyList<TenantContext>>(_tenants.Values.ToList().AsReadOnly());
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