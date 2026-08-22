// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Analytics.Abstractions;
using KKBOT.Analytics.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CsvHelper;
using CsvHelper.Configuration;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KKBOT.Analytics.Services;

public sealed class ExportService : IExportService
{
    private readonly AnalyticsDbContext _dbContext;
    private readonly ILogger<ExportService> _logger;

    public ExportService(AnalyticsDbContext dbContext, ILogger<ExportService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
        
        // Configure QuestPDF
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<byte[]> ExportDashboardAsync(Guid dashboardId, ExportFormat format, string userId, CancellationToken ct = default)
    {
        var dashboard = await _dbContext.Dashboards
            .Include(d => d.Widgets)
            .FirstOrDefaultAsync(d => d.Id == dashboardId && (d.OwnerId == userId || d.IsPublic), ct);

        if (dashboard == null)
            throw new InvalidOperationException("Dashboard not found or access denied");

        var data = new
        {
            dashboard.Id,
            dashboard.Name,
            dashboard.Description,
            dashboard.CreatedAt,
            dashboard.UpdatedAt,
            Widgets = dashboard.Widgets.Select(w => new
            {
                w.Id,
                w.WidgetType,
                w.Title,
                w.Description,
                w.PositionX,
                w.PositionY,
                w.Width,
                w.Height,
                w.ConfigJson,
                w.QueryJson
            })
        };

        return format switch
        {
            ExportFormat.Json => ExportToJson(data),
            ExportFormat.Csv => ExportToCsv(new[] { new { dashboard.Name, WidgetCount = dashboard.Widgets.Count } }),
            ExportFormat.Excel => ExportToExcel(data),
            ExportFormat.Pdf => ExportDashboardToPdf(dashboard),
            ExportFormat.Parquet => ExportToParquet(new[] { new { dashboard.Name, WidgetCount = dashboard.Widgets.Count } }),
            _ => throw new ArgumentException($"Unsupported format: {format}")
        };
    }

    public async Task<byte[]> ExportReportAsync(Guid reportId, ExportFormat format, string userId, CancellationToken ct = default)
    {
        var report = await _dbContext.Reports
            .Include(r => r.Parameters)
            .Include(r => r.Executions)
            .FirstOrDefaultAsync(r => r.Id == reportId && r.OwnerId == userId, ct);

        if (report == null)
            throw new InvalidOperationException("Report not found");

        var data = new
        {
            report.Id,
            report.Name,
            report.Description,
            report.Category,
            report.TemplateJson,
            report.DataSourceJson,
            report.DefaultFormat,
            report.IsScheduled,
            report.CronExpression,
            Parameters = report.Parameters.Select(p => new { p.Name, p.DisplayName, p.DataType, p.IsRequired, p.DefaultValue }),
            RecentExecutions = report.Executions.Take(10).Select(e => new { e.Id, e.Status, e.Format, e.StartedAt, e.CompletedAt, e.Duration, e.RowCount })
        };

        return format switch
        {
            ExportFormat.Json => ExportToJson(data),
            ExportFormat.Csv => ExportToCsv(new[] { new { report.Name, report.Category, ExecutionCount = report.Executions.Count } }),
            ExportFormat.Excel => ExportToExcel(data),
            ExportFormat.Pdf => ExportReportToPdf(report),
            ExportFormat.Parquet => ExportToParquet(new[] { new { report.Name, ExecutionCount = report.Executions.Count } }),
            _ => throw new ArgumentException($"Unsupported format: {format}")
        };
    }

    public async Task<byte[]> ExportMetricsAsync(List<Guid> metricIds, DateTime from, DateTime to, ExportFormat format, string userId, CancellationToken ct = default)
    {
        var metrics = await _dbContext.MetricValues
            .Where(v => metricIds.Contains(v.MetricId) && v.Timestamp >= from && v.Timestamp <= to)
            .OrderBy(v => v.Timestamp)
            .Select(v => new
            {
                v.MetricId,
                v.Timestamp,
                v.Value,
                v.Labels,
                v.Min,
                v.Max,
                v.Count,
                v.Sum
            })
            .ToListAsync(ct);

        return format switch
        {
            ExportFormat.Json => ExportToJson(new { Metrics = metrics, From = from, To = to }),
            ExportFormat.Csv => ExportToCsv(metrics),
            ExportFormat.Excel => ExportToExcel(new { Metrics = metrics, From = from, To = to }),
            ExportFormat.Pdf => ExportMetricsToPdf(metrics, from, to),
            ExportFormat.Parquet => ExportToParquet(metrics),
            _ => throw new ArgumentException($"Unsupported format: {format}")
        };
    }

    public async Task<byte[]> ExportDashboardImageAsync(Guid dashboardId, ImageFormat format, int width, int height, string userId, CancellationToken ct = default)
    {
        var dashboard = await _dbContext.Dashboards
            .FirstOrDefaultAsync(d => d.Id == dashboardId && (d.OwnerId == userId || d.IsPublic), ct);

        if (dashboard == null)
            throw new InvalidOperationException("Dashboard not found");

        // In a real implementation, this would render the dashboard to an image
        // using a headless browser or canvas rendering
        // For now, return a placeholder
        var placeholder = $"Dashboard: {dashboard.Name}\nSize: {width}x{height}\nFormat: {format}\nGenerated: {DateTime.UtcNow}";
        return Encoding.UTF8.GetBytes(placeholder);
    }

    private byte[] ExportToJson(object data)
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        return Encoding.UTF8.GetBytes(json);
    }

