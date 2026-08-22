// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Analytics.Models;

namespace KKBOT.Analytics.Abstractions;

public interface IDashboardService
{
    Task<Dashboard> CreateDashboardAsync(CreateDashboardRequest request, string userId, CancellationToken ct = default);
    Task<Dashboard?> GetDashboardAsync(Guid dashboardId, string userId, CancellationToken ct = default);
    Task<List<Dashboard>> GetDashboardsAsync(string userId, DashboardFilter filter, CancellationToken ct = default);
    Task<Dashboard> UpdateDashboardAsync(Guid dashboardId, UpdateDashboardRequest request, string userId, CancellationToken ct = default);
    Task<bool> DeleteDashboardAsync(Guid dashboardId, string userId, CancellationToken ct = default);
    Task<Dashboard> DuplicateDashboardAsync(Guid dashboardId, string userId, CancellationToken ct = default);
    
    // Widgets
    Task<DashboardWidget> AddWidgetAsync(Guid dashboardId, CreateWidgetRequest request, string userId, CancellationToken ct = default);
    Task<DashboardWidget> UpdateWidgetAsync(Guid widgetId, UpdateWidgetRequest request, string userId, CancellationToken ct = default);
    Task<bool> RemoveWidgetAsync(Guid widgetId, string userId, CancellationToken ct = default);
    Task<bool> ReorderWidgetsAsync(Guid dashboardId, List<WidgetPosition> positions, string userId, CancellationToken ct = default);
    
    // Sharing
    Task<DashboardShare> ShareDashboardAsync(Guid dashboardId, ShareDashboardRequest request, string userId, CancellationToken ct = default);
    Task<bool> UpdateShareAsync(Guid shareId, UpdateShareRequest request, string userId, CancellationToken ct = default);
    Task<bool> RemoveShareAsync(Guid shareId, string userId, CancellationToken ct = default);
    
    // Subscriptions
    Task<DashboardSubscription> CreateSubscriptionAsync(Guid dashboardId, CreateSubscriptionRequest request, string userId, CancellationToken ct = default);
    Task<bool> UpdateSubscriptionAsync(Guid subscriptionId, UpdateSubscriptionRequest request, string userId, CancellationToken ct = default);
    Task<bool> DeleteSubscriptionAsync(Guid subscriptionId, string userId, CancellationToken ct = default);
}

public sealed record CreateDashboardRequest(
    string Name,
    string Description,
    bool IsPublic = false,
    string Tags = "",
    string LayoutJson = "[]",
    string Theme = "light"
);

public sealed record UpdateDashboardRequest(
    string? Name = null,
    string? Description = null,
    bool? IsPublic = null,
    string? Tags = null,
    string? LayoutJson = null,
    string? Theme = null
);

public sealed record DashboardFilter(
    string? Search = null,
    string? Tag = null,
    bool? IsPublic = null,
    bool? IsFavorite = null,
    string? OwnerId = null,
    int Page = 1,
    int PageSize = 20,
    string SortBy = "updatedAt",
    bool SortDescending = true
);

public sealed record CreateWidgetRequest(
    string WidgetType,
    string Title,
    string Description,
    string ConfigJson,
    string QueryJson,
    int PositionX,
    int PositionY,
    int Width,
    int Height,
    Dictionary<string, object> Options
);

public sealed record UpdateWidgetRequest(
    string? Title = null,
    string? Description = null,
    string? ConfigJson = null,
    string? QueryJson = null,
    int? PositionX = null,
    int? PositionY = null,
    int? Width = null,
    int? Height = null,
    bool? IsVisible = null,
    Dictionary<string, object>? Options = null
);

public sealed record WidgetPosition(
    string WidgetId,
    int PositionX,
    int PositionY,
    int Width,
    int Height,
    int ZIndex
);

public sealed record ShareDashboardRequest(
    string SharedWithUserId,
    string? SharedWithRole = null,
    SharePermission Permission = SharePermission.View
);

