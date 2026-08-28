// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using KKBOT.Identity;

namespace KKBOT.Identity.Authorization;

/// <summary>
/// Requirement for permission-based authorization.
/// </summary>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }
    public string? ResourceType { get; }
    public string? ResourceId { get; }

    public PermissionRequirement(string permission, string? resourceType = null, string? resourceId = null)
    {
        Permission = permission;
        ResourceType = resourceType;
        ResourceId = resourceId;
    }
}

/// <summary>
/// Authorization handler for permission requirements.
/// Supports both global permissions and resource-specific permissions.
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly ILogger<PermissionAuthorizationHandler> _logger;
    private readonly IServiceProvider _serviceProvider;

    // Permission hierarchy: higher includes lower
    private static readonly Dictionary<string, string[]> PermissionHierarchy = new(StringComparer.OrdinalIgnoreCase)
    {
        ["*"] = ["*"],
        ["admin"] = ["*"],
        ["tenant.admin"] = ["tenant.*", "instance.*", "job.*", "plugin.*", "device.*", "ai.*", "marketplace.*", "analytics.*", "collaboration.*"],
        ["operator"] = ["instance.read", "instance.write", "job.execute", "job.read", "device.read", "device.write", "plugin.read", "ai.use", "analytics.read"],
        ["viewer"] = ["instance.read", "job.read", "device.read", "plugin.read", "ai.use", "analytics.read"]
    };

    public PermissionAuthorizationHandler(ILogger<PermissionAuthorizationHandler> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var user = context.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            _logger.LogDebug("Authorization failed: User not authenticated");
            return;
        }

        // Check if user has the required permission
        var hasPermission = await CheckPermissionAsync(user, requirement);
        
        if (hasPermission)
        {
            _logger.LogDebug("Authorization succeeded: User {UserId} has permission {Permission}", 
                GetUserId(user), requirement.Permission);
            context.Succeed(requirement);
        }
        else
        {
            _logger.LogWarning("Authorization failed: User {UserId} lacks permission {Permission} for {ResourceType}:{ResourceId}",
                GetUserId(user), requirement.Permission, requirement.ResourceType, requirement.ResourceId);
        }
    }

    private async Task<bool> CheckPermissionAsync(ClaimsPrincipal user, PermissionRequirement requirement)
    {
        var userId = GetUserId(user);
        var tenantId = GetTenantId(user);
        var roles = GetUserRoles(user);

        // 1. Check direct permission claims
        var userPermissions = user.Claims
            .Where(c => c.Type == "permission" || c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 2. Check role-based permissions
        foreach (var role in roles)
        {
            if (PermissionHierarchy.TryGetValue(role, out var rolePermissions))
            {
                foreach (var perm in rolePermissions)
                {
                    userPermissions.Add(perm);
                }
            }
        }

        // 3. Check for wildcard permissions
        if (userPermissions.Contains("*") || userPermissions.Contains("admin"))
        {
            return true;
        }

        // 4. Check exact permission match
        if (userPermissions.Contains(requirement.Permission))
        {
            return true;
        }

        // 5. Check wildcard permission match (e.g., "instance.*" matches "instance.read")
        var permissionPrefix = requirement.Permission.Split('.')[0] + ".*";
        if (userPermissions.Contains(permissionPrefix))
        {
            return true;
        }

        // 6. Resource-specific checks (tenant isolation, ownership)
        if (!string.IsNullOrEmpty(requirement.ResourceType) && !string.IsNullOrEmpty(requirement.ResourceId))
        {
            return await CheckResourcePermissionAsync(user, requirement, tenantId, userId);
        }

        // 7. Tenant-level permission check
        if (!string.IsNullOrEmpty(tenantId))
        {
            var tenantPermission = $"tenant.{tenantId}.{requirement.Permission}";
            if (userPermissions.Contains(tenantPermission))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<bool> CheckResourcePermissionAsync(
        ClaimsPrincipal user, 
        PermissionRequirement requirement, 
        string? tenantId, 
        string? userId)
    {
        // For resource-specific permissions, we can check ownership or tenant membership
        // This is where you'd integrate with your resource services
        
        switch (requirement.ResourceType!.ToLowerInvariant())
        {
            case "instance":
                return await CheckInstancePermissionAsync(user, requirement.ResourceId!, requirement.Permission, tenantId);
            
            case "job":
                return await CheckJobPermissionAsync(user, requirement.ResourceId!, requirement.Permission, tenantId);
            
            case "plugin":
                return await CheckPluginPermissionAsync(user, requirement.ResourceId!, requirement.Permission, tenantId);
            
            case "device":
                return await CheckDevicePermissionAsync(user, requirement.ResourceId!, requirement.Permission, tenantId);
            
            case "tenant":
                return await CheckTenantPermissionAsync(user, requirement.ResourceId!, requirement.Permission);
        }

        // Default: allow if user has the base permission
        return false;
    }

    private async Task<bool> CheckInstancePermissionAsync(ClaimsPrincipal user, string instanceId, string permission, string? tenantId)
    {
        // Check if user owns the instance or has tenant admin access
        if (!string.IsNullOrEmpty(tenantId))
        {
            var tenantAdminPerm = $"tenant.{tenantId}.admin";
            var userPermissions = user.Claims.Where(c => c.Type == "permission").Select(c => c.Value).ToHashSet();
            if (userPermissions.Contains(tenantAdminPerm))
                return true;
        }

        // Could check instance ownership via database
        // For now, rely on global permissions
        return false;
    }

    private async Task<bool> CheckJobPermissionAsync(ClaimsPrincipal user, string jobId, string permission, string? tenantId)
    {
        // Similar to instance - check ownership or tenant access
        return false;
    }

    private async Task<bool> CheckPluginPermissionAsync(ClaimsPrincipal user, string pluginId, string permission, string? tenantId)
    {
        // Plugin permissions are typically global or tenant-scoped
        return false;
    }

    private async Task<bool> CheckDevicePermissionAsync(ClaimsPrincipal user, string deviceId, string permission, string? tenantId)
    {
        // Device permissions are typically global
        return false;
    }

    private async Task<bool> CheckTenantPermissionAsync(ClaimsPrincipal user, string tenantId, string permission)
    {
        // Check if user is member of tenant with appropriate role
        var userTenants = user.Claims.Where(c => c.Type == "tenant").Select(c => c.Value).ToHashSet();
        if (userTenants.Contains(tenantId))
        {
            // Check tenant-specific role
            var tenantRoleClaim = user.Claims.FirstOrDefault(c => c.Type == $"tenant_role:{tenantId}");
            if (tenantRoleClaim != null && PermissionHierarchy.TryGetValue(tenantRoleClaim.Value, out var perms))
            {
                return perms.Any(p => p == permission || p == "*" || p == permission.Split('.')[0] + ".*");
            }
        }
        return false;
    }

    private static string? GetUserId(ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? user.FindFirst("sub")?.Value
            ?? user.FindFirst("user_id")?.Value;
    }

    private static string? GetTenantId(ClaimsPrincipal user)
    {
        return user.FindFirst("tenant_id")?.Value
            ?? user.FindFirst("tenant")?.Value;
    }

    private static IEnumerable<string> GetUserRoles(ClaimsPrincipal user)
    {
        return user.Claims
            .Where(c => c.Type == ClaimTypes.Role || c.Type == "role")
            .Select(c => c.Value)
            .Distinct();
    }
}

/// <summary>
/// Attribute for requiring permissions on controllers/actions.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permission, string? resourceType = null, string? resourceId = null)
    {
        var requirement = new PermissionRequirement(permission, resourceType, resourceId);
        var policyName = $"Permission:{permission}" 
            + (resourceType != null ? $":{resourceType}" : "")
            + (resourceId != null ? $":{resourceId}" : "");

        Policy = policyName;
        
        // Register the policy dynamically (will be picked up by AuthorizationOptions)
        // In practice, you'd register policies at startup
    }
}

/// <summary>
/// Extension methods for registering permission policies.
/// </summary>
public static class PermissionPolicyExtensions
{
    /// <summary>
    /// Registers standard KKBOT permission policies.
    /// </summary>
    public static AuthorizationOptions AddKkbotPermissionPolicies(this AuthorizationOptions options)
    {
        // Instance permissions
        options.AddPolicy("Permission:instance.read", policy => 
            policy.Requirements.Add(new PermissionRequirement("instance.read")));
        options.AddPolicy("Permission:instance.write", policy => 
            policy.Requirements.Add(new PermissionRequirement("instance.write")));
        options.AddPolicy("Permission:instance.execute", policy => 
            policy.Requirements.Add(new PermissionRequirement("instance.execute")));
        options.AddPolicy("Permission:instance.delete", policy => 
            policy.Requirements.Add(new PermissionRequirement("instance.delete")));

        // Job permissions
        options.AddPolicy("Permission:job.read", policy => 
            policy.Requirements.Add(new PermissionRequirement("job.read")));
        options.AddPolicy("Permission:job.write", policy => 
            policy.Requirements.Add(new PermissionRequirement("job.write")));
        options.AddPolicy("Permission:job.execute", policy => 
            policy.Requirements.Add(new PermissionRequirement("job.execute")));
        options.AddPolicy("Permission:job.cancel", policy => 
            policy.Requirements.Add(new PermissionRequirement("job.cancel")));

        // Device permissions
        options.AddPolicy("Permission:device.read", policy => 
            policy.Requirements.Add(new PermissionRequirement("device.read")));
        options.AddPolicy("Permission:device.write", policy => 
            policy.Requirements.Add(new PermissionRequirement("device.write")));
        options.AddPolicy("Permission:device.manage", policy => 
            policy.Requirements.Add(new PermissionRequirement("device.manage")));

        // Plugin permissions
        options.AddPolicy("Permission:plugin.read", policy => 
            policy.Requirements.Add(new PermissionRequirement("plugin.read")));
        options.AddPolicy("Permission:plugin.write", policy => 
            policy.Requirements.Add(new PermissionRequirement("plugin.write")));
        options.AddPolicy("Permission:plugin.install", policy => 
            policy.Requirements.Add(new PermissionRequirement("plugin.install")));
        options.AddPolicy("Permission:plugin.publish", policy => 
            policy.Requirements.Add(new PermissionRequirement("plugin.publish")));

        // AI permissions
        options.AddPolicy("Permission:ai.use", policy => 
            policy.Requirements.Add(new PermissionRequirement("ai.use")));
        options.AddPolicy("Permission:ai.train", policy => 
            policy.Requirements.Add(new PermissionRequirement("ai.train")));
        options.AddPolicy("Permission:ai.deploy", policy => 
            policy.Requirements.Add(new PermissionRequirement("ai.deploy")));

        // Marketplace permissions
        options.AddPolicy("Permission:marketplace.read", policy => 
            policy.Requirements.Add(new PermissionRequirement("marketplace.read")));
        options.AddPolicy("Permission:marketplace.write", policy => 
            policy.Requirements.Add(new PermissionRequirement("marketplace.write")));
        options.AddPolicy("Permission:marketplace.moderate", policy => 
            policy.Requirements.Add(new PermissionRequirement("marketplace.moderate")));

        // Analytics permissions
        options.AddPolicy("Permission:analytics.read", policy => 
            policy.Requirements.Add(new PermissionRequirement("analytics.read")));
        options.AddPolicy("Permission:analytics.write", policy => 
            policy.Requirements.Add(new PermissionRequirement("analytics.write")));

        // Collaboration permissions
        options.AddPolicy("Permission:collaboration.read", policy => 
            policy.Requirements.Add(new PermissionRequirement("collaboration.read")));
        options.AddPolicy("Permission:collaboration.write", policy => 
            policy.Requirements.Add(new PermissionRequirement("collaboration.write")));
        options.AddPolicy("Permission:collaboration.moderate", policy => 
            policy.Requirements.Add(new PermissionRequirement("collaboration.moderate")));

        // Tenant permissions
        options.AddPolicy("Permission:tenant.read", policy => 
            policy.Requirements.Add(new PermissionRequirement("tenant.read")));
        options.AddPolicy("Permission:tenant.write", policy => 
            policy.Requirements.Add(new PermissionRequirement("tenant.write")));
        options.AddPolicy("Permission:tenant.admin", policy => 
            policy.Requirements.Add(new PermissionRequirement("tenant.admin")));

        // Admin permissions
        options.AddPolicy("Permission:admin", policy => 
            policy.Requirements.Add(new PermissionRequirement("admin")));

        return options;
    }
}