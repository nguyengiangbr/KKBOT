// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using KKBOT.Licensing.Models;
using KKBOT.LicenseServer.Data;

namespace KKBOT.LicenseServer.Services;

/// <summary>
/// License activation and deactivation service.
/// </summary>
public interface IActivationService
{
    Task<LicenseActivationResponse> ActivateAsync(LicenseActivationRequest request, CancellationToken ct = default);
    Task<bool> DeactivateAsync(string licenseKey, string hardwareId, string? reason, CancellationToken ct = default);
    Task<bool> HeartbeatAsync(string licenseKey, string hardwareId, HeartbeatRequest request, CancellationToken ct = default);
    Task<List<LicenseActivationEntity>> GetActiveActivationsAsync(string licenseKey, CancellationToken ct = default);
    Task<int> GetActiveSeatCountAsync(string licenseKey, CancellationToken ct = default);
    Task<bool> ReleaseStaleSeatsAsync(TimeSpan timeout, CancellationToken ct = default);
    Task<List<StaleActivationInfo>> GetStaleActivationsAsync(TimeSpan timeout, CancellationToken ct = default);
}

public sealed class ActivationService : IActivationService
{
    private readonly LicenseDbContext _db;
    private readonly ILogger<ActivationService> _logger;
    private readonly IHubContext<LicenseHub> _hubContext;
    private readonly IMetricsRegistry _metrics;

    public ActivationService(
        LicenseDbContext db,
        ILogger<ActivationService> logger,
        IHubContext<LicenseHub> hubContext,
        IMetricsRegistry metrics)
    {
        _db = db;
        _logger = logger;
        _hubContext = hubContext;
        _metrics = metrics;
    }

    public async Task<LicenseActivationResponse> ActivateAsync(LicenseActivationRequest request, CancellationToken ct = default)
    {
        // This is a simplified version - full implementation in LicenseService
        // This method focuses on the activation/deactivation mechanics
        
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

            await NotifyActivationChangedAsync(license.Key, "reactivated", request.HardwareId);

            return new LicenseActivationResponse
            {
                Success = true,
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

        await NotifyActivationChangedAsync(license.Key, "activated", request.HardwareId);

        return new LicenseActivationResponse
        {
            Success = true,
            SeatsUsed = license.Activations.Count(a => a.IsActive),
            SeatsRemaining = Math.Max(0, license.SeatLimit - license.Activations.Count(a => a.IsActive)),
            GraceExpiresAt = DateTimeOffset.UtcNow.AddDays(license.OfflineGraceDays)
        };
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

            await NotifyActivationChangedAsync(licenseKey, "deactivated", hardwareId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "License deactivation failed for {Key}", licenseKey);
            return false;
        }
    }

    public async Task<bool> HeartbeatAsync(string licenseKey, string hardwareId, HeartbeatRequest request, CancellationToken ct = default)
    {
        try
        {
            var activation = await _db.Activations
                .FirstOrDefaultAsync(a => a.License.Key == licenseKey && a.HardwareId == hardwareId && a.IsActive, ct);

            if (activation == null) return false;

            activation.LastHeartbeatAt = DateTimeOffset.UtcNow;
            activation.ClientVersion = request.ClientVersion;
            activation.InstalledModules = request.InstalledModules ?? activation.InstalledModules;

            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Heartbeat failed for {Key}", licenseKey);
            return false;
        }
    }

    public async Task<List<LicenseActivationEntity>> GetActiveActivationsAsync(string licenseKey, CancellationToken ct = default)
    {
        return await _db.Activations
            .Where(a => a.License.Key == licenseKey && a.IsActive)
            .OrderByDescending(a => a.LastHeartbeatAt)
            .ToListAsync(ct);
    }

    public async Task<int> GetActiveSeatCountAsync(string licenseKey, CancellationToken ct = default)
    {
        return await _db.Activations
            .Where(a => a.License.Key == licenseKey && a.IsActive)
            .CountAsync(ct);
    }

    public async Task<bool> ReleaseStaleSeatsAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        var cutoff = DateTimeOffset.UtcNow - timeout;
        
        var staleActivations = await _db.Activations
            .Where(a => a.IsActive && a.LastHeartbeatAt < cutoff)
            .ToListAsync(ct);

        if (staleActivations.Count == 0) return false;

        foreach (var activation in staleActivations)
        {
            activation.IsActive = false;
            activation.DeactivatedAt = DateTimeOffset.UtcNow;
            activation.DeactivationReason = $"Stale seat released (timeout: {timeout.TotalMinutes} minutes)";
        }

        await _db.SaveChangesAsync(ct);

        foreach (var activation in staleActivations)
        {
            await NotifyActivationChangedAsync(activation.License.Key, "stale_released", activation.HardwareId);
        }

        _logger.LogInformation("Released {Count} stale seats", staleActivations.Count);
        return true;
    }

    public async Task<List<StaleActivationInfo>> GetStaleActivationsAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        var cutoff = DateTimeOffset.UtcNow - timeout;
        
        return await _db.Activations
            .Where(a => a.IsActive && a.LastHeartbeatAt < cutoff)
            .Include(a => a.License)
            .Select(a => new StaleActivationInfo
            {
                LicenseKey = a.License.Key,
                HardwareId = a.HardwareId,
                MachineName = a.MachineName,
                LastHeartbeatAt = a.LastHeartbeatAt,
                StaleDuration = DateTimeOffset.UtcNow - a.LastHeartbeatAt
            })
            .ToListAsync(ct);
    }

    private async Task NotifyActivationChangedAsync(string licenseKey, string action, string? hardwareId)
    {
        try
        {
            // This would be called from the hub context
            // Implementation depends on how hub context is injected
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify activation change");
        }
    }
}

public sealed class HeartbeatRequest
{
    public string ClientVersion { get; set; } = string.Empty;
    public string[] InstalledModules { get; set; } = Array.Empty<string>();
    public Dictionary<string, object> Metrics { get; set; } = new();
}

public sealed class StaleActivationInfo
{
    public string LicenseKey { get; set; } = string.Empty;
    public string HardwareId { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public DateTimeOffset LastHeartbeatAt { get; set; }
    public TimeSpan StaleDuration { get; set; }
}