public sealed record UpdateShareRequest(
    SharePermission? Permission = null
);

public sealed record CreateSubscriptionRequest(
    SubscriptionFrequency Frequency,
    string Format,
    string DeliveryMethod,
    string DeliveryConfigJson
);

public sealed record UpdateSubscriptionRequest(
    SubscriptionFrequency? Frequency = null,
    string? Format = null,
    string? DeliveryMethod = null,
    string? DeliveryConfigJson = null,
    bool? IsActive = null
);

public interface IReportService
{
    Task<Report> CreateReportAsync(CreateReportRequest request, string userId, CancellationToken ct = default);
    Task<Report?> GetReportAsync(Guid reportId, string userId, CancellationToken ct = default);
    Task<List<Report>> GetReportsAsync(string userId, ReportFilter filter, CancellationToken ct = default);
    Task<Report> UpdateReportAsync(Guid reportId, UpdateReportRequest request, string userId, CancellationToken ct = default);
    Task<bool> DeleteReportAsync(Guid reportId, string userId, CancellationToken ct = default);
    
    Task<ReportExecution> ExecuteReportAsync(Guid reportId, ExecuteReportRequest request, string userId, CancellationToken ct = default);
    Task<ReportExecution?> GetExecutionAsync(Guid executionId, string userId, CancellationToken ct = default);
    Task<List<ReportExecution>> GetExecutionHistoryAsync(Guid reportId, ExecutionHistoryFilter filter, CancellationToken ct = default);
    Task<bool> CancelExecutionAsync(Guid executionId, string userId, CancellationToken ct = default);
    Task<byte[]> DownloadReportAsync(Guid executionId, string userId, CancellationToken ct = default);
}

public sealed record CreateReportRequest(
    string Name,
    string Description,
    string Category,
    string TemplateJson,
    string DataSourceJson,
    ReportFormat DefaultFormat,
    bool IsScheduled,
    string CronExpression,
    List<ReportParameter> Parameters
);

public sealed record UpdateReportRequest(
    string? Name = null,
    string? Description = null,
    string? Category = null,
    string? TemplateJson = null,
    string? DataSourceJson = null,
    ReportFormat? DefaultFormat = null,
    bool? IsScheduled = null,
    string? CronExpression = null,
    bool? IsActive = null
);

public sealed record ReportFilter(
    string? Search = null,
    string? Category = null,
    bool? IsScheduled = null,
    bool? IsActive = null,
    int Page = 1,
    int PageSize = 20,
    string SortBy = "updatedAt",
    bool SortDescending = true
);

public sealed record ExecuteReportRequest(
    Dictionary<string, object> Parameters,
    ReportFormat Format
);

public sealed record ExecutionHistoryFilter(
    ExecutionStatus? Status = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Page = 1,
    int PageSize = 20
);

public interface IDataSourceService
{
    Task<DataSource> CreateDataSourceAsync(CreateDataSourceRequest request, string userId, CancellationToken ct = default);
    Task<DataSource?> GetDataSourceAsync(Guid dataSourceId, string userId, CancellationToken ct = default);
    Task<List<DataSource>> GetDataSourcesAsync(string userId, DataSourceFilter filter, CancellationToken ct = default);
    Task<DataSource> UpdateDataSourceAsync(Guid dataSourceId, UpdateDataSourceRequest request, string userId, CancellationToken ct = default);
    Task<bool> DeleteDataSourceAsync(Guid dataSourceId, string userId, CancellationToken ct = default);
    Task<bool> TestConnectionAsync(Guid dataSourceId, string userId, CancellationToken ct = default);
    Task<List<DataSourceTable>> GetTablesAsync(Guid dataSourceId, string userId, CancellationToken ct = default);
    Task<List<DataSourceColumn>> GetColumnsAsync(Guid dataSourceId, string tableName, string userId, CancellationToken ct = default);
    Task<QueryResult> ExecuteQueryAsync(Guid dataSourceId, string query, Dictionary<string, object> parameters, int maxRows, CancellationToken ct = default);
}

