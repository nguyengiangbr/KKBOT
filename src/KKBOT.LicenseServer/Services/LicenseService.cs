// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using KKBOT.Licensing.Models;
using KKBOT.LicenseServer.Data;
using KKBOT.Observability;

namespace KKBOT.LicenseServer.Services;

/// <summary>
/// License management service.
/// </summary>
public interface ILicenseService
{
    Task<LicenseValidationResult> ValidateAsync(LicenseValidationRequest request, CancellationToken ct = default);
    Task<LicenseActivationResponse> ActivateAsync(LicenseActivationRequest request, CancellationToken ct = default);
    Task<bool> DeactivateAsync(string licenseKey, string hardwareId, string? reason, CancellationToken ct = default);
    Task<LicenseEntity?> GetLicenseAsync(string licenseKey, CancellationToken ct = default);
    Task<LicenseEntity> CreateLicenseAsync(CreateLicenseRequest request, string publisherId, CancellationToken ct = default);
    Task<LicenseEntity?> UpdateLicenseAsync(string licenseKey, UpdateLicenseRequest request, CancellationToken ct = default);
    Task<bool> RevokeLicenseAsync(string licenseKey, string reason, string revokedBy, CancellationToken ct = default);
    Task<List<LicenseEntity>> GetLicensesAsync(LicenseFilter filter, CancellationToken ct = default);
    Task<LicenseAnalytics> GetAnalyticsAsync(string licenseKey, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
    Task<bool> RecordValidationAsync(string licenseKey, string hardwareId, bool isValid, TimeSpan duration, bool isOffline, CancellationToken ct = default);
}

public sealed class LicenseService : ILicenseService
{
    private readonly LicenseDbContext _db;
    private readonly ILogger<LicenseService> _logger;
    private readonly IMetricsRegistry _metrics;
    private readonly IHubContext<LicenseHub> _hubContext;

    public LicenseService(
        LicenseDbContext db,
        ILogger<LicenseService> logger,
        IMetricsRegistry metrics,
        IHubContext<LicenseHub> hubContext)
    {
        _db = db;
        _logger = logger;
        _metrics = metrics;
        _hubContext = hubContext;
    }

