// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using Microsoft.EntityFrameworkCore;
using KKBOT.Analytics.Models;

namespace KKBOT.Analytics.Data;

public sealed class AnalyticsDbContext : DbContext
{
    public DbSet<Dashboard> Dashboards => Set<Dashboard>();
    public DbSet<DashboardWidget> DashboardWidgets => Set<DashboardWidget>();
    public DbSet<DashboardShare> DashboardShares => Set<DashboardShare>();
    public DbSet<DashboardSubscription> DashboardSubscriptions => Set<DashboardSubscription>();
    
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<ReportParameter> ReportParameters => Set<ReportParameter>();
    public DbSet<ReportExecution> ReportExecutions => Set<ReportExecution>();
    
    public DbSet<DataSource> DataSources => Set<DataSource>();
    public DbSet<DataSourceTable> DataSourceTables => Set<DataSourceTable>();
    public DbSet<DataSourceColumn> DataSourceColumns => Set<DataSourceColumn>();
    
    public DbSet<MetricDefinition> Metrics => Set<MetricDefinition>();
    public DbSet<MetricValue> MetricValues => Set<MetricValue>();
    
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();
    public DbSet<AlertAction> AlertActions => Set<AlertAction>();
    public DbSet<Alert> Alerts => Set<Alert>();
    
    public DbSet<AnomalyDetector> AnomalyDetectors => Set<AnomalyDetector>();
    public DbSet<AnomalyResult> AnomalyResults => Set<AnomalyResult>();
    
    public DbSet<ForecastModel> ForecastModels => Set<ForecastModel>();
    public DbSet<Forecast> Forecasts => Set<Forecast>();
    public DbSet<ForecastPoint> ForecastPoints => Set<ForecastPoint>();
    
    public DbSet<KpiDefinition> KpiDefinitions => Set<KpiDefinition>();
    public DbSet<KpiValue> KpiValues => Set<KpiValue>();
    
    public DbSet<CohortAnalysis> CohortAnalyses => Set<CohortAnalysis>();
    public DbSet<CohortResult> CohortResults => Set<CohortResult>();
    public DbSet<PeriodValue> PeriodValues => Set<PeriodValue>();
    
    public DbSet<FunnelAnalysis> FunnelAnalyses => Set<FunnelAnalysis>();
    public DbSet<FunnelStep> FunnelSteps => Set<FunnelStep>();
    public DbSet<FunnelResult> FunnelResults => Set<FunnelResult>();
    public DbSet<StepResult> StepResults => Set<StepResult>();

    public AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Dashboard
        modelBuilder.Entity<Dashboard>(e =>
        {
            e.HasKey(d => d.Id);
            e.HasIndex(d => d.OwnerId);
            e.HasIndex(d => d.TenantId);
            e.HasIndex(d => d.CreatedAt);
            e.Property(d => d.LayoutJson).HasColumnType("TEXT");
            e.Property(d => d.Tags).HasColumnType("TEXT");
        });

        modelBuilder.Entity<DashboardWidget>(e =>
        {
            e.HasKey(w => w.Id);
            e.HasIndex(w => w.DashboardId);
            e.Property(w => w.ConfigJson).HasColumnType("TEXT");
            e.Property(w => w.QueryJson).HasColumnType("TEXT");
            e.Property(w => w.Options).HasColumnType("TEXT");
            e.HasOne(w => w.Dashboard)
                .WithMany(d => d.Widgets)
                .HasForeignKey(w => w.DashboardId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DashboardShare>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => new { s.DashboardId, s.SharedWithUserId }).IsUnique();
            e.HasOne(s => s.Dashboard)
                .WithMany(d => d.Shares)
                .HasForeignKey(s => s.DashboardId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DashboardSubscription>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => new { s.DashboardId, s.SubscriberId }).IsUnique();
            e.Property(s => s.DeliveryConfigJson).HasColumnType("TEXT");
            e.HasOne(s => s.Dashboard)
                .WithMany(d => d.Subscriptions)
                .HasForeignKey(s => s.DashboardId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Report
        modelBuilder.Entity<Report>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.OwnerId);
            e.HasIndex(r => r.TenantId);
            e.HasIndex(r => r.CreatedAt);
            e.Property(r => r.TemplateJson).HasColumnType("TEXT");
            e.Property(r => r.DataSourceJson).HasColumnType("TEXT");
        });