public sealed record CreateDataSourceRequest(
    string Name,
    string Description,
    DataSourceType Type,
    string ConnectionString,
    Dictionary<string, object> Options
);

public sealed record UpdateDataSourceRequest(
    string? Name = null,
    string? Description = null,
    string? ConnectionString = null,
    Dictionary<string, object>? Options = null,
    bool? IsActive = null,
    bool? IsShared = null
);

public sealed record DataSourceFilter(
    DataSourceType? Type = null,
    bool? IsActive = null,
    bool? IsShared = null,
    int Page = 1,
    int PageSize = 20
);

public sealed record QueryResult(
    List<Dictionary<string, object>> Rows,
    List<string> Columns,
    int TotalRows,
    TimeSpan Duration,
    string Query
);

public interface IMetricService
{
    Task<MetricDefinition> CreateMetricAsync(CreateMetricRequest request, string userId, CancellationToken ct = default);
    Task<MetricDefinition?> GetMetricAsync(Guid metricId, string userId, CancellationToken ct = default);
    Task<List<MetricDefinition>> GetMetricsAsync(string userId, MetricFilter filter, CancellationToken ct = default);
    Task<MetricDefinition> UpdateMetricAsync(Guid metricId, UpdateMetricRequest request, string userId, CancellationToken ct = default);
    Task<bool> DeleteMetricAsync(Guid metricId, string userId, CancellationToken ct = default);
    
    Task<MetricValue> RecordValueAsync(RecordMetricValueRequest request, CancellationToken ct = default);
    Task<List<MetricValue>> GetValuesAsync(Guid metricId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<AggregatedMetricResult> GetAggregatedAsync(Guid metricId, DateTime from, DateTime to, AggregationFunction aggregation, TimeSpan interval, CancellationToken ct = default);
}

public sealed record CreateMetricRequest(
    string Name,
    string DisplayName,
    string Description,
    string Unit,
    MetricType Type,
    string Category,
    string Tags,
    string QueryJson,
    string DataSourceId,
    int RetentionDays,
    int AggregationIntervalSeconds,
    AggregationFunction Aggregation
);

public sealed record UpdateMetricRequest(
    string? DisplayName = null,
    string? Description = null,
    string? Unit = null,
    string? Category = null,
    string? Tags = null,
    string? QueryJson = null,
    int? RetentionDays = null,
    int? AggregationIntervalSeconds = null,
    AggregationFunction? Aggregation = null,
    bool? IsActive = null
);

public sealed record MetricFilter(
    MetricType? Type = null,
    string? Category = null,
    string[]? Tags = null,
    bool? IsActive = null,
    int Page = 1,
    int PageSize = 20
);

public sealed record RecordMetricValueRequest(
    Guid MetricId,
    double Value,
    DateTime? Timestamp = null,
    Dictionary<string, string> Labels = null,
    double? Min = null,
    double? Max = null,
    long? Count = null,
    double? Sum = null
);

public sealed record AggregatedMetricResult(
    List<MetricValue> Values,
    double Sum,
    double Avg,
    double Min,
    double Max,
    long Count,
    double StdDev
);

public interface IAlertService
{
    Task<AlertRule> CreateAlertRuleAsync(CreateAlertRuleRequest request, string userId, CancellationToken ct = default);
    Task<AlertRule?> GetAlertRuleAsync(Guid ruleId, string userId, CancellationToken ct = default);
    Task<List<AlertRule>> GetAlertRulesAsync(string userId, AlertRuleFilter filter, CancellationToken ct = default);
    Task<AlertRule> UpdateAlertRuleAsync(Guid ruleId, UpdateAlertRuleRequest request, string userId, CancellationToken ct = default);
    Task<bool> DeleteAlertRuleAsync(Guid ruleId, string userId, CancellationToken ct = default);
    Task<bool> TestAlertRuleAsync(Guid ruleId, string userId, CancellationToken ct = default);
    
