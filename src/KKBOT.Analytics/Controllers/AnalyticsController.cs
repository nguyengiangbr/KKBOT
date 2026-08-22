// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Analytics.Abstractions;
using KKBOT.Analytics.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace KKBOT.Analytics.Controllers;

[ApiController]
[Route("api/v1/analytics")]
[Authorize(Policy = "RequirePermission:analytics.view")]
[Produces("application/json")]
[SwaggerTag("Analytics & Business Intelligence")]
public sealed class AnalyticsController : ControllerBase
{
    private readonly IDashboardService _dashboardService;
    private readonly IReportService _reportService;
    private readonly IDataSourceService _dataSourceService;
    private readonly IMetricService _metricService;
    private readonly IAlertService _alertService;
    private readonly IAnomalyDetectionService _anomalyService;
    private readonly IForecastService _forecastService;
    private readonly IKpiService _kpiService;
    private readonly ICohortAnalysisService _cohortService;
    private readonly IFunnelAnalysisService _funnelService;
    private readonly IExportService _exportService;
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(
        IDashboardService dashboardService,
        IReportService reportService,
        IDataSourceService dataSourceService,
        IMetricService metricService,
        IAlertService alertService,
        IAnomalyDetectionService anomalyService,
        IForecastService forecastService,
        IKpiService kpiService,
        ICohortAnalysisService cohortService,
        IFunnelAnalysisService funnelService,
        IExportService exportService,
        ILogger<AnalyticsController> logger)
    {
        _dashboardService = dashboardService;
        _reportService = reportService;
        _dataSourceService = dataSourceService;
        _metricService = metricService;
        _alertService = alertService;
        _anomalyService = anomalyService;
        _forecastService = forecastService;
        _kpiService = kpiService;
        _cohortService = cohortService;
        _funnelService = funnelService;
        _exportService = exportService;
        _logger = logger;
    }

    // Dashboard endpoints
    [HttpGet("dashboards")]
    [ProducesResponseType(typeof(List<Dashboard>), 200)]
    [SwaggerOperation("List dashboards with filtering and pagination")]
    public async Task<ActionResult<List<Dashboard>>> GetDashboards(
        [FromQuery] string? search,
        [FromQuery] string? tag,
        [FromQuery] bool? isPublic,
        [FromQuery] bool? isFavorite,
        [FromQuery] string? ownerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string sortBy = "updatedAt",
        [FromQuery] bool sortDescending = true,
        CancellationToken ct = default)
    {
        var filter = new DashboardFilter(search, tag, isPublic, isFavorite, ownerId, page, pageSize, sortBy, sortDescending);
        var dashboards = await _dashboardService.GetDashboardsAsync(GetCurrentUserId(), filter, ct);
        return Ok(dashboards);
    }

