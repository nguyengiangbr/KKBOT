// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KKBOT.Analytics.Models;

/// <summary>
/// BI Dashboard configuration and metadata
/// </summary>
public sealed class Dashboard
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public bool IsPublic { get; set; } = false;
    public string LayoutJson { get; set; } = "[]";
    public string Theme { get; set; } = "light";
    public string Tags { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastViewedAt { get; set; }
    public int ViewCount { get; set; } = 0;
    public bool IsFavorite { get; set; } = false;

    // Navigation
    public ICollection<DashboardWidget> Widgets { get; set; } = new List<DashboardWidget>();
    public ICollection<DashboardShare> Shares { get; set; } = new List<DashboardShare>();
    public ICollection<DashboardSubscription> Subscriptions { get; set; } = new List<DashboardSubscription>();
}

public sealed class DashboardWidget
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DashboardId { get; set; }
    public Dashboard Dashboard { get; set; } = null!;
    public string WidgetType { get; set; } = string.Empty; // chart, table, metric, text, iframe, map
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ConfigJson { get; set; } = "{}";
    public string QueryJson { get; set; } = "{}";
    public int PositionX { get; set; } = 0;
    public int PositionY { get; set; } = 0;
    public int Width { get; set; } = 6;
    public int Height { get; set; } = 4;
    public int ZIndex { get; set; } = 0;
    public bool IsVisible { get; set; } = true;
    public Dictionary<string, object> Options { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class DashboardShare
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DashboardId { get; set; }
    public Dashboard Dashboard { get; set; } = null!;
    public string SharedWithUserId { get; set; } = string.Empty;
    public string SharedWithRole { get; set; } = string.Empty;
    public SharePermission Permission { get; set; } = SharePermission.View;
    public DateTime SharedAt { get; set; } = DateTime.UtcNow;
    public string SharedByUserId { get; set; } = string.Empty;
}

public enum SharePermission
{
    View,
    Edit,
    Admin
}

public sealed class DashboardSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DashboardId { get; set; }
    public Dashboard Dashboard { get; set; } = null!;
    public string SubscriberId { get; set; } = string.Empty;
    public SubscriptionFrequency Frequency { get; set; } = SubscriptionFrequency.Daily;
    public string Format { get; set; } = "pdf"; // pdf, csv, excel, image
    public string DeliveryMethod { get; set; } = "email"; // email, webhook, slack
    public string DeliveryConfigJson { get; set; } = "{}";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastSentAt { get; set; }
    public DateTime? NextSendAt { get; set; }
}

public enum SubscriptionFrequency
{
    Hourly,
    Daily,
    Weekly,
    Monthly,
    Custom
}

/// <summary>
/// Report definitions and execution
/// </summary>
public sealed class Report
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string TemplateJson { get; set; } = "{}";
    public string DataSourceJson { get; set; } = "{}";
    public ReportFormat DefaultFormat { get; set; } = ReportFormat.Pdf;
    public bool IsScheduled { get; set; } = false;
    public string CronExpression { get; set; } = string.Empty;
    public TimeZoneInfo TimeZone { get; set; } = TimeZoneInfo.Utc;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastRunAt { get; set; }
    public DateTime? NextRunAt { get; set; }

    // Navigation
    public ICollection<ReportExecution> Executions { get; set; } = new List<ReportExecution>();
    public ICollection<ReportParameter> Parameters { get; set; } = new List<ReportParameter>();
}

public sealed class ReportParameter
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReportId { get; set; }
    public Report Report { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string DataType { get; set; } = "string";
    public bool IsRequired { get; set; } = false;
    public string DefaultValue { get; set; } = string.Empty;
    public string ValidationRegex { get; set; } = string.Empty;
    public string OptionsJson { get; set; } = "[]";
    public int Order { get; set; } = 0;
}

public sealed class ReportExecution
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReportId { get; set; }
    public Report Report { get; set; } = null!;
    public string ExecutedByUserId { get; set; } = string.Empty;
    public ExecutionStatus Status { get; set; } = ExecutionStatus.Pending;
    public ReportFormat Format { get; set; } = ReportFormat.Pdf;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public TimeSpan Duration { get; set; }
    public string OutputPath { get; set; } = string.Empty;
    public long OutputSizeBytes { get; set; } = 0;
    public int RowCount { get; set; } = 0;
    public string ErrorMessage { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public enum ExecutionStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}

public enum ReportFormat
{
    Pdf,
    Excel,
    Csv,
    Json,
    Html,
    Png,
    Parquet
}

