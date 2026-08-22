// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace KKBOT.Domain.Enums;

/// <summary>
/// Represents system permissions using flags for bitwise operations.
/// </summary>
[Flags]
public enum Permission
{
    /// <summary>
    /// No permissions.
    /// </summary>
    None = 0,

    // Instance permissions
    /// <summary>
    /// Permission to view instances.
    /// </summary>
    InstanceView = 1 << 0,

    /// <summary>
    /// Permission to create instances.
    /// </summary>
    InstanceCreate = 1 << 1,

    /// <summary>
    /// Permission to update instances.
    /// </summary>
    InstanceUpdate = 1 << 2,

    /// <summary>
    /// Permission to delete instances.
    /// </summary>
    InstanceDelete = 1 << 3,

    /// <summary>
    /// Permission to start instances.
    /// </summary>
    InstanceStart = 1 << 4,

    /// <summary>
    /// Permission to stop instances.
    /// </summary>
    InstanceStop = 1 << 5,

    /// <summary>
    /// Permission to pause/resume instances.
    /// </summary>
    InstancePause = 1 << 6,

    /// <summary>
    /// Permission to assign profiles to instances.
    /// </summary>
    InstanceAssignProfile = 1 << 7,

    /// <summary>
    /// Permission to trigger recovery on instances.
    /// </summary>
    InstanceRecover = 1 << 8,

    /// <summary>
    /// Permission to manually approve blocked tasks.
    /// </summary>
    InstanceApprove = 1 << 9,

    // Job/Task permissions
    /// <summary>
    /// Permission to view jobs and tasks.
    /// </summary>
    JobView = 1 << 10,

    /// <summary>
    /// Permission to create jobs.
    /// </summary>
    JobCreate = 1 << 11,

    /// <summary>
    /// Permission to cancel jobs.
    /// </summary>
    JobCancel = 1 << 12,

    /// <summary>
    /// Permission to retry failed jobs.
    /// </summary>
    JobRetry = 1 << 13,

    // Profile permissions
    /// <summary>
    /// Permission to view profiles.
    /// </summary>
    ProfileView = 1 << 14,

    /// <summary>
    /// Permission to create profiles.
    /// </summary>
    ProfileCreate = 1 << 15,

    /// <summary>
    /// Permission to update profiles.
    /// </summary>
    ProfileUpdate = 1 << 16,

    /// <summary>
    /// Permission to delete profiles.
    /// </summary>
    ProfileDelete = 1 << 17,

    /// <summary>
    /// Permission to validate profiles.
    /// </summary>
    ProfileValidate = 1 << 18,

    // Module permissions
    /// <summary>
    /// Permission to view modules.
    /// </summary>
    ModuleView = 1 << 19,

    /// <summary>
    /// Permission to load/unload modules.
    /// </summary>
    ModuleManage = 1 << 20,

    /// <summary>
    /// Permission to update module configuration.
    /// </summary>
    ModuleConfigure = 1 << 21,

    // Device permissions
    /// <summary>
    /// Permission to view devices.
    /// </summary>
    DeviceView = 1 << 22,

    /// <summary>
    /// Permission to manage device connections.
    /// </summary>
    DeviceManage = 1 << 23,

    // System permissions
    /// <summary>
    /// Permission to view system logs.
    /// </summary>
    LogView = 1 << 24,

    /// <summary>
    /// Permission to export logs.
    /// </summary>
    LogExport = 1 << 25,

    /// <summary>
    /// Permission to view system metrics.
    /// </summary>
    MetricsView = 1 << 26,

    /// <summary>
    /// Permission to view audit logs.
    /// </summary>
    AuditView = 1 << 27,

    /// <summary>
    /// Permission to manage system settings.
    /// </summary>
    SettingsManage = 1 << 28,

    /// <summary>
    /// Permission to manage operators and roles.
    /// </summary>
    UserManage = 1 << 29,

    /// <summary>
    /// Permission to trigger emergency stop.
    /// </summary>
    EmergencyStop = 1 << 30,

    /// <summary>
    /// Full administrative access.
    /// </summary>
    Admin = InstanceView | InstanceCreate | InstanceUpdate | InstanceDelete |
            InstanceStart | InstanceStop | InstancePause | InstanceAssignProfile |
            InstanceRecover | InstanceApprove | JobView | JobCreate | JobCancel |
            JobRetry | ProfileView | ProfileCreate | ProfileUpdate | ProfileDelete |
            ProfileValidate | ModuleView | ModuleManage | ModuleConfigure |
            DeviceView | DeviceManage | LogView | LogExport | MetricsView |
            AuditView | SettingsManage | UserManage | EmergencyStop,

