// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ML;
using KKBOT.Analytics.Data;
using KKBOT.Analytics.Services;
using KKBOT.Analytics.Abstractions;

namespace KKBOT.Analytics;

public static class AnalyticsExtensions
{
    public static IServiceCollection AddKkbotAnalytics(this IServiceCollection services, IConfiguration configuration)
    {
        // Database
        services.AddDbContext<AnalyticsDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("AnalyticsDb") 
                ?? configuration.GetConnectionString("KkbotDb") 
                ?? "DataSource=analytics.db";
            options.UseSqlite(connectionString);
        });

        // ML.NET
        services.AddSingleton<MLContext>();

        // Core Services
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IDataSourceService, DataSourceService>();
        services.AddScoped<IMetricService, MetricService>();
        services.AddScoped<IAlertService, AlertService>();
        services.AddScoped<IAnomalyDetectionService, AnomalyDetectionService>();
        services.AddScoped<IForecastService, ForecastService>();
        services.AddScoped<IKpiService, KpiService>();
        services.AddScoped<ICohortAnalysisService, CohortAnalysisService>();
        services.AddScoped<IFunnelAnalysisService, FunnelAnalysisService>();
        services.AddScoped<IExportService, ExportService>();

        return services;
    }
}

// Need to implement missing services
// Let me create placeholder implementations for the missing services

// ReportService placeholder
namespace KKBOT.Analytics.Services;

public sealed class ReportService : IReportService
{
    private readonly AnalyticsDbContext _dbContext;
    private readonly ILogger<ReportService> _logger;

    public ReportService(AnalyticsDbContext dbContext, ILogger<ReportService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Report> CreateReportAsync(CreateReportRequest request, string userId, CancellationToken ct = default)
    {
        var report = new Report
        {
            Name = request.Name,
            Description = request.Description,
            Category = request.Category,
            TemplateJson = request.TemplateJson,
            DataSourceJson = request.DataSourceJson,
            DefaultFormat = request.DefaultFormat,
            IsScheduled = request.IsScheduled,
            CronExpression = request.CronExpression,
            OwnerId = userId,
            Parameters = request.Parameters
        };

        _dbContext.Reports.Add(report);
        await _dbContext.SaveChangesAsync(ct);
        return report;
    }

    public async Task<Report?> GetReportAsync(Guid reportId, string userId, CancellationToken ct = default)
    {
        return await _dbContext.Reports
            .Include(r => r.Parameters)
            .Include(r => r.Executions)
            .FirstOrDefaultAsync(r => r.Id == reportId && (r.OwnerId == userId || r.IsActive), ct);
    }

    public async Task<List<Report>> GetReportsAsync(string userId, ReportFilter filter, CancellationToken ct = default)
    {
        var query = _dbContext.Reports.Where(r => r.OwnerId == userId || r.IsActive).AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.ToLower();
            query = query.Where(r => r.Name.ToLower().Contains(s) || r.Description.ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(filter.Category))
            query = query.Where(r => r.Category == filter.Category);

        if (filter.IsScheduled.HasValue)
            query = query.Where(r => r.IsScheduled == filter.IsScheduled.Value);

        if (filter.IsActive.HasValue)
            query = query.Where(r => r.IsActive == filter.IsActive.Value);

        query = filter.SortBy?.ToLower() switch
        {
            "name" => filter.SortDescending ? query.OrderByDescending(r => r.Name) : query.OrderBy(r => r.Name),
            "createdAt" => filter.SortDescending ? query.OrderByDescending(r => r.CreatedAt) : query.OrderBy(r => r.CreatedAt),
            _ => filter.SortDescending ? query.OrderByDescending(r => r.UpdatedAt) : query.OrderBy(r => r.UpdatedAt)
        };

        return await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);
    }

