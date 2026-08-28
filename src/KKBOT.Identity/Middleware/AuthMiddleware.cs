// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using KKBOT.Identity.Services;

namespace KKBOT.Identity.Middleware;

public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    public TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Resolve tenant from various sources
        var tenantId = await ResolveTenantIdAsync(context);
        
        if (tenantId != null)
        {
            context.Items["TenantId"] = tenantId;
            
            // Validate tenant exists and is active
            using var scope = context.RequestServices.CreateScope();
            var identityService = scope.ServiceProvider.GetRequiredService<IdentityService>();
            var tenant = await identityService.GetTenantAsync(tenantId.Value);
            
            if (tenant == null || !tenant.IsActive)
            {
                _logger.LogWarning("Tenant {TenantId} not found or inactive", tenantId);
                context.Response.StatusCode = 403;
                await context.Response.WriteAsJsonAsync(new { error = "Invalid tenant" });
                return;
            }
            
            context.Items["Tenant"] = tenant;
        }

        await _next(context);
    }

    private async Task<Guid?> ResolveTenantIdAsync(HttpContext context)
    {
        // 1. Check header
        if (context.Request.Headers.TryGetValue("X-Tenant-ID", out var headerTenantId))
        {
            if (Guid.TryParse(headerTenantId.FirstOrDefault(), out var tenantId))
                return tenantId;
        }

        // 2. Check subdomain
        var host = context.Request.Host.Host;
        if (host.Contains('.'))
        {
            var subdomain = host.Split('.')[0];
            if (!string.IsNullOrEmpty(subdomain) && subdomain != "www" && subdomain != "api")
            {
                using var scope = context.RequestServices.CreateScope();
                var identityService = scope.ServiceProvider.GetRequiredService<IdentityService>();
                var tenant = (await identityService.GetTenantsAsync())
                    .FirstOrDefault(t => t.Domain == host || t.Name.Equals(subdomain, System.StringComparison.OrdinalIgnoreCase));
                if (tenant != null)
                    return tenant.Id;
            }
        }

        // 3. Check claim (from JWT)
        var tenantClaim = context.User.FindFirst("tenant_id")?.Value;
        if (Guid.TryParse(tenantClaim, out var claimTenantId))
            return claimTenantId;

        // 4. Default tenant
        return null;
    }
}

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly ILogger<PermissionAuthorizationHandler> _logger;

    public PermissionAuthorizationHandler(ILogger<PermissionAuthorizationHandler> logger)
    {
        _logger = logger;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var user = context.User;
        
        // Check if user has the required permission directly
        var hasPermission = user.HasClaim(c => c.Type == "permission" && c.Value == requirement.Permission);
        
        if (hasPermission)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Check if user has a role that grants the permission
        var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        var userPermissions = roles
            .SelectMany(r => KKBOTPermissions.RolePermissions.GetValueOrDefault(r, System.Array.Empty<string>()))
            .Distinct()
            .ToList();

        if (userPermissions.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Check for wildcard permissions (e.g., "instances.*")
        var wildcardPermission = requirement.Permission.Split('.')[0] + ".*";
        if (userPermissions.Contains(wildcardPermission))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        _logger.LogDebug("Permission denied: {Permission} for user {User}", requirement.Permission, user.Identity?.Name);
        return Task.CompletedTask;
    }
}

public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }

    public PermissionRequirement(string permission)
    {
        Permission = permission;
    }
}

public static class PermissionPolicyBuilder
{
    public static AuthorizationPolicyBuilder RequirePermission(this AuthorizationPolicyBuilder builder, string permission)
    {
        return builder.AddRequirements(new PermissionRequirement(permission));
    }
}

// Extension for easy use in controllers
public static class HttpContextExtensions
{
    public static Guid? GetTenantId(this HttpContext context)
    {
        return context.Items["TenantId"] as Guid?;
    }

    public static ApplicationTenant? GetTenant(this HttpContext context)
    {
        return context.Items["Tenant"] as ApplicationTenant;
    }

    public static Guid GetUserId(this HttpContext context)
    {
        var userIdClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var id) ? id : Guid.Empty;
    }

    public static string[] GetUserPermissions(this HttpContext context)
    {
        return context.User.FindAll("permission").Select(c => c.Value).ToArray();
    }

    public static string[] GetUserRoles(this HttpContext context)
    {
        return context.User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToArray();
    }

    public static bool HasPermission(this HttpContext context, string permission)
    {
        var permissions = context.GetUserPermissions();
        if (permissions.Contains(permission)) return true;
        
        var roles = context.GetUserRoles();
        var rolePermissions = roles
            .SelectMany(r => KKBOTPermissions.RolePermissions.GetValueOrDefault(r, System.Array.Empty<string>()))
            .Distinct();
        
        return rolePermissions.Contains(permission) || rolePermissions.Contains(permission.Split('.')[0] + ".*");
    }
}