        modelBuilder.Entity<ReportParameter>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.ReportId);
            e.Property(p => p.OptionsJson).HasColumnType("TEXT");
            e.HasOne(p => p.Report)
                .WithMany(r => r.Parameters)
                .HasForeignKey(p => p.ReportId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReportExecution>(e =>
        {
            e.HasKey(e => e.Id);
            e.HasIndex(e => e.ReportId);
            e.HasIndex(e => e.ExecutedByUserId);
            e.HasIndex(e => e.StartedAt);
            e.Property(e => e.Parameters).HasColumnType("TEXT");
            e.Property(e => e.Metadata).HasColumnType("TEXT");
            e.HasOne(e => e.Report)
                .WithMany(r => r.Executions)
                .HasForeignKey(e => e.ReportId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // DataSource
        modelBuilder.Entity<DataSource>(e =>
        {
            e.HasKey(d => d.Id);
            e.HasIndex(d => d.TenantId);
            e.HasIndex(d => d.Type);
            e.Property(d => d.ConnectionString).HasColumnType("TEXT");
            e.Property(d => d.Options).HasColumnType("TEXT");
        });

        modelBuilder.Entity<DataSourceTable>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasIndex(t => t.DataSourceId);
            e.Property(t => t.Columns).HasColumnType("TEXT");
            e.HasOne(t => t.DataSource)
                .WithMany(d => d.Tables)
                .HasForeignKey(t => t.DataSourceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DataSourceColumn>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasIndex(c => c.TableId);
            e.HasOne(c => c.Table)
                .WithMany(t => t.Columns)
                .HasForeignKey(c => c.TableId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Metrics
        modelBuilder.Entity<MetricDefinition>(e =>
        {
            e.HasKey(m => m.Id);
            e.HasIndex(m => m.Name);
            e.HasIndex(m => m.Category);
            e.HasIndex(m => m.DataSourceId);
            e.Property(m => m.QueryJson).HasColumnType("TEXT");
            e.Property(m => m.Tags).HasColumnType("TEXT");
        });

        modelBuilder.Entity<MetricValue>(e =>
        {
            e.HasKey(m => m.Id);
            e.HasIndex(m => new { m.MetricId, m.Timestamp });
            e.HasIndex(m => m.Timestamp);
            e.Property(m => m.Labels).HasColumnType("TEXT");
            e.HasOne(m => m.Metric)
                .WithMany()
                .HasForeignKey(m => m.MetricId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Alerts
        modelBuilder.Entity<AlertRule>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasIndex(a => a.MetricId);
            e.HasIndex(a => a.TenantId);
            e.HasIndex(a => a.IsActive);
            e.Property(a => a.Condition).HasColumnType("TEXT");
            e.Property(a => a.Labels).HasColumnType("TEXT");
            e.Property(a => a.Annotations).HasColumnType("TEXT");
        });

        modelBuilder.Entity<AlertAction>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasIndex(a => a.AlertRuleId);
            e.Property(a => a.ConfigJson).HasColumnType("TEXT");
            e.HasOne(a => a.AlertRule)
                .WithMany(r => r.Actions)
                .HasForeignKey(a => a.AlertRuleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Alert>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasIndex(a => a.AlertRuleId);
            e.HasIndex(a => a.Status);
            e.HasIndex(a => a.Severity);
            e.HasIndex(a => a.StartedAt);
            e.Property(a => a.Labels).HasColumnType("TEXT");
            e.Property(a => a.Annotations).HasColumnType("TEXT");
            e.Property(a => a.Values).HasColumnType("TEXT");
            e.Property(a => a.Metadata).HasColumnType("TEXT");
            e.HasOne(a => a.AlertRule)
                .WithMany()
                .HasForeignKey(a => a.AlertRuleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Anomaly Detection
        modelBuilder.Entity<AnomalyDetector>(e =>
        {
            e.HasKey(d => d.Id);
            e.HasIndex(d => d.MetricId);
            e.HasIndex(d => d.IsActive);
            e.Property(d => d.ModelPath).HasMaxLength(500);
            e.Property(d => d.Hyperparameters).HasColumnType("TEXT");
        });

        modelBuilder.Entity<AnomalyResult>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasIndex(a => a.DetectorId);
            e.HasIndex(a => a.MetricId);
            e.HasIndex(a => a.Timestamp);
            e.HasIndex(a => a.IsAnomaly);
            e.HasIndex(a => a.IsAcknowledged);
            e.Property(a => a.Context).HasColumnType("TEXT");
            e.Property(a => a.Features).HasColumnType("TEXT");
        });

        // Forecasting
        modelBuilder.Entity<ForecastModel>(e =>
        {
            e.HasKey(f => f.Id);
            e.HasIndex(f => f.MetricId);
            e.HasIndex(f => f.IsActive);
            e.Property(f => f.ModelPath).HasMaxLength(500);
            e.Property(f => f.Hyperparameters).HasColumnType("TEXT");
        });

        modelBuilder.Entity<Forecast>(e =>
        {
            e.HasKey(f => f.Id);
            e.HasIndex(f => f.ModelId);
            e.HasIndex(f => f.MetricId);
            e.HasIndex(f => f.GeneratedAt);
            e.Property(f => f.Points).HasColumnType("TEXT");
            e.Property(f => f.Metadata).HasColumnType("TEXT");
        });

        modelBuilder.Entity<ForecastPoint>(e =>
        {
            e.HasKey(f => f.Id);
            e.HasIndex(f => f.ForecastId);
        });

        // KPIs
        modelBuilder.Entity<KpiDefinition>(e =>
        {
            e.HasKey(k => k.Id);
            e.HasIndex(k => k.Category);
            e.HasIndex(k => k.MetricId);
            e.HasIndex(k => k.OwnerId);
            e.Property(k => k.Tags).HasColumnType("TEXT");
        });

        modelBuilder.Entity<KpiValue>(e =>
        {
            e.HasKey(k => k.Id);
            e.HasIndex(k => k.KpiId);
            e.HasIndex(k => k.Timestamp);
            e.Property(k => k.Breakdown).HasColumnType("TEXT");
        });

        // Cohort Analysis
        modelBuilder.Entity<CohortAnalysis>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.CohortDefinition).HasColumnType("TEXT");
            e.Property(c => c.MetricDefinition).HasColumnType("TEXT");
        });

        modelBuilder.Entity<CohortResult>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasIndex(c => c.CohortAnalysisId);
        });

        modelBuilder.Entity<PeriodValue>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.CohortResultId);
        });

        // Funnel Analysis
        modelBuilder.Entity<FunnelAnalysis>(e =>
        {
            e.HasKey(f => f.Id);
            e.Property(f => f.FilterJson).HasColumnType("TEXT");
        });

        modelBuilder.Entity<FunnelStep>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.FunnelAnalysisId);
            e.Property(s => s.FilterJson).HasColumnType("TEXT");
            e.HasOne(s => s.FunnelAnalysis)
                .WithMany(f => f.Steps)
                .HasForeignKey(s => s.FunnelAnalysisId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FunnelResult>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.FunnelAnalysisId);
            e.HasOne(r => r.FunnelAnalysis)
                .WithOne(f => f.Result)
                .HasForeignKey<FunnelResult>(r => r.FunnelAnalysisId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StepResult>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.FunnelResultId);
        });
    }
}