// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace KKBOT.Marketplace.Server.Models;

// ---------------------------------------------------------------------------
// Shared analytics enums
// ---------------------------------------------------------------------------

public enum TrendPeriod
{
    Last7Days,
    Last30Days,
    Last90Days,
    LastYear,
    AllTime
}

public enum ReportType
{
    Overview,
    Revenue,
    Plugins,
    Publishers,
    Moderation,
    Health,
    UserBehavior,
    Search,
    RealTime,
    Payouts,
    Custom
}

public enum ReportFormat
{
    CSV,
    JSON,
    Excel,
    PDF,
    Html,
    Png,
    Parquet
}

// ---------------------------------------------------------------------------
// Overview & trends
// ---------------------------------------------------------------------------

public sealed class MarketplaceOverview
{
    public long TotalPlugins { get; set; }
    public long PublishedPlugins { get; set; }
    public long TotalPublishers { get; set; }
    public long VerifiedPublishers { get; set; }
    public long TotalDownloads { get; set; }
    public long MonthlyActiveUsers { get; set; }
    public double AverageRating { get; set; }
    public long TotalReviews { get; set; }
    public long PendingModeration { get; set; }
    public double ConversionRate { get; set; }
    public Dictionary<string, long> PluginsByCategory { get; set; } = new();
    public Dictionary<string, long> PluginsByStatus { get; set; } = new();
}

public sealed class MarketplaceTrends
{
    public TrendPeriod Period { get; set; }
    public List<TrendDataPoint> PluginGrowth { get; set; } = new();
    public List<TrendDataPoint> DownloadTrends { get; set; } = new();
    public List<TrendDataPoint> PublisherGrowth { get; set; } = new();
    public List<TrendDataPoint> RevenueTrends { get; set; } = new();
    public List<TrendDataPoint> ReviewTrends { get; set; } = new();
    public List<TrendDataPoint> ActiveUsers { get; set; } = new();
}

public sealed class TrendDataPoint
{
    public System.DateTime Date { get; set; }
    public double Value { get; set; }
    public string? Label { get; set; }
}

// ---------------------------------------------------------------------------
// Plugin analytics
// ---------------------------------------------------------------------------

public sealed class RatingDistribution
{
    public int FiveStar { get; set; }
    public int FourStar { get; set; }
    public int ThreeStar { get; set; }
    public int TwoStar { get; set; }
    public int OneStar { get; set; }
    public double Average { get; set; }
}

public sealed class VersionPerformance
{
    public string Version { get; set; } = string.Empty;
    public long Downloads { get; set; }
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public System.DateTime PublishedAt { get; set; }
    public System.TimeSpan TimeToFirstDownload { get; set; }
}

public sealed class PluginAnalytics
{
    public string PluginId { get; set; } = string.Empty;
    public string PluginName { get; set; } = string.Empty;
    public long TotalDownloads { get; set; }
    public long MonthlyDownloads { get; set; }
    public long WeeklyDownloads { get; set; }
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public RatingDistribution RatingDistribution { get; set; } = new();
    public List<VersionPerformance> VersionPerformance { get; set; } = new();
    public List<TrendDataPoint> DownloadTrends { get; set; } = new();
    public List<TrendDataPoint> RatingTrends { get; set; } = new();
    public Dictionary<string, long> DownloadsByPlatform { get; set; } = new();
    public Dictionary<string, long> DownloadsByRegion { get; set; } = new();
    public double RetentionRate { get; set; }
    public double ChurnRate { get; set; }
    public double AverageTimeToFirstDownload { get; set; }
}

public sealed class PluginAnalyticsSummary
{
    public string PluginId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long TotalDownloads { get; set; }
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public double GrowthRate { get; set; }
}

public sealed class PluginPerformanceSummary
{
    public string PluginId { get; set; } = string.Empty;
    public string PluginName { get; set; } = string.Empty;
    public long Downloads { get; set; }
    public double Rating { get; set; }
    public decimal Revenue { get; set; }
}

