// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

namespace KKBOT.Plugins.Abstractions;

/// <summary>
/// Overall health status of a plugin module.
/// </summary>
public enum HealthStatus
{
    Healthy,
    Degraded,
    Unhealthy
}

/// <summary>
/// Health information for a plugin module.
/// </summary>
public class ModuleHealth
{
    public string ModuleId { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public HealthStatus Status { get; set; }
    public DateTimeOffset CheckedAt { get; set; }
    public List<string> Issues { get; set; } = new();
}
