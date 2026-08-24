// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KKBOT.Security.Audit;

/// <summary>
/// Middleware for structured audit logging with PII redaction.
/// </summary>
public sealed class AuditLogMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditLogMiddleware> _logger;
    private readonly AuditLogOptions _options;
    private readonly PiiRedactionMiddleware _piiMiddleware;

    public AuditLogMiddleware(
        RequestDelegate next,
        ILogger<AuditLogMiddleware> logger,
        IOptions<AuditLogOptions> options,
        PiiRedactionMiddleware piiMiddleware)
    {
        _next = next;
        _logger = logger;
        _options = options.Value;
        _piiMiddleware = piiMiddleware;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.Enabled)
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var correlationId = context.TraceIdentifier;
        
        // Capture request
        var requestInfo = await CaptureRequestAsync(context);
        
        try
        {
            await _next(context);
            
            stopwatch.Stop();
            
            // Log successful request
            await LogAuditAsync(context, requestInfo, null, stopwatch.Elapsed, true);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            // Log error
            await LogAuditAsync(context, requestInfo, ex, stopwatch.Elapsed, false);
            throw;
        }
    }

    private async Task<AuditRequestInfo> CaptureRequestAsync(HttpContext context)
    {
        var request = context.Request;
        
        // Enable buffering to allow reading the body multiple times
        request.EnableBuffering();
        
        string body = string.Empty;
        if (request.ContentLength > 0 && request.Body.CanSeek)
        {
            request.Body.Position = 0;
            using var reader = new StreamReader(request.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            request.Body.Position = 0;
        }

        return new AuditRequestInfo
        {
            Method = context.Request.Method,
            Path = context.Request.Path.Value,
            QueryString = context.Request.QueryString.Value,
            Headers = context.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()),
            Body = context.Request.Body.CanSeek ? await ReadBodyAsync(context.Request.Body) : string.Empty,
            ContentType = context.Request.ContentType,
            ContentLength = context.Request.ContentLength
        };
    }

    private async Task<string> ReadBodyAsync(Stream stream)
    {
        stream.Position = 0;
        using var reader = new StreamReader(stream, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        stream.Position = 0;
        return body;
    }

    private async Task LogAuditAsync(HttpContext context, AuditRequestInfo requestInfo, Exception? exception, TimeSpan duration, bool success)
    {
        var auditEntry = new AuditLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            CorrelationId = context.TraceIdentifier,
            UserId = context.User?.FindFirst("sub")?.Value ?? context.User?.FindFirst("user_id")?.Value,
            IpAddress = context.Connection.RemoteIpAddress?.ToString(),
            UserAgent = context.Request.Headers.UserAgent.ToString(),
            Method = context.Request.Method,
            Path = context.Request.Path.Value,
            QueryString = context.Request.QueryString.Value,
            StatusCode = context.Response.StatusCode,
            DurationMs = (long)duration.TotalMilliseconds,
            Success = success,
            ErrorMessage = exception?.Message,
            ErrorType = exception?.GetType().Name,
            Request = new AuditRequestInfo
            {
                Method = context.Request.Method,
                Path = context.Request.Path.Value,
                QueryString = context.Request.QueryString.Value,
                ContentType = context.Request.ContentType,
                ContentLength = context.Request.ContentLength
            },
            Response = new AuditResponseInfo
            {
                StatusCode = context.Response.StatusCode,
                ContentType = context.Response.ContentType
            }
        };

        // Redact PII from the audit entry
        var json = JsonSerializer.Serialize(auditEntry, new JsonSerializerOptions { WriteIndented = false });
        var redactedJson = RedactPii(json);

        // Log to structured log
        if (_options.LogToFile)
        {
            await File.AppendAllTextAsync(_options.LogFilePath, redactedJson + Environment.NewLine);
        }

        if (_options.LogToConsole)
        {
            var level = success ? LogLevel.Information : LogLevel.Error;
            _logger.Log(level, "Audit: {AuditJson}", redactedJson);
        }
    }

    private string RedactPii(string input)
    {
        // Simple PII redaction for audit logs
        var patterns = new[]
        {
            new { Pattern = new Regex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", RegexOptions.Compiled), Replacement = "[EMAIL]" },
            new { Pattern = new Regex(@"\b\d{4}[- ]?\d{4}[- ]?\d{4}[- ]?\d{4}\b", RegexOptions.Compiled), Replacement = "[CREDIT_CARD]" },
            new { Pattern = new Regex(@"\b\d{3}-\d{2}-\d{4}\b", RegexOptions.Compiled), Replacement = "[SSN]" },
            new { Pattern = new Regex(@"\b(?:\d{1,3}\.){3}\d{1,3}\b", RegexOptions.Compiled), Replacement = "[IP]" },
            new { Pattern = new Regex(@"\beyJ[A-Za-z0-9_-]*\.[A-Za-z0-9_-]*\.[A-Za-z0-9_-]*\b", RegexOptions.Compiled), Replacement = "[JWT]" },
            new { Pattern = new Regex(@"(password|token|secret|key|apikey)[\s:=]+[A-Za-z0-9_-]{10,}", RegexOptions.Compiled | RegexOptions.IgnoreCase), Replacement = "[REDACTED]" },
        };

        var result = input;
        foreach (var item in patterns)
        {
            result = item.Pattern.Replace(result, item.Replacement);
        }
        return result;
    }
}

/// <summary>
/// Options for audit logging.
/// </summary>
public sealed class AuditLogOptions
{
    public bool Enabled { get; set; } = true;
    public bool LogToConsole { get; set; } = true;
    public bool LogToFile { get; set; } = true;
    public string LogFilePath { get; set; } = "logs/audit.log";
    public bool IncludeRequestBody { get; set; } = false;
    public bool IncludeResponseBody { get; set; } = false;
    public int MaxBodyLength { get; set; } = 10000;
}

/// <summary>
/// Audit request information.
/// </summary>
public sealed class AuditRequestInfo
{
    public string Method { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string QueryString { get; init; } = string.Empty;
    public Dictionary<string, string> Headers { get; init; } = new();
    public string Body { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long? ContentLength { get; init; }
}

/// <summary>
/// Audit response information.
/// </summary>
public sealed class AuditResponseInfo
{
    public int StatusCode { get; init; }
    public string ContentType { get; init; } = string.Empty;
}

/// <summary>
/// Audit log entry.
/// </summary>
public sealed class AuditLogEntry
{
    public DateTimeOffset Timestamp { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string IpAddress { get; init; } = string.Empty;
    public string UserAgent { get; init; } = string.Empty;
    public string Method { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string QueryString { get; init; } = string.Empty;
    public int StatusCode { get; init; }
    public long DurationMs { get; init; }
    public bool Success { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
    public string ErrorType { get; init; } = string.Empty;
    public AuditRequestInfo Request { get; init; } = new();
    public AuditResponseInfo Response { get; init; } = new();
}

/// <summary>
/// Extension methods for registering audit logging middleware.
/// </summary>
public static class AuditLogExtensions
{
    public static IServiceCollection AddAuditLogging(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AuditLogOptions>(configuration.GetSection("AuditLog"));
        services.AddTransient<AuditLogMiddleware>();
        return services;
    }

    public static IApplicationBuilder UseAuditLogging(this IApplicationBuilder app)
    {
        return app.UseMiddleware<AuditLogMiddleware>();
    }
}