    public async Task<LicenseValidationResult> ValidateAsync(LicenseValidationRequest request, CancellationToken ct = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Validate key format
            if (!LicenseKeyFormatValidator.IsValidFormat(request.Key))
            {
                return LicenseValidationResult.Invalid(
                    LicenseValidationError.InvalidKeyFormat,
                    "Invalid license key format",
                    stopwatch.Elapsed);
            }

            // Find license
            var license = await _db.Licenses
                .Include(l => l.Activations)
                .FirstOrDefaultAsync(l => l.Key == request.Key, ct);

            if (license == null)
            {
                stopwatch.Stop();
                return LicenseValidationResult.Invalid(
                    LicenseValidationError.InvalidKeyFormat,
                    "License not found",
                    stopwatch.Elapsed);
            }

            // Check if revoked
            if (license.IsRevoked)
            {
                stopwatch.Stop();
                return LicenseValidationResult.Invalid(
                    LicenseValidationError.Revoked,
                    $"License revoked: {license.RevocationReason}",
                    stopwatch.Elapsed);
            }

            // Check expiry
            if (license.ExpiresAt.HasValue && DateTimeOffset.UtcNow > license.ExpiresAt.Value)
            {
                stopwatch.Stop();
                return LicenseValidationResult.Invalid(
                    LicenseValidationError.Expired,
                    "License has expired",
                    stopwatch.Elapsed);
            }

            // Check hardware binding
            if (license.AllowedHardwareIds.Length > 0 && 
                !license.AllowedHardwareIds.Contains("*") &&
                !license.AllowedHardwareIds.Contains(request.HardwareId))
            {
                stopwatch.Stop();
                return LicenseValidationResult.Invalid(
                    LicenseValidationError.HardwareMismatch,
                    "Hardware ID does not match license",
                    stopwatch.Elapsed);
            }

            // Check seat availability
            var activeActivations = license.Activations
                .Where(a => a.IsActive && a.HardwareId != request.HardwareId)
                .Count();

            if (activeActivations >= license.SeatLimit)
            {
                stopwatch.Stop();
                return LicenseValidationResult.Invalid(
                    LicenseValidationError.SeatLimitExceeded,
                    $"Seat limit exceeded ({activeActivations}/{license.SeatLimit})",
                    stopwatch.Elapsed);
            }

            // Check feature access (will be validated by client)
            // Server validates the license itself, client checks features

            stopwatch.Stop();
            await RecordValidationAsync(license.Key, request.HardwareId, true, stopwatch.Elapsed, false);

            var licenseModel = MapToModel(license);
            return LicenseValidationResult.Valid(licenseModel, stopwatch.Elapsed, false);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "License validation failed for {Key}", request.Key);
            return LicenseValidationResult.Invalid(
                LicenseValidationError.ServerUnreachable,
                "Validation failed",
                stopwatch.Elapsed);
        }
    }

    public async Task<LicenseActivationResponse> ActivateAsync(LicenseActivationRequest request, CancellationToken ct = default)
    {
        try
        {
            var license = await _db.Licenses
                .Include(l => l.Activations)
                .FirstOrDefaultAsync(l => l.Key == request.Key, ct);

            if (license == null)
            {
                return new LicenseActivationResponse
                {
                    Success = false,
                    ErrorMessage = "License not found",
                    ErrorCode = LicenseValidationError.InvalidKeyFormat
                };
            }

            if (license.IsRevoked)
            {
                return new LicenseActivationResponse
                {
                    Success = false,
                    ErrorMessage = "License has been revoked",
                    ErrorCode = LicenseValidationError.Revoked
                };
            }

            if (license.ExpiresAt.HasValue && DateTimeOffset.UtcNow > license.ExpiresAt.Value)
            {
                return new LicenseActivationResponse
                {
                    Success = false,
                    ErrorMessage = "License has expired",
                    ErrorCode = LicenseValidationError.Expired
                };
            }

            // Check hardware binding
            if (license.AllowedHardwareIds.Length > 0 && 
                !license.AllowedHardwareIds.Contains("*") &&
                !license.AllowedHardwareIds.Contains(request.HardwareId))
            {
                return new LicenseActivationResponse
                {
                    Success = false,
                    ErrorMessage = "Hardware ID not allowed for this license",
                    ErrorCode = LicenseValidationError.HardwareMismatch
                };
            }

            // Check existing activation
            var existingActivation = license.Activations
                .FirstOrDefault(a => a.HardwareId == request.HardwareId);

            if (existingActivation != null)
            {
                // Reactivate existing
                existingActivation.IsActive = true;
                existingActivation.LastHeartbeatAt = DateTimeOffset.UtcNow;
                existingActivation.ClientVersion = request.ClientVersion;
                existingActivation.MachineName = request.MachineName;
                existingActivation.OsVersion = request.OsVersion;
                existingActivation.InstalledModules = request.InstalledModules ?? Array.Empty<string>();
                existingActivation.DeactivatedAt = null;
                existingActivation.DeactivationReason = null;

                await _db.SaveChangesAsync(ct);

                await NotifyLicenseChangedAsync(license.Key, "reactivated", request.HardwareId);

                return new LicenseActivationResponse
                {
                    Success = true,
                    License = MapToModel(license),
                    SeatsUsed = license.Activations.Count(a => a.IsActive),
                    SeatsRemaining = Math.Max(0, license.SeatLimit - license.Activations.Count(a => a.IsActive))
                };
            }

            // Check seat availability
            var activeCount = license.Activations.Count(a => a.IsActive);
            if (activeCount >= license.SeatLimit)
            {
                return new LicenseActivationResponse
                {
                    Success = false,
                    ErrorMessage = $"Seat limit exceeded ({activeCount}/{license.SeatLimit})",
                    ErrorCode = LicenseValidationError.SeatLimitExceeded
                };
            }

            // Create new activation
            var activation = new LicenseActivationEntity
            {
                LicenseId = license.Id,
                HardwareId = request.HardwareId,
                MachineName = request.MachineName,
                OsVersion = request.OsVersion,
                ClientVersion = request.ClientVersion,
                InstalledModules = request.InstalledModules ?? Array.Empty<string>(),
                IsActive = true,
                ActivatedAt = DateTimeOffset.UtcNow,
                LastHeartbeatAt = DateTimeOffset.UtcNow
            };

            license.Activations.Add(activation);
            await _db.SaveChangesAsync(ct);

            await NotifyLicenseChangedAsync(license.Key, "activated", request.HardwareId);

            // Log audit
            await LogAuditAsync("license_activated", license.Key, null, new
            {
                hardwareId = request.HardwareId,
                machineName = request.MachineName
            });

            return new LicenseActivationResponse
            {
                Success = true,
                License = MapToModel(license),
                SeatsUsed = license.Activations.Count(a => a.IsActive),
                SeatsRemaining = Math.Max(0, license.SeatLimit - license.Activations.Count(a => a.IsActive)),
                GraceExpiresAt = DateTimeOffset.UtcNow.AddDays(license.OfflineGraceDays)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "License activation failed for {Key}", request.Key);
            return new LicenseActivationResponse
            {
                Success = false,
                ErrorMessage = "Activation failed",
                ErrorCode = LicenseValidationError.ServerUnreachable
            };
        }
    }

    public async Task<bool> DeactivateAsync(string licenseKey, string hardwareId, string? reason, CancellationToken ct = default)
    {
        try
        {
            var license = await _db.Licenses
                .Include(l => l.Activations)
                .FirstOrDefaultAsync(l => l.Key == licenseKey, ct);

            if (license == null) return false;

            var activation = license.Activations
                .FirstOrDefault(a => a.HardwareId == hardwareId && a.IsActive);

            if (activation == null) return false;

            activation.IsActive = false;
            activation.DeactivatedAt = DateTimeOffset.UtcNow;
            activation.DeactivationReason = reason ?? "User requested deactivation";

            await _db.SaveChangesAsync(ct);

            await NotifyLicenseChangedAsync(license.Key, "deactivated", hardwareId);

            await LogAuditAsync("license_deactivated", licenseKey, null, new
            {
                hardwareId,
                reason
            });

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "License deactivation failed for {Key}", licenseKey);
            return false;
        }
    }

    public async Task<LicenseEntity?> GetLicenseAsync(string licenseKey, CancellationToken ct = default)
    {
        return await _db.Licenses
            .Include(l => l.Activations)
            .Include(l => l.Analytics)
            .FirstOrDefaultAsync(l => l.Key == licenseKey, ct);
    }

    public async Task<LicenseEntity> CreateLicenseAsync(CreateLicenseRequest request, string publisherId, CancellationToken ct = default)
    {
        var key = LicenseKeyGenerator.GenerateKey();
        var features = LicenseFeatureMatrix.GetFeatures(request.Tier);
        var seatLimit = LicenseFeatureMatrix.GetSeatLimit(request.Tier);
        var offlineGrace = LicenseFeatureMatrix.GetOfflineGraceDays(request.Tier);

        var license = new LicenseEntity
        {
            Key = key,
            Name = request.Name,
            Description = request.Description,
            Tier = request.Tier,
            SeatLimit = seatLimit,
            Features = features,
            OfflineGraceDays = offlineGrace,
            ExpiresAt = request.ExpiresAt,
            PublisherId = publisherId,
            Metadata = request.Metadata ?? new Dictionary<string, string>()
        };

        _db.Licenses.Add(license);
        await _db.SaveChangesAsync(ct);

        await LogAuditAsync("license_created", license.Key, publisherId, new
        {
            tier = request.Tier,
            seatLimit = request.SeatLimit
        });

        return license;
    }

    public async Task<LicenseEntity?> UpdateLicenseAsync(string licenseKey, UpdateLicenseRequest request, CancellationToken ct = default)
    {
        var license = await _db.Licenses.FirstOrDefaultAsync(l => l.Key == licenseKey, ct);
        if (license == null) return null;

        var changes = new Dictionary<string, object>();

        if (request.Name != null && request.Name != license.Name)
        {
            changes["name"] = new { old = license.Name, new = request.Name };
            license.Name = request.Name;
        }

        if (request.Description != null)
        {
            license.Description = request.Description;
        }

        if (request.Tier.HasValue && request.Tier.Value != license.Tier)
        {
            changes["tier"] = new { old = license.Tier, new = request.Tier.Value };
            license.Tier = request.Tier.Value;
            license.Features = LicenseFeatureMatrix.GetFeatures(request.Tier.Value);
            license.SeatLimit = LicenseFeatureMatrix.GetSeatLimit(request.Tier.Value);
            license.OfflineGraceDays = LicenseFeatureMatrix.GetOfflineGraceDays(request.Tier.Value);
        }

        if (request.SeatLimit.HasValue && request.SeatLimit.Value != license.SeatLimit)
        {
            changes["seatLimit"] = new { old = license.SeatLimit, new = request.SeatLimit.Value };
            license.SeatLimit = request.SeatLimit.Value;
        }

        if (request.ExpiresAt.HasValue)
        {
            changes["expiresAt"] = new { old = license.ExpiresAt, new = request.ExpiresAt };
            license.ExpiresAt = request.ExpiresAt;
        }

        if (request.Metadata != null)
        {
            license.Metadata = request.Metadata;
        }

        if (request.IsRevoked.HasValue && request.IsRevoked.Value != license.IsRevoked)
        {
            changes["revoked"] = new { old = license.IsRevoked, new = request.IsRevoked.Value };
            license.IsRevoked = request.IsRevoked.Value;
            license.RevocationReason = request.RevocationReason ?? string.Empty;
        }

        license.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        await LogAuditAsync("license_updated", license.Key, null, changes);

        return license;
    }

    public async Task<bool> RevokeLicenseAsync(string licenseKey, string reason, string revokedBy, CancellationToken ct = default)
    {
        var license = await _db.Licenses.FirstOrDefaultAsync(l => l.Key == licenseKey, ct);
        if (license == null) return false;

        license.IsRevoked = true;
        license.RevocationReason = reason;
        license.UpdatedAt = DateTimeOffset.UtcNow;

        // Deactivate all active activations
        var activations = await _db.Activations
            .Where(a => a.LicenseId == license.Id && a.IsActive)
            .ToListAsync(ct);

        foreach (var activation in activations)
        {
            activation.IsActive = false;
            activation.DeactivatedAt = DateTimeOffset.UtcNow;
            activation.DeactivationReason = $"License revoked: {reason}";
        }

        await _db.SaveChangesAsync(ct);

        await NotifyLicenseChangedAsync(license.Key, "revoked", null);

        await LogAuditAsync("license_revoked", license.Key, revokedBy, new
        {
            reason,
            revokedBy
        });

        return true;
    }

    public async Task<List<LicenseEntity>> GetLicensesAsync(LicenseFilter filter, CancellationToken ct = default)
    {
        var query = _db.Licenses
            .Include(l => l.Activations)
            .Include(l => l.Publisher)
            .AsQueryable();

        if (!string.IsNullOrEmpty(filter.PublisherId))
        {
            query = query.Where(l => l.PublisherId == filter.PublisherId);
        }

        if (filter.Tier.HasValue)
        {
            query = query.Where(l => l.Tier == filter.Tier.Value);
        }

        if (!string.IsNullOrEmpty(filter.Search))
        {
            var search = filter.Search.ToLower();
            query = query.Where(l => l.Name.ToLower().Contains(search) || 
                                     l.Key.ToLower().Contains(search) ||
                                     l.Description.ToLower().Contains(search));
        }

        if (filter.IsRevoked.HasValue)
        {
            query = query.Where(l => l.IsRevoked == filter.IsRevoked.Value);
        }

        if (filter.IsExpired.HasValue)
        {
            if (filter.IsExpired.Value)
            {
                query = query.Where(l => l.ExpiresAt.HasValue && l.ExpiresAt.Value < DateTimeOffset.UtcNow);
            }
            else
            {
                query = query.Where(l => !l.ExpiresAt.HasValue || l.ExpiresAt.Value > DateTimeOffset.UtcNow);
            }
        }

        return await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip(filter.Offset)
            .Take(filter.Limit)
            .ToListAsync(ct);
    }

    public async Task<LicenseAnalytics> GetAnalyticsAsync(string licenseKey, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var analytics = await _db.Analytics
            .Where(a => a.LicenseId == _db.Licenses
                .Where(l => l.Key == licenseKey)
                .Select(l => l.Id)
                .FirstOrDefault())
            .Where(a => a.Date >= from && a.Date <= to)
            .ToListAsync(ct);

        var featureUsage = analytics
            .SelectMany(a => a.FeatureUsage)
            .GroupBy(kvp => kvp.Key)
            .ToDictionary(g => g.Key, g => g.Sum(kvp => kvp.Value));

        return new LicenseAnalytics
        {
            LicenseId = licenseKey,
            TotalActivations = analytics.Sum(a => a.ActivationsCount),
            TotalDeactivations = analytics.Sum(a => a.DeactivationsCount),
            TotalValidations = analytics.Sum(a => a.ValidationCount),
            UniqueHardwareIds = analytics.Max(a => a.UniqueHardwareIds),
            FeatureUsage = featureUsage,
            DailyStats = analytics.Select(a => new DailyLicenseStat
            {
                Date = a.Date,
                Activations = a.ActivationsCount,
                Deactivations = a.DeactivationsCount,
                Validations = a.ValidationCount,
                UniqueUsers = a.UniqueHardwareIds,
                FeatureUsage = a.FeatureUsage
            }).ToList()
        };
    }

    public async Task<bool> RecordValidationAsync(string licenseKey, string hardwareId, bool isValid, TimeSpan duration, bool isOffline, CancellationToken ct = default)
    {
        try
        {
            var licenseId = await _db.Licenses
                .Where(l => l.Key == licenseKey)
                .Select(l => l.Id)
                .FirstOrDefaultAsync(ct);

            if (licenseId == Guid.Empty) return false;

            var today = DateTimeOffset.UtcNow.Date;
            var analytics = await _db.Analytics
                .FirstOrDefaultAsync(a => a.LicenseId == licenseId && a.Date == today, ct);

            if (analytics == null)
            {
                analytics = new LicenseAnalyticsEntity
                {
                    LicenseId = licenseId,
                    Date = today
                };
                _db.Analytics.Add(analytics);
            }

            analytics.ValidationCount++;
            if (isValid)
            {
                // Track unique hardware IDs (simplified)
            }

            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record validation for {Key}", licenseKey);
            return false;
        }
    }

    // Helper methods
    private License MapToModel(LicenseEntity entity)
    {
        return new License
        {
            Key = entity.Key,
            Name = entity.Name,
            Description = entity.Description,
            Tier = entity.Tier,
            SeatLimit = entity.SeatLimit,
            Features = entity.Features,
            AllowedHardwareIds = entity.AllowedHardwareIds,
            OfflineGraceDays = entity.OfflineGraceDays,
            ExpiresAt = entity.ExpiresAt,
            IssuedAt = entity.IssuedAt,
            Issuer = entity.Issuer,
            Metadata = entity.Metadata,
            Signature = string.Empty // Not stored in DB
        };
    }

    private async Task LogAuditAsync(string action, string licenseKey, string? userId, object details, CancellationToken ct = default)
    {
        var audit = new AuditLogEntity
        {
            Action = action,
            LicenseId = licenseKey,
            UserId = userId,
            Details = JsonSerializer.Serialize(details),
            CorrelationId = Guid.NewGuid().ToString("N")
        };

        _db.AuditLogs.Add(audit);
        await _db.SaveChangesAsync(ct);
    }

    private async Task NotifyLicenseChangedAsync(string licenseKey, string action, string? hardwareId)
    {
        try
        {
            await _hubContext.Clients.Group($"license:{licenseKey}")
                .SendAsync("LicenseChanged", new { licenseKey, action, hardwareId, timestamp = DateTimeOffset.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify license change");
        }
    }
}

public sealed class CreateLicenseRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public LicenseTier Tier { get; set; } = LicenseTier.Basic;
    public int? SeatLimit { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public sealed class UpdateLicenseRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public LicenseTier? Tier { get; set; }
    public int? SeatLimit { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
    public bool? IsRevoked { get; set; }
    public string? RevocationReason { get; set; }
}

public sealed class LicenseFilter
{
    public string? PublisherId { get; set; }
    public LicenseTier? Tier { get; set; }
    public string? Search { get; set; }
    public bool? IsRevoked { get; set; }
    public bool? IsExpired { get; set; }
    public int Offset { get; set; } = 0;
    public int Limit { get; set; } = 20;
}

public sealed class LicenseAnalytics
{
    public string LicenseId { get; set; } = string.Empty;
    public long TotalActivations { get; set; }
    public long TotalDeactivations { get; set; }
    public long TotalValidations { get; set; }
    public int UniqueHardwareIds { get; set; }
    public Dictionary<string, long> FeatureUsage { get; set; } = new();
    public List<DailyLicenseStat> DailyStats { get; set; } = new();
}

public sealed class DailyLicenseStat
{
    public DateTimeOffset Date { get; set; }
    public int Activations { get; set; }
    public int Deactivations { get; set; }
    public long Validations { get; set; }
    public int UniqueUsers { get; set; }
    public Dictionary<string, long> FeatureUsage { get; set; } = new();
}

