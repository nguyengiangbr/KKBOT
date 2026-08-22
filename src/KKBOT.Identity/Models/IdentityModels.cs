// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace KKBOT.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;
    public string? TimeZone { get; set; } = "UTC";
    public string? Language { get; set; } = "en";
    
    // Tenant relationship
    public Guid? TenantId { get; set; }
    public ApplicationTenant? Tenant { get; set; }
    
    // Preferences
    public string? PreferencesJson { get; set; }
    
    public string FullName => $"{FirstName} {LastName}".Trim();
}

public sealed class ApplicationRole : IdentityRole<Guid>
{
    public string Description { get; set; } = string.Empty;
    public bool IsSystemRole { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Permission mapping
    public string? PermissionsJson { get; set; }
}

public sealed class ApplicationTenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Domain { get; set; }
    public string? LogoUrl { get; set; }
    public TenantSettings Settings { get; set; } = new();
    public TenantSubscription Subscription { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsActive { get; set; } = true;
    
    // Navigation
    public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
}

public sealed record TenantSettings(
    int MaxUsers = 100,
    int MaxInstances = 50,
    int MaxConcurrentJobs = 200,
    bool EnableAi = true,
    bool EnablePlugins = true,
    bool EnableMarketplace = true,
    bool EnableAdvancedOrchestration = true,
    Dictionary<string, string> CustomSettings = default!,
    AllowedIpRanges = default!
);

public sealed record TenantSubscription(
    SubscriptionTier Tier = SubscriptionTier.Free,
    DateTime? ExpiresAt = null,
    int MaxApiCallsPerMonth = 10000,
    long MaxStorageBytes = 1073741824, // 1GB
    Dictionary<string, object> Features = default!
);

public enum SubscriptionTier
{
    Free,
    Starter,
    Professional,
    Enterprise,
    Custom
}

public sealed record AllowedIpRanges(string[]? Ipv4 = null, string[]? Ipv6 = null);

// Permission system
public static class KKBOTPermissions
{
    // Instance permissions
    public const string Instances_View = "instances.view";
    public const string Instances_Create = "instances.create";
    public const string Instances_Edit = "instances.edit";
    public const string Instances_Delete = "instances.delete";
    public const string Instances_Start = "instances.start";
    public const string Instances_Stop = "instances.stop";
    public const string Instances_Manage = "instances.manage";
    
    // Job permissions
    public const string Jobs_View = "jobs.view";
    public const string Jobs_Create = "jobs.create";
    public const string Jobs_Cancel = "jobs.cancel";
    public const string Jobs_Delete = "jobs.delete";
    public const string Jobs_Manage = "jobs.manage";
    
    // Plugin permissions
    public const string Plugins_View = "plugins.view";
    public const string Plugins_Install = "plugins.install";
    public const string Plugins_Update = "plugins.update";
    public const string Plugins_Uninstall = "plugins.uninstall";
    public const string Plugins_Manage = "plugins.manage";
    public const string Plugins_Publish = "plugins.publish";
    
    // Marketplace permissions
    public const string Marketplace_Browse = "marketplace.browse";
    public const string Marketplace_Install = "marketplace.install";
    public const string Marketplace_Publish = "marketplace.publish";
    
    // Device permissions
    public const string Devices_View = "devices.view";
    public const string Devices_Manage = "devices.manage";
    public const string Devices_Connect = "devices.connect";
    
    // AI permissions
    public const string AI_Use = "ai.use";
    public const string AI_ManageModels = "ai.manage_models";
    
    // Settings permissions
    public const string Settings_View = "settings.view";
    public const string Settings_Edit = "settings.edit";
    public const string Settings_Security = "settings.security";
    
    // User management permissions
    public const string Users_View = "users.view";
    public const string Users_Create = "users.create";
    public const string Users_Edit = "users.edit";
    public const string Users_Delete = "users.delete";
    public const string Users_ManageRoles = "users.manage_roles";
    
    // Tenant permissions
    public const string Tenant_View = "tenant.view";
    public const string Tenant_Edit = "tenant.edit";
    public const string Tenant_ManageUsers = "tenant.manage_users";
    public const string Tenant_ManageSubscription = "tenant.manage_subscription";
    
    // Audit/Monitoring
    public const string Audit_View = "audit.view";
    public const string Metrics_View = "metrics.view";
    public const string Logs_View = "logs.view";
    
    // All permissions grouped
    public static readonly string[] All = new[]
    {
        Instances_View, Instances_Create, Instances_Edit, Instances_Delete, Instances_Start, Instances_Stop, Instances_Manage,
        Jobs_View, Jobs_Create, Jobs_Cancel, Jobs_Delete, Jobs_Manage,
        Plugins_View, Plugins_Install, Plugins_Update, Plugins_Uninstall, Plugins_Manage, Plugins_Publish,
        Marketplace_Browse, Marketplace_Install, Marketplace_Publish,
        Devices_View, Devices_Manage, Devices_Connect,
        AI_Use, AI_ManageModels,
        Settings_View, Settings_Edit, Settings_Security,
        Users_View, Users_Create, Users_Edit, Users_Delete, Users_ManageRoles,
        Tenant_View, Tenant_Edit, Tenant_ManageUsers, Tenant_ManageSubscription,
        Audit_View, Metrics_View, Logs_View
    };
    
    public static readonly Dictionary<string, string[]> RolePermissions = new()
    {
        ["SuperAdmin"] = All,
        ["TenantAdmin"] = new[]
        {
            Instances_View, Instances_Create, Instances_Edit, Instances_Delete, Instances_Start, Instances_Stop, Instances_Manage,
            Jobs_View, Jobs_Create, Jobs_Cancel, Jobs_Delete, Jobs_Manage,
            Plugins_View, Plugins_Install, Plugins_Update, Plugins_Uninstall, Plugins_Manage,
            Marketplace_Browse, Marketplace_Install,
            Devices_View, Devices_Manage, Devices_Connect,
            AI_Use,
            Settings_View, Settings_Edit,
            Users_View, Users_Create, Users_Edit, Users_Delete, Users_ManageRoles,
            Tenant_View, Tenant_Edit, Tenant_ManageUsers,
            Audit_View, Metrics_View, Logs_View
        },
        ["Operator"] = new[]
        {
            Instances_View, Instances_Start, Instances_Stop,
            Jobs_View, Jobs_Create, Jobs_Cancel,
            Plugins_View, Plugins_Install, Plugins_Update,
            Marketplace_Browse, Marketplace_Install,
            Devices_View, Devices_Connect,
            AI_Use,
            Settings_View,
            Logs_View
        },
        ["Viewer"] = new[]
        {
            Instances_View,
            Jobs_View,
            Plugins_View,
            Marketplace_Browse,
            Devices_View,
            Settings_View,
            Logs_View
        },
        ["Developer"] = new[]
        {
            Instances_View, Instances_Create, Instances_Edit, Instances_Start, Instances_Stop,
            Jobs_View, Jobs_Create, Jobs_Cancel,
            Plugins_View, Plugins_Install, Plugins_Update, Plugins_Uninstall, Plugins_Manage, Plugins_Publish,
            Marketplace_Browse, Marketplace_Install, Marketplace_Publish,
            Devices_View, Devices_Connect,
            AI_Use, AI_ManageModels,
            Settings_View,
            Audit_View, Metrics_View, Logs_View
        }
    };
}