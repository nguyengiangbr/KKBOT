// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

namespace KKBOT.Orchestration.MultiTenancy;

/// <summary>
/// Concrete tenant context used by the orchestration multi-tenancy provider.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    public string TenantId { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public TenantSettings Settings { get; set; } = new();
}