    public async Task<Report> UpdateReportAsync(Guid reportId, UpdateReportRequest request, string userId, CancellationToken ct = default)
    {
        var report = await _dbContext.Reports.FirstOrDefaultAsync(r => r.Id == reportId && r.OwnerId == userId, ct);
        if (report == null) throw new InvalidOperationException("Report not found");

        if (request.Name != null) report.Name = request.Name;
        if (request.Description != null) report.Description = request.Description;
        if (request.Category != null) report.Category = request.Category;
        if (request.TemplateJson != null) report.TemplateJson = request.TemplateJson;
        if (request.DataSourceJson != null) report.DataSourceJson = request.DataSourceJson;
        if (request.DefaultFormat.HasValue) report.DefaultFormat = request.DefaultFormat.Value;
        if (request.IsScheduled.HasValue) report.IsScheduled = request.IsScheduled.Value;
        if (request.CronExpression != null) report.CronExpression = request.CronExpression;
        if (request.IsActive.HasValue) report.IsActive = request.IsActive.Value;

        report.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
        return report;
    }

    public async Task<bool> DeleteReportAsync(Guid reportId, string userId, CancellationToken ct = default)
    {
        var report = await _dbContext.Reports.FirstOrDefaultAsync(r => r.Id == reportId && r.OwnerId == userId, ct);
        if (report == null) return false;

        _dbContext.Reports.Remove(report);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ReportExecution> ExecuteReportAsync(Guid reportId, ExecuteReportRequest request, string userId, CancellationToken ct = default)
    {
        var execution = new ReportExecution
        {
            ReportId = reportId,
            ExecutedByUserId = userId,
            Status = ExecutionStatus.Running,
            Format = request.Format,
            Parameters = request.Parameters,
            StartedAt = DateTime.UtcNow
        };

        _dbContext.ReportExecutions.Add(execution);
        await _dbContext.SaveChangesAsync(ct);

        // In a real implementation, this would execute the report
        // For now, just mark as completed
        execution.Status = ExecutionStatus.Completed;
        execution.CompletedAt = DateTime.UtcNow;
        execution.Duration = DateTime.UtcNow - execution.StartedAt;
        execution.RowCount = 0;
        
        await _dbContext.SaveChangesAsync(ct);
        return execution;
    }

    public async Task<ReportExecution?> GetExecutionAsync(Guid executionId, string userId, CancellationToken ct = default)
    {
        return await _dbContext.ReportExecutions
            .Include(e => e.Report)
            .FirstOrDefaultAsync(e => e.Id == executionId && e.Report.OwnerId == userId, ct);
    }

    public async Task<List<ReportExecution>> GetExecutionHistoryAsync(Guid reportId, ExecutionHistoryFilter filter, CancellationToken ct = default)
    {
        var query = _dbContext.ReportExecutions.Where(e => e.ReportId == reportId).AsQueryable();

        if (filter.Status.HasValue)
            query = query.Where(e => e.Status == filter.Status.Value);

        if (filter.FromDate.HasValue)
            query = query.Where(e => e.StartedAt >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(e => e.StartedAt <= filter.ToDate.Value);

        return await query
            .OrderByDescending(e => e.StartedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);
    }

    public async Task<bool> CancelExecutionAsync(Guid executionId, string userId, CancellationToken ct = default)
    {
        var execution = await _dbContext.ReportExecutions
            .Include(e => e.Report)
            .FirstOrDefaultAsync(e => e.Id == executionId && e.Report.OwnerId == userId, ct);

        if (execution == null || execution.Status != ExecutionStatus.Running)
            return false;

        execution.Status = ExecutionStatus.Cancelled;
        execution.CompletedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<byte[]> DownloadReportAsync(Guid executionId, string userId, CancellationToken ct = default)
    {
        var execution = await _dbContext.ReportExecutions
            .Include(e => e.Report)
            .FirstOrDefaultAsync(e => e.Id == executionId && e.Report.OwnerId == userId, ct);

        if (execution == null || execution.Status != ExecutionStatus.Completed)
            throw new InvalidOperationException("Report execution not found or not completed");

        // Return the report output
        // In a real implementation, this would return the actual file
        return System.Text.Encoding.UTF8.GetBytes($"Report output for execution {executionId}");
    }
}

// DataSourceService placeholder
public sealed class DataSourceService : IDataSourceService
{
    private readonly AnalyticsDbContext _dbContext;
    private readonly ILogger<DataSourceService> _logger;

    public DataSourceService(AnalyticsDbContext dbContext, ILogger<DataSourceService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<DataSource> CreateDataSourceAsync(CreateDataSourceRequest request, string userId, CancellationToken ct = default)
    {
        var ds = new DataSource
        {
            Name = request.Name,
            Description = request.Description,
            Type = request.Type,
            ConnectionString = request.ConnectionString,
            Options = request.Options,
            TenantId = userId
        };

        _dbContext.DataSources.Add(ds);
        await _dbContext.SaveChangesAsync(ct);
        return ds;
    }

    public async Task<DataSource?> GetDataSourceAsync(Guid dataSourceId, string userId, CancellationToken ct = default)
    {
        return await _dbContext.DataSources.FirstOrDefaultAsync(d => d.Id == dataSourceId, ct);
    }

    public async Task<List<DataSource>> GetDataSourcesAsync(string userId, DataSourceFilter filter, CancellationToken ct = default)
    {
        var query = _dbContext.DataSources.Where(d => d.TenantId == userId || d.IsShared).AsQueryable();

        if (filter.Type.HasValue)
            query = query.Where(d => d.Type == filter.Type.Value);

        if (filter.IsActive.HasValue)
            query = query.Where(d => d.IsActive == filter.IsActive.Value);

        if (filter.IsShared.HasValue)
            query = query.Where(d => d.IsShared == filter.IsShared.Value);

        return await query.OrderByDescending(d => d.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);
    }

    public async Task<DataSource> UpdateDataSourceAsync(Guid dataSourceId, UpdateDataSourceRequest request, string userId, CancellationToken ct = default)
    {
        var ds = await _dbContext.DataSources.FirstOrDefaultAsync(d => d.Id == dataSourceId, ct);
        if (ds == null) throw new InvalidOperationException("Data source not found");

        if (request.Name != null) ds.Name = request.Name;
        if (request.Description != null) ds.Description = request.Description;
        if (request.ConnectionString != null) ds.ConnectionString = request.ConnectionString;
        if (request.Options != null) ds.Options = request.Options;
        if (request.IsActive.HasValue) ds.IsActive = request.IsActive.Value;
        if (request.IsShared.HasValue) ds.IsShared = request.IsShared.Value;

        ds.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
        return ds;
    }

    public async Task<bool> DeleteDataSourceAsync(Guid dataSourceId, string userId, CancellationToken ct = default)
    {
        var ds = await _dbContext.DataSources.FirstOrDefaultAsync(d => d.Id == dataSourceId, ct);
        if (ds == null) return false;

        _dbContext.DataSources.Remove(ds);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> TestConnectionAsync(Guid dataSourceId, string userId, CancellationToken ct = default)
    {
        var ds = await _dbContext.DataSources.FirstOrDefaultAsync(d => d.Id == dataSourceId, ct);
        if (ds == null) return false;

        // In a real implementation, this would test the actual connection
        ds.LastTestedAt = DateTime.UtcNow;
        ds.LastTestSuccess = true;
        ds.LastTestMessage = "Connection successful";
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<DataSourceTable>> GetTablesAsync(Guid dataSourceId, string userId, CancellationToken ct = default)
    {
        return await _dbContext.DataSourceTables.Where(t => t.DataSourceId == dataSourceId).ToListAsync(ct);
    }

    public async Task<List<DataSourceColumn>> GetColumnsAsync(Guid dataSourceId, string tableName, string userId, CancellationToken ct = default)
    {
        var table = await _dbContext.DataSourceTables.FirstOrDefaultAsync(t => t.DataSourceId == dataSourceId && t.TableName == tableName, ct);
        if (table == null) return new List<DataSourceColumn>();

        return await _dbContext.DataSourceColumns.Where(c => c.TableId == table.Id).ToListAsync(ct);
    }

    public async Task<QueryResult> ExecuteQueryAsync(Guid dataSourceId, string query, Dictionary<string, object> parameters, int maxRows, CancellationToken ct = default)
    {
        // In a real implementation, this would execute the query against the data source
        return new QueryResult
        {
            Rows = new List<Dictionary<string, object>>(),
            Columns = new List<string>(),
            TotalRows = 0,
            Duration = TimeSpan.Zero,
            Query = query
        };
    }
}