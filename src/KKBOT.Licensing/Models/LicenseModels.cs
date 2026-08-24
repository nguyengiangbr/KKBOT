// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace KKBOT.Licensing.Models;

/// <summary>
/// License feature flags for module-level gating.
/// </summary>
[Flags]
public enum LicenseFeature : ulong
{
    None = 0,
    Core = 1UL << 0,           // Core automation framework
    Combat = 1UL << 1,         // Combat & Skill Combo (Module 5)
    Quest = 1UL << 2,          // Auto Quest & Party (Module 5)
    Logistics = 1UL << 3,      // Auto Heal/Buff/Refill/Repair (Module 4)
    Economy = 1UL << 4,        // Auto Vending/Trading (Module 6)
    AntiDetection = 1UL << 5,  // Humanizer, Telegram Bot (Module 7)
    ResourceOptimizer = 1UL << 6, // FPS Limiter, RAM Cleaner (Module 3)
    NetworkProxy = 1UL << 7,   // Proxy Binding, Isolation (Module 2)
    VisualScripting = 1UL << 8, // Visual Scenario Editor
    MLPlatform = 1UL << 9,     // ML Platform Features
    Sim2Real = 1UL << 10,      // Sim2Real Pipeline
    Marketplace = 1UL << 11,   // Plugin Marketplace Access
    All = ulong.MaxValue
}

/// <summary>
/// License tier definitions.
/// </summary>
public enum LicenseTier
{
    Trial = 0,
    Basic = 1,
    Pro = 2,
    Enterprise = 3
}

/// <summary>
/// License metadata and validation data.
/// </summary>
public sealed class License
{
    /// <summary>
    /// Unique license key (Base32 encoded with checksum).
    /// Format: KKBOT-XXXX-XXXX-XXXX-XXXX
    /// </summary>
    [Required]
    [StringLength(29, MinimumLength = 29)]
    [RegularExpression("^KKBOT-[A-Z2-7]{4}-[A-Z2-7]{4}-[A-Z2-7]{4}-[A-Z2-7]{4}$")]
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// License tier.
    /// </summary>
    [Required]
    public LicenseTier Tier { get; init; } = LicenseTier.Trial;

    /// <summary>
    /// Maximum concurrent seats/instances allowed.
    /// </summary>
    [Range(1, 10000)]
    public int SeatLimit { get; init; } = 1;

    /// <summary>
    /// License expiry date (UTC). Null = perpetual.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// Enabled features bitmask.
    /// </summary>
    [Required]
    public LicenseFeature Features { get; init; } = LicenseFeature.Core;

    /// <summary>
    /// Allowed hardware fingerprints (SHA256 hashes).
    /// Empty = any hardware (not recommended for production).
    /// </summary>
    public string[] AllowedHardwareIds { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Offline grace period in days.
    /// </summary>
    [Range(0, 365)]
    public int OfflineGraceDays { get; init; } = 7;

    /// <summary>
    /// License creation timestamp (UTC).
    /// </summary>
    [Required]
    public DateTimeOffset IssuedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// License issuer identifier.
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Issuer { get; init; } = "KKBOT";

    /// <summary>
    /// Ed25519 signature of the license payload.
    /// Base64 encoded.
    /// </summary>
    [Required]
    [StringLength(88)] // Base64 encoded 64-byte signature
    public string Signature { get; init; } = string.Empty;

    /// <summary>
    /// Additional metadata (custom fields).
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = new();

    /// <summary>
    /// Checks if license is currently valid (not expired).
    /// </summary>
    public bool IsExpired => ExpiresAt.HasValue && DateTimeOffset.UtcNow > ExpiresAt.Value;

    /// <summary>
    /// Checks if license has a specific feature.
    /// </summary>
    public bool HasFeature(LicenseFeature feature) => (Features & feature) == feature;

    /// <summary>
    /// Checks if license is in trial mode.
    /// </summary>
    public bool IsTrial => Tier == LicenseTier.Trial;

    /// <summary>
    /// Gets days until expiry. Negative if expired.
    /// </summary>
    public int DaysUntilExpiry => ExpiresAt.HasValue 
        ? (int)Math.Ceiling((ExpiresAt.Value - DateTimeOffset.UtcNow).TotalDays)
        : int.MaxValue;

    /// <summary>
    /// Creates a license payload for signing (excludes signature).
    /// </summary>
    public byte[] GetSigningPayload()
    {
        var payload = new
        {
            Key,
            Tier = (int)Tier,
            SeatLimit,
            ExpiresAt = ExpiresAt?.ToUnixTimeSeconds(),
            Features = (ulong)Features,
            AllowedHardwareIds,
            OfflineGraceDays,
            IssuedAt = IssuedAt.ToUnixTimeSeconds(),
            Issuer,
            Metadata
        };
        return System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(payload);
    }
}

/// <summary>
/// License validation result.
/// </summary>
public sealed class LicenseValidationResult
{
    public bool IsValid { get; init; }
    public License? License { get; init; }
    public LicenseValidationError Error { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTimeOffset ValidatedAt { get; init; } = DateTimeOffset.UtcNow;
    public TimeSpan ValidationDuration { get; init; }
    public bool IsOfflineValidation { get; init; }

    public static LicenseValidationResult Valid(License license, TimeSpan duration, bool offline = false) =>
        new() { IsValid = true, License = license, Error = LicenseValidationError.None, ValidationDuration = duration, IsOfflineValidation = offline };

    public static LicenseValidationResult Invalid(LicenseValidationError error, string message, TimeSpan duration) =>
        new() { IsValid = false, Error = error, ErrorMessage = message, ValidationDuration = duration };
}

/// <summary>
/// License validation error codes.
/// </summary>
public enum LicenseValidationError
{
    None = 0,
    InvalidSignature = 1,
    Expired = 2,
    InvalidKeyFormat = 3,
    HardwareMismatch = 4,
    SeatLimitExceeded = 5,
    FeatureNotLicensed = 6,
    OfflineGraceExpired = 7,
    Revoked = 8,
    ServerUnreachable = 9,
    InvalidKeyFormat = 10,
    ClockSkewDetected = 11,
    InvalidSignatureAlgorithm = 12
}

/// <summary>
/// License activation request.
/// </summary>
public sealed class LicenseActivationRequest
{
    [Required]
    [StringLength(29, MinimumLength = 29)]
    public string Key { get; init; } = string.Empty;

