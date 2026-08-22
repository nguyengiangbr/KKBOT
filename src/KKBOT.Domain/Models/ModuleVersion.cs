// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KKBOT.Domain.Models;

/// <summary>
/// Represents a version of a module for rollback/compatibility tracking.
/// </summary>
public class ModuleVersion : Entity
{
    /// <summary>
    /// Foreign key to the module.
    /// </summary>
    [Required]
    public Guid ModuleId { get; set; }

    /// <summary>
    /// Navigation property to the module.
    /// </summary>
    [ForeignKey(nameof(ModuleId))]
    public virtual Module Module { get; set; } = null!;

    /// <summary>
    /// Version string.
    /// </summary>
    [MaxLength(50)]
    [Required]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Release notes / changelog.
    /// </summary>
    [MaxLength(5000)]
    public string? ReleaseNotes { get; set; }

    /// <summary>
    /// Path to the module assembly.
    /// </summary>
    [MaxLength(500)]
    public string? AssemblyPath { get; set; }

    /// <summary>
    /// Path to the module assets.
    /// </summary>
    [MaxLength(500)]
    public string? AssetsPath { get; set; }

    /// <summary>
    /// Checksum of the assembly.
    /// </summary>
    [MaxLength(128)]
    public string? AssemblyChecksum { get; set; }

    /// <summary>
    /// Minimum framework version required.
    /// </summary>
    [MaxLength(50)]
    public string? MinFrameworkVersion { get; set; }

    /// <summary>
    /// Maximum framework version supported.
    /// </summary>
    [MaxLength(50)]
    public string? MaxFrameworkVersion { get; set; }

    /// <summary>
    /// Whether this version is currently active.
    /// </summary>
    public bool IsActive { get; set; } = false;

    /// <summary>
    /// Whether this version is deprecated.
    /// </summary>
    public bool IsDeprecated { get; set; } = false;

    /// <summary>
    /// Deprecation reason.
    /// </summary>
    [MaxLength(500)]
    public string? DeprecationReason { get; set; }

    /// <summary>
    /// Release date.
    /// </summary>
    public DateTimeOffset ReleasedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Download URL (for remote modules).
    /// </summary>
    [MaxLength(500)]
    public string? DownloadUrl { get; set; }

    /// <summary>
    /// Size in bytes.
    /// </summary>
    public long SizeBytes { get; set; }
}