// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Marketplace.Server.Models;
using KKBOT.Marketplace.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace KKBOT.Marketplace.Server.Controllers;

[ApiController]
[Route("api/v1/analytics")]
[Authorize(Policy = "RequirePermission:analytics.view")]
[Produces("application/json")]
[SwaggerTag("Analytics & Reporting")]
public sealed class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analytics;
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(IAnalyticsService analytics, ILogger<AnalyticsController> logger)
    {
        _analytics = analytics;
        _logger = logger;
    }

    [HttpGet("overview")]
    [ProducesResponseType(typeof(MarketplaceOverview), 200)]
    [SwaggerOperation("Get marketplace overview")]
    public async Task<ActionResult<MarketplaceOverview>> GetOverview(CancellationToken ct)
    {
        var overview = await _analytics.GetOverviewAsync(ct);
        return Ok(overview);
    }

    [HttpGet("trends")]
    [ProducesResponseType(typeof(MarketplaceTrends), 200)]
    [SwaggerOperation("Get marketplace trends")]
    public async Task<ActionResult<MarketplaceTrends>> GetTrends(
        [FromQuery] TrendPeriod period = TrendPeriod.Last30Days,
        CancellationToken ct = default)
    {
        var trends = await _analytics.GetTrendsAsync(period, ct);
        return Ok(trends);
    }

    [HttpGet("plugins/top")]
    [ProducesResponseType(typeof(IEnumerable<PluginAnalyticsSummary>), 200)]
    [SwaggerOperation("Get top plugins by downloads/rating")]
    public async Task<ActionResult<IEnumerable<PluginAnalyticsSummary>>> GetTopPlugins(
        [FromQuery] int limit = 10,
        [FromQuery] string sortBy = "downloads",
        CancellationToken ct = default)
    {
        // Would implement actual service method
        return Ok(new List<PluginAnalyticsSummary>());
    }

    [HttpGet("plugins/{pluginId}")]
    [ProducesResponseType(typeof(PluginAnalytics), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get detailed analytics for a specific plugin")]
    public async Task<ActionResult<PluginAnalytics>> GetPluginAnalytics(string pluginId, CancellationToken ct)
    {
        try
        {
            var analytics = await _analytics.GetPluginAnalyticsAsync(pluginId, ct);
            return Ok(analytics);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("publishers/top")]
    [ProducesResponseType(typeof(IEnumerable<PublisherAnalyticsSummary>), 200)]
    [SwaggerOperation("Get top publishers by revenue/downloads")]
    public async Task<ActionResult<IEnumerable<PublisherAnalyticsSummary>>> GetTopPublishers(
        [FromQuery] int limit = 10,
        [FromQuery] string sortBy = "revenue",
        CancellationToken ct = default)
    {
        // Would implement actual service method
        return Ok(new List<PublisherAnalyticsSummary>());
    }

    [HttpGet("publishers/{publisherId}")]
    [ProducesResponseType(typeof(PublisherAnalytics), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get detailed analytics for a specific publisher")]
    public async Task<ActionResult<PublisherAnalytics>> GetPublisherAnalytics(Guid publisherId, CancellationToken ct)
    {
        try
        {
            var analytics = await _analytics.GetPublisherAnalyticsAsync(publisherId, ct);
            return Ok(analytics);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("revenue")]
    [ProducesResponseType(typeof(RevenueAnalytics), 200)]
    [SwaggerOperation("Get revenue analytics")]
    public async Task<ActionResult<RevenueAnalytics>> GetRevenueAnalytics(CancellationToken ct)
    {
        var analytics = await _analytics.GetRevenueAnalyticsAsync(ct);
        return Ok(analytics);
    }

    [HttpGet("health")]
    [ProducesResponseType(typeof(PlatformHealthMetrics), 200)]
    [SwaggerOperation("Get platform health metrics")]
    public async Task<ActionResult<PlatformHealthMetrics>> GetHealthMetrics(CancellationToken ct)
    {
        var health = await _analytics.GetPlatformHealthMetricsAsync(ct);
        return Ok(health);
    }

    [HttpGet("moderation")]
    [ProducesResponseType(typeof(ModerationAnalytics), 200)]
    [SwaggerOperation("Get moderation analytics")]
    public async Task<ActionResult<ModerationAnalytics>> GetModerationAnalytics(CancellationToken ct)
    {
        var analytics = await _analytics.GetModerationAnalyticsAsync(ct);
        return Ok(analytics);
    }

    [HttpGet("user-behavior")]
    [ProducesResponseType(typeof(UserBehaviorMetrics), 200)]
    [SwaggerOperation("Get user behavior metrics")]
    public async Task<ActionResult<UserBehaviorMetrics>> GetUserBehavior(CancellationToken ct)
    {
        var metrics = await _analytics.GetUserBehaviorMetricsAsync(ct);
        return Ok(metrics);
    }

    [HttpGet("search")]
    [ProducesResponseType(typeof(SearchAnalytics), 200)]
    [SwaggerOperation("Get search analytics")]
    public async Task<ActionResult<SearchAnalytics>> GetSearchAnalytics(CancellationToken ct)
    {
        var analytics = await _analytics.GetSearchAnalyticsAsync(ct);
        return Ok(analytics);
    }

    [HttpGet("real-time")]
    [ProducesResponseType(typeof(RealTimeMetrics), 200)]
    [SwaggerOperation("Get real-time metrics")]
    public async Task<ActionResult<RealTimeMetrics>> GetRealTimeMetrics(CancellationToken ct)
    {
        var metrics = await _analytics.GetRealTimeMetricsAsync(ct);
        return Ok(metrics);
    }

    [HttpGet("export")]
    [ProducesResponseType(typeof(FileContentResult), 200)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Export analytics report")]
    public async Task<IActionResult> ExportReport(
        [FromQuery] ReportType type = ReportType.Overview,
        [FromQuery] ReportFormat format = ReportFormat.CSV,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate = to ?? DateTime.UtcNow;

        var data = await _analytics.ExportAnalyticsReportAsync(type, format, fromDate, toDate, ct);

        var contentType = format switch
        {
            ReportFormat.CSV => "text/csv",
            ReportFormat.JSON => "application/json",
            ReportFormat.Excel => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ReportFormat.PDF => "application/pdf",
            _ => "application/octet-stream"
        };

        var fileName = $"kkbot-analytics-{type}-{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}.{format.ToString().ToLower()}";

        return File(data, contentType, fileName);
    }
}