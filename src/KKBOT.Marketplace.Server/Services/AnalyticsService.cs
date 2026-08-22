// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Marketplace.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CsvHelper;
using CsvHelper.Configuration;

namespace KKBOT.Marketplace.Server.Services;

public sealed class AnalyticsService : IAnalyticsService
{
    private readonly MarketplaceDbContext _dbContext;
    private readonly ILogger<AnalyticsService> _logger;

    public AnalyticsService(MarketplaceDbContext dbContext, ILogger<AnalyticsService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<MarketplaceOverview> GetOverviewAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var monthAgo = now.AddMonths(-1);

        var totalPlugins = await _dbContext.PluginPackages.CountAsync(ct);
        var publishedPlugins = await _dbContext.PluginPackages.CountAsync(p => p.Status == PluginStatus.Published, ct);
        var totalPublishers = await _dbContext.Publishers.CountAsync(ct);
        var verifiedPublishers = await _dbContext.Publishers.CountAsync(p => p.VerificationStatus == "verified", ct);
        
        var totalDownloads = await _dbContext.PluginPackages.SumAsync(p => (long?)p.DownloadCount ?? 0, ct) ?? 0;
        var monthlyDownloads = await _dbContext.PluginDownloads
            .Where(d => d.DownloadedAt >= monthAgo)
            .CountAsync(ct);

        var avgRating = await _dbContext.PluginPackages
            .Where(p => p.ReviewCount > 0)
            .AverageAsync(p => (double?)p.AverageRating ?? 0, ct) ?? 0;

        var totalReviews = await _dbContext.PluginReviews.CountAsync(ct);
        var pendingModeration = await _dbContext.ModerationQueue.CountAsync(m => m.Status == ModerationStatus.Pending, ct);

        var pluginsByCategory = await _dbContext.PluginPackages
            .Where(p => p.Status == PluginStatus.Published)
            .GroupBy(p => p.Metadata.Tags.FirstOrDefault() ?? "Other")
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Category, x => (long)x.Count, ct);