    Task<List<Alert>> GetAlertsAsync(string userId, AlertFilter filter, CancellationToken ct = default);
    Task<Alert?> GetAlertAsync(Guid alertId, string userId, CancellationToken ct = default);
    Task<Alert> AcknowledgeAlertAsync(Guid alertId, AcknowledgeAlertRequest request, string userId, CancellationToken ct = default);
    Task<bool> ResolveAlertAsync(Guid alertId, string userId, CancellationToken ct = default);
    Task<bool> SilenceAlertAsync(Guid alertId, SilenceAlertRequest request, string userId, CancellationToken ct = default);
}

public sealed record CreateAlertRuleRequest(
    string Name,
    string Description,
    Guid MetricId,
    AlertCondition Condition,
    AlertSeverity Severity,
    List<AlertAction> Actions,
    int EvaluationIntervalSeconds,
    int ForDurationSeconds
);

public sealed record UpdateAlertRuleRequest(
    string? Name = null,
    string? Description = null,
    AlertCondition? Condition = null,
    AlertSeverity? Severity = null,
    List<AlertAction>? Actions = null,
    int? EvaluationIntervalSeconds = null,
    int? ForDurationSeconds = null,
    bool? IsActive = null
);

public sealed record AlertRuleFilter(
    AlertSeverity? Severity = null,
    bool? IsActive = null,
    Guid? MetricId = null,
    int Page = 1,
    int PageSize = 20
);

public sealed record AlertFilter(
    AlertStatus? Status = null,
    AlertSeverity? Severity = null,
    Guid? AlertRuleId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Page = 1,
    int PageSize = 20
);

public sealed record AcknowledgeAlertRequest(
    string Comment
);

public sealed record SilenceAlertRequest(
    TimeSpan Duration,
    string Reason
);

public interface IAnomalyDetectionService
{
    Task<AnomalyDetector> CreateDetectorAsync(CreateDetectorRequest request, string userId, CancellationToken ct = default);
    Task<AnomalyDetector?> GetDetectorAsync(Guid detectorId, string userId, CancellationToken ct = default);
    Task<List<AnomalyDetector>> GetDetectorsAsync(string userId, DetectorFilter filter, CancellationToken ct = default);
    Task<AnomalyDetector> UpdateDetectorAsync(Guid detectorId, UpdateDetectorRequest request, string userId, CancellationToken ct = default);
    Task<bool> DeleteDetectorAsync(Guid detectorId, string userId, CancellationToken ct = default);
    Task<bool> TrainDetectorAsync(Guid detectorId, string userId, CancellationToken ct = default);
    Task<ModelMetrics> EvaluateDetectorAsync(Guid detectorId, string userId, CancellationToken ct = default);
    
