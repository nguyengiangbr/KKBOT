// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using KKBOT.LicenseServer.Data;
using KKBOT.Observability;

namespace KKBOT.LicenseServer.Services;

/// <summary>
/// Audit logging service for compliance and security tracking.
/// </summary>
public interface IAuditService
{
    Task LogAsync(string action, string? licenseKey = null, string? userId = null, object? details = null, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default);
    Task<PagedResult<AuditLogEntity>> GetAuditLogsAsync(AuditLogFilter filter, CancellationToken ct = default);
    Task<AuditLogEntity?> GetAuditLogAsync(Guid id, CancellationToken ct = default);
    Task<long> GetAuditLogCountAsync(AuditLogFilter filter, CancellationToken ct = default);
    Task ExportAuditLogsAsync(Stream output, AuditLogFilter filter, CancellationToken ct = default);
}

public sealed class AuditService : IAuditService
{
    private readonly LicenseDbContext _db;
    private readonly ILogger<AuditService> _logger;
    private readonly IMetricsRegistry _metrics;

    public AuditService(
        LicenseDbContext db,
        ILogger<AuditService> logger,
        IMetricsRegistry metrics)
    {
        _db = db;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task LogAsync(string action, string? licenseKey = null, string? userId = null, object? details = null, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
    {
        try
        {
            var audit = new AuditLogEntity
            {
                Action = action,
                LicenseId = licenseKey,
                UserId = userId,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Details = details != null ? JsonSerializer.Serialize(details) : "{}",
                CorrelationId = Guid.NewGuid().ToString("N")
            };

            _db.AuditLogs.Add(audit);
            await _db.SaveChangesAsync(ct);

            _metrics.IncrementCounter("audit_log_entries_total", new Dictionary<string, string> { ["action"] = action });
        }
        catch (Exception ex)
        {
            // Log but don't throw - audit failures shouldn't break core functionality
            Console.WriteLine($"Failed to log audit: {ex.Message}");
        }
    }

    public async Task<PagedResult<AuditLogEntity>> GetAuditLogsAsync(AuditLogFilter filter, CancellationToken ct = default)
    {
        var query = _db.AuditLogs.AsQueryable();

        if (!string.IsNullOrEmpty(filter.Action))
        {
            query = query.Where(a => a.Action == filter.Action);
        }

        if (!string.IsNullOrEmpty(filter.LicenseKey))
        {
            query = query.Where(a => a.LicenseId == filter.LicenseKey);
        }

        if (!string.IsNullOrEmpty(filter.UserId))
        {
            query = query.Where(a => a.UserId == filter.UserId);
        }

        if (filter.From.HasValue)
        {
            query = query.Where(a => a.Timestamp >= filter.From.Value);
        }

        if (filter.To.HasValue)
        {
            query = query.Where(a => a.Timestamp <= filter.To.Value);
        }

        if (!string.IsNullOrEmpty(filter.CorrelationId))
        {
            query = query.Where(a => a.CorrelationId == filter.CorrelationId);
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip(filter.Offset)
            .Take(filter.Limit)
            .ToListAsync(ct);

        return new PagedResult<AuditLogEntity>
        {
            Items = items,
            TotalCount = totalCount,
            Offset = filter.Offset,
            Limit = filter.Limit
        };
    }

    public async Task<AuditLogEntity?> GetAuditLogAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.AuditLogs.FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    public async Task<long> GetAuditLogCountAsync(AuditLogFilter filter, CancellationToken ct = default)
    {
        var query = _db.AuditLogs.AsQueryable();

        if (!string.IsNullOrEmpty(filter.Action))
        {
            query = query.Where(a => a.Action == filter.Action);
        }

        if (!string.IsNullOrEmpty(filter.LicenseKey))
        {
            query = query.Where(a => a.LicenseId == filter.LicenseKey);
        }

        if (!string.IsNullOrEmpty(filter.UserId))
        {
            query = query.Where(a => a.UserId == filter.UserId);
        }

        if (filter.From.HasValue)
        {
            query = query.Where(a => a.Timestamp >= filter.From.Value);
        }

        if (filter.To.HasValue)
        {
            query = query.Where(a => a.Timestamp <= filter.To.Value);
        }

        return await query.CountAsync(ct);
    }

    public async Task ExportAuditLogsAsync(Stream output, AuditLogFilter filter, CancellationToken ct = default)
    {
        var query = _db.AuditLogs.AsQueryable();

        if (!string.IsNullOrEmpty(filter.Action))
        {
            query = query.Where(a => a.Action == filter.Action);
        }

        if (!string.IsNullOrEmpty(filter.LicenseKey))
        {
            query = query.Where(a => a.LicenseId == filter.LicenseKey);
        }

        if (!string.IsNullOrEmpty(filter.UserId))
        {
            query = query.Where(a => a.UserId == filter.UserId);
        }

        if (filter.From.HasValue)
        {
            query = query.Where(a => a.Timestamp >= filter.From.Value);
        }

        if (filter.To.HasValue)
        {
            query = query.Where(a => a.Timestamp <= filter.To.Value);
        }

        var logs = await query
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync(ct);

        var csv = new StringBuilder();
        csv.AppendLine("Id,Action,LicenseId,UserId,IpAddress,UserAgent,Details,Timestamp,CorrelationId");

        foreach (var log in logs)
        {
            var details = log.Details.Replace("\"", "\"\"");
            csv.AppendLine($"{log.Id},{log.Action},{log.LicenseId},{log.UserId},{log.IpAddress},{log.UserAgent},\"{details}\",{log.Timestamp:O},{log.CorrelationId}");
        }

        var bytes = Encoding.UTF8.GetBytes(csv.ToString());
        await output.WriteAsync(bytes, ct);
    }
}

public sealed class AuditLogFilter
{
    public string? Action { get; set; }
    public string? LicenseKey { get; set; }
    public string? UserId { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public string? CorrelationId { get; set; }
    public int Offset { get; set; } = 0;
    public int Limit { get; set; } = 100;
}

public sealed class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public long TotalCount { get; set; }
    public int Offset { get; set; }
    public int Limit { get; set; }
    public bool HasNextPage => Offset + Limit < TotalCount;
    public bool HasPreviousPage => Offset > 0;
}
