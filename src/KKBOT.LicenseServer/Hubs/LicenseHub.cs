// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using KKBOT.LicenseServer.Data;
using KKBOT.Observability;

namespace KKBOT.LicenseServer.Hubs;

/// <summary>
/// SignalR Hub for real-time license updates.
/// </summary>
[Authorize]
public sealed class LicenseHub : Hub
{
    private readonly ILogger<LicenseHub> _logger;
    private readonly IMetricsRegistry _metrics;
    private readonly ILogger<LicenseHub> _logger;

    public LicenseHub(ILogger<LicenseHub> logger, IMetricsRegistry metrics)
    {
        _logger = logger;
        _metrics = metrics;
    }

    /// <summary>
    /// Called when a client connects.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRoles = Context.User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        var licenseKey = Context.GetHttpContext()?.Request.Query["licenseKey"].ToString();

        _logger.LogInformation("Client connected: {ConnectionId}, User: {UserId}, Roles: {Roles}, LicenseKey: {LicenseKey}",
            Context.ConnectionId, userId, string.Join(",", userRoles ?? new()), licenseKey);

        // Join license-specific group if license key provided
        if (!string.IsNullOrEmpty(licenseKey))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"license:{licenseKey}");
            _logger.LogDebug("Client {ConnectionId} joined license group: {LicenseKey}", Context.ConnectionId, licenseKey);
        }

        // Join role-based groups
        if (userRoles != null)
        {
            foreach (var role in userRoles)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"role:{role}");
            }
        }

        // Track connection metrics
        _metrics.IncrementCounter("license_hub_connections_total");

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        _logger.LogInformation("Client disconnected: {ConnectionId}, User: {UserId}, Exception: {Exception}",
            Context.ConnectionId, userId, exception?.Message);

        _metrics.IncrementCounter("license_hub_disconnections_total");

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Subscribe to license updates.
    /// </summary>
    public async Task SubscribeToLicense(string licenseKey)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        // Verify user has access to this license
        var hasAccess = await HasLicenseAccessAsync(userId, licenseKey);
        if (!hasAccess)
        {
            throw new HubException("Access denied to license");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"license:{licenseKey}");
        
        _logger.LogDebug("Client {ConnectionId} subscribed to license: {LicenseKey}", Context.ConnectionId, licenseKey);
        
        // Send current license status
        await Clients.Caller.SendAsync("LicenseSubscribed", new { licenseKey, subscribedAt = DateTimeOffset.UtcNow });
    }

    /// <summary>
    /// Unsubscribe from license updates.
    /// </summary>
    public async Task UnsubscribeFromLicense(string licenseKey)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"license:{licenseKey}");
        
        _logger.LogDebug("Client {ConnectionId} unsubscribed from license: {LicenseKey}", Context.ConnectionId, licenseKey);
        
        await Clients.Caller.SendAsync("LicenseUnsubscribed", new { licenseKey, unsubscribedAt = DateTimeOffset.UtcNow });
    }

    /// <summary>
    /// Subscribe to multiple licenses (for admin dashboard).
    /// </summary>
    public async Task SubscribeToLicenses(string[] licenseKeys)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRoles = Context.User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? new();
        
        // Admins can subscribe to any license
        var isAdmin = userRoles.Contains("Admin") || userRoles.Contains("LicenseManager");
        
        foreach (var licenseKey in licenseKeys)
        {
            if (!isAdmin)
            {
                var hasAccess = await HasLicenseAccessAsync(userId, licenseKey);
                if (!hasAccess) continue;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"license:{licenseKey}");
        }

        await Clients.Caller.SendAsync("LicensesSubscribed", new { licenseKeys, subscribedAt = DateTimeOffset.UtcNow });
    }

    /// <summary>
    /// Get current license status.
    /// </summary>
    public async Task<LicenseStatusResponse> GetLicenseStatus(string licenseKey)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        var hasAccess = await HasLicenseAccessAsync(userId, licenseKey);
        if (!hasAccess)
        {
            throw new HubException("Access denied to license");
        }

        // This would typically call a service to get license status
        // For now, return a placeholder response
        return new LicenseStatusResponse
        {
            LicenseKey = licenseKey,
            IsActive = true,
            SeatsUsed = 0,
            SeatsTotal = 0,
            LastChecked = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Send heartbeat to keep connection alive.
    /// </summary>
    public Task Heartbeat()
    {
        return Task.CompletedTask;
    }

    private async Task<bool> HasLicenseAccessAsync(string? userId, string licenseKey)
    {
        // In a real implementation, this would check the database
        // For now, admins have access to everything
        var userRoles = Context.User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? new();
        var isAdmin = userRoles.Contains("Admin") || userRoles.Contains("LicenseManager");
        
        if (isAdmin) return true;

        // Check if user owns the license or is in the same organization
        // This would be a database lookup in real implementation
        return false; // Placeholder
    }
}

/// <summary>
/// License status response for SignalR.
/// </summary>
public sealed class LicenseStatusResponse
{
    public string LicenseKey { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int SeatsUsed { get; set; }
    public int SeatsTotal { get; set; }
    public DateTimeOffset LastChecked { get; set; }
}

/// <summary>
/// Hub filter for logging and metrics.
/// </summary>
public sealed class LicenseHubFilter : IHubFilter
{
    private readonly ILogger<LicenseHubFilter> _logger;
    private readonly IMetricsRegistry _metrics;

    public LicenseHubFilter(ILogger<LicenseHubFilter> logger, IMetricsRegistry metrics)
    {
        _logger = logger;
        _metrics = metrics;
    }

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var methodName = invocationContext.HubMethodName;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var result = await next(invocationContext);
            stopwatch.Stop();

            _metrics.RecordHistogram("license_hub_method_duration_seconds", stopwatch.Elapsed.TotalSeconds,
                new Dictionary<string, string> { ["method"] = methodName, ["status"] = "success" });

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metrics.IncrementCounter("license_hub_method_errors_total",
                new Dictionary<string, string> { ["method"] = methodName, ["error"] = ex.GetType().Name });

            _logger.LogError(ex, "Error in hub method {Method}", methodName);
            throw;
        }
    }

    public async Task OnConnectedAsync(HubLifetimeContext context)
    {
        _metrics.IncrementCounter("license_hub_connections_total");
        await Task.CompletedTask;
    }

    public async Task OnDisconnectedAsync(HubLifetimeContext context, Exception? exception)
    {
        _metrics.IncrementCounter("license_hub_disconnections_total");
        await Task.CompletedTask;
    }
}