    Task<AnomalyResult> DetectAsync(Guid detectorId, double value, Dictionary<string, object> context, CancellationToken ct = default);
    Task<List<AnomalyResult>> DetectBatchAsync(Guid detectorId, List<double> values, List<Dictionary<string, object>> contexts, CancellationToken ct = default);
    Task<List<AnomalyResult>> GetAnomaliesAsync(string userId, AnomalyFilter filter, CancellationToken ct = default);
    Task<AnomalyResult?> GetAnomalyAsync(Guid anomalyId, string userId, CancellationToken ct = default);
    Task<bool> AcknowledgeAnomalyAsync(Guid anomalyId, string userId, CancellationToken ct = default);
}

public sealed record CreateDetectorRequest(
    string Name,
    string Description,
    AnomalyDetectionAlgorithm Algorithm,
    Guid MetricId,
    Dictionary<string, object> Hyperparameters,
    double Sensitivity,
    int TrainingWindowDays,
    int RetrainingIntervalHours
);

public sealed record UpdateDetectorRequest(
    string? Name = null,
    string? Description = null,
    Dictionary<string, object>? Hyperparameters = null,
    double? Sensitivity = null,
    int? TrainingWindowDays = null,
    int? RetrainingIntervalHours = null,
    bool? IsActive = null
);

public sealed record DetectorFilter(
    AnomalyDetectionAlgorithm? Algorithm = null,
    bool? IsTrained = null,
    bool? IsActive = null,
    Guid? MetricId = null,
    int Page = 1,
    int PageSize = 20
);

public sealed record AnomalyFilter(
    bool? IsAnomaly = null,
    AnomalySeverity? Severity = null,
    Guid? DetectorId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    bool? IsAcknowledged = null,
    int Page = 1,
    int PageSize = 20
);

public interface IForecastService
{
    Task<ForecastModel> CreateForecastModelAsync(CreateForecastModelRequest request, string userId, CancellationToken ct = default);
    Task<ForecastModel?> GetForecastModelAsync(Guid modelId, string userId, CancellationToken ct = default);
    Task<List<ForecastModel>> GetForecastModelsAsync(string userId, ForecastModelFilter filter, CancellationToken ct = default);
    Task<ForecastModel> UpdateForecastModelAsync(Guid modelId, UpdateForecastModelRequest request, string userId, CancellationToken ct = default);
    Task<bool> DeleteForecastModelAsync(Guid modelId, string userId, CancellationToken ct = default);
    Task<bool> TrainModelAsync(Guid modelId, string userId, CancellationToken ct = default);
    Task<ModelMetrics> EvaluateModelAsync(Guid modelId, string userId, CancellationToken ct = default);
    
    Task<Forecast> GenerateForecastAsync(Guid modelId, int horizon, CancellationToken ct = default);
    Task<Forecast?> GetForecastAsync(Guid forecastId, string userId, CancellationToken ct = default);
    Task<List<Forecast>> GetForecastsAsync(string userId, ForecastFilter filter, CancellationToken ct = default);
    Task<bool> DeleteForecastAsync(Guid forecastId, string userId, CancellationToken ct = default);
}

public sealed record CreateForecastModelRequest(
    string Name,
    string Description,
    ForecastAlgorithm Algorithm,
    Guid MetricId,
    Dictionary<string, object> Hyperparameters,
    int ForecastHorizon,
    int SeasonalityPeriod,
    double ConfidenceLevel,
    int TrainingWindowDays,
    int RetrainingIntervalHours
);

public sealed record UpdateForecastModelRequest(
    string? Name = null,
    string? Description = null,
    Dictionary<string, object>? Hyperparameters = null,
    int? ForecastHorizon = null,
    int? SeasonalityPeriod = null,
    double? ConfidenceLevel = null,
    int? TrainingWindowDays = null,
    int? RetrainingIntervalHours = null,
    bool? IsActive = null
);

public sealed record ForecastModelFilter(
    ForecastAlgorithm? Algorithm = null,
    bool? IsTrained = null,
    bool? IsActive = null,
    Guid? MetricId = null,
    int Page = 1,
    int PageSize = 20
);

public sealed record ForecastFilter(
    ForecastAlgorithm? Algorithm = null,
    Guid? ModelId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Page = 1,
    int PageSize = 20
);

public interface IKpiService
{
    Task<KpiDefinition> CreateKpiAsync(CreateKpiRequest request, string userId, CancellationToken ct = default);
    Task<KpiDefinition?> GetKpiAsync(Guid kpiId, string userId, CancellationToken ct = default);
    Task<List<KpiDefinition>> GetKpisAsync(string userId, KpiFilter filter, CancellationToken ct = default);
    Task<KpiDefinition> UpdateKpiAsync(Guid kpiId, UpdateKpiRequest request, string userId, CancellationToken ct = default);
    Task<bool> DeleteKpiAsync(Guid kpiId, string userId, CancellationToken ct = default);
    
