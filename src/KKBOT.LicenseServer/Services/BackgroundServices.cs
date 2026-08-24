// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KKBOT.LicenseServer.Data;
using KKBOT.Licensing.Models;
using KKBOT.Observability;

namespace KKBOT.LicenseServer.Services;

/// <summary>
/// Background service for cleaning up expired/revoked licenses and stale activations.
/// </summary>
public sealed class LicenseCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LicenseCleanupService> _logger;
    private readonly LicenseCleanupOptions _options;
    private readonly IMetricsRegistry _metrics;

    public LicenseCleanupService(
        IServiceProvider serviceProvider,
        ILogger<LicenseCleanupService> logger,
        IOptions<LicenseCleanupOptions> options,
        IMetricsRegistry metrics)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
        _metrics = metrics;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("License Cleanup Service started");

        // Initial delay
        await Task.Delay(TimeSpan.FromMinutes(_options.InitialDelayMinutes), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformCleanupAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during license cleanup");
            }

            // Wait for next interval
            await Task.Delay(TimeSpan.FromHours(_options.IntervalHours), stoppingToken);
        }

        _logger.LogInformation("License Cleanup Service stopped");
    }

    private async Task PerformCleanupAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LicenseDbContext>();
        var activationService = scope.ServiceProvider.GetRequiredService<IActivationService>();

        _logger.LogInformation("Starting license cleanup cycle");

        // 1. Release stale seats (activations with no heartbeat > threshold)
        var staleTimeout = TimeSpan.FromMinutes(_options.StaleSeatTimeoutMinutes);
        await activationService.ReleaseStaleSeatsAsync(TimeSpan.FromMinutes(_options.StaleSeatTimeoutMinutes), ct);

        // 2. Clean up expired trial licenses (older than 30 days after expiry)
        var trialCutoff = DateTimeOffset.UtcNow.AddDays(-30);
        var expiredTrials = await _db.Licenses
            .Where(l => l.Tier == LicenseTier.Trial 
                     && l.ExpiresAt.HasValue 
                     && l.ExpiresAt.Value < trialCutoff
                     && !l.IsRevoked)
            .ToListAsync(ct);

        foreach (var license in expiredTrials)
        {
            license.IsRevoked = true;
            license.RevocationReason = "Trial license expired and cleanup period passed";
            license.UpdatedAt = DateTimeOffset.UtcNow;

            // Deactivate all activations
            var activations = await db.Activations
                .Where(a => a.LicenseId == license.Id && a.IsActive)
                .ToListAsync(ct);

            foreach (var activation in activations)
            {
                activation.IsActive = false;
                activation.DeactivatedAt = DateTimeOffset.UtcNow;
                activation.DeactivationReason = "Trial license expired and cleaned up";
            }
        }

        if (expiredTrials.Any())
        {
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Revoked {Count} expired trial licenses", expiredTrials.Count);
        }

        // 3. Clean up orphaned activations (license deleted but activation remains)
        var orphanedActivations = await db.Activations
            .Where(a => !db.Licenses.Any(l => l.Id == a.LicenseId))
            .ToListAsync(ct);

        if (orphanedActivations.Any())
        {
            db.Activations.RemoveRange(orphanedActivations);
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Removed {Count} orphaned activations", orphanedActivations.Count);
        }

        // 4. Clean up old audit logs (older than retention period)
        var auditCutoff = DateTimeOffset.UtcNow.AddDays(-_options.AuditLogRetentionDays);
        var oldAuditLogs = await db.AuditLogs
            .Where(a => a.Timestamp < auditCutoff)
            .ToListAsync(ct);

        if (oldAuditLogs.Any())
        {
            db.AuditLogs.RemoveRange(oldAuditLogs);
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Removed {Count} old audit logs", oldAuditLogs.Count);
        }

        // 5. Clean up old analytics data (older than retention period)
        var analyticsCutoff = DateTimeOffset.UtcNow.AddDays(-_options.AnalyticsRetentionDays);
        var oldAnalytics = await db.Analytics
            .Where(a => a.Date < analyticsCutoff)
            .ToListAsync(ct);

        if (oldAnalytics.Any())
        {
            db.Analytics.RemoveRange(oldAnalytics);
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Removed {Count} old analytics records", oldAnalytics.Count);
        }

        _metrics.IncrementCounter("license_cleanup_cycles_total");
        _logger.LogInformation("License cleanup cycle completed");
    }
}