    private byte[] ExportToCsv<T>(IEnumerable<T> data)
    {
        using var memoryStream = new MemoryStream();
        using var writer = new StreamWriter(memoryStream);
        using var csv = new CsvWriter(writer, new CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture));
        
        csv.WriteRecords(data);
        writer.Flush();
        return memoryStream.ToArray();
    }

    private byte[] ExportToExcel(object data)
    {
        // Would use EPPlus or similar library
        // For now, return JSON as placeholder
        return ExportToJson(new { data, note = "Excel export requires EPPlus library" });
    }

    private byte[] ExportToParquet(object data)
    {
        // Would use Apache Parquet library
        // For now, return JSON as placeholder
        return ExportToJson(new { data, note = "Parquet export requires Apache Parquet library" });
    }

    private byte[] ExportDashboardToPdf(Dashboard dashboard)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(50);
                page.Header().Column(col =>
                {
                    col.Item().Text(dashboard.Name).FontSize(24).Bold();
                    col.Item().Text(dashboard.Description).FontSize(12).FontColor(QuestPDF.Helpers.Colors.Grey.Medium);
                });

                page.Content().Column(col =>
                {
                    col.Item().Text($"Created: {dashboard.CreatedAt:yyyy-MM-dd}").FontSize(10);
                    col.Item().Text($"Updated: {dashboard.UpdatedAt:yyyy-MM-dd}").FontSize(10);
                    col.Item().Text($"Widgets: {dashboard.Widgets.Count}").FontSize(10);

                    col.Item().PaddingTop(20).Text("Widgets").FontSize(16).Bold();

                    foreach (var widget in dashboard.Widgets)
                    {
                        col.Item().PaddingTop(10).Column(wcol =>
                        {
                            wcol.Item().Text(widget.Title).FontSize(14).Bold();
                            wcol.Item().Text($"Type: {widget.WidgetType}").FontSize(10);
                            wcol.Item().Text($"Position: ({widget.PositionX}, {widget.PositionY}) Size: {widget.Width}x{widget.Height}").FontSize(10);
                        });
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span($"Page ").CurrentPageNumber();
                    text.Span(" of ").TotalPages();
                });
            });
        });

        using var stream = new MemoryStream();
        document.GeneratePdf(stream);
        return stream.ToArray();
    }

    private byte[] ExportReportToPdf(Report report)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(50);
                page.Header().Column(col =>
                {
                    col.Item().Text(report.Name).FontSize(24).Bold();
                    col.Item().Text(report.Description).FontSize(12).FontColor(QuestPDF.Helpers.Colors.Grey.Medium);
                });

                page.Content().Column(col =>
                {
                    col.Item().Text($"Category: {report.Category}").FontSize(10);
                    col.Item().Text($"Format: {report.DefaultFormat}").FontSize(10);
                    col.Item().Text($"Scheduled: {(report.IsScheduled ? "Yes" : "No")}").FontSize(10);
                    if (!string.IsNullOrEmpty(report.CronExpression))
                        col.Item().Text($"Schedule: {report.CronExpression}").FontSize(10);

                    col.Item().PaddingTop(20).Text("Parameters").FontSize(16).Bold();
                    foreach (var param in report.Parameters)
                    {
                        col.Item().Text($"- {param.DisplayName} ({param.DataType}){(param.IsRequired ? " [Required]" : "")}: {param.DefaultValue}").FontSize(10);
                    }

                    col.Item().PaddingTop(20).Text("Recent Executions").FontSize(16).Bold();
                    foreach (var exec in report.Executions.Take(10))
                    {
                        col.Item().Text($"{exec.StartedAt:yyyy-MM-dd HH:mm} | {exec.Status} | {exec.Duration} | Rows: {exec.RowCount}").FontSize(10);
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span($"Page ").CurrentPageNumber();
                    text.Span(" of ").TotalPages();
                });
            });
        });

        using var stream = new MemoryStream();
        document.GeneratePdf(stream);
        return stream.ToArray();
    }

    private byte[] ExportMetricsToPdf(List<MetricValue> metrics, DateTime from, DateTime to)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(50);
                page.Header().Column(col =>
                {
                    col.Item().Text("Metrics Export").FontSize(24).Bold();
                    col.Item().Text($"Period: {from:yyyy-MM-dd} to {to:yyyy-MM-dd}").FontSize(12);
                });

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2); // Timestamp
                        columns.RelativeColumn(1); // Value
                        columns.RelativeColumn(1); // Min
                        columns.RelativeColumn(1); // Max
                    });

                    table.Header(header =>
                    {
                        header.Cell().Text("Timestamp").Bold();
                        header.Cell().Text("Value").Bold();
                        header.Cell().Text("Min").Bold();
                        header.Cell().Text("Max").Bold();
                    });

                    foreach (var metric in metrics)
                    {
                        table.Cell().Text(metric.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
                        table.Cell().Text(metric.Value.ToString("F4"));
                        table.Cell().Text(metric.Min?.ToString("F4") ?? "");
                        table.Cell().Text(metric.Max?.ToString("F4") ?? "");
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span($"Page ").CurrentPageNumber();
                    text.Span(" of ").TotalPages();
                });
            });
        });

        using var stream = new MemoryStream();
        document.GeneratePdf(stream);
        return stream.ToArray();
    }
}