    Task<KpiValue> GetCurrentValueAsync(Guid kpiId, CancellationToken ct = default);
    Task<List<KpiValue>> GetKpiHistoryAsync(Guid kpiId, DateTime from, DateTime to, CancellationToken ct = default);
}

public sealed record CreateKpiRequest(
    string Name,
    string DisplayName,
    string Description,
    string Category,
    string Unit,
    string FormatString,
    Guid MetricId,
    KpiTarget Target,
    KpiThresholds Thresholds,
    int UpdateFrequencySeconds,
    List<string> Tags
);

public sealed record UpdateKpiRequest(
    string? DisplayName = null,
    string? Description = null,
    string? Category = null,
    string? Unit = null,
    string? FormatString = null,
    KpiTarget? Target = null,
    KpiThresholds? Thresholds = null,
    int? UpdateFrequencySeconds = null,
    List<string>? Tags = null,
    bool? IsActive = null
);

public sealed record KpiFilter(
    string? Category = null,
    bool? IsActive = null,
    string? OwnerId = null,
    int Page = 1,
    int PageSize = 20
);

public interface ICohortAnalysisService
{
    Task<CohortAnalysis> CreateCohortAnalysisAsync(CreateCohortAnalysisRequest request, string userId, CancellationToken ct = default);
    Task<CohortAnalysis?> GetCohortAnalysisAsync(Guid analysisId, string userId, CancellationToken ct = default);
    Task<List<CohortAnalysis>> GetCohortAnalysesAsync(string userId, CohortAnalysisFilter filter, CancellationToken ct = default);
    Task<CohortAnalysis> RunCohortAnalysisAsync(Guid analysisId, string userId, CancellationToken ct = default);
}

public sealed record CreateCohortAnalysisRequest(
    string Name,
    string Description,
    CohortType Type,
    DateTime StartDate,
    DateTime EndDate,
    string CohortDefinition,
    string MetricDefinition,
    int PeriodCount,
    PeriodGranularity Granularity
);

public sealed record CohortAnalysisFilter(
    CohortType? Type = null,
    int Page = 1,
    int PageSize = 20
);

public interface IFunnelAnalysisService
{
    Task<FunnelAnalysis> CreateFunnelAnalysisAsync(CreateFunnelAnalysisRequest request, string userId, CancellationToken ct = default);
    Task<FunnelAnalysis?> GetFunnelAnalysisAsync(Guid analysisId, string userId, CancellationToken ct = default);
    Task<List<FunnelAnalysis>> GetFunnelAnalysesAsync(string userId, FunnelAnalysisFilter filter, CancellationToken ct = default);
    Task<FunnelAnalysis> RunFunnelAnalysisAsync(Guid analysisId, string userId, CancellationToken ct = default);
    Task<FunnelComparison> CompareFunnelsAsync(List<Guid> analysisIds, string userId, CancellationToken ct = default);
}

public sealed record CreateFunnelAnalysisRequest(
    string Name,
    string Description,
    List<FunnelStep> Steps,
    DateTime StartDate,
    DateTime EndDate,
    string FilterJson
);

public sealed record FunnelAnalysisFilter(
    int Page = 1,
    int PageSize = 20
);

public sealed class FunnelComparison
{
    public List<FunnelAnalysis> Funnels { get; set; } = new();
    public Dictionary<string, object> Comparison { get; set; } = new();
}

public interface IExportService
{
    Task<byte[]> ExportDashboardAsync(Guid dashboardId, ExportFormat format, string userId, CancellationToken ct = default);
    Task<byte[]> ExportReportAsync(Guid reportId, ExportFormat format, string userId, CancellationToken ct = default);
    Task<byte[]> ExportMetricsAsync(List<Guid> metricIds, DateTime from, DateTime to, ExportFormat format, string userId, CancellationToken ct = default);
    Task<byte[]> ExportDashboardImageAsync(Guid dashboardId, ImageFormat format, int width, int height, string userId, CancellationToken ct = default);
}

public enum ExportFormat
{
    Csv,
    Json,
    Excel,
    Pdf,
    Png,
    Parquet
}

public enum ImageFormat
{
    Png,
    Jpeg,
    Webp,
    Pdf,
    Svg
}