// ---------------------------------------------------------------------------
// Publisher analytics
// ---------------------------------------------------------------------------

public sealed class PayoutSummary
{
    public decimal TotalPaid { get; set; }
    public decimal PendingPayouts { get; set; }
    public decimal LastPayoutAmount { get; set; }
    public System.DateTime? LastPayoutDate { get; set; }
    public int CompletedPayouts { get; set; }
    public int FailedPayouts { get; set; }
}

public sealed class PublisherAnalytics
{
    public System.Guid PublisherId { get; set; }
    public string PublisherName { get; set; } = string.Empty;
    public int TotalPlugins { get; set; }
    public long TotalDownloads { get; set; }
    public long MonthlyDownloads { get; set; }
    public double AverageRating { get; set; }
    public decimal GrossRevenue { get; set; }
    public decimal NetRevenue { get; set; }
    public decimal PlatformFees { get; set; }
    public int ActivePlugins { get; set; }
    public List<PluginPerformanceSummary> TopPlugins { get; set; } = new();
    public List<TrendDataPoint> RevenueTrends { get; set; } = new();
    public List<TrendDataPoint> DownloadTrends { get; set; } = new();
    public PayoutSummary PayoutSummary { get; set; } = new();
}

public sealed class PublisherAnalyticsSummary
{
    public System.Guid PublisherId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int TotalPlugins { get; set; }
    public long TotalDownloads { get; set; }
    public decimal Revenue { get; set; }
    public double AverageRating { get; set; }
}

// ---------------------------------------------------------------------------
// User behavior, search, revenue & payout analytics
// ---------------------------------------------------------------------------

public sealed class UserBehaviorMetrics
{
    public long TotalUsers { get; set; }
    public int NewUsersToday { get; set; }
    public int NewUsersThisWeek { get; set; }
    public int NewUsersThisMonth { get; set; }
    public int ActiveUsersToday { get; set; }
    public int ActiveUsersThisWeek { get; set; }
    public int ActiveUsersThisMonth { get; set; }
    public double AverageSessionDurationMinutes { get; set; }
    public double AveragePagesPerSession { get; set; }
    public double BounceRate { get; set; }
    public Dictionary<string, long> UsersByRole { get; set; } = new();
    public Dictionary<string, long> UsersByTenant { get; set; } = new();
    public List<TrendDataPoint> UserRetention { get; set; } = new();
    public Dictionary<string, long> FeatureUsage { get; set; } = new();
}

public sealed class SearchTermMetric
{
    public string Term { get; set; } = string.Empty;
    public long Count { get; set; }
    public double ClickThroughRate { get; set; }
}

public sealed class SearchAnalytics
{
    public long TotalSearches { get; set; }
    public long UniqueSearchTerms { get; set; }
    public double ZeroResultsRate { get; set; }
    public List<SearchTermMetric> TopSearchTerms { get; set; } = new();
    public List<SearchTermMetric> ZeroResultTerms { get; set; } = new();
    public double AverageResultsPerSearch { get; set; }
    public double ClickThroughRate { get; set; }
    public System.TimeSpan AverageTimeToClick { get; set; }
    public Dictionary<string, long> SearchesByCategory { get; set; } = new();
    public Dictionary<string, long> SearchesByFilter { get; set; } = new();
}

public sealed class RevenueAnalytics
{
    public decimal TotalRevenue { get; set; }
    public decimal MonthlyRevenue { get; set; }
    public decimal PlatformFees { get; set; }
    public decimal PublisherPayouts { get; set; }
    public List<TrendDataPoint> RevenueTrends { get; set; } = new();
    public Dictionary<string, decimal> RevenueByCategory { get; set; } = new();
    public Dictionary<string, decimal> RevenueByPlugin { get; set; } = new();
    public decimal AverageRevenuePerPlugin { get; set; }
    public decimal AverageRevenuePerPublisher { get; set; }
    public int PayingUsers { get; set; }
    public double ConversionRate { get; set; }
    public decimal ARPU { get; set; }
    public decimal ARPPU { get; set; }
}

