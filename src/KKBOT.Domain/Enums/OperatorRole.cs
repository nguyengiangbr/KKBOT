// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace KKBOT.Domain.Enums;

/// <summary>
/// Represents the role of an operator in the system.
/// </summary>
public enum OperatorRole
{
    /// <summary>
    /// Viewer - read-only access to dashboard and logs.
    /// </summary>
    Viewer = 0,

    /// <summary>
    /// Operator - can start/stop instances, run jobs, approve tasks.
    /// </summary>
    Operator = 1,

    /// <summary>
    /// Developer - can manage profiles, modules, devices.
    /// </summary>
    Developer = 2,

    /// <summary>
    /// Administrator - full system access including user management.
    /// </summary>
    Administrator = 3,

    /// <summary>
    /// Auditor - read-only access to audit logs and compliance data.
    /// </summary>
    Auditor = 4
}

/// <summary>
/// Extension methods for OperatorRole.
/// </summary>
public static class OperatorRoleExtensions
{
    /// <summary>
    /// Gets the default permissions for a role.
    /// </summary>
    public static Permission GetDefaultPermissions(this OperatorRole role)
        => role switch
        {
            OperatorRole.Viewer => Permission.Viewer,
            OperatorRole.Operator => Permission.Operator,
            OperatorRole.Developer => Permission.Developer,
            OperatorRole.Administrator => Permission.Admin,
            OperatorRole.Auditor => Permission.AuditView | Permission.LogView | Permission.MetricsView | Permission.InstanceView | Permission.JobView | Permission.ProfileView,
            _ => Permission.None
        };

    /// <summary>
    /// Gets the display name for the role.
    /// </summary>
    public static string GetDisplayName(this OperatorRole role)
        => role switch
        {
            OperatorRole.Viewer => "Viewer",
            OperatorRole.Operator => "Operator",
            OperatorRole.Developer => "Developer",
            OperatorRole.Administrator => "Administrator",
            OperatorRole.Auditor => "Auditor",
            _ => role.ToString()
        };

    /// <summary>
    /// Determines if the role can manage other operators.
    /// </summary>
    public static bool CanManageUsers(this OperatorRole role)
        => role == OperatorRole.Administrator;

    /// <summary>
    /// Determines if the role can modify system settings.
    /// </summary>
    public static bool CanManageSettings(this OperatorRole role)
        => role == OperatorRole.Administrator;

    /// <summary>
    /// Determines if the role can trigger emergency stop.
    /// </summary>
    public static bool CanEmergencyStop(this OperatorRole role)
        => role is OperatorRole.Operator or OperatorRole.Developer or OperatorRole.Administrator;

    /// <summary>
    /// Determines if the role is at least the specified level.
    /// </summary>
    public static bool IsAtLeast(this OperatorRole role, OperatorRole minimum)
        => (int)role >= (int)minimum;
}