public sealed class LicenseCleanupOptions
{
    public int InitialDelayMinutes { get; set; } = 5;
    public int IntervalHours { get; set; } = 6;
    public int StaleSeatTimeoutMinutes { get; set; } = 10;
    public int AuditLogRetentionDays { get; set; } = 90;
    public int AnalyticsRetentionDays { get; set; } = 365;
}

/// <summary>
/// Background service for notifying about expiring licenses.
/// </summary>
public sealed class LicenseExpirationNotifier : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LicenseExpirationNotifier> _logger;
    private readonly LicenseExpirationNotifierOptions _options;
    private readonly IMetricsRegistry _metrics;

    public LicenseExpirationNotifier(
        IServiceProvider serviceProvider,
        ILogger<LicenseExpirationNotifier> logger,
        IOptions<LicenseExpirationNotifierOptions> options,
        IMetricsRegistry metrics)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
        _metrics = metrics;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("License Expiration Notifier started");

        // Initial delay
        await Task.Delay(TimeSpan.FromMinutes(_options.InitialDelayMinutes), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckExpiringLicensesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking expiring licenses");
            }

            // Wait for next interval
            await Task.Delay(TimeSpan.FromHours(_options.IntervalHours), stoppingToken);
        }

        _logger.LogInformation("License Expiration Notifier stopped");
    }

    private async Task CheckExpiringLicensesAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LicenseDbContext>();
        var notificationService = scope.ServiceProvider.GetService<INotificationService>();

        _logger.LogDebug("Checking for expiring licenses");

        var now = DateTimeOffset.UtcNow;
        var warningThresholds = new[] { 30, 14, 7, 3, 1 }; // days before expiry

        foreach (var days in warningThresholds)
        {
            var thresholdDate = DateTimeOffset.UtcNow.AddDays(days);

            var expiringLicenses = await db.Licenses
                .Where(l => !l.IsRevoked 
                         && l.ExpiresAt.HasValue 
                         && l.ExpiresAt.Value.Date == thresholdDate.Date
                         && l.Tier != LicenseTier.Trial)
                .Include(l => l.Publisher)
                .ToListAsync(ct);

            foreach (var license in expiringLicenses)
            {
                try
                {
                    if (notificationService != null)
                    {
                        await notificationService.SendLicenseExpiringAsync(
                            license.Publisher.Email,
                            license.Key,
                            license.Name,
                            license.ExpiresAt.Value,
                            days,
                            ct);
                    }

                    _logger.LogInformation("Sent expiration warning for license {Key} ({Days} days)", license.Key, days);
                    _metrics.IncrementCounter("license_expiration_warnings_sent_total",
                        new Dictionary<string, string> { ["days"] = days.ToString() });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send expiration warning for license {Key}", license.Key);
                }
            }
        }

        // Also check for licenses expiring today (final warning)
        var expiringToday = await db.Licenses
            .Where(l => !l.IsRevoked 
                     && l.ExpiresAt.HasValue 
                     && l.ExpiresAt.Value.Date == DateTimeOffset.UtcNow.Date
                     && l.Tier != LicenseTier.Trial)
            .Include(l => l.Publisher)
            .ToListAsync(ct);

        foreach (var license in expiringToday)
        {
            try
            {
                if (notificationService != null)
                {
                    await notificationService.SendLicenseExpiringAsync(
                        license.Publisher.Email,
                        license.Key,
                        license.Name,
                        license.ExpiresAt.Value,
                        0,
                        ct);
                }

                _logger.LogWarning("License {Key} expires TODAY!", license.Key);
                _metrics.IncrementCounter("license_expiration_final_warnings_sent_total");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send final expiration warning for license {Key}", license.Key);
            }
        }

        _logger.LogDebug("Expiration check completed");
    }
}

public sealed class LicenseExpirationNotifierOptions
{
    public int InitialDelayMinutes { get; set; } = 10;
    public int IntervalHours { get; set; } = 24; // Run once per day
}

