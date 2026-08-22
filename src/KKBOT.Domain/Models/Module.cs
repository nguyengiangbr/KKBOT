// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KKBOT.Domain.Models;

/// <summary>
/// Represents a game module (plugin) that provides game-specific logic.
/// </summary>
public class Module : Entity
{
    /// <summary>
    /// Unique key for the module (e.g., "genshin-impact", "honkai-star-rail").
    /// </summary>
    [MaxLength(100)]
    [Required]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Display name.
    /// </summary>
    [MaxLength(200)]
    [Required]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Description.
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Module version (semantic versioning).
    /// </summary>
    [MaxLength(50)]
    [Required]
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Minimum framework version required.
    /// </summary>
    [MaxLength(50)]
    public string? MinFrameworkVersion { get; set; }

    /// <summary>
    /// Maximum framework version supported (exclusive).
    /// </summary>
    [MaxLength(50)]
    public string? MaxFrameworkVersion { get; set; }

    /// <summary>
    /// Author of the module.
    /// </summary>
    [MaxLength(100)]
    public string? Author { get; set; }

    /// <summary>
    /// Module repository URL.
    /// </summary>
    [MaxLength(500)]
    public string? RepositoryUrl { get; set; }

    /// <summary>
    /// Module documentation URL.
    /// </summary>
    [MaxLength(500)]
    public string? DocumentationUrl { get; set; }

    /// <summary>
    /// Whether the module is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Whether the module is a built-in/core module.
    /// </summary>
    public bool IsBuiltIn { get; set; } = false;

    /// <summary>
    /// Whether the module has been validated.
    /// </summary>
    public bool IsValidated { get; set; } = false;

    /// <summary>
    /// Validation result message.
    /// </summary>
    [MaxLength(2000)]
    public string? ValidationMessage { get; set; }

    /// <summary>
    /// Path to the module assembly (relative to modules directory).
    /// </summary>
    [MaxLength(500)]
    public string? AssemblyPath { get; set; }

    /// <summary>
    /// Path to the module assets directory.
    /// </summary>
    [MaxLength(500)]
    public string? AssetsPath { get; set; }

    /// <summary>
    /// Supported game package names (for Android).
    /// </summary>
    [MaxLength(1000)]
    public string? SupportedPackages { get; set; }

    /// <summary>
    /// Supported screen resolutions (JSON array).
    /// </summary>
    [Column(TypeName = "json")]
    public string? SupportedResolutions { get; set; }

    /// <summary>
    /// Required permissions for the module.
    /// </summary>
    [Column(TypeName = "json")]
    public string? RequiredPermissions { get; set; }

    /// <summary>
    /// Module configuration schema (JSON Schema).
    /// </summary>
    [Column(TypeName = "json")]
    public string? ConfigSchema { get; set; }

    /// <summary>
    /// Default configuration.
    /// </summary>
    [Column(TypeName = "json")]
    public string? DefaultConfig { get; set; }

    /// <summary>
    /// Checksum of the module assembly for integrity verification.
    /// </summary>
    [MaxLength(128)]
    public string? AssemblyChecksum { get; set; }

    /// <summary>
    /// Tags for categorization.
    /// </summary>
    [MaxLength(500)]
    public string? Tags { get; set; }

    /// <summary>
    /// Navigation property for module versions.
    /// </summary>
    public virtual ICollection<ModuleVersion> Versions { get; set; } = new List<ModuleVersion>();

    /// <summary>
    /// Navigation property for profiles using this module.
    /// </summary>
    public virtual ICollection<Profile> Profiles { get; set; } = new List<Profile>();

    /// <summary>
    /// Navigation property for instances using this module.
    /// </summary>
    public virtual ICollection<Instance> Instances { get; set; } = new List<Instance>();

    /// <summary>
    /// Checks if the module is compatible with a framework version.
    /// </summary>
    public bool IsCompatibleWith(string frameworkVersion)
    {
        if (string.IsNullOrEmpty(MinFrameworkVersion) && string.IsNullOrEmpty(MaxFrameworkVersion))
            return true;

        var current = ParseVersion(frameworkVersion);
        var min = string.IsNullOrEmpty(MinFrameworkVersion) ? (0, 0, 0) : ParseVersion(MinFrameworkVersion!);
        var max = string.IsNullOrEmpty(MaxFrameworkVersion) ? (int.MaxValue, int.MaxValue, int.MaxValue) : ParseVersion(MaxFrameworkVersion!);

        return CompareVersion(current, min) >= 0 && CompareVersion(current, max) < 0;
    }

    /// <summary>
    /// Validates the module configuration against schema.
    /// </summary>
    public bool ValidateConfig(string config, out string? error)
    {
        error = null;
        if (string.IsNullOrEmpty(ConfigSchema))
            return true;

        // JSON Schema validation would go here
        // For now, just return true
        return true;
    }

    private static (int Major, int Minor, int Patch) ParseVersion(string version)
    {
        var parts = version.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var major = parts.Length > 0 && int.TryParse(parts[0], out var m) ? m : 0;
        var minor = parts.Length > 1 && int.TryParse(parts[1], out var n) ? n : 0;
        var patch = parts.Length > 2 && int.TryParse(parts[2], out var p) ? p : 0;
        return (major, minor, patch);
    }

    private static int CompareVersion((int Major, int Minor, int Patch) a, (int Major, int Minor, int Patch) b)
    {
        if (a.Major != b.Major) return a.Major.CompareTo(b.Major);
        if (a.Minor != b.Minor) return a.Minor.CompareTo(b.Minor);
        return a.Patch.CompareTo(b.Patch);
    }
}