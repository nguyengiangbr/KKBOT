// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using KKBOT.Domain.Enums;

namespace KKBOT.Security;

public sealed class PermissionEvaluator
{
    private readonly ILogger<PermissionEvaluator> _logger;
    public PermissionEvaluator(ILogger<PermissionEvaluator> logger) => _logger = logger;
    public bool HasPermission(OperatorRole role, string requiredPermission)
    {
        // Simplified mapping – in real world this would be a lookup table.
        var rolePermissions = role switch
        {
            OperatorRole.Admin => new[] { "*" },
            OperatorRole.Operator => new[] { "DEVICE_READ", "INPUT_WRITE", "JOB_EXECUTE" },
            OperatorRole.Viewer => new[] { "DASHBOARD_VIEW" },
            _ => System.Array.Empty<string>()
        };
        var has = rolePermissions.Contains("*") || rolePermissions.Contains(requiredPermission);
        _logger.LogDebug("Permission check: role={Role}, required={Permission}, result={Result}", role, requiredPermission, has);
        return has;
    }
}

public enum OperatorRole { Admin, Operator, Viewer }