        var pluginsByStatus = await _dbContext.PluginPackages
            .GroupBy(p => p.Status.ToString())
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => (long)x.Count, ct);

        var conversionRate = await CalculateConversionRateAsync(ct);

        return new MarketplaceOverview
        {
            TotalPlugins = totalPlugins,
            PublishedPlugins = publishedPlugins,
            TotalPublishers = totalPublishers,
            VerifiedPublishers = verifiedPublishers,
            TotalDownloads = totalDownloads,
            MonthlyActiveUsers = await GetMonthlyActiveUsersAsync(ct),
            AverageRating = Math.Round(avgRating, 2),
            TotalReviews = totalReviews,
            PendingModeration = pendingModeration,
            ConversionRate = conversionRate,
            PluginsByCategory = pluginsByCategory,
            PluginsByStatus = pluginsByStatus
        };
    }

    public async Task<MarketplaceTrends> GetTrendsAsync(TrendPeriod period = TrendPeriod.Last30Days, CancellationToken ct = default)
    {
        var (fromDate, interval) = GetPeriodBounds(period);
        var now = DateTime.UtcNow;

        var pluginGrowth = await GetDailyTrendAsync(
            _dbContext.PluginPackages.Where(p => p.CreatedAt >= fromDate),
            p => p.CreatedAt,
            interval,
            fromDate,
            ct);

        var downloadTrends = await GetDailyTrendAsync(
            _dbContext.PluginDownloads.Where(d => d.DownloadedAt >= fromDate),
            d => d.DownloadedAt,
            interval,
            fromDate,
            ct);

        var publisherGrowth = await GetDailyTrendAsync(
            _dbContext.Publishers.Where(p => p.CreatedAt >= fromDate),
            p => p.CreatedAt,
            interval,
            fromDate,
            ct);

        var revenueTrends = await GetDailyTrendAsync(
            _dbContext.PublisherPayouts.Where(p => p.CreatedAt >= fromDate && p.Status == PayoutStatus.Completed),
            p => p.CreatedAt,
            interval,
            fromDate,
            ct,
            p => p.NetAmount);

        var reviewTrends = await GetDailyTrendAsync(
            _dbContext.PluginReviews.Where(r => r.CreatedAt >= fromDate),
            r => r.CreatedAt,
            interval,
            fromDate,
            ct);

        var activeUsers = await GetDailyActiveUsersTrendAsync(fromDate, interval, ct);

        return new MarketplaceTrends
        {
            Period = period,
            PluginGrowth = pluginGrowth,
            DownloadTrends = downloadTrends,
            PublisherGrowth = publisherGrowth,
            RevenueTrends = revenueTrends,
            ReviewTrends = reviewTrends,
            ActiveUsers = activeUsers
        };
    }

    public async Task<PluginAnalytics> GetPluginAnalyticsAsync(string pluginId, CancellationToken ct = default)
    {
        var plugin = await _dbContext.PluginPackages
            .Include(p => p.Versions)
            .Include(p => p.Reviews)
            .Include(p => p.Downloads)
            .FirstOrDefaultAsync(p => p.PluginId == pluginId, ct);

        if (plugin == null)
            throw new InvalidOperationException($"Plugin {pluginId} not found");

        var versionPerformance = plugin.Versions
            .Select(v => new VersionPerformance
            {
                Version = v.Version,
                Downloads = v.DownloadCount,
                AverageRating = v.Status == VersionStatus.Published ? plugin.AverageRating : 0,
                ReviewCount = plugin.Reviews.Count(r => r.CreatedAt >= v.PublishedAt),
                PublishedAt = v.PublishedAt,
                TimeToFirstDownload = CalculateTimeToFirstDownload(v)
            })
            .OrderByDescending(v => v.Downloads)
            .ToList();

        var downloadTrends = await GetDailyTrendAsync(
            _dbContext.PluginDownloads.Where(d => d.PluginVersion.PluginPackageId == plugin.Id),
            d => d.DownloadedAt,
            TimeSpan.FromDays(1),
            DateTime.UtcNow.AddDays(-30),
            ct);

        var ratingTrends = plugin.Reviews
            .GroupBy(r => r.CreatedAt.Date)
            .Select(g => new TrendDataPoint
            {
                Date = g.Key,
                Value = g.Average(r => r.Rating),
                Label = g.Average(r => r.Rating).ToString("F1")
            })
            .OrderBy(t => t.Date)
            .ToList();

        var ratingDistribution = new RatingDistribution
        {
            FiveStar = plugin.Reviews.Count(r => r.Rating == 5),
            FourStar = plugin.Reviews.Count(r => r.Rating == 4),
            ThreeStar = plugin.Reviews.Count(r => r.Rating == 3),
            TwoStar = plugin.Reviews.Count(r => r.Rating == 2),
            OneStar = plugin.Reviews.Count(r => r.Rating == 1),
            Average = plugin.AverageRating
        };

        var downloadsByRegion = await _dbContext.PluginDownloads
            .Where(d => d.PluginVersion.PluginPackageId == plugin.Id && !string.IsNullOrEmpty(d.CountryCode))
            .GroupBy(d => d.CountryCode)
            .Select(g => new { Country = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Country, x => (long)x.Count, CancellationToken.None);

        return new PluginAnalytics
        {
            PluginId = plugin.PluginId,
            PluginName = plugin.Name,
            TotalDownloads = plugin.DownloadCount,
            MonthlyDownloads = plugin.Downloads.Count(d => d.DownloadedAt >= DateTime.UtcNow.AddMonths(-1)),
            WeeklyDownloads = plugin.Downloads.Count(d => d.DownloadedAt >= DateTime.UtcNow.AddDays(-7)),
            AverageRating = plugin.AverageRating,
            ReviewCount = plugin.ReviewCount,
            RatingDistribution = new RatingDistribution
            {
                FiveStar = plugin.Reviews.Count(r => r.Rating == 5),
                FourStar = plugin.Reviews.Count(r => r.Rating == 4),
                ThreeStar = plugin.Reviews.Count(r => r.Rating == 3),
                TwoStar = plugin.Reviews.Count(r => r.Rating == 2),
                OneStar = plugin.Reviews.Count(r => r.Rating == 1),
                Average = plugin.AverageRating
            },
            VersionPerformance = plugin.Versions
                .Select(v => new VersionPerformance
                {
                    Version = v.Version,
                    Downloads = v.DownloadCount,
                    AverageRating = v.Status == VersionStatus.Published ? plugin.AverageRating : 0,
                    ReviewCount = plugin.Reviews.Count(r => r.CreatedAt >= v.PublishedAt),
                    PublishedAt = v.PublishedAt,
                    TimeToFirstDownload = CalculateTimeToFirstDownload(v)
                })
                .OrderByDescending(v => v.Downloads)
                .ToList(),
            DownloadTrends = await GetDailyTrendAsync(
                _dbContext.PluginDownloads.Where(d => d.PluginVersion.PluginPackageId == plugin.Id),
                d => d.DownloadedAt,
                TimeSpan.FromDays(1),
                DateTime.UtcNow.AddDays(-30),
                CancellationToken.None),
            RatingTrends = plugin.Reviews
                .GroupBy(r => r.CreatedAt.Date)
                .Select(g => new TrendDataPoint
                {
                    Date = g.Key,
                    Value = g.Average(r => r.Rating),
                    Label = g.Average(r => r.Rating).ToString("F1")
                })
                .OrderBy(t => t.Date)
                .ToList(),
            DownloadsByPlatform = new Dictionary<string, long>(), // Would need platform tracking
            DownloadsByRegion = await _dbContext.PluginDownloads
                .Where(d => d.PluginVersion.PluginPackageId == plugin.Id && !string.IsNullOrEmpty(d.CountryCode))
                .GroupBy(d => d.CountryCode)
                .Select(g => new { Country = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Country, x => (long)x.Count, CancellationToken.None),
            RetentionRate = await CalculateRetentionRateAsync(plugin.Id, ct),
            ChurnRate = await CalculateChurnRateAsync(plugin.Id, ct),
            AverageTimeToFirstDownload = CalculateAverageTimeToFirstDownload(plugin)
        };
    }

    public async Task<PublisherAnalytics> GetPublisherAnalyticsAsync(Guid publisherId, CancellationToken ct = default)
    {
        var publisher = await _dbContext.Publishers
            .Include(p => p.Packages)
                .ThenInclude(p => p.Versions)
            .Include(p => p.Payouts)
            .FirstOrDefaultAsync(p => p.Id == publisherId, ct);

        if (publisher == null)
            throw new InvalidOperationException($"Publisher {publisherId} not found");

        var pluginIds = publisher.Packages.Select(p => p.Id).ToList();
        var totalDownloads = publisher.Packages.Sum(p => p.DownloadCount);
        var monthlyDownloads = await _dbContext.PluginDownloads
            .Where(d => d.PluginVersion.PluginPackageId != null && 
                       pluginIds.Contains(d.PluginVersion.PluginPackageId.Value) &&
                       d.DownloadedAt >= DateTime.UtcNow.AddMonths(-1))
            .CountAsync(ct);

        var allReviews = await _dbContext.PluginReviews
            .Where(r => pluginIds.Contains(r.PluginPackageId))
            .ToListAsync(ct);

        var completedPayouts = publisher.Payouts
            .Where(p => p.Status == PayoutStatus.Completed)
            .ToList();

        var pendingPayouts = publisher.Payouts
            .Where(p => p.Status == PayoutStatus.Pending || p.Status == PayoutStatus.Processing)
            .ToList();

        return new PublisherAnalytics
        {
            PublisherId = publisher.Id,
            PublisherName = publisher.Name,
            TotalPlugins = publisher.Packages.Count,
            TotalDownloads = totalDownloads,
            MonthlyDownloads = monthlyDownloads,
            AverageRating = allReviews.Any() ? allReviews.Average(r => r.Rating) : 0,
            GrossRevenue = publisher.Payouts.Where(p => p.Status == PayoutStatus.Completed).Sum(p => p.GrossRevenue),
            NetRevenue = publisher.Payouts.Where(p => p.Status == PayoutStatus.Completed).Sum(p => p.NetAmount),
            PlatformFees = publisher.Payouts.Where(p => p.Status == PayoutStatus.Completed).Sum(p => p.PlatformFee),
            ActivePlugins = publisher.Packages.Count(p => p.Status == PluginStatus.Published),
            TopPlugins = publisher.Packages
                .OrderByDescending(p => p.DownloadCount)
                .Take(5)
                .Select(p => new PluginPerformanceSummary
                {
                    PluginId = p.PluginId,
                    PluginName = p.Name,
                    Downloads = p.DownloadCount,
                    Rating = p.AverageRating,
                    Revenue = p.Versions.Sum(v => v.DownloadCount) * 0 // Would calculate from actual sales
                })
                .ToList(),
            RevenueTrends = await GetDailyTrendAsync(
                _dbContext.PublisherPayouts.Where(p => p.PublisherId == publisherId && p.Status == PayoutStatus.Completed),
                p => p.CreatedAt,
                TimeSpan.FromDays(1),
                DateTime.UtcNow.AddMonths(-1),
                ct,
                p => p.NetAmount),
            DownloadTrends = await GetDailyTrendAsync(
                _dbContext.PluginDownloads.Where(d => d.PluginVersion.PluginPackageId != null && pluginIds.Contains(d.PluginVersion.PluginPackageId.Value)),
                d => d.DownloadedAt,
                TimeSpan.FromDays(1),
                DateTime.UtcNow.AddMonths(-1),
                ct),
            PayoutSummary = new PayoutSummary
            {
                TotalPaid = completedPayouts.Sum(p => p.NetAmount),
                PendingPayouts = pendingPayouts.Sum(p => p.NetAmount),
                LastPayoutAmount = completedPayouts.OrderByDescending(p => p.PaidAt).FirstOrDefault()?.NetAmount ?? 0,
                LastPayoutDate = completedPayouts.OrderByDescending(p => p.PaidAt).FirstOrDefault()?.PaidAt,
                CompletedPayouts = completedPayouts.Count,
                FailedPayouts = publisher.Payouts.Count(p => p.Status == PayoutStatus.Failed)
            }
        };
    }

    public async Task<UserBehaviorMetrics> GetUserBehaviorMetricsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var today = now.Date;
        var weekAgo = now.AddDays(-7);
        var monthAgo = now.AddMonths(-1);

        var totalUsers = await _dbContext.Users.CountAsync(ct); // Would need Users table
        var newUsersToday = 0; // Would track from audit logs
        var newUsersThisWeek = 0;
        var newUsersThisMonth = 0;

        // These would come from audit logs / session tracking
        var activeUsersToday = 0;
        var activeUsersThisWeek = 0;
        var activeUsersThisMonth = 0;

        return new UserBehaviorMetrics
        {
            TotalUsers = totalUsers,
            NewUsersToday = newUsersToday,
            NewUsersThisWeek = newUsersThisWeek,
            NewUsersThisMonth = newUsersThisMonth,
            ActiveUsersToday = activeUsersToday,
            ActiveUsersThisWeek = activeUsersThisWeek,
            ActiveUsersThisMonth = activeUsersThisMonth,
            AverageSessionDurationMinutes = 0,
            AveragePagesPerSession = 0,
            BounceRate = 0,
            UsersByRole = new Dictionary<string, long>(),
            UsersByTenant = new Dictionary<string, long>(),
            UserRetention = new List<TrendDataPoint>(),
            FeatureUsage = new Dictionary<string, long>()
        };
    }

    public async Task<SearchAnalytics> GetSearchAnalyticsAsync(CancellationToken ct = default)
    {
        // Would need search log table
        return new SearchAnalytics
        {
            TotalSearches = 0,
            UniqueSearchTerms = 0,
            ZeroResultsRate = 0,
            TopSearchTerms = new List<SearchTermMetric>(),
            ZeroResultTerms = new List<SearchTermMetric>(),
            AverageResultsPerSearch = 0,
            ClickThroughRate = 0,
            AverageTimeToClick = TimeSpan.Zero,
            SearchesByCategory = new Dictionary<string, long>(),
            SearchesByFilter = new Dictionary<string, long>()
        };
    }

    public async Task<RevenueAnalytics> GetRevenueAnalyticsAsync(CancellationToken ct = default)
    {
        var completedPayouts = await _dbContext.PublisherPayouts
            .Where(p => p.Status == PayoutStatus.Completed)
            .ToListAsync(ct);

        var totalRevenue = completedPayouts.Sum(p => p.GrossRevenue);
        var monthlyRevenue = completedPayouts
            .Where(p => p.CreatedAt >= DateTime.UtcNow.AddMonths(-1))
            .Sum(p => p.GrossRevenue);

        var revenueTrends = await GetDailyTrendAsync(
            _dbContext.PublisherPayouts.Where(p => p.Status == PayoutStatus.Completed),
            p => p.CreatedAt,
            TimeSpan.FromDays(1),
            DateTime.UtcNow.AddMonths(-1),
            ct,
            p => p.GrossRevenue);

        var revenueByCategory = await _dbContext.PluginPackages
            .Where(p => p.Status == PluginStatus.Published)
            .GroupBy(p => p.Metadata.Tags.FirstOrDefault() ?? "Other")
            .Select(g => new { Category = g.Key, Revenue = g.Sum(p => (decimal?)p.DownloadCount ?? 0) })
            .ToDictionaryAsync(x => x.Category, x => x.Revenue, ct);

        return new RevenueAnalytics
        {
            TotalRevenue = completedPayouts.Sum(p => p.GrossRevenue),
            MonthlyRevenue = completedPayouts.Where(p => p.CreatedAt >= DateTime.UtcNow.AddMonths(-1)).Sum(p => p.GrossRevenue),
            PlatformFees = completedPayouts.Sum(p => p.PlatformFee),
            PublisherPayouts = completedPayouts.Sum(p => p.NetAmount),
            RevenueTrends = revenueTrends,
            RevenueByCategory = revenueByCategory,
            RevenueByPlugin = new Dictionary<string, decimal>(),
            AverageRevenuePerPlugin = 0,
            AverageRevenuePerPublisher = 0,
            PayingUsers = 0,
            ConversionRate = 0,
            ARPU = 0,
            ARPPU = 0
        };
    }

    public async Task<PayoutAnalytics> GetPayoutAnalyticsAsync(Guid publisherId, CancellationToken ct = default)
    {
        var payouts = await _dbContext.PublisherPayouts
            .Where(p => p.PublisherId == publisherId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

        var completed = payouts.Where(p => p.Status == PayoutStatus.Completed).ToList();
        var pending = payouts.Where(p => p.Status == PayoutStatus.Pending || p.Status == PayoutStatus.Processing).ToList();

        return new PayoutAnalytics
        {
            PublisherId = publisherId,
            TotalPaid = payouts.Where(p => p.Status == PayoutStatus.Completed).Sum(p => p.NetAmount),
            PendingAmount = payouts.Where(p => p.Status == PayoutStatus.Pending || p.Status == PayoutStatus.Processing).Sum(p => p.NetAmount),
            CompletedPayouts = payouts.Count(p => p.Status == PayoutStatus.Completed),
            FailedPayouts = payouts.Count(p => p.Status == PayoutStatus.Failed),
            AveragePayoutAmount = completed.Any() ? completed.Average(p => p.NetAmount) : 0,
            AveragePayoutTime = completed.Any() 
                ? TimeSpan.FromTicks((long)completed.Average(p => (p.PaidAt.Value - p.CreatedAt).Ticks)) 
                : TimeSpan.Zero,
            PayoutTrends = payouts
                .GroupBy(p => p.CreatedAt.Date)
                .Select(g => new TrendDataPoint { Date = g.Key, Value = (double)g.Sum(p => p.NetAmount) })
                .OrderBy(t => t.Date)
                .ToList(),
            RecentPayouts = payouts.Take(10).Select(p => new PayoutDetail
            {
                Id = p.Id,
                Amount = p.NetAmount,
                Currency = p.Currency,
                Status = p.Status,
                CreatedAt = p.CreatedAt,
                PaidAt = p.PaidAt,
                Period = $"{p.PeriodStart} - {p.PeriodEnd}"
            }).ToList()
        };
    }

    public async Task<PlatformHealthMetrics> GetPlatformHealthMetricsAsync(CancellationToken ct = default)
    {
        // These would come from monitoring system
        return new PlatformHealthMetrics
        {
            ApiUptime = 99.9,
            AverageResponseTimeMs = 150,
            ErrorRate = 0.01,
            RequestsPerMinute = 1000,
            CpuUsagePercent = 45,
            MemoryUsagePercent = 60,
            DiskUsagePercent = 30,
            ActiveConnections = 100,
            QueueDepth = 10,
            ServiceHealth = new Dictionary<string, ServiceHealth>
            {
                ["API"] = new ServiceHealth { Name = "API", Status = "healthy", LatencyMs = 150, ErrorRate = 0.01, LastCheck = DateTime.UtcNow },
                ["Database"] = new ServiceHealth { Name = "Database", Status = "healthy", LatencyMs = 50, ErrorRate = 0.001, LastCheck = DateTime.UtcNow },
                ["Storage"] = new ServiceHealth { Name = "Storage", Status = "healthy", LatencyMs = 200, ErrorRate = 0.005, LastCheck = DateTime.UtcNow },
                ["MessageQueue"] = new ServiceHealth { Name = "MessageQueue", Status = "healthy", LatencyMs = 10, ErrorRate = 0, LastCheck = DateTime.UtcNow }
            },
            RecentIncidents = new List<IncidentSummary>()
        };
    }

    public async Task<ModerationAnalytics> GetModerationAnalyticsAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        var queue = await _dbContext.ModerationQueue.ToListAsync(ct);

        var moderatorPerformance = await _dbContext.ModerationQueue
            .Where(m => m.ModeratorId != null && m.ResolvedAt != null)
            .GroupBy(m => m.ModeratorId)
            .Select(g => new ModeratorPerformance
            {
                ModeratorId = g.Key!,
                ModeratorName = g.First().ModeratorName,
                ItemsReviewed = g.Count(),
                Approved = g.Count(m => m.Status == ModerationStatus.Approved),
                Rejected = g.Count(m => m.Status == ModerationStatus.Rejected),
                AverageReviewTime = TimeSpan.FromTicks((long)g.Average(m => (m.ResolvedAt.Value - m.SubmittedAt).Ticks)),
                AccuracyScore = 0.95 // Would calculate from audit
            })
            .ToListAsync(ct);

        return new ModerationAnalytics
        {
            TotalQueued = queue.Count,
            Pending = queue.Count(m => m.Status == ModerationStatus.Pending),
            UnderReview = queue.Count(m => m.Status == ModerationStatus.UnderReview),
            ApprovedToday = queue.Count(m => m.Status == ModerationStatus.Approved && m.ResolvedAt?.Date == DateTime.UtcNow.Date),
            RejectedToday = queue.Count(m => m.Status == ModerationStatus.Rejected && m.ResolvedAt?.Date == DateTime.UtcNow.Date),
            Escalated = queue.Count(m => m.Status == ModerationStatus.Escalated),
            AverageReviewTime = TimeSpan.FromTicks((long)queue.Where(m => m.ResolvedAt != null).Average(m => (m.ResolvedAt.Value - m.SubmittedAt).Ticks)),
            ApprovalRate = queue.Where(m => m.Status == ModerationStatus.Approved || m.Status == ModerationStatus.Rejected).Any() 
                ? (double)queue.Count(m => m.Status == ModerationStatus.Approved) / queue.Count(m => m.Status == ModerationStatus.Approved || m.Status == ModerationStatus.Rejected) 
                : 0,
            ByEntityType = queue.GroupBy(m => m.EntityType).ToDictionary(g => g.Key, g => (long)g.Count()),
            ByPriority = queue.GroupBy(m => m.Priority.ToString()).ToDictionary(g => g.Key, g => (long)g.Count()),
            ModeratorPerformance = new List<ModeratorPerformance>(),
            QueueTrends = await GetDailyTrendAsync(
                _dbContext.ModerationQueue,
                m => m.SubmittedAt,
                TimeSpan.FromDays(1),
                DateTime.UtcNow.AddDays(-30),
                ct)
        };
    }

    public async Task<RealTimeMetrics> GetRealTimeMetricsAsync(CancellationToken ct = default)
    {
        var lastMinute = DateTime.UtcNow.AddMinutes(-1);
        var lastHour = DateTime.UtcNow.AddHours(-1);

        return new RealTimeMetrics
        {
            ActiveUsersNow = 0, // Would come from SignalR connections
            DownloadsLastMinute = await _dbContext.PluginDownloads.CountAsync(d => d.DownloadedAt >= lastMinute, ct),
            SearchesLastMinute = 0, // Would need search log
            ActiveDownloads = 0,
            PendingModeration = await _dbContext.ModerationQueue.CountAsync(m => m.Status == ModerationStatus.Pending, ct),
            CurrentErrorRate = 0.01,
            CurrentResponseTimeMs = 150,
            ActiveUsersByPage = new Dictionary<string, int>(),
            RecentActivity = new List<RecentActivity>()
        };
    }

    public async Task<byte[]> ExportAnalyticsReportAsync(ReportType type, ReportFormat format, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var data = await GetReportDataAsync(type, from, to, ct);

        return format switch
        {
            ReportFormat.CSV => ExportToCsv(data),
            ReportFormat.JSON => Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true })),
            ReportFormat.Excel => ExportToExcel(data), // Would need EPPlus or similar
            ReportFormat.PDF => ExportToPdf(data), // Would need a PDF library
            _ => throw new ArgumentException($"Unsupported format: {format}")
        };
    }

    // Helper methods

    private async Task<long> GetMonthlyActiveUsersAsync(CancellationToken ct)
    {
        // Would track from audit logs / sessions
        return 0;
    }

    private async Task<double> CalculateConversionRateAsync(CancellationToken ct)
    {
        var totalVisitors = 10000; // Would come from analytics
        var conversions = await _dbContext.PluginDownloads.CountAsync(ct);
        return totalVisitors > 0 ? (double)conversions / totalVisitors : 0;
    }

    private async Task<double> CalculateRetentionRateAsync(Guid pluginId, CancellationToken ct)
    {
        // Would track user retention over time
        return 0.75; // Placeholder
    }

    private async Task<double> CalculateChurnRateAsync(Guid pluginId, CancellationToken ct)
    {
        return 0.25; // Placeholder
    }

    private TimeSpan CalculateTimeToFirstDownload(PluginVersion version)
    {
        var firstDownload = _dbContext.PluginDownloads
            .Where(d => d.PluginVersionId == version.Id)
            .OrderBy(d => d.DownloadedAt)
            .FirstOrDefault();

        if (firstDownload != null)
            return firstDownload.DownloadedAt - version.PublishedAt;

        return TimeSpan.Zero;
    }

    private TimeSpan CalculateAverageTimeToFirstDownload(PluginPackage plugin)
    {
        var times = plugin.Versions
            .Select(v => CalculateTimeToFirstDownload(v))
            .Where(t => t > TimeSpan.Zero)
            .ToList();

        if (!times.Any()) return TimeSpan.Zero;
        return TimeSpan.FromTicks((long)times.Average(t => t.Ticks));
    }

    private (DateTime fromDate, TimeSpan interval) GetPeriodBounds(TrendPeriod period)
    {
        var now = DateTime.UtcNow;
        return period switch
        {
            TrendPeriod.Last7Days => (now.AddDays(-7), TimeSpan.FromHours(6)),
            TrendPeriod.Last30Days => (now.AddDays(-30), TimeSpan.FromDays(1)),
            TrendPeriod.Last90Days => (now.AddDays(-90), TimeSpan.FromDays(1)),
            TrendPeriod.LastYear => (now.AddYears(-1), TimeSpan.FromDays(7)),
            TrendPeriod.AllTime => (DateTime.UtcNow.AddYears(-5), TimeSpan.FromDays(30)),
            _ => (now.AddDays(-30), TimeSpan.FromDays(1))
        };
    }

    private async Task<List<TrendDataPoint>> GetDailyTrendAsync<T>(
        IQueryable<T> query,
        System.Linq.Expressions.Expression<Func<T, DateTime>> dateSelector,
        TimeSpan interval,
        DateTime fromDate,
        CancellationToken ct,
        System.Linq.Expressions.Expression<Func<T, double>>? valueSelector = null)
    {
        // For simplicity, group by date
        var grouped = await query
            .Where(BuildDateFilter(dateSelector, fromDate))
            .GroupBy(dateSelector)
            .Select(g => new
            {
                Date = g.Key.Date,
                Count = g.Count(),
                Sum = valueSelector != null ? g.Sum(valueSelector) : 0.0
            })
            .OrderBy(x => x.Date)
            .ToListAsync(CancellationToken.None);

        var result = new List<TrendDataPoint>();
        var current = fromDate.Date;
        var endDate = DateTime.UtcNow.Date;

        while (current <= endDate)
        {
            var dayData = grouped.FirstOrDefault(g => g.Date == current);
            result.Add(new TrendDataPoint
            {
                Date = current,
                Value = valueSelector != null ? dayData?.Sum ?? 0 : dayData?.Count ?? 0,
                Label = (valueSelector != null ? dayData?.Sum ?? 0 : dayData?.Count ?? 0).ToString()
            });
            current = current.AddDays(1);
        }

        return result;
    }

    private System.Linq.Expressions.Expression<Func<T, bool>> BuildDateFilter<T>(
        System.Linq.Expressions.Expression<Func<T, DateTime>> dateSelector,
        DateTime fromDate)
    {
        var param = dateSelector.Parameters[0];
        var body = System.Linq.Expressions.Expression.GreaterThanOrEqual(
            System.Linq.Expressions.Expression.Property(dateSelector.Body, "Date"),
            System.Linq.Expressions.Expression.Constant(fromDate.Date));
        return System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, param);
    }

    private async Task<List<TrendDataPoint>> GetDailyActiveUsersTrendAsync(DateTime fromDate, TimeSpan interval, CancellationToken ct)
    {
        // Would need user session tracking
        return new List<TrendDataPoint>();
    }

    private object GetReportDataAsync(ReportType type, DateTime from, DateTime to, CancellationToken ct)
    {
        return new { Type = type, From = from, To = to, Message = "Report data would be generated here" };
    }

    private byte[] ExportToCsv(object data)
    {
        using var memoryStream = new MemoryStream();
        using var writer = new StreamWriter(memoryStream);
        using var csv = new CsvWriter(writer, new CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture));
        
        // Would serialize data to CSV
        writer.WriteLine("Exported data placeholder");
        writer.Flush();
        return memoryStream.ToArray();
    }

    private byte[] ExportToExcel(object data)
    {
        // Would use EPPlus or similar
        return Encoding.UTF8.GetBytes("Excel export placeholder");
    }

    private byte[] ExportToPdf(object data)
    {
        // Would use a PDF library like QuestPDF or PdfSharp
        return Encoding.UTF8.GetBytes("PDF export placeholder");
    }
}