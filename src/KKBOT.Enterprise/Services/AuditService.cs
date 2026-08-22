// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Enterprise.Abstractions;
using KKBOT.Enterprise.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CsvHelper;
using CsvHelper.Configuration;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KKBOT.Enterprise.Services;

public sealed class AuditService : IAuditService
{
    private readonly EnterpriseDbContext _dbContext;
    private readonly ILogger<AuditService> _logger;

    public AuditService(EnterpriseDbContext dbContext, ILogger<AuditService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task LogAsync(AuditLogEntry entry, CancellationToken ct = default)
    {
        // Enrich entry with context
        if (entry.Timestamp == default)
            entry.Timestamp = DateTime.UtcNow;

        if (string.IsNullOrEmpty(entry.CorrelationId))
            entry.CorrelationId = System.Diagnostics.Activity.Current?.Id ?? Guid.NewGuid().ToString();

        entry.SourceSystem = "KKBOT";
        entry.Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        entry.Version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";

        _dbContext.AuditLogEntries.Add(entry);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<List<AuditLogEntry>> QueryAsync(AuditQuery query, CancellationToken ct = default)
    {
        var q = _dbContext.AuditLogEntries.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.TenantId))
            q = q.Where(a => a.TenantId == query.TenantId);

        if (!string.IsNullOrWhiteSpace(query.UserId))
            q = q.Where(a => a.UserId == query.UserId);

        if (query.EventType.HasValue)
            q = q.Where(a => a.EventType == query.EventType.Value);

        if (query.Outcome.HasValue)
            q = q.Where(a => a.Outcome == query.Outcome.Value);

        if (query.RiskLevel.HasValue)
            q = q.Where(a => a.RiskLevel == query.RiskLevel.Value);

        if (query.FromDate.HasValue)
            q = q.Where(a => a.Timestamp >= query.FromDate.Value);

        if (query.ToDate.HasValue)
            q = q.Where(a => a.Timestamp <= query.ToDate.Value);

        if (!string.IsNullOrWhiteSpace(query.ResourceType))
            q = q.Where(a => a.ResourceType == query.ResourceType);

        if (!string.IsNullOrWhiteSpace(query.ResourceId))
            q = q.Where(a => a.ResourceId == query.ResourceId);

        if (!string.IsNullOrWhiteSpace(query.Action))
            q = q.Where(a => a.Action == query.Action);

        if (!string.IsNullOrWhiteSpace(query.EventCategory))
            q = q.Where(a => a.EventCategory == query.EventCategory);

        if (!string.IsNullOrWhiteSpace(query.CorrelationId))
            q = q.Where(a => a.CorrelationId == query.CorrelationId);

        if (!string.IsNullOrWhiteSpace(query.TraceId))
            q = q.Where(a => a.TraceId == query.TraceId);

        var orderBy = query.SortBy?.ToLower() switch
        {
            "timestamp" => query.SortDescending ? q.OrderByDescending(a => a.Timestamp) : q.OrderBy(a => a.Timestamp),
            "userId" => query.SortDescending ? q.OrderByDescending(a => a.UserId) : q.OrderBy(a => a.UserId),
            "eventType" => query.SortDescending ? q.OrderByDescending(a => a.EventType) : q.OrderBy(a => a.EventType),
            "outcome" => query.SortDescending ? q.OrderByDescending(a => a.Outcome) : q.OrderBy(a => a.Outcome),
            "riskLevel" => query.SortDescending ? q.OrderByDescending(a => a.RiskLevel) : q.OrderBy(a => a.RiskLevel),
            _ => query.SortDescending ? q.OrderByDescending(a => a.Timestamp) : q.OrderBy(a => a.Timestamp)
        );

        return await orderBy
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);
    }

    public async Task<long> CountAsync(AuditQuery query, CancellationToken ct = default)
    {
        var q = _dbContext.AuditLogEntries.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.TenantId))
            q = q.Where(a => a.TenantId == query.TenantId);

        if (!string.IsNullOrWhiteSpace(query.UserId))
            q = q.Where(a => a.UserId == query.UserId);

        if (query.EventType.HasValue)
            q = q.Where(a => a.EventType == query.EventType.Value);

        if (query.Outcome.HasValue)
            q = q.Where(a => a.Outcome == query.Outcome.Value);

        if (query.RiskLevel.HasValue)
            q = q.Where(a => a.RiskLevel == query.RiskLevel.Value);

        if (query.FromDate.HasValue)
            q = q.Where(a => a.Timestamp >= query.FromDate.Value);

        if (query.ToDate.HasValue)
            q = q.Where(a => a.Timestamp <= query.ToDate.Value);

        if (!string.IsNullOrWhiteSpace(query.ResourceType))
            q = q.Where(a => a.ResourceType == query.ResourceType);

        if (!string.IsNullOrWhiteSpace(query.ResourceId))
            q = q.Where(a => a.ResourceId == query.ResourceId);

        if (!string.IsNullOrWhiteSpace(query.Action))
            q = q.Where(a => a.Action == query.Action);

        if (!string.IsNullOrWhiteSpace(query.EventCategory))
            q = q.Where(a => a.EventCategory == query.EventCategory);

        if (!string.IsNullOrWhiteSpace(query.CorrelationId))
            q = q.Where(a => a.CorrelationId == query.CorrelationId);

        if (!string.IsNullOrWhiteSpace(query.TraceId))
            q = q.Where(a => a.TraceId == query.TraceId);

        return await q.LongCountAsync(ct);
    }

    public async Task<AuditStats> GetStatisticsAsync(string tenantId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var query = _dbContext.AuditLogEntries
            .Where(a => a.TenantId == tenantId && a.Timestamp >= from && a.Timestamp <= to);

        var totalEvents = await query.LongCountAsync(ct);
        var successCount = await query.CountAsync(a => a.Outcome == AuditOutcome.Success, ct);
        var failureCount = await query.CountAsync(a => a.Outcome == AuditOutcome.Failure, ct);
        var criticalCount = await query.CountAsync(a => a.RiskLevel == RiskLevel.Critical, ct);
        var highCount = await query.CountAsync(a => a.RiskLevel == RiskLevel.High, ct);
        var mediumCount = await query.CountAsync(a => a.RiskLevel == RiskLevel.Medium, ct);
        var lowCount = await query.CountAsync(a => a.RiskLevel == RiskLevel.Low, ct);

        var eventsByType = await _dbContext.AuditLogEntries
            .Where(a => a.TenantId == query.TenantId && a.Timestamp >= from && a.Timestamp <= to)
            .GroupBy(a => a.EventType)
            .Select(g => new { Type = g.Key, Count = g.LongCount() })
            .ToDictionaryAsync(g => g.Type.ToString(), g => g.Count, ct);

        var eventsByCategory = await _dbContext.AuditLogEntries
            .Where(a => a.TenantId == query.TenantId && a.Timestamp >= from && a.Timestamp <= to)
            .GroupBy(a => a.EventCategory)
            .Select(g => new { Category = g.Key, Count = g.LongCount() })
            .ToDictionaryAsync(g => g.Category, g => g.Count, ct);

        var eventsByOutcome = await _dbContext.AuditLogEntries
            .Where(a => a.TenantId == query.TenantId && a.Timestamp >= from && a.Timestamp <= to)
            .GroupBy(a => a.Outcome)
            .Select(g => new { Outcome = g.Key, Count = g.LongCount() })
            .ToDictionaryAsync(g => g.Outcome.ToString(), g => g.Count, ct);

        var eventsByRiskLevel = await _dbContext.AuditLogEntries
            .Where(a => a.TenantId == query.TenantId && a.Timestamp >= from && a.Timestamp <= to)
            .GroupBy(a => a.RiskLevel)
            .Select(g => new { Risk = g.Key, Count = g.LongCount() })
            .ToDictionaryAsync(g => g.Risk.ToString(), g => g.Count, ct);

        var eventsByUser = await _dbContext.AuditLogEntries
            .Where(a => a.TenantId == query.TenantId && a.Timestamp >= from && a.Timestamp <= to)
            .GroupBy(a => a.UserId)
            .Select(g => new { UserId = g.Key, Count = g.LongCount() })
            .OrderByDescending(g => g.Count)
            .Take(20)
            .ToDictionaryAsync(g => g.UserId, g => g.Count, ct);

        var eventsByResourceType = await _dbContext.AuditLogEntries
            .Where(a => a.TenantId == query.TenantId && a.Timestamp >= from && a.Timestamp <= to)
            .GroupBy(a => a.ResourceType)
            .Select(g => new { Type = g.Key, Count = g.LongCount() })
            .ToDictionaryAsync(g => g.Type, g => g.Count, ct);

        var avgDuration = await _dbContext.AuditLogEntries
            .Where(a => a.TenantId == query.TenantId && a.Timestamp >= from && a.Timestamp <= to && a.Duration != default)
            .Select(a => a.Duration.TotalMilliseconds)
            .AverageAsync(ct);

        var errorRate = totalEvents > 0 ? (double)failureCount / totalEvents : 0;

        return new AuditStats
        {
            TotalEvents = totalEvents,
            SuccessCount = successCount,
            FailureCount = failureCount,
            CriticalCount = criticalCount,
            HighCount = highCount,
            MediumCount = mediumCount,
            LowCount = lowCount,
            EventsByType = eventsByType,
            EventsByCategory = eventsByCategory,
            EventsByOutcome = eventsByOutcome,
            EventsByRiskLevel = eventsByRiskLevel,
            EventsByUser = eventsByUser,
            EventsByResourceType = eventsByResourceType,
            AverageDuration = TimeSpan.FromMilliseconds(avgDuration),
            ErrorRate = errorRate
        };
    }

    public async Task<byte[]> ExportAsync(AuditQuery query, ExportFormat format, CancellationToken ct = default)
    {
        var entries = await QueryAsync(query, ct);

        return format switch
        {
            ExportFormat.Csv => ExportToCsv(entries),
            ExportFormat.Json => ExportToJson(entries),
            ExportFormat.Excel => ExportToExcel(entries),
            ExportFormat.Pdf => ExportToPdf(entries),
            ExportFormat.Parquet => ExportToParquet(entries),
            _ => throw new ArgumentException($"Unsupported format: {format}")
        };
    }

    public async Task<bool> PurgeAsync(DateTime beforeDate, string tenantId, CancellationToken ct = default)
    {
        var entries = await _dbContext.AuditLogEntries
            .Where(a => a.TenantId == tenantId && a.Timestamp < beforeDate)
            .ToListAsync(ct);

        if (!entries.Any()) return true;

        _dbContext.AuditLogEntries.RemoveRange(entries);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Purged {Count} audit log entries before {Date}", entries.Count, beforeDate);
        return true;
    }

    public async Task<AuditLogEntry?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbContext.AuditLogEntries.FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    // Helper methods
    private byte[] ExportToCsv(List<AuditLogEntry> entries)
    {
        using var memoryStream = new MemoryStream();
        using var writer = new StreamWriter(memoryStream);
        using var csv = new CsvWriter(writer, new CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture));
        
        csv.WriteRecords(entries.Select(e => new
        {
            e.Id,
            e.Timestamp,
            e.TenantId,
            e.UserId,
            e.UserName,
            e.EventType,
            e.EventCategory,
            e.Action,
            e.ResourceType,
            e.ResourceId,
            e.Outcome,
            e.RiskLevel,
            e.HttpMethod,
            e.RequestPath,
            e.Duration,
            e.ErrorMessage
        }));
        
        writer.Flush();
        return memoryStream.ToArray();
    }

    private byte[] ExportToJson(List<AuditLogEntry> entries)
    {
        var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
        return Encoding.UTF8.GetBytes(json);
    }

    private byte[] ExportToExcel(List<AuditLogEntry> entries)
    {
        // Would use EPPlus or similar
        return Encoding.UTF8.GetBytes("Excel export requires EPPlus library");
    }

    private byte[] ExportToPdf(List<AuditLogEntry> entries)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(50);
                page.Header().Text("KKBOT Audit Log Export").FontSize(20).Bold();
                
                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2); // Timestamp
                        columns.RelativeColumn(1); // User
                        columns.RelativeColumn(1); // Event Type
                        columns.RelativeColumn(1); // Action
                        columns.RelativeColumn(1); // Outcome
                        columns.RelativeColumn(1); // Risk Level
                    });

                    table.Header(header =>
                    {
                        header.Cell().Text("Timestamp").Bold();
                        header.Cell().Text("User").Bold();
                        header.Cell().Text("Event Type").Bold();
                        header.Cell().Text("Action").Bold();
                        header.Cell().Text("Outcome").Bold();
                        header.Cell().Text("Risk").Bold();
                    });

                    foreach (var entry in entries)
                    {
                        table.Cell().Text(entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
                        table.Cell().Text(entry.UserName);
                        table.Cell().Text(entry.EventType.ToString());
                        table.Cell().Text(entry.Action);
                        table.Cell().Text(entry.Outcome.ToString());
                        header.Cell().Text(entry.RiskLevel.ToString());
                    }
                });
            });
        }

        using var stream = new MemoryStream();
        document.GeneratePdf(stream);
        return stream.ToArray();
    }

    private byte[] ExportToParquet(List<AuditLogEntry> entries)
    {
        // Would use Apache Parquet library
        return Encoding.UTF8.GetBytes("Parquet export requires Apache Parquet library");
    }
}