// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using KKBOT.Licensing.Models;
using KKBOT.LicenseServer.Data;
using KKBOT.Observability;

namespace KKBOT.LicenseServer.Services;

/// <summary>
/// Analytics service for license metrics and reporting.
/// </summary>
public interface IAnalyticsService
{
    Task<LicenseAnalytics> GetLicenseAnalyticsAsync(string licenseKey, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
    Task<PublisherAnalytics> GetPublisherAnalyticsAsync(string publisherId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
    Task<GlobalAnalytics> GetGlobalAnalyticsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
    Task RecordFeatureUsageAsync(string licenseKey, LicenseFeature feature, CancellationToken ct = default);
    Task RecordValidationAsync(string licenseKey, string hardwareId, bool isValid, TimeSpan duration, bool isOffline, CancellationToken ct = default);
    Task<DashboardMetrics> GetDashboardMetricsAsync(CancellationToken ct = default);
    Task<LicenseHealthReport> GetLicenseHealthReportAsync(string licenseKey, CancellationToken ct = default);
}

public sealed class AnalyticsService : IAnalyticsService
{
    private readonly LicenseDbContext _db;
    private readonly ILogger<AnalyticsService> _logger;
    private readonly IMetricsRegistry _metrics;

    public AnalyticsService(
        LicenseDbContext db,
        ILogger<AnalyticsService> logger,
        IMetricsRegistry metrics)
    {
        _db = db;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task<LicenseAnalytics> GetLicenseAnalyticsAsync(string licenseKey, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var licenseId = await _db.Licenses
            .Where(l => l.Key == licenseKey)
            .Select(l => l.Id)
            .FirstOrDefaultAsync(ct);

        if (licenseId == Guid.Empty)
        {
            throw new KeyNotFoundException($"License {licenseKey} not found");
        }

        var analytics = await _db.Analytics
            .Where(a => a.LicenseId == licenseId && a.Date >= from && a.Date <= to)
            .OrderBy(a => a.Date)
            .ToListAsync(ct);

        var featureUsage = analytics
            .SelectMany(a => a.FeatureUsage)
            .GroupBy(kvp => kvp.Key)
            .ToDictionary(g => g.Key, g => g.Sum(kvp => kvp.Value));

        var dailyStats = analytics.Select(a => new DailyLicenseStat
        {
            Date = a.Date,
            Activations = a.ActivationsCount,
            Deactivations = a.DeactivationsCount,
            Validations = a.ValidationCount,
            UniqueUsers = a.UniqueHardwareIds,
            FeatureUsage = a.FeatureUsage
        }).ToList();

        var license = await _db.Licenses
            .Include(l => l.Activations)
            .FirstOrDefaultAsync(l => l.Id == licenseId, ct);

        return new LicenseAnalytics
        {
            LicenseId = license?.Key ?? string.Empty,
            TotalActivations = analytics.Sum(a => a.ActivationsCount),
            TotalDeactivations = analytics.Sum(a => a.DeactivationsCount),
            TotalValidations = analytics.Sum(a => a.ValidationCount),
            UniqueHardwareIds = analytics.Max(a => a.UniqueHardwareIds),
            FeatureUsage = featureUsage,
            DailyStats = dailyStats,
            CurrentSeatsUsed = license?.Activations.Count(a => a.IsActive) ?? 0,
            SeatLimit = license?.SeatLimit ?? 0,
            Tier = license?.Tier ?? LicenseTier.Trial,
            ExpiresAt = license?.ExpiresAt
        };
    }

    public async Task<PublisherAnalytics> GetPublisherAnalyticsAsync(string publisherId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var licenses = await _db.Licenses
            .Where(l => l.PublisherId == publisherId)
            .Select(l => l.Id)
            .ToListAsync(ct);

        if (!licenses.Any())
        {
            return new PublisherAnalytics { PublisherId = publisherId };
        }

        var analytics = await _db.Analytics
            .Where(a => licenses.Contains(a.LicenseId) && a.Date >= from && a.Date <= to)
            .ToListAsync(ct);

        var licenseKeys = await _db.Licenses
            .Where(l => l.PublisherId == publisherId)
            .Select(l => l.Key)
            .ToListAsync(ct);

        var activations = await _db.Activations
            .Where(a => licenseKeys.Contains(a.License.Key) && a.IsActive)
            .GroupBy(a => a.License.Key)
            .Select(g => new { LicenseKey = g.Key, ActiveSeats = g.Count() })
            .ToListAsync(ct);

        var tierDistribution = await _db.Licenses
            .Where(l => l.PublisherId == publisherId)
            .GroupBy(l => l.Tier)
            .Select(g => new TierDistributionStat { Tier = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return new PublisherAnalytics
        {
            PublisherId = publisherId,
            TotalLicenses = licenseKeys.Count,
            TotalActivations = analytics.Sum(a => a.ActivationsCount),
            TotalDeactivations = analytics.Sum(a => a.DeactivationsCount),
            TotalValidations = analytics.Sum(a => a.ValidationCount),
            TotalActiveSeats = activations.Sum(a => a.ActiveSeats),
            TierDistribution = tierDistribution.ToDictionary(t => t.Tier, t => t.Count),
            ActiveLicensesByKey = activations.ToDictionary(a => a.LicenseKey, a => a.ActiveSeats),
            PeriodFrom = from,
            PeriodTo = to
        };
    }

    public async Task<GlobalAnalytics> GetGlobalAnalyticsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var totalLicenses = await _db.Licenses.CountAsync(ct);
        var activeLicenses = await _db.Licenses.CountAsync(l => !l.IsRevoked && (!l.ExpiresAt.HasValue || l.ExpiresAt > DateTimeOffset.UtcNow), ct);
        var revokedLicenses = await _db.Licenses.CountAsync(l => l.IsRevoked, ct);
        var expiredLicenses = await _db.Licenses.CountAsync(l => l.ExpiresAt.HasValue && l.ExpiresAt < DateTimeOffset.UtcNow, ct);

        var activeActivations = await _db.Activations.CountAsync(a => a.IsActive, ct);
        var totalSeatsUsed = activeActivations;
        var totalSeatsAvailable = await _db.Licenses.SumAsync(l => (long)l.SeatLimit, ct);

        var analytics = await _db.Analytics
            .Where(a => a.Date >= from && a.Date <= to)
            .ToListAsync(ct);

        var tierDistribution = await _db.Licenses
            .GroupBy(l => l.Tier)
            .Select(g => new TierDistributionStat { Tier = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var featureUsage = analytics
            .SelectMany(a => a.FeatureUsage)
            .GroupBy(kvp => kvp.Key)
            .ToDictionary(g => g.Key, g => g.Sum(kvp => kvp.Value));

        return new GlobalAnalytics
        {
            TotalLicenses = totalLicenses,
            ActiveLicenses = activeLicenses,
            RevokedLicenses = revokedLicenses,
            ExpiredLicenses = expiredLicenses,
            TotalActiveSeats = activeActivations,
            TotalSeatsAvailable = totalSeatsAvailable,
            SeatUtilizationPercent = totalSeatsAvailable > 0 ? (double)activeActivations / totalSeatsAvailable * 100 : 0,
            TotalValidations = analytics.Sum(a => a.ValidationCount),
            TotalActivations = analytics.Sum(a => a.ActivationsCount),
            TotalDeactivations = analytics.Sum(a => a.DeactivationsCount),
            TierDistribution = tierDistribution.ToDictionary(t => t.Tier, t => t.Count),
            FeatureUsage = featureUsage,
            PeriodFrom = from,
            PeriodTo = to
        };
    }

    public async Task RecordFeatureUsageAsync(string licenseKey, LicenseFeature feature, CancellationToken ct = default)
    {
        try
        {
            var licenseId = await _db.Licenses
                .Where(l => l.Key == licenseKey)
                .Select(l => l.Id)
                .FirstOrDefaultAsync(ct);

            if (licenseId == Guid.Empty) return;

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

            var featureKey = feature.ToString();
            analytics.FeatureUsage[featureKey] = analytics.FeatureUsage.GetValueOrDefault(featureKey, 0) + 1;

            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Log but don't throw - analytics shouldn't break core functionality
            Console.WriteLine($"Failed to record feature usage: {ex.Message}");
        }
    }

    public async Task RecordValidationAsync(string licenseKey, string hardwareId, bool isValid, TimeSpan duration, bool isOffline, CancellationToken ct = default)
    {
        try
        {
            var licenseId = await _db.Licenses
                .Where(l => l.Key == licenseKey)
                .Select(l => l.Id)
                .FirstOrDefaultAsync(ct);

            if (licenseId == Guid.Empty) return;

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
                // Track unique hardware IDs (simplified - in production would use HyperLogLog)
            }

            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to record validation: {ex.Message}");
        }
    }

    public async Task<DashboardMetrics> GetDashboardMetricsAsync(CancellationToken ct = default)
    {
        var totalLicenses = await _db.Licenses.CountAsync(ct);
        var activeLicenses = await _db.Licenses.CountAsync(l => !l.IsRevoked && (!l.ExpiresAt.HasValue || l.ExpiresAt > DateTimeOffset.UtcNow), ct);
        var activeActivations = await _db.Activations.CountAsync(a => a.IsActive, ct);
        var totalSeatsAvailable = await _db.Licenses.SumAsync(l => (long)l.SeatLimit, ct);

        var last24h = DateTimeOffset.UtcNow.AddHours(-24);
        var recentActivations = await _db.Activations.CountAsync(a => a.ActivatedAt >= last24h, ct);
        var recentDeactivations = await _db.Activations.CountAsync(a => a.DeactivatedAt >= last24h, ct);

        var validationsLast24h = await _db.Analytics
            .Where(a => a.Date >= DateTimeOffset.UtcNow.AddDays(-1))
            .SumAsync(a => a.ValidationCount, ct);

        return new DashboardMetrics
        {
            TotalLicenses = totalLicenses,
            ActiveLicenses = totalLicenses - (await _db.Licenses.CountAsync(l => l.IsRevoked || (l.ExpiresAt.HasValue && l.ExpiresAt < DateTimeOffset.UtcNow), ct)),
            ActiveSeats = activeActivations,
            TotalSeats = totalSeatsAvailable,
            SeatUtilizationPercent = totalSeatsAvailable > 0 ? (double)activeActivations / totalSeatsAvailable * 100 : 0,
            ActivationsLast24h = recentActivations,
            DeactivationsLast24h = recentDeactivations,
            ValidationsLast24h = validationsLast24h,
            GeneratedAt = DateTimeOffset.UtcNow
        };
    }

    public async Task<LicenseHealthReport> GetLicenseHealthReportAsync(string licenseKey, CancellationToken ct = default)
    {
        var license = await _db.Licenses
            .Include(l => l.Activations)
            .FirstOrDefaultAsync(l => l.Key == licenseKey, ct);

        if (license == null)
        {
            throw new KeyNotFoundException($"License {licenseKey} not found");
        }

        var activeActivations = license.Activations.Where(a => a.IsActive).ToList();
        var staleActivations = license.Activations
            .Where(a => a.IsActive && a.LastHeartbeatAt < DateTimeOffset.UtcNow.AddMinutes(-10))
            .ToList();

        var issues = new List<string>();

        if (license.IsRevoked)
        {
            issues.Add($"License is revoked: {license.RevocationReason}");
        }

        if (license.ExpiresAt.HasValue && license.ExpiresAt.Value < DateTimeOffset.UtcNow.AddDays(7))
        {
            issues.Add($"License expires soon: {license.ExpiresAt.Value:yyyy-MM-dd}");
        }

        if (staleActivations.Any())
        {
            issues.Add($"{staleActivations.Count} stale activations (no heartbeat > 10 min)");
        }

        if (license.Activations.Count(a => a.IsActive) >= license.SeatLimit * 0.9)
        {
            issues.Add($"Seat utilization > 90% ({license.Activations.Count(a => a.IsActive)}/{license.SeatLimit})");
        }

        return new LicenseHealthReport
        {
            LicenseKey = license.Key,
            IsHealthy = !issues.Any(),
            Issues = issues,
            ActiveSeats = license.Activations.Count(a => a.IsActive),
            SeatLimit = license.SeatLimit,
            SeatUtilizationPercent = license.SeatLimit > 0 ? (double)license.Activations.Count(a => a.IsActive) / license.SeatLimit * 100 : 0,
            ExpiresAt = license.ExpiresAt,
            IsRevoked = license.IsRevoked,
            StaleActivationsCount = staleActivations.Count,
            LastChecked = DateTimeOffset.UtcNow
        };
    }
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
    public int CurrentSeatsUsed { get; set; }
    public int SeatLimit { get; set; }
    public LicenseTier Tier { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}

public sealed class PublisherAnalytics
{
    public string PublisherId { get; set; } = string.Empty;
    public int TotalLicenses { get; set; }
    public long TotalActivations { get; set; }
    public long TotalDeactivations { get; set; }
    public long TotalValidations { get; set; }
    public int TotalActiveSeats { get; set; }
    public Dictionary<LicenseTier, int> TierDistribution { get; set; } = new();
    public Dictionary<string, int> ActiveLicensesByKey { get; set; } = new();
    public DateTimeOffset PeriodFrom { get; set; }
    public DateTimeOffset PeriodTo { get; set; }
}

public sealed class GlobalAnalytics
{
    public int TotalLicenses { get; set; }
    public int ActiveLicenses { get; set; }
    public int RevokedLicenses { get; set; }
    public int ExpiredLicenses { get; set; }
    public int TotalActiveSeats { get; set; }
    public long TotalSeatsAvailable { get; set; }
    public double SeatUtilizationPercent { get; set; }
    public long TotalValidations { get; set; }
    public long TotalActivations { get; set; }
    public long TotalDeactivations { get; set; }
    public Dictionary<LicenseTier, int> TierDistribution { get; set; } = new();
    public Dictionary<string, long> FeatureUsage { get; set; } = new();
    public DateTimeOffset PeriodFrom { get; set; }
    public DateTimeOffset PeriodTo { get; set; }
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

public sealed class DashboardMetrics
{
    public int TotalLicenses { get; set; }
    public int ActiveLicenses { get; set; }
    public int ActiveSeats { get; set; }
    public long TotalSeats { get; set; }
    public double SeatUtilizationPercent { get; set; }
    public int ActivationsLast24h { get; set; }
    public int DeactivationsLast24h { get; set; }
    public long ValidationsLast24h { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
}

public sealed class TierDistributionStat
{
    public LicenseTier Tier { get; set; }
    public int Count { get; set; }
}

public sealed class LicenseHealthReport
{
    public string LicenseKey { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public List<string> Issues { get; set; } = new();
    public int ActiveSeats { get; set; }
    public int SeatLimit { get; set; }
    public double SeatUtilizationPercent { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public int StaleActivationsCount { get; set; }
    public DateTimeOffset LastChecked { get; set; }
}

