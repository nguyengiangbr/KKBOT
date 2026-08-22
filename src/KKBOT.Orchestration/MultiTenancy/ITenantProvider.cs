// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KKBOT.Orchestration.MultiTenancy;

public interface ITenantContext
{
    string TenantId { get; }
    string TenantName { get; }
    TenantSettings Settings { get; }
}

public sealed record TenantSettings(
    int MaxInstances = 10,
    int MaxConcurrentJobs = 20,
    bool EnableAi = true,
    bool EnablePlugins = true,
    string? CustomConfig = null,
    Dictionary<string, string> Features = default!
);

public interface ITenantProvider
{
    Task<TenantContext?> GetTenantAsync(string tenantId, CancellationToken ct);
    Task<IReadOnlyList<TenantContext>> GetAllTenantsAsync(CancellationToken ct);
    Task<TenantContext> CreateTenantAsync(string tenantId, string tenantName, TenantSettings? settings = null, CancellationToken ct = default);
    Task UpdateTenantAsync(TenantContext tenant, CancellationToken ct);
    Task DeleteTenantAsync(string tenantId, CancellationToken ct);
}

public interface ITenantResolver
{
    string? ResolveTenantId(HttpContext context);
    string? ResolveTenantId(string? explicitTenantId = null);
}

// Tenant-aware base interface
public interface ITenantAware
{
    string TenantId { get; }
}

// Extension for repositories
public interface ITenantScopedRepository<T> where T : class, ITenantAware
{
    Task<T?> GetAsync(string tenantId, Guid id, CancellationToken ct);
    Task<IReadOnlyList<T>> GetAllAsync(string tenantId, CancellationToken ct);
    Task AddAsync(string tenantId, T entity, CancellationToken ct);
    Task UpdateAsync(string tenantId, T entity, CancellationToken ct);
    Task DeleteAsync(string tenantId, Guid id, CancellationToken ct);
}