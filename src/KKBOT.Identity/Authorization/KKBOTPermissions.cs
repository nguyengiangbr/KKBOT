// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace KKBOT.Identity.Authorization;

/// <summary>
/// Defines all KKBOT permissions.
/// </summary>
public static class KKBOTPermissions
{
    // Instance permissions
    public const string InstanceRead = "instance.read";
    public const string InstanceWrite = "instance.write";
    public const string InstanceExecute = "instance.execute";
    public const string InstanceDelete = "instance.delete";
    public const string InstanceManage = "instance.manage";

    // Job permissions
    public const string JobRead = "job.read";
    public const string JobWrite = "job.write";
    public const string JobExecute = "job.execute";
    public const string JobCancel = "job.cancel";
    public const string JobManage = "job.manage";

    // Device permissions
    public const string DeviceRead = "device.read";
    public const string DeviceWrite = "device.write";
    public const string DeviceManage = "device.manage";

    // Plugin permissions
    public const string PluginRead = "plugin.read";
    public const string PluginWrite = "plugin.write";
    public const string PluginInstall = "plugin.install";
    public const string PluginPublish = "plugin.publish";
    public const string PluginManage = "plugin.manage";

    // AI permissions
    public const string AIUse = "ai.use";
    public const string AITrain = "ai.train";
    public const string AIDeploy = "ai.deploy";
    public const string AIManage = "ai.manage";

    // Marketplace permissions
    public const string MarketplaceRead = "marketplace.read";
    public const string MarketplaceWrite = "marketplace.write";
    public const string MarketplaceModerate = "marketplace.moderate";
    public const string MarketplacePublish = "marketplace.publish";
    public const string MarketplaceManage = "marketplace.manage";

    // Analytics permissions
    public const string AnalyticsRead = "analytics.read";
    public const string AnalyticsWrite = "analytics.write";
    public const string AnalyticsManage = "analytics.manage";

    // Collaboration permissions
    public const string CollaborationRead = "collaboration.read";
    public const string CollaborationWrite = "collaboration.write";
    public const string CollaborationModerate = "collaboration.moderate";
    public const string CollaborationManage = "collaboration.manage";

    // Tenant permissions
    public const string TenantRead = "tenant.read";
    public const string TenantWrite = "tenant.write";
    public const string TenantAdmin = "tenant.admin";
    public const string TenantManage = "tenant.manage";

    // Admin permissions
    public const string Admin = "admin";
    public const string SystemAdmin = "system.admin";

    // Visual Scripting permissions
    public const string WorkflowRead = "workflow.read";
    public const string WorkflowWrite = "workflow.write";
    public const string WorkflowExecute = "workflow.execute";
    public const string WorkflowManage = "workflow.manage";

    // All permissions as a list
    public static readonly IReadOnlyList<string> All = new[]
    {
        InstanceRead, InstanceWrite, InstanceExecute, InstanceDelete, InstanceManage,
        JobRead, JobWrite, JobExecute, JobCancel, JobManage,
        DeviceRead, DeviceWrite, DeviceManage,
        PluginRead, PluginWrite, PluginInstall, PluginPublish, PluginManage,
        AIUse, AITrain, AIDeploy, AIManage,
        MarketplaceRead, MarketplaceWrite, MarketplaceModerate, MarketplacePublish, MarketplaceManage,
        AnalyticsRead, AnalyticsWrite, AnalyticsManage,
        CollaborationRead, CollaborationWrite, CollaborationModerate, CollaborationManage,
        TenantRead, TenantWrite, TenantAdmin, TenantManage,
        Admin, SystemAdmin,
        WorkflowRead, WorkflowWrite, WorkflowExecute, WorkflowManage
    };

    // Role to permissions mapping
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> RolePermissions = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
    {
        ["SuperAdmin"] = All,
        ["SystemAdmin"] = All,
        ["TenantAdmin"] = new[]
        {
            InstanceRead, InstanceWrite, InstanceExecute, InstanceDelete, InstanceManage,
            JobRead, JobWrite, JobExecute, JobCancel, JobManage,
            DeviceRead, DeviceWrite, DeviceManage,
            PluginRead, PluginWrite, PluginInstall, PluginManage,
            AIUse, AITrain, AIDeploy, AIManage,
            MarketplaceRead, MarketplaceWrite, MarketplaceModerate,
            AnalyticsRead, AnalyticsWrite,
            CollaborationRead, CollaborationWrite, CollaborationModerate,
            TenantRead, TenantWrite, TenantAdmin,
            WorkflowRead, WorkflowWrite, WorkflowExecute, WorkflowManage
        },
        ["Operator"] = new[]
        {
            InstanceRead, InstanceWrite, InstanceExecute,
            JobRead, JobWrite, JobExecute, JobCancel,
            DeviceRead, DeviceWrite,
            PluginRead, AIUse,
            MarketplaceRead,
            AnalyticsRead,
            CollaborationRead, CollaborationWrite,
            WorkflowRead, WorkflowWrite, WorkflowExecute
        },
        ["Developer"] = new[]
        {
            InstanceRead, InstanceWrite, InstanceExecute,
            JobRead, JobWrite, JobExecute,
            DeviceRead, DeviceWrite,
            PluginRead, PluginWrite, PluginInstall,
            AIUse, AITrain, AIDeploy,
            MarketplaceRead, MarketplaceWrite, MarketplacePublish,
            AnalyticsRead, AnalyticsWrite,
            CollaborationRead, CollaborationWrite,
            WorkflowRead, WorkflowWrite, WorkflowExecute, WorkflowManage
        },
        ["Viewer"] = new[]
        {
            InstanceRead,
            JobRead,
            DeviceRead,
            PluginRead,
            AIUse,
            MarketplaceRead,
            AnalyticsRead,
            CollaborationRead,
            WorkflowRead
        }
    };

    // Permission groups for UI
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> PermissionGroups = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
    {
        ["Instances"] = new[] { InstanceRead, InstanceWrite, InstanceExecute, InstanceDelete, InstanceManage },
        ["Jobs"] = new[] { JobRead, JobWrite, JobExecute, JobCancel, JobManage },
        ["Devices"] = new[] { DeviceRead, DeviceWrite, DeviceManage },
        ["Plugins"] = new[] { PluginRead, PluginWrite, PluginInstall, PluginPublish, PluginManage },
        ["AI"] = new[] { AIUse, AITrain, AIDeploy, AIManage },
        ["Marketplace"] = new[] { MarketplaceRead, MarketplaceWrite, MarketplaceModerate, MarketplacePublish, MarketplaceManage },
        ["Analytics"] = new[] { AnalyticsRead, AnalyticsWrite, AnalyticsManage },
        ["Collaboration"] = new[] { CollaborationRead, CollaborationWrite, CollaborationModerate, CollaborationManage },
        ["Tenants"] = new[] { TenantRead, TenantWrite, TenantAdmin, TenantManage },
        ["Admin"] = new[] { Admin, SystemAdmin },
        ["Workflows"] = new[] { WorkflowRead, WorkflowWrite, WorkflowExecute, WorkflowManage }
    };
}