public sealed class PayoutDetail
{
    public System.Guid Id { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public PayoutStatus Status { get; set; }
    public System.DateTime CreatedAt { get; set; }
    public System.DateTime? PaidAt { get; set; }
    public string Period { get; set; } = string.Empty;
}

public sealed class PayoutAnalytics
{
    public System.Guid PublisherId { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal PendingAmount { get; set; }
    public int CompletedPayouts { get; set; }
    public int FailedPayouts { get; set; }
    public double AveragePayoutAmount { get; set; }
    public System.TimeSpan AveragePayoutTime { get; set; }
    public List<TrendDataPoint> PayoutTrends { get; set; } = new();
    public List<PayoutDetail> RecentPayouts { get; set; } = new();
}

// ---------------------------------------------------------------------------
// Platform health, moderation & real-time analytics
// ---------------------------------------------------------------------------

public sealed class ServiceHealth
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double LatencyMs { get; set; }
    public double ErrorRate { get; set; }
    public System.DateTime LastCheck { get; set; }
}

public sealed class IncidentSummary
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public System.DateTime StartedAt { get; set; }
    public System.DateTime? ResolvedAt { get; set; }
}

public sealed class PlatformHealthMetrics
{
    public double ApiUptime { get; set; }
    public double AverageResponseTimeMs { get; set; }
    public double ErrorRate { get; set; }
    public long RequestsPerMinute { get; set; }
    public double CpuUsagePercent { get; set; }
    public double MemoryUsagePercent { get; set; }
    public double DiskUsagePercent { get; set; }
    public long ActiveConnections { get; set; }
    public long QueueDepth { get; set; }
    public Dictionary<string, ServiceHealth> ServiceHealth { get; set; } = new();
    public List<IncidentSummary> RecentIncidents { get; set; } = new();
}

public sealed class ModeratorPerformance
{
    public string ModeratorId { get; set; } = string.Empty;
    public string ModeratorName { get; set; } = string.Empty;
    public int ItemsReviewed { get; set; }
    public int Approved { get; set; }
    public int Rejected { get; set; }
    public System.TimeSpan AverageReviewTime { get; set; }
    public double AccuracyScore { get; set; }
}

public sealed class ModerationAnalytics
{
    public long TotalQueued { get; set; }
    public long Pending { get; set; }
    public long UnderReview { get; set; }
    public long ApprovedToday { get; set; }
    public long RejectedToday { get; set; }
    public long Escalated { get; set; }
    public System.TimeSpan AverageReviewTime { get; set; }
    public double ApprovalRate { get; set; }
    public Dictionary<string, long> ByEntityType { get; set; } = new();
    public Dictionary<string, long> ByPriority { get; set; } = new();
    public List<ModeratorPerformance> ModeratorPerformance { get; set; } = new();
    public List<TrendDataPoint> QueueTrends { get; set; } = new();
}

public sealed class RecentActivity
{
    public string Type { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public System.DateTime Timestamp { get; set; }
    public string? Detail { get; set; }
}

public sealed class RealTimeMetrics
{
    public int ActiveUsersNow { get; set; }
    public long DownloadsLastMinute { get; set; }
    public long SearchesLastMinute { get; set; }
    public int ActiveDownloads { get; set; }
    public long PendingModeration { get; set; }
    public double CurrentErrorRate { get; set; }
    public double CurrentResponseTimeMs { get; set; }
    public Dictionary<string, int> ActiveUsersByPage { get; set; } = new();
    public List<RecentActivity> RecentActivity { get; set; } = new();
}

// ---------------------------------------------------------------------------
// Webhook request DTOs
// ---------------------------------------------------------------------------

public sealed class CreateWebhookRequest
{
    public string Url { get; set; } = string.Empty;
    public string? Secret { get; set; }
    public List<string>? EventTypes { get; set; }
}

public sealed class UpdateWebhookRequest
{
    public string? Url { get; set; }
    public string? Secret { get; set; }
    public List<string>? EventTypes { get; set; }
    public bool? IsActive { get; set; }
}
