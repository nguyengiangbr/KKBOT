// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KKBOT.LicenseServer.RateLimiting;

/// <summary>
/// Rate limiting middleware with sliding window algorithm.
/// </summary>
public sealed class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly RateLimitingOptions _options;
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, RateLimitBucket> _buckets = new();

    public RateLimitingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitingMiddleware> logger,
        IOptions<RateLimitingOptions> options,
        IMemoryCache cache)
    {
        _next = next;
        _logger = logger;
        _options = options.Value;
        _cache = cache;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.Enabled)
        {
            await _next(context);
            return;
        }

        // Skip rate limiting for health checks
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }

        // Get client identifier
        var clientId = GetClientIdentifier(context);
        var policy = GetPolicyForPath(context.Request.Path);
        
        var bucket = _buckets.GetOrAdd(clientId, _ => new RateLimitBucket());
        
        lock (bucket)
        {
            var now = DateTimeOffset.UtcNow;
            
            // Remove expired entries
            bucket.Requests.RemoveAll(r => r < now - policy.Window);
            
            if (bucket.Requests.Count >= policy.PermitLimit)
            {
                var oldestRequest = bucket.Requests[0];
                var retryAfter = policy.Window - (DateTimeOffset.UtcNow - oldestRequest);
                
                context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                context.Response.Headers["Retry-After"] = Math.Ceiling(retryAfter.TotalSeconds).ToString();
                context.Response.Headers["X-RateLimit-Limit"] = policy.PermitLimit.ToString();
                context.Response.Headers["X-RateLimit-Remaining"] = "0";
                context.Response.Headers["X-RateLimit-Reset"] = DateTimeOffset.UtcNow.Add(retryAfter).ToUnixTimeSeconds().ToString();
                
                _logger.LogWarning("Rate limit exceeded for {ClientId} on {Path}", clientId, context.Request.Path);
                
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Rate limit exceeded",
                    message = "Too many requests. Try again in " + Math.Ceiling(retryAfter.TotalSeconds) + " seconds.",
                    retryAfterSeconds = Math.Ceiling(retryAfter.TotalSeconds)
                });
                return;
            }

            bucket.Requests.Add(now);
            
            context.Response.Headers["X-RateLimit-Limit"] = policy.PermitLimit.ToString();
            context.Response.Headers["X-RateLimit-Remaining"] = (policy.PermitLimit - bucket.Requests.Count).ToString();
            context.Response.Headers["X-RateLimit-Reset"] = DateTimeOffset.UtcNow.Add(policy.Window).ToUnixTimeSeconds().ToString();
        }

        await _next(context);
    }

    private string GetClientIdentifier(HttpContext context)
    {
        // Try to get user ID from claims
        var userId = context.User?.FindFirst("sub")?.Value
            ?? context.User?.FindFirst("user_id")?.Value;
            
        if (!string.IsNullOrEmpty(userId))
        {
            return "user:" + userId;
        }

        // Fall back to IP address
        var ip = context.Connection.RemoteIpAddress?.ToString() 
            ?? context.Request.Headers["X-Forwarded-For"].FirstOrDefault() 
            ?? "unknown";
            
        return "ip:" + ip;
    }

    private RateLimitPolicy GetPolicyForPath(PathString path)
    {
        var pathStr = path.Value?.ToLowerInvariant() ?? "";
        
        // Check for specific policy matches
        foreach (var policy in _options.Policies)
        {
            if (pathStr.StartsWith(policy.Path.ToLowerInvariant()))
            {
                return policy;
            }
        }
        
        // Default policy
        return _options.DefaultPolicy;
    }
}

/// <summary>
/// Rate limit bucket for tracking requests.
/// </summary>
internal sealed class RateLimitBucket
{
    public List<DateTimeOffset> Requests { get; } = new();
}

/// <summary>
/// Rate limit policy configuration.
/// </summary>
public sealed class RateLimitPolicy
{
    public string Path { get; init; } = "/";
    public int PermitLimit { get; init; } = 100;
    public TimeSpan Window { get; init; } = TimeSpan.FromMinutes(1);
}

/// <summary>
/// Rate limiting configuration options.
/// </summary>
public sealed class RateLimitingOptions
{
    public bool Enabled { get; set; } = true;
    public RateLimitPolicy DefaultPolicy { get; set; } = new RateLimitPolicy
    {
        Path = "/",
        PermitLimit = 100,
        Window = TimeSpan.FromMinutes(1)
    };
    public List<RateLimitPolicy> Policies { get; set; } = new()
    {
        new RateLimitPolicy { Path = "/api/license/validate", PermitLimit = 1000, Window = TimeSpan.FromMinutes(1) },
        new RateLimitPolicy { Path = "/api/license/activate", PermitLimit = 10, Window = TimeSpan.FromMinutes(1) },
        new RateLimitPolicy { Path = "/api/license/", PermitLimit = 50, Window = TimeSpan.FromMinutes(1) },
        new RateLimitPolicy { Path = "/api/auth/", PermitLimit = 5, Window = TimeSpan.FromMinutes(1) },
        new RateLimitPolicy { Path = "/api/ai/", PermitLimit = 30, Window = TimeSpan.FromMinutes(1) },
    };
}

/// <summary>
/// Extension methods for registering rate limiting.
/// </summary>
public static class RateLimitingExtensions
{
    public static IServiceCollection AddKkbotRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RateLimitingOptions>(configuration.GetSection("RateLimiting"));
        services.AddMemoryCache();
        services.AddSingleton<RateLimitingMiddleware>();
        return services;
    }
    
    public static IApplicationBuilder UseKkbotRateLimiting(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RateLimitingMiddleware>();
    }
}