    [Required]
    public string HardwareId { get; init; } = string.Empty;

    public string? MachineName { get; init; }
    public string? OsVersion { get; init; }
    public string? ClientVersion { get; init; }
    public string[]? InstalledModules { get; init; }
}

/// <summary>
/// License activation response.
/// </summary>
public sealed class LicenseActivationResponse
{
    public bool Success { get; init; }
    public License? License { get; init; }
    public string? ErrorMessage { get; init; }
    public LicenseValidationError ErrorCode { get; init; }
    public int SeatsUsed { get; init; }
    public int SeatsRemaining { get; init; }
    public DateTimeOffset? GraceExpiresAt { get; init; }
}

/// <summary>
/// License validation request (periodic check).
/// </summary>
public sealed class LicenseValidationRequest
{
    [Required]
    [StringLength(29, MinimumLength = 29)]
    public string Key { get; init; } = string.Empty;

    [Required]
    public string HardwareId { get; init; } = string.Empty;

    public bool ForceOnlineCheck { get; init; } = false;
    public string? ClientVersion { get; init; }
}

/// <summary>
/// Hardware fingerprint data.
/// </summary>
public sealed class HardwareFingerprint
{
    public string MotherboardSerial { get; init; } = string.Empty;
    public string MacAddresses { get; init; } = string.Empty;
    public string DiskSerials { get; init; } = string.Empty;
    public string CpuId { get; init; } = string.Empty;
    public string BiosVersion { get; init; } = string.Empty;

    /// <summary>
    /// Generates stable SHA256 hash of hardware components.
    /// </summary>
    public string ComputeHash()
    {
        var components = new[] { MotherboardSerial, MacAddresses, DiskSerials, CpuId, BiosVersion }
            .Where(s => !string.IsNullOrEmpty(s))
            .OrderBy(s => s);
        
        var combined = string.Join("|", components);
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(combined));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

/// <summary>
/// License feature tier mapping.
/// </summary>
public static class LicenseFeatureMatrix
{
    public static readonly Dictionary<LicenseTier, LicenseFeature> TierFeatures = new()
    {
        [LicenseTier.Trial] = LicenseFeature.Core | LicenseFeature.Combat | LicenseFeature.Logistics,
        [LicenseTier.Basic] = LicenseFeature.Core | LicenseFeature.Combat | LicenseFeature.Logistics | LicenseFeature.Quest | LicenseFeature.ResourceOptimizer,
        [LicenseTier.Pro] = LicenseFeature.Core | LicenseFeature.Combat | LicenseFeature.Logistics | LicenseFeature.Quest 
                          | LicenseFeature.Economy | LicenseFeature.AntiDetection | LicenseFeature.ResourceOptimizer 
                          | LicenseFeature.NetworkProxy | LicenseFeature.VisualScripting,
        [LicenseTier.Enterprise] = LicenseFeature.All
    };

    public static readonly Dictionary<LicenseTier, int> TierSeatLimits = new()
    {
        [LicenseTier.Trial] = 1,
        [LicenseTier.Basic] = 5,
        [LicenseTier.Pro] = 20,
        [LicenseTier.Enterprise] = 100
    };

    public static readonly Dictionary<LicenseTier, int> TierOfflineGraceDays = new()
    {
        [LicenseTier.Trial] = 1,
        [LicenseTier.Basic] = 3,
        [LicenseTier.Pro] = 7,
        [LicenseTier.Enterprise] = 30
    };

    public static LicenseFeature GetFeatures(LicenseTier tier) => TierFeatures.GetValueOrDefault(tier, LicenseFeature.Core);
    public static int GetSeatLimit(LicenseTier tier) => TierSeatLimits.GetValueOrDefault(tier, 1);
    public static int GetOfflineGraceDays(LicenseTier tier) => TierOfflineGraceDays.GetValueOrDefault(tier, 1);
}

/// <summary>
/// Offline license cache entry.
/// </summary>
public sealed class OfflineLicenseCache
{
    public string LicenseKey { get; init; } = string.Empty;
    public byte[] EncryptedLicenseData { get; init; } = Array.Empty<byte>();
    public DateTimeOffset CachedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset GraceExpiresAt { get; init; }
    public int ValidationCount { get; init; } = 0;
    public DateTimeOffset LastValidationAt { get; init; } = DateTimeOffset.UtcNow;
    public bool IsGraceExpired => DateTimeOffset.UtcNow > GraceExpiresAt;
}