    [HttpGet("dashboards/{dashboardId}")]
    [ProducesResponseType(typeof(Dashboard), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get dashboard by ID")]
    public async Task<ActionResult<Dashboard>> GetDashboard(Guid dashboardId, CancellationToken ct)
    {
        var dashboard = await _dashboardService.GetDashboardAsync(dashboardId, GetCurrentUserId(), ct);
        return dashboard == null ? NotFound() : Ok(dashboard);
    }

    [HttpPost("dashboards")]
    [Authorize(Policy = "RequirePermission:analytics.manage")]
    [ProducesResponseType(typeof(Dashboard), 201)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Create a new dashboard")]
    public async Task<ActionResult<Dashboard>> CreateDashboard([FromBody] CreateDashboardRequest request, CancellationToken ct)
    {
        var dashboard = await _dashboardService.CreateDashboardAsync(request, GetCurrentUserId(), ct);
        return CreatedAtAction(nameof(GetDashboard), new { dashboardId = dashboard.Id }, dashboard);
    }

    [HttpPut("dashboards/{dashboardId}")]
    [Authorize(Policy = "RequirePermission:analytics.manage")]
    [ProducesResponseType(typeof(Dashboard), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(403)]
    [SwaggerOperation("Update dashboard")]
    public async Task<ActionResult<Dashboard>> UpdateDashboard(Guid dashboardId, [FromBody] UpdateDashboardRequest request, CancellationToken ct)
    {
        try
        {
            var dashboard = await _dashboardService.UpdateDashboardAsync(dashboardId, request, GetCurrentUserId(), ct);
            return Ok(dashboard);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpDelete("dashboards/{dashboardId}")]
    [Authorize(Policy = "RequirePermission:analytics.manage")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(403)]
    [SwaggerOperation("Delete a dashboard")]
    public async Task<IActionResult> DeleteDashboard(Guid dashboardId, CancellationToken ct)
    {
        var success = await _dashboardService.DeleteDashboardAsync(dashboardId, GetCurrentUserId(), ct);
        return success ? NoContent() : NotFound();
    }

    [HttpPost("dashboards/{dashboardId}/duplicate")]
    [ProducesResponseType(typeof(Dashboard), 201)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Duplicate a dashboard")]
    public async Task<ActionResult<Dashboard>> DuplicateDashboard(Guid dashboardId, CancellationToken ct)
    {
        var dashboard = await _dashboardService.DuplicateDashboardAsync(dashboardId, GetCurrentUserId(), ct);
        return CreatedAtAction(nameof(GetDashboard), new { dashboardId = dashboard.Id }, dashboard);
    }

    // Widget endpoints
    [HttpPost("dashboards/{dashboardId}/widgets")]
    [Authorize(Policy = "RequirePermission:analytics.manage")]
    [ProducesResponseType(typeof(DashboardWidget), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Add a widget to dashboard")]
    public async Task<ActionResult<DashboardWidget>> AddWidget(Guid dashboardId, [FromBody] CreateWidgetRequest request, CancellationToken ct)
    {
        var widget = await _dashboardService.AddWidgetAsync(dashboardId, request, GetCurrentUserId(), ct);
        return CreatedAtAction(nameof(GetWidget), new { widgetId = widget.Id }, widget);
    }

    [HttpGet("widgets/{widgetId}")]
    [ProducesResponseType(typeof(DashboardWidget), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get widget by ID")]
    public async Task<ActionResult<DashboardWidget>> GetWidget(Guid widgetId, CancellationToken ct)
    {
        // Would need to implement GetWidgetAsync in service
        return NotFound();
    }

    [HttpPut("widgets/{widgetId}")]
    [Authorize(Policy = "RequirePermission:analytics.manage")]
    [ProducesResponseType(typeof(DashboardWidget), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(403)]
    [SwaggerOperation("Update widget")]
    public async Task<ActionResult<DashboardWidget>> UpdateWidget(Guid widgetId, [FromBody] UpdateWidgetRequest request, CancellationToken ct)
    {
        try
        {
            var widget = await _dashboardService.UpdateWidgetAsync(widgetId, request, GetCurrentUserId(), ct);
            return Ok(widget);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpDelete("widgets/{widgetId}")]
    [Authorize(Policy = "RequirePermission:analytics.manage")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(403)]
    [SwaggerOperation("Remove widget from dashboard")]
    public async Task<IActionResult> RemoveWidget(Guid widgetId, CancellationToken ct)
    {
        var success = await _dashboardService.RemoveWidgetAsync(widgetId, GetCurrentUserId(), ct);
        return success ? NoContent() : NotFound();
    }

    [HttpPost("dashboards/{dashboardId}/widgets/reorder")]
    [Authorize(Policy = "RequirePermission:analytics.manage")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(403)]
    [SwaggerOperation("Reorder widgets")]
    public async Task<IActionResult> ReorderWidgets(Guid dashboardId, [FromBody] List<WidgetPosition> positions, CancellationToken ct)
    {
        var success = await _dashboardService.ReorderWidgetsAsync(dashboardId, positions, GetCurrentUserId(), ct);
        return success ? Ok() : NotFound();
    }

    // Dashboard sharing
    [HttpPost("dashboards/{dashboardId}/shares")]
    [Authorize(Policy = "RequirePermission:analytics.manage")]
    [ProducesResponseType(typeof(DashboardShare), 201)]
    [ProducesResponseType(404)]
    [ProducesResponseType(403)]
    [SwaggerOperation("Share dashboard with user or role")]
    public async Task<ActionResult<DashboardShare>> ShareDashboard(Guid dashboardId, [FromBody] ShareDashboardRequest request, CancellationToken ct)
    {
        var share = await _dashboardService.ShareDashboardAsync(dashboardId, request, GetCurrentUserId(), ct);
        return CreatedAtAction(nameof(GetDashboard), new { dashboardId }, share);
    }

    [HttpPut("shares/{shareId}")]
    [Authorize(Policy = "RequirePermission:analytics.manage")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(403)]
    [SwaggerOperation("Update share permissions")]
    public async Task<IActionResult> UpdateShare(Guid shareId, [FromBody] UpdateShareRequest request, CancellationToken ct)
    {
        var success = await _dashboardService.UpdateShareAsync(shareId, request, GetCurrentUserId(), ct);
        return success ? Ok() : NotFound();
    }

    [HttpDelete("shares/{shareId}")]
    [Authorize(Policy = "RequirePermission:analytics.manage")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(403)]
    [SwaggerOperation("Remove share")]
    public async Task<IActionResult> RemoveShare(Guid shareId, CancellationToken ct)
    {
        var success = await _dashboardService.RemoveShareAsync(shareId, GetCurrentUserId(), ct);
        return success ? NoContent() : NotFound();
    }

    // Dashboard subscriptions
    [HttpPost("dashboards/{dashboardId}/subscriptions")]
    [ProducesResponseType(typeof(DashboardSubscription), 201)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Create dashboard subscription")]
    public async Task<ActionResult<DashboardSubscription>> CreateSubscription(Guid dashboardId, [FromBody] CreateSubscriptionRequest request, CancellationToken ct)
    {
        var subscription = await _dashboardService.CreateSubscriptionAsync(dashboardId, request, GetCurrentUserId(), ct);
        return CreatedAtAction(nameof(GetDashboard), new { dashboardId }, subscription);
    }

    [HttpPut("subscriptions/{subscriptionId}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(403)]
    [SwaggerOperation("Update subscription")]
    public async Task<IActionResult> UpdateSubscription(Guid subscriptionId, [FromBody] UpdateSubscriptionRequest request, CancellationToken ct)
    {
        var success = await _dashboardService.UpdateSubscriptionAsync(subscriptionId, request, GetCurrentUserId(), ct);
        return success ? Ok() : NotFound();
    }

    [HttpDelete("subscriptions/{subscriptionId}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(403)]
    [SwaggerOperation("Delete subscription")]
    public async Task<IActionResult> DeleteSubscription(Guid subscriptionId, CancellationToken ct)
    {
        var success = await _dashboardService.DeleteSubscriptionAsync(subscriptionId, GetCurrentUserId(), ct);
        return success ? NoContent() : NotFound();
    }

    // Reports
    [HttpGet("reports")]
    [ProducesResponseType(typeof(List<Report>), 200)]
    [SwaggerOperation("List reports")]
    public async Task<ActionResult<List<Report>>> GetReports(
        [FromQuery] string? search,
        [FromQuery] string? category,
        [FromQuery] bool? isScheduled,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string sortBy = "updatedAt",
        [FromQuery] bool sortDescending = true,
        CancellationToken ct = default)
    {
        var filter = new ReportFilter(search, category, isScheduled, isActive, page, pageSize, sortBy, sortDescending);
        var reports = await _reportService.GetReportsAsync(GetCurrentUserId(), filter, ct);
        return Ok(reports);
    }

    [HttpGet("reports/{reportId}")]
    [ProducesResponseType(typeof(Report), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get report by ID")]
    public async Task<ActionResult<Report>> GetReport(Guid reportId, CancellationToken ct)
    {
        var report = await _reportService.GetReportAsync(reportId, GetCurrentUserId(), ct);
        return report == null ? NotFound() : Ok(report);
    }

    [HttpPost("reports")]
    [Authorize(Policy = "RequirePermission:analytics.manage")]
    [ProducesResponseType(typeof(Report), 201)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Create a new report")]
    public async Task<ActionResult<Report>> CreateReport([FromBody] CreateReportRequest request, CancellationToken ct)
    {
        var report = await _reportService.CreateReportAsync(request, GetCurrentUserId(), ct);
        return CreatedAtAction(nameof(GetReport), new { reportId = report.Id }, report);
    }

    [HttpPost("reports/{reportId}/execute")]
    [ProducesResponseType(typeof(ReportExecution), 201)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Execute a report")]
    public async Task<ActionResult<ReportExecution>> ExecuteReport(Guid reportId, [FromBody] ExecuteReportRequest request, CancellationToken ct)
    {
        var execution = await _reportService.ExecuteReportAsync(reportId, request, GetCurrentUserId(), ct);
        return CreatedAtAction(nameof(GetExecution), new { executionId = execution.Id }, execution);
    }

    [HttpGet("reports/{reportId}/executions")]
    [ProducesResponseType(typeof(List<ReportExecution>), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("List report executions")]
    public async Task<ActionResult<List<ReportExecution>>> GetExecutionHistory(
        Guid reportId,
        [FromQuery] ExecutionStatus? status,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var filter = new ExecutionHistoryFilter(status, fromDate, toDate, page, pageSize);
        var executions = await _reportService.GetExecutionHistoryAsync(reportId, filter, ct);
        return Ok(executions);
    }

    [HttpGet("executions/{executionId}")]
    [ProducesResponseType(typeof(ReportExecution), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get report execution")]
    public async Task<ActionResult<ReportExecution>> GetExecution(Guid executionId, CancellationToken ct)
    {
        var execution = await _reportService.GetExecutionAsync(executionId, GetCurrentUserId(), ct);
        return execution == null ? NotFound() : Ok(execution);
    }

    [HttpPost("executions/{executionId}/cancel")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Cancel report execution")]
    public async Task<IActionResult> CancelExecution(Guid executionId, CancellationToken ct)
    {
        var success = await _reportService.CancelExecutionAsync(executionId, GetCurrentUserId(), ct);
        return success ? Ok(new { message = "Execution cancelled" }) : NotFound();
    }

    [HttpGet("executions/{executionId}/download")]
    [ProducesResponseType(typeof(FileContentResult), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Download report output")]
    public async Task<IActionResult> DownloadReport(Guid executionId, CancellationToken ct)
    {
        var bytes = await _reportService.DownloadReportAsync(executionId, GetCurrentUserId(), ct);
        var execution = await _reportService.GetExecutionAsync(executionId, GetCurrentUserId(), ct);
        var fileName = $"report-{execution?.Report?.Name ?? "report"}-{DateTime.UtcNow:yyyyMMdd}.pdf";
        return File(bytes, "application/pdf", fileName);
    }

    // Metrics
    [HttpGet("metrics")]
    [ProducesResponseType(typeof(List<MetricDefinition>), 200)]
    [SwaggerOperation("List metrics")]
    public async Task<ActionResult<List<MetricDefinition>>> GetMetrics(
        [FromQuery] MetricType? type,
        [FromQuery] string? category,
        [FromQuery] string[]? tags,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var filter = new MetricFilter(type, category, tags, isActive, page, pageSize);
        var metrics = await _metricService.GetMetricsAsync(GetCurrentUserId(), filter, ct);
        return Ok(metrics);
    }

    [HttpPost("metrics")]
    [Authorize(Policy = "RequirePermission:analytics.manage")]
    [ProducesResponseType(typeof(MetricDefinition), 201)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Create a metric")]
    public async Task<ActionResult<MetricDefinition>> CreateMetric([FromBody] CreateMetricRequest request, CancellationToken ct)
    {
        var metric = await _metricService.CreateMetricAsync(request, GetCurrentUserId(), ct);
        return CreatedAtAction(nameof(GetMetric), new { metricId = metric.Id }, metric);
    }

    [HttpGet("metrics/{metricId}")]
    [ProducesResponseType(typeof(MetricDefinition), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get metric by ID")]
    public async Task<ActionResult<MetricDefinition>> GetMetric(Guid metricId, CancellationToken ct)
    {
        var metric = await _metricService.GetMetricAsync(metricId, GetCurrentUserId(), ct);
        return metric == null ? NotFound() : Ok(metric);
    }

    [HttpPost("metrics/{metricId}/values")]
    [ProducesResponseType(typeof(MetricValue), 201)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Record a metric value")]
    public async Task<ActionResult<MetricValue>> RecordMetricValue(Guid metricId, [FromBody] RecordMetricValueRequest request, CancellationToken ct)
    {
        var value = await _metricService.RecordValueAsync(request with { MetricId = metricId }, ct);
        return CreatedAtAction(nameof(GetMetricValues), new { metricId }, value);
    }

    [HttpGet("metrics/{metricId}/values")]
    [ProducesResponseType(typeof(List<MetricValue>), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get metric values")]
    public async Task<ActionResult<List<MetricValue>>> GetMetricValues(
        Guid metricId,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        CancellationToken ct = default)
    {
        var values = await _metricService.GetValuesAsync(metricId, from, to, ct);
        return Ok(values);
    }

    [HttpGet("metrics/{metricId}/aggregated")]
    [ProducesResponseType(typeof(AggregatedMetricResult), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get aggregated metric values")]
    public async Task<ActionResult<AggregatedMetricResult>> GetAggregatedMetrics(
        Guid metricId,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] AggregationFunction aggregation = AggregationFunction.Avg,
        [FromQuery] int intervalSeconds = 60,
        CancellationToken ct = default)
    {
        var result = await _metricService.GetAggregatedAsync(metricId, from, to, aggregation, TimeSpan.FromSeconds(intervalSeconds), ct);
        return Ok(result);
    }

    // Alerts
    [HttpGet("alerts")]
    [ProducesResponseType(typeof(List<Alert>), 200)]
    [SwaggerOperation("List alerts")]
    public async Task<ActionResult<List<Alert>>> GetAlerts(
        [FromQuery] AlertStatus? status,
        [FromQuery] AlertSeverity? severity,
        [FromQuery] Guid? alertRuleId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var filter = new AlertFilter(status, severity, alertRuleId, fromDate, toDate, page, pageSize);
        var alerts = await _alertService.GetAlertsAsync(GetCurrentUserId(), filter, ct);
        return Ok(alerts);
    }

    [HttpPost("alert-rules")]
    [Authorize(Policy = "RequirePermission:analytics.manage")]
    [ProducesResponseType(typeof(AlertRule), 201)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Create alert rule")]
    public async Task<ActionResult<AlertRule>> CreateAlertRule([FromBody] CreateAlertRuleRequest request, CancellationToken ct)
    {
        var rule = await _alertService.CreateAlertRuleAsync(request, GetCurrentUserId(), ct);
        return CreatedAtAction(nameof(GetAlertRule), new { ruleId = rule.Id }, rule);
    }

    [HttpGet("alert-rules/{ruleId}")]
    [ProducesResponseType(typeof(AlertRule), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get alert rule")]
    public async Task<ActionResult<AlertRule>> GetAlertRule(Guid ruleId, CancellationToken ct)
    {
        var rule = await _alertService.GetAlertRuleAsync(ruleId, GetCurrentUserId(), ct);
        return rule == null ? NotFound() : Ok(rule);
    }

    [HttpPost("alerts/{alertId}/acknowledge")]
    [ProducesResponseType(typeof(Alert), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Acknowledge alert")]
    public async Task<ActionResult<Alert>> AcknowledgeAlert(Guid alertId, [FromBody] AcknowledgeAlertRequest request, CancellationToken ct)
    {
        var alert = await _alertService.AcknowledgeAlertAsync(alertId, request, GetCurrentUserId(), ct);
        return Ok(alert);
    }

    [HttpPost("alerts/{alertId}/resolve")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Resolve alert")]
    public async Task<IActionResult> ResolveAlert(Guid alertId, CancellationToken ct)
    {
        var success = await _alertService.ResolveAlertAsync(alertId, GetCurrentUserId(), ct);
        return success ? Ok(new { message = "Alert resolved" }) : NotFound();
    }

    // Anomaly Detection
    [HttpPost("anomaly/detectors")]
    [Authorize(Policy = "RequirePermission:analytics.manage")]
    [ProducesResponseType(typeof(AnomalyDetector), 201)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Create anomaly detector")]
    public async Task<ActionResult<AnomalyDetector>> CreateDetector([FromBody] CreateDetectorRequest request, CancellationToken ct)
    {
        var detector = await _anomalyService.CreateDetectorAsync(request, GetCurrentUserId(), ct);
        return CreatedAtAction(nameof(GetDetector), new { detectorId = detector.Id }, detector);
    }

    [HttpGet("anomaly/detectors/{detectorId}")]
    [ProducesResponseType(typeof(AnomalyDetector), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get anomaly detector")]
    public async Task<ActionResult<AnomalyDetector>> GetDetector(Guid detectorId, CancellationToken ct)
    {
        var detector = await _anomalyService.GetDetectorAsync(detectorId, GetCurrentUserId(), ct);
        return detector == null ? NotFound() : Ok(detector);
    }

    [HttpPost("anomaly/detectors/{detectorId}/train")]
    [Authorize(Policy = "RequirePermission:analytics.manage")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Train anomaly detector")]
    public async Task<IActionResult> TrainDetector(Guid detectorId, CancellationToken ct)
    {
        var success = await _anomalyService.TrainDetectorAsync(detectorId, GetCurrentUserId(), ct);
        return success ? Ok(new { message = "Training started" }) : NotFound();
    }

    [HttpPost("anomaly/detect")]
    [ProducesResponseType(typeof(AnomalyResult), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Detect anomaly for a value")]
    public async Task<ActionResult<AnomalyResult>> DetectAnomaly(Guid detectorId, [FromBody] DetectAnomalyRequest request, CancellationToken ct)
    {
        var result = await _anomalyService.DetectAsync(detectorId, request.Value, request.Context, ct);
        return Ok(result);
    }

    [HttpGet("anomaly/detectors/{detectorId}/anomalies")]
    [ProducesResponseType(typeof(List<AnomalyResult>), 200)]
    [SwaggerOperation("Get anomalies for detector")]
    public async Task<ActionResult<List<AnomalyResult>>> GetAnomalies(
        Guid detectorId,
        [FromQuery] bool? isAnomaly,
        [FromQuery] AnomalySeverity? severity,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] bool? isAcknowledged,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var filter = new AnomalyFilter(isAnomaly, severity, detectorId, null, fromDate, toDate, isAcknowledged, page, pageSize);
        var anomalies = await _anomalyService.GetAnomaliesAsync(GetCurrentUserId(), filter, ct);
        return Ok(anomalies);
    }

    // Forecasting
    [HttpPost("forecast/models")]
    [Authorize(Policy = "RequirePermission:analytics.manage")]
    [ProducesResponseType(typeof(ForecastModel), 201)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Create forecast model")]
    public async Task<ActionResult<ForecastModel>> CreateForecastModel([FromBody] CreateForecastModelRequest request, CancellationToken ct)
    {
        var model = await _forecastService.CreateForecastModelAsync(request, GetCurrentUserId(), ct);
        return CreatedAtAction(nameof(GetForecastModel), new { modelId = model.Id }, model);
    }

    [HttpPost("forecast/models/{modelId}/train")]
    [Authorize(Policy = "RequirePermission:analytics.manage")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Train forecast model")]
    public async Task<IActionResult> TrainForecastModel(Guid modelId, CancellationToken ct)
    {
        var success = await _forecastService.TrainModelAsync(modelId, GetCurrentUserId(), ct);
        return success ? Ok(new { message = "Training started" }) : NotFound();
    }

    [HttpPost("forecast/models/{modelId}/forecast")]
    [ProducesResponseType(typeof(Forecast), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Generate forecast")]
    public async Task<ActionResult<Forecast>> GenerateForecast(Guid modelId, [FromBody] GenerateForecastRequest request, CancellationToken ct)
    {
        var forecast = await _forecastService.GenerateForecastAsync(modelId, request.Horizon, ct);
        return Ok(forecast);
    }

    // KPIs
    [HttpGet("kpis")]
    [ProducesResponseType(typeof(List<KpiDefinition>), 200)]
    [SwaggerOperation("List KPIs")]
    public async Task<ActionResult<List<KpiDefinition>>> GetKpis(
        [FromQuery] string? category,
        [FromQuery] bool? isActive,
        [FromQuery] string? ownerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var filter = new KpiFilter(category, isActive, ownerId, page, pageSize);
        var kpis = await _kpiService.GetKpisAsync(GetCurrentUserId(), filter, ct);
        return Ok(kpis);
    }

    [HttpGet("kpis/{kpiId}/value")]
    [ProducesResponseType(typeof(KpiValue), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get current KPI value")]
    public async Task<ActionResult<KpiValue>> GetKpiValue(Guid kpiId, CancellationToken ct)
    {
        var value = await _kpiService.GetCurrentValueAsync(kpiId, ct);
        return Ok(value);
    }

    // Cohort Analysis
    [HttpPost("cohorts")]
    [Authorize(Policy = "RequirePermission:analytics.manage")]
    [ProducesResponseType(typeof(CohortAnalysis), 201)]
    [SwaggerOperation("Create cohort analysis")]
    public async Task<ActionResult<CohortAnalysis>> CreateCohort([FromBody] CreateCohortAnalysisRequest request, CancellationToken ct)
    {
        var analysis = await _cohortService.CreateCohortAnalysisAsync(request, GetCurrentUserId(), ct);
        return CreatedAtAction(nameof(GetCohort), new { analysisId = analysis.Id }, analysis);
    }

    [HttpPost("cohorts/{analysisId}/run")]
    [ProducesResponseType(typeof(CohortAnalysis), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Run cohort analysis")]
    public async Task<ActionResult<CohortAnalysis>> RunCohort(Guid analysisId, CancellationToken ct)
    {
        var analysis = await _cohortService.RunCohortAnalysisAsync(analysisId, GetCurrentUserId(), ct);
        return Ok(analysis);
    }

    // Funnel Analysis
    [HttpPost("funnels")]
    [Authorize(Policy = "RequirePermission:analytics.manage")]
    [ProducesResponseType(typeof(FunnelAnalysis), 201)]
    [SwaggerOperation("Create funnel analysis")]
    public async Task<ActionResult<FunnelAnalysis>> CreateFunnel([FromBody] CreateFunnelAnalysisRequest request, CancellationToken ct)
    {
        var funnel = await _funnelService.CreateFunnelAnalysisAsync(request, GetCurrentUserId(), ct);
        return CreatedAtAction(nameof(GetFunnel), new { analysisId = funnel.Id }, funnel);
    }

    [HttpPost("funnels/{analysisId}/run")]
    [ProducesResponseType(typeof(FunnelAnalysis), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Run funnel analysis")]
    public async Task<ActionResult<FunnelAnalysis>> RunFunnel(Guid analysisId, CancellationToken ct)
    {
        var analysis = await _funnelService.RunFunnelAnalysisAsync(analysisId, GetCurrentUserId(), ct);
        return Ok(analysis);
    }

    [HttpPost("funnels/compare")]
    [ProducesResponseType(typeof(FunnelComparison), 200)]
    [SwaggerOperation("Compare multiple funnels")]
    public async Task<ActionResult<FunnelComparison>> CompareFunnels([FromBody] CompareFunnelsRequest request, CancellationToken ct)
    {
        var comparison = await _funnelService.CompareFunnelsAsync(request.AnalysisIds, GetCurrentUserId(), ct);
        return Ok(comparison);
    }

    // Export
    [HttpGet("export/dashboard/{dashboardId}")]
    [ProducesResponseType(typeof(FileContentResult), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Export dashboard")]
    public async Task<IActionResult> ExportDashboard(
        Guid dashboardId,
        [FromQuery] ExportFormat format = ExportFormat.Csv,
        CancellationToken ct = default)
    {
        var bytes = await _exportService.ExportDashboardAsync(dashboardId, format, GetCurrentUserId(), ct);
        var contentType = format switch
        {
            ExportFormat.Csv => "text/csv",
            ExportFormat.Json => "application/json",
            ExportFormat.Excel => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ExportFormat.Pdf => "application/pdf",
            ExportFormat.Parquet => "application/parquet",
            _ => "application/octet-stream"
        };
        return File(bytes, contentType, $"dashboard-{dashboardId}.{format.ToString().ToLower()}");
    }

    [HttpGet("export/report/{reportId}")]
    [ProducesResponseType(typeof(FileContentResult), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Export report")]
    public async Task<IActionResult> ExportReport(
        Guid reportId,
        [FromQuery] ExportFormat format = ExportFormat.Csv,
        CancellationToken ct = default)
    {
        var bytes = await _exportService.ExportReportAsync(reportId, format, GetCurrentUserId(), ct);
        var contentType = format switch
        {
            ExportFormat.Csv => "text/csv",
            ExportFormat.Json => "application/json",
            ExportFormat.Excel => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ExportFormat.Pdf => "application/pdf",
            ExportFormat.Parquet => "application/parquet",
            _ => "application/octet-stream"
        };
        return File(bytes, contentType, $"report-{reportId}.{format.ToString().ToLower()}");
    }

    [HttpGet("export/metrics")]
    [ProducesResponseType(typeof(FileContentResult), 200)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Export metrics data")]
    public async Task<IActionResult> ExportMetrics(
        [FromQuery] List<Guid> metricIds,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] ExportFormat format = ExportFormat.Csv,
        CancellationToken ct = default)
    {
        var bytes = await _exportService.ExportMetricsAsync(metricIds, from, to, format, GetCurrentUserId(), ct);
        var contentType = format switch
        {
            ExportFormat.Csv => "text/csv",
            ExportFormat.Json => "application/json",
            ExportFormat.Excel => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ExportFormat.Pdf => "application/pdf",
            ExportFormat.Parquet => "application/parquet",
            _ => "application/octet-stream"
        };
        return File(bytes, contentType, $"metrics-{DateTime.UtcNow:yyyyMMdd}.{format.ToString().ToLower()}");
    }

    private string GetCurrentUserId() => User.FindFirst("sub")?.Value ?? "system";
}