    /// <summary>
    /// Operator permissions (can operate but not manage system).
    /// </summary>
    Operator = InstanceView | InstanceStart | InstanceStop | InstancePause |
               InstanceAssignProfile | InstanceRecover | InstanceApprove |
               JobView | JobCreate | JobCancel | JobRetry |
               ProfileView | ProfileValidate | ModuleView | DeviceView |
               LogView | MetricsView | AuditView,

    /// <summary>
    /// Viewer permissions (read-only).
    /// </summary>
    Viewer = InstanceView | JobView | ProfileView | ModuleView |
             DeviceView | LogView | MetricsView | AuditView,

    /// <summary>
    /// Developer permissions (can manage modules and profiles).
    /// </summary>
    Developer = Operator | ProfileCreate | ProfileUpdate | ProfileDelete |
                ModuleManage | ModuleConfigure | DeviceManage | LogExport
}

/// <summary>
/// Extension methods for Permission.
/// </summary>
public static class PermissionExtensions
{
    /// <summary>
    /// Checks if a permission set contains a specific permission.
    /// </summary>
    public static bool Has(this Permission permissions, Permission permission)
        => (permissions & permission) == permission;

    /// <summary>
    /// Checks if a permission set contains any of the specified permissions.
    /// </summary>
    public static bool HasAny(this Permission permissions, Permission permission)
        => (permissions & permission) != 0;

    /// <summary>
    /// Adds a permission to the set.
    /// </summary>
    public static Permission Add(this Permission permissions, Permission permission)
        => permissions | permission;

    /// <summary>
    /// Removes a permission from the set.
    /// </summary>
    public static Permission Remove(this Permission permissions, Permission permission)
        => permissions & ~permission;

    /// <summary>
    /// Gets all individual permissions in the set.
    /// </summary>
    public static IEnumerable<Permission> GetIndividual(this Permission permissions)
    {
        foreach (Permission perm in Enum.GetValues<Permission>())
        {
            if (perm != Permission.None && permissions.Has(perm))
            {
                yield return perm;
            }
        }
    }

    /// <summary>
    /// Gets the display name for a permission.
    /// </summary>
    public static string GetDisplayName(this Permission permission)
        => permission switch
        {
            Permission.InstanceView => "View Instances",
            Permission.InstanceCreate => "Create Instances",
            Permission.InstanceUpdate => "Update Instances",
            Permission.InstanceDelete => "Delete Instances",
            Permission.InstanceStart => "Start Instances",
            Permission.InstanceStop => "Stop Instances",
            Permission.InstancePause => "Pause/Resume Instances",
            Permission.InstanceAssignProfile => "Assign Profiles",
            Permission.InstanceRecover => "Trigger Recovery",
            Permission.InstanceApprove => "Approve Tasks",
            Permission.JobView => "View Jobs",
            Permission.JobCreate => "Create Jobs",
            Permission.JobCancel => "Cancel Jobs",
            Permission.JobRetry => "Retry Jobs",
            Permission.ProfileView => "View Profiles",
            Permission.ProfileCreate => "Create Profiles",
            Permission.ProfileUpdate => "Update Profiles",
            Permission.ProfileDelete => "Delete Profiles",
            Permission.ProfileValidate => "Validate Profiles",
            Permission.ModuleView => "View Modules",
            Permission.ModuleManage => "Manage Modules",
            Permission.ModuleConfigure => "Configure Modules",
            Permission.DeviceView => "View Devices",
            Permission.DeviceManage => "Manage Devices",
            Permission.LogView => "View Logs",
            Permission.LogExport => "Export Logs",
            Permission.MetricsView => "View Metrics",
            Permission.AuditView => "View Audit Logs",
            Permission.SettingsManage => "Manage Settings",
            Permission.UserManage => "Manage Users",
            Permission.EmergencyStop => "Emergency Stop",
            Permission.Admin => "Administrator",
            Permission.Operator => "Operator",
            Permission.Viewer => "Viewer",
            Permission.Developer => "Developer",
            _ => permission.ToString()
        };
}