/// <summary>
/// Data source connections and metadata
/// </summary>
public sealed class DataSource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DataSourceType Type { get; set; } = DataSourceType.Sql;
    public string ConnectionString { get; set; } = string.Empty; // Encrypted
    public string TenantId { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsShared { get; set; } = false;
    public Dictionary<string, object> Options { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastTestedAt { get; set; }
    public bool LastTestSuccess { get; set; } = false;
    public string LastTestMessage { get; set; } = string.Empty;

    // Navigation
    public ICollection<DataSourceTable> Tables { get; set; } = new List<DataSourceTable>();
}

public enum DataSourceType
{
    Sql,
    PostgreSql,
    MySql,
    Sqlite,
    MongoDb,
    Elasticsearch,
    Prometheus,
    InfluxDb,
    ClickHouse,
    BigQuery,
    Snowflake,
    Redshift,
    Athena,
    Databricks,
    Custom
}

public sealed class DataSourceTable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DataSourceId { get; set; }
    public DataSource DataSource { get; set; } = null!;
    public string SchemaName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public long RowCount { get; set; } = 0;
    public long SizeBytes { get; set; } = 0;
    public DateTime LastAnalyzedAt { get; set; } = DateTime.UtcNow;
    public List<DataSourceColumn> Columns { get; set; } = new();
}

public sealed class DataSourceColumn
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TableId { get; set; }
    public DataSourceTable Table { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsNullable { get; set; } = true;
    public bool IsPrimaryKey { get; set; } = false;
    public bool IsForeignKey { get; set; } = false;
    public string DefaultValue { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public int OrdinalPosition { get; set; } = 0;
}

/// <summary>
/// Metric definitions and time-series data
/// </summary>
public sealed class MetricDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public MetricType Type { get; set; } = MetricType.Gauge;
    public string Category { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public string QueryJson { get; set; } = "{}";
    public string DataSourceId { get; set; } = string.Empty;
    public int RetentionDays { get; set; } = 90;
    public int AggregationIntervalSeconds { get; set; } = 60;
    public AggregationFunction Aggregation { get; set; } = AggregationFunction.Avg;
    public bool IsActive { get; set; } = true;
    public Dictionary<string, object> Tags { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum MetricType
{
    Counter,
    Gauge,
    Histogram,
    Summary,
    Rate
}

public enum AggregationFunction
{
    Sum,
    Avg,
    Min,
    Max,
    Count,
    Percentile50,
    Percentile90,
    Percentile95,
    Percentile99,
    StdDev,
    Variance
}

public sealed class MetricValue
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MetricId { get; set; }
    public MetricDefinition Metric { get; set; } = null!;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public double Value { get; set; } = 0;
    public Dictionary<string, string> Labels { get; set; } = new();
    public double? Min { get; set; }
    public double? Max { get; set; }
    public long? Count { get; set; }
    public double? Sum { get; set; }
}

/// <summary>
/// Alert definitions and management
/// </summary>
public sealed class AlertRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid MetricId { get; set; }
    public MetricDefinition Metric { get; set; } = null!;
    public AlertCondition Condition { get; set; } = new();
    public AlertSeverity Severity { get; set; } = AlertSeverity.Warning;
    public string TenantId { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int EvaluationIntervalSeconds { get; set; } = 60;
    public int ForDurationSeconds { get; set; } = 0;
    public List<AlertAction> Actions { get; set; } = new();
    public Dictionary<string, object> Labels { get; set; } = new();
    public Dictionary<string, object> Annotations { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastTriggeredAt { get; set; }
    public long TriggerCount { get; set; } = 0;
}

public sealed class AlertCondition
{
    public ConditionOperator Operator { get; set; } = ConditionOperator.GreaterThan;
    public double Threshold { get; set; } = 0;
    public string? ThresholdExpression { get; set; }
    public int EvaluationWindowSeconds { get; set; } = 300;
    public int MinDataPoints { get; set; } = 1;
    public AggregationFunction Aggregation { get; set; } = AggregationFunction.Avg;
}

public enum ConditionOperator
{
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    Equal,
    NotEqual,
    Between,
    Outside,
    RegexMatch,
    IsNull,
    IsNotNull
}

public sealed class AlertAction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AlertRuleId { get; set; }
    public AlertActionType Type { get; set; } = AlertActionType.Email;
    public string ConfigJson { get; set; } = "{}";
    public bool IsActive { get; set; } = true;
    public int Order { get; set; } = 0;
    public string Template { get; set; } = string.Empty;
}

public enum AlertActionType
{
    Email,
    Webhook,
    Slack,
    Teams,
    PagerDuty,
    Opsgenie,
    Sms,
    PushNotification,
    Runbook,
    AutoRemediation
}

public sealed class Alert
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AlertRuleId { get; set; }
    public AlertRule AlertRule { get; set; } = null!;
    public AlertStatus Status { get; set; } = AlertStatus.Firing;
    public AlertSeverity Severity { get; set; } = AlertSeverity.Warning;
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, string> Labels { get; set; } = new();
    public Dictionary<string, string> Annotations { get; set; } = new();
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public string AcknowledgedByUserId { get; set; } = string.Empty;
    public string AcknowledgedComment { get; set; } = string.Empty;
    public Dictionary<string, object> Values { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public enum AlertStatus
{
    Firing,
    Pending,
    Resolved,
    Silenced,
    Suppressed
}

public enum AlertSeverity
{
    Info,
    Warning,
    Critical,
    Emergency
}

/// <summary>
/// Anomaly detection models and results
/// </summary>
public sealed class AnomalyDetector
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AnomalyDetectionAlgorithm Algorithm { get; set; } = AnomalyDetectionAlgorithm.IsolationForest;
    public Guid MetricId { get; set; }
    public MetricDefinition Metric { get; set; } = null!;
    public string ModelPath { get; set; } = string.Empty;
    public string ModelVersion { get; set; } = string.Empty;
    public bool IsTrained { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public Dictionary<string, object> Hyperparameters { get; set; } = new();
    public double Sensitivity { get; set; } = 0.95;
    public int TrainingWindowDays { get; set; } = 30;
    public int RetrainingIntervalHours { get; set; } = 24;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastTrainedAt { get; set; }
    public DateTime? LastDetectionAt { get; set; }
    public long DetectionCount { get; set; } = 0;
}

public enum AnomalyDetectionAlgorithm
{
    IsolationForest,
    LocalOutlierFactor,
    OneClassSVM,
    AutoEncoder,
    LSTM,
    Prophet,
    ARIMA,
    SeasonalDecomposition,
    ZScore,
    ModifiedZScore,
    IQR,
    Custom
}

public sealed class AnomalyResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DetectorId { get; set; }
    public AnomalyDetector Detector { get; set; } = null!;
    public Guid MetricId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public double Value { get; set; }
    public double ExpectedValue { get; set; }
    public double AnomalyScore { get; set; } // 0-1
    public AnomalySeverity Severity { get; set; } = AnomalySeverity.Low;
    public bool IsAnomaly { get; set; } = false;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, object> Context { get; set; } = new();
    public Dictionary<string, object> Features { get; set; } = new();
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public bool IsAcknowledged { get; set; } = false;
    public string? AcknowledgedByUserId { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
}

public enum AnomalySeverity
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Predictive models and forecasts
/// </summary>
public sealed class ForecastModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ForecastAlgorithm Algorithm { get; set; } = ForecastAlgorithm.Prophet;
    public Guid MetricId { get; set; }
    public MetricDefinition Metric { get; set; } = null!;
    public string ModelPath { get; set; } = string.Empty;
    public string ModelVersion { get; set; } = string.Empty;
    public bool IsTrained { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public Dictionary<string, object> Hyperparameters { get; set; } = new();
    public int ForecastHorizon { get; set; } = 24; // hours
    public int SeasonalityPeriod { get; set; } = 24; // hours
    public double ConfidenceLevel { get; set; } = 0.95;
    public int TrainingWindowDays { get; set; } = 90;
    public int RetrainingIntervalHours { get; set; } = 168; // weekly
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastTrainedAt { get; set; }
    public DateTime? LastForecastAt { get; set; }
    public ModelMetrics? LastMetrics { get; set; }
}

public enum ForecastAlgorithm
{
    Prophet,
    ARIMA,
    SARIMA,
    ETS,
    LSTM,
    GRU,
    Transformer,
    NBEATS,
    TFT,
    AutoARIMA,
    AutoETS,
    LightGBM,
    XGBoost,
    LinearRegression,
    RandomForest,
    Custom
}

public sealed class ModelMetrics
{
    public double MAE { get; set; }
    public double MSE { get; set; }
    public double RMSE { get; set; }
    public double MAPE { get; set; }
    public double SMAPE { get; set; }
    public double R2 { get; set; }
    public double MAEPercent { get; set; }
    public double Coverage { get; set; }
    public Dictionary<string, double> PerHorizonMetrics { get; set; } = new();
}

public sealed class Forecast
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ModelId { get; set; }
    public ForecastModel Model { get; set; } = null!;
    public Guid MetricId { get; set; }
    public DateTime ForecastStart { get; set; }
    public DateTime ForecastEnd { get; set; }
    public int Horizon { get; set; }
    public List<ForecastPoint> Points { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public double OverallAccuracy { get; set; } = 0;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public sealed class ForecastPoint
{
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
    public double LowerBound { get; set; }
    public double UpperBound { get; set; }
    public double? ActualValue { get; set; }
    public double? Error { get; set; }
}

/// <summary>
/// KPI and Scorecard definitions
/// </summary>
public sealed class KpiDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string FormatString { get; set; } = "N2";
    public Guid MetricId { get; set; }
    public MetricDefinition Metric { get; set; } = null!;
    public KpiTarget Target { get; set; } = new();
    public KpiThresholds Thresholds { get; set; } = new();
    public int UpdateFrequencySeconds { get; set; } = 60;
    public string OwnerId { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}

public sealed class KpiTarget
{
    public double Value { get; set; } = 0;
    public TargetDirection Direction { get; set; } = TargetDirection.HigherIsBetter;
    public string Timeframe { get; set; } = "monthly";
    public DateTime? TargetDate { get; set; }
}

public enum TargetDirection
{
    HigherIsBetter,
    LowerIsBetter,
    TargetIsBest
}

public sealed class KpiThresholds
{
    public double? CriticalLow { get; set; }
    public double? WarningLow { get; set; }
    public double? Target { get; set; }
    public double? WarningHigh { get; set; }
    public double? CriticalHigh { get; set; }
    public string ColorScheme { get; set; } = "default"; // default, traffic-light, reversed
}

public sealed class KpiValue
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid KpiId { get; set; }
    public KpiDefinition Kpi { get; set; } = null!;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public double Value { get; set; }
    public double? TargetValue { get; set; }
    public KpiStatus Status { get; set; } = KpiStatus.OnTrack;
    public double PercentToTarget { get; set; }
    public double Trend { get; set; } // positive = improving
    public Dictionary<string, object> Breakdown { get; set; } = new();
}

public enum KpiStatus
{
    OnTrack,
    AtRisk,
    OffTrack,
    Achieved,
    NoData
}

/// <summary>
/// Cohort and retention analysis
/// </summary>
public sealed class CohortAnalysis
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public CohortType Type { get; set; } = CohortType.Acquisition;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string CohortDefinition { get; set; } = string.Empty; // JSON
    public string MetricDefinition { get; set; } = string.Empty; // JSON
    public int PeriodCount { get; set; } = 12;
    public PeriodGranularity Granularity { get; set; } = PeriodGranularity.Weekly;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<CohortResult> Results { get; set; } = new();
}

public enum CohortType
{
    Acquisition,
    Behavioral,
    Predictive
}

public enum PeriodGranularity
{
    Daily,
    Weekly,
    Monthly,
    Quarterly
}

public sealed class CohortResult
{
    public string CohortId { get; set; } = string.Empty;
    public string CohortLabel { get; set; } = string.Empty;
    public int CohortSize { get; set; } = 0;
    public DateTime CohortDate { get; set; }
    public List<PeriodValue> Periods { get; set; } = new();
}

public sealed class PeriodValue
{
    public int PeriodIndex { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public int UsersCount { get; set; }
    public int RetainedUsers { get; set; }
    public double RetentionRate { get; set; }
    public double Value { get; set; }
}

/// <summary>
/// Funnel analysis
/// </summary>
public sealed class FunnelAnalysis
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<FunnelStep> Steps { get; set; } = new();
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string FilterJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public FunnelResult? Result { get; set; }
}

public sealed class FunnelStep
{
    public int Order { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    public string FilterJson { get; set; } = "{}";
    public bool IsRequired { get; set; } = true;
}

public sealed class FunnelResult
{
    public List<StepResult> StepResults { get; set; } = new();
    public double OverallConversionRate { get; set; }
    public double AverageTimeToConvert { get; set; }
    public int TotalEntrances { get; set; }
    public int TotalConversions { get; set; }
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class StepResult
{
    public int StepOrder { get; set; }
    public string StepName { get; set; } = string.Empty;
    public int UsersEntered { get; set; }
    public int UsersCompleted { get; set; }
    public double ConversionRate { get; set; }
    public double DropOffRate { get; set; }
    public double AverageTimeInStep { get; set; }
    public Dictionary<string, int> DropOffReasons { get; set; } = new();
}