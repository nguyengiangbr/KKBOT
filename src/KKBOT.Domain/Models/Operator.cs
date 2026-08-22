// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KKBOT.Domain.Enums;

namespace KKBOT.Domain.Models;

/// <summary>
/// Represents a human operator of the system.
/// </summary>
public class Operator : Entity
{
    /// <summary>
    /// Username.
    /// </summary>
    [MaxLength(100)]
    [Required]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Email address.
    /// </summary>
    [MaxLength(200)]
    public string? Email { get; set; }

    /// <summary>
    /// Display name.
    /// </summary>
    [MaxLength(200)]
    [Required]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Role of the operator.
    /// </summary>
    [Required]
    public OperatorRole Role { get; set; } = OperatorRole.Operator;

    /// <summary>
    /// Permission flags for fine-grained control.
    /// </summary>
    public Permission Permissions { get; set; } = Permission.None;

    /// <summary>
    /// Hash of the operator's password.
    /// </summary>
    [MaxLength(256)]
    public string? PasswordHash { get; set; }

    /// <summary>
    /// API token for programmatic access.
    /// </summary>
    [MaxLength(256)]
    public string? ApiToken { get; set; }

    /// <summary>
    /// Whether the operator account is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Whether the operator account is locked.
    /// </summary>
    public bool IsLocked { get; set; } = false;

    /// <summary>
    /// Whether MFA is enabled.
    /// </summary>
    public bool IsMfaEnabled { get; set; } = false;

    /// <summary>
    /// Last login timestamp.
    /// </summary>
    public DateTimeOffset? LastLoginAt { get; set; }

    /// <summary>
    /// Number of failed login attempts.
    /// </summary>
    public int FailedLoginAttempts { get; set; } = 0;

    /// <summary>
    /// Preferences (JSON).
    /// </summary>
    [Column(TypeName = "json")]
    public string? Preferences { get; set; }

    /// <summary>
    /// Navigation property for audit events triggered by this operator.
    /// </summary>
    public virtual ICollection<AuditEvent>? AuditEvents { get; set; }

    /// <summary>
    /// Checks if the operator has a specific permission.
    /// </summary>
    public bool HasPermission(Permission permission)
        => Permissions.Has(permission);

    /// <summary>
    /// Assigns the default permissions for the operator's role.
    /// </summary>
    public void AssignDefaultPermissions()
    {
        Permissions = Role.GetDefaultPermissions();
    }

    /// <summary>
    /// Grants a specific permission.
    /// </summary>
    public void GrantPermission(Permission permission)
    {
        Permissions = Permissions.Add(permission);
    }

    /// <summary>
    /// Revokes a specific permission.
    /// </summary>
    public void RevokePermission(Permission permission)
    {
        Permissions = Permissions.Remove(permission);
    }

    /// <summary>
    /// Records a successful login.
    /// </summary>
    public void RecordSuccessfulLogin()
    {
        LastLoginAt = DateTimeOffset.UtcNow;
        FailedLoginAttempts = 0;
        Touch();
    }

    /// <summary>
    /// Records a failed login attempt.
    /// </summary>
    public void RecordFailedLogin(int maxAttempts = 5)
    {
        FailedLoginAttempts++;
        if (FailedLoginAttempts >= maxAttempts)
        {
            IsLocked = true;
        }
        Touch();
    }

    /// <summary>
    /// Unlocks the operator account.
    /// </summary>
    public void Unlock()
    {
        IsLocked = false;
        FailedLoginAttempts = 0;
        Touch();
    }
}

/// <summary>
/// Represents a permission assignment for a specific entity.
/// </summary>
public class PermissionAssignment : Entity
{
    /// <summary>
    /// Foreign key to the operator.
    /// </summary>
    [Required]
    public Guid OperatorId { get; set; }

    /// <summary>
    /// Navigation property to the operator.
    /// </summary>
    [ForeignKey(nameof(OperatorId))]
    public virtual Operator Operator { get; set; } = null!;

    /// <summary>
    /// Target type (instance, profile, module, device).
    /// </summary>
    [MaxLength(50)]
    [Required]
    public string TargetType { get; set; } = string.Empty;

    /// <summary>
    /// Target ID.
    /// </summary>
    [Required]
    public Guid TargetId { get; set; }

    /// <summary>
    /// Permission flags assigned for this target.
    /// </summary>
    public Permission Permissions { get; set; } = Permission.None;

    /// <summary>
    /// Whether this is a deny assignment (deny takes precedence).
    /// </summary>
    public bool IsDeny { get; set; } = false;

    /// <summary>
    /// Expiration date (null = no expiration).
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }
}