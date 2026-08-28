// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KKBOT.Security.Audit;

/// <summary>
/// Middleware for automatic PII redaction in HTTP request/response logging.
/// </summary>
public sealed class PiiRedactionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PiiRedactionMiddleware> _logger;
    private readonly PiiRedactionOptions _options;
    private static readonly Regex[] _piiPatterns = new[]
    {
        // Email addresses
        new Regex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", RegexOptions.Compiled),
        // Phone numbers (various formats)
        new Regex(@"\b(\+?\d{1,3}[-.\s]?)?\(?\d{3}\)?[-.\s]?\d{3}[-.\s]?\d{4}\b", RegexOptions.Compiled),
        // Credit card numbers (basic pattern)
        new Regex(@"\b\d{4}[- ]?\d{4}[- ]?\d{4}[- ]?\d{4}\b", RegexOptions.Compiled),
        // SSN (US format)
        new Regex(@"\b\d{3}-\d{2}-\d{4}\b", RegexOptions.Compiled),
        // IP addresses (IPv4)
        new Regex(@"\b(?:\d{1,3}\.){3}\d{1,3}\b", RegexOptions.Compiled),
        // IPv6 addresses
        new Regex(@"\b(?:[0-9a-fA-F]{1,4}:){7}[0-9a-fA-F]{1,4}\b", RegexOptions.Compiled),
        // MAC addresses
        new Regex(@"\b([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})\b", RegexOptions.Compiled),
        // JWT tokens
        new Regex(@"\beyJ[A-Za-z0-9_-]*\.[A-Za-z0-9_-]*\.[A-Za-z0-9_-]*\b", RegexOptions.Compiled),
        // API keys (common patterns)
        new Regex(@"\b(sk|pk)_(live|test)_[A-Za-z0-9]{24,}\b", RegexOptions.Compiled),
        // Generic API keys
        new Regex(@"\b(api[_-]?key|api[_-]?secret|access[_-]?token)[\s:=]+[A-Za-z0-9_-]{20,}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        // Connection strings
        new Regex(@"Server=.*?;.*?Database=.*?;.*?(User ID|UID|Password|PWD)=.*?[;]*", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        // Credit card with separators
        new Regex(@"\b\d{4}[- ]\d{4}[- ]\d{4}[- ]\d{4}\b", RegexOptions.Compiled),
    };

    public PiiRedactionMiddleware(
        RequestDelegate next,
        ILogger<PiiRedactionMiddleware> logger,
        IOptions<PiiRedactionOptions> options)
    {
        _next = next;
        _logger = logger;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.Enabled)
        {
            await _next(context);
            return;
        }

        // Capture request body
        var originalBody = context.Response.Body;
        using var requestBodyBuffer = new MemoryStream();
        using var responseBodyBuffer = new MemoryStream();
        
        try
        {
            // Enable buffering for request body
            context.Request.EnableBuffering();
            
            // Capture request body
            var requestBody = await ReadRequestBodyAsync(context);
            var redactedRequest = RedactPii(requestBody);
            
            // Capture response
            context.Response.Body = responseBodyBuffer;
            
            await _next(context);
            
            // Capture response body
            responseBodyBuffer.Seek(0, SeekOrigin.Begin);
            var responseBody = await new StreamReader(responseBodyBuffer).ReadToEndAsync();
            var redactedResponse = RedactPii(responseBody);
            
            // Log redacted request/response
            if (_options.LogRequests || _options.LogResponses)
            {
                var logEntry = new
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    Method = context.Request.Method,
                    Path = context.Request.Path.Value,
                    QueryString = context.Request.QueryString.Value,
                    StatusCode = context.Response.StatusCode,
                    RequestBody = _options.LogRequests ? redactedRequest : "[REDACTED]",
                    ResponseBody = _options.LogResponses ? redactedResponse : "[REDACTED]",
                    UserAgent = context.Request.Headers.UserAgent.ToString(),
                    ClientIP = GetClientIp(context),
                    UserId = GetUserId(context),
                    CorrelationId = GetCorrelationId(context)
                };

                var logJson = JsonSerializer.Serialize(logEntry, new JsonSerializerOptions
                {
                    WriteIndented = false
                });

                _logger.LogInformation("HTTP {Method} {Path} - {StatusCode} - {CorrelationId}",
                    context.Request.Method, context.Request.Path, context.Response.StatusCode, GetCorrelationId(context));

                if (_options.LogFullDetails)
                {
                    _logger.LogDebug("Full request/response: {LogEntry}", logJson);
                }
            }
            
            // Restore response body
            responseBodyBuffer.Seek(0, SeekOrigin.Begin);
            await responseBodyBuffer.CopyToAsync(originalBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in PII redaction middleware");
            throw;
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    private async Task<string> ReadRequestBodyAsync(HttpContext context)
    {
        context.Request.EnableBuffering();
        context.Request.Body.Position = 0;
        using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;
        return body;
    }

    private string RedactPii(string input)
    {
        if (string.IsNullOrEmpty(input) || !_options.Enabled)
        {
            return input;
        }

        var result = input;
        
        foreach (var pattern in _piiPatterns)
        {
            result = pattern.Replace(result, _options.ReplacementToken);
        }

        // Additional JSON-specific redaction
        if (_options.RedactJsonFields && (input.TrimStart().StartsWith('{') || input.TrimStart().StartsWith('[')))
        {
            try
            {
                var jsonDoc = JsonDocument.Parse(input);
                var redactedJson = RedactJsonFields(jsonDoc.RootElement);
                return JsonSerializer.Serialize(redactedJson);
            }
            catch
            {
                // Not valid JSON, return pattern-redacted version
            }
        }

        return result;
    }

    private JsonElement RedactJsonFields(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var obj = new Dictionary<string, JsonElement>();
            foreach (var prop in element.EnumerateObject())
            {
                if (IsSensitiveField(prop.Name))
                {
                    obj[prop.Name] = JsonDocument.Parse("[REDACTED]").RootElement;
                }
                else
                {
                    obj[prop.Name] = RedactJsonFields(prop.Value);
                }
            }
            return JsonDocument.Parse(JsonSerializer.Serialize(obj)).RootElement;
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var arr = new List<JsonElement>();
            foreach (var item in element.EnumerateArray())
            {
                arr.Add(RedactJsonFields(item));
            }
            return JsonDocument.Parse(JsonSerializer.Serialize(arr)).RootElement;
        }
        return element;
    }

    private bool IsSensitiveField(string fieldName)
    {
        var sensitiveFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "password", "passwd", "pwd", "secret", "token", "apikey", "api_key",
            "access_token", "refresh_token", "authorization", "credential",
            "ssn", "ssn_number", "social_security", "credit_card", "cc_number",
            "cvv", "cvc", "pin", "private_key", "privatekey", "secret_key",
            "access_token", "refresh_token", "id_token", "jwt", "session_id"
        };

        return sensitiveFields.Contains(fieldName);
    }

    private string GetClientIp(HttpContext context)
    {
        return context.Connection.RemoteIpAddress?.ToString()
            ?? context.Request.Headers["X-Forwarded-For"].FirstOrDefault()
            ?? "unknown";
    }

    private string? GetUserId(HttpContext context)
    {
        return context.User?.FindFirst("sub")?.Value
            ?? context.User?.FindFirst("user_id")?.Value;
    }

    private string GetCorrelationId(HttpContext context)
    {
        return context.TraceIdentifier;
    }
}

/// <summary>
/// Options for PII redaction.
/// </summary>
public sealed class PiiRedactionOptions
{
    public bool Enabled { get; set; } = true;
    public bool LogRequests { get; set; } = true;
    public bool LogResponses { get; set; } = false;
    public bool LogFullDetails { get; set; } = false;
    public bool RedactJsonFields { get; set; } = true;
    public string ReplacementToken { get; set; } = "[REDACTED]";
    public string[] CustomPatterns { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Extension methods for registering PII redaction middleware.
/// </summary>
public static class PiiRedactionExtensions
{
    public static IServiceCollection AddPiiRedaction(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PiiRedactionOptions>(configuration.GetSection("PiiRedaction"));
        services.AddTransient<PiiRedactionMiddleware>();
        return services;
    }

    public static IApplicationBuilder UsePiiRedaction(this IApplicationBuilder app)
    {
        return app.UseMiddleware<PiiRedactionMiddleware>();
    }
}