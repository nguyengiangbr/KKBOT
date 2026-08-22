// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace KKBOT.Marketplace.Server.Hubs;

public sealed class MarketplaceHub : Hub
{
    private readonly ILogger<MarketplaceHub> _logger;
    private static readonly ConcurrentDictionary<string, HashSet<string>> _userGroups = new();
    private static readonly ConcurrentDictionary<string, string> _connectionUserMap = new();

    public MarketplaceHub(ILogger<MarketplaceHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst("sub")?.Value ?? Context.ConnectionId;
        _connectionUserMap[Context.ConnectionId] = userId;

        // Add to user-specific group
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");

        // Add to global marketplace group
        await Groups.AddToGroupAsync(Context.ConnectionId, "marketplace:global");

        _logger.LogInformation("Client connected: {ConnectionId} (User: {UserId})", Context.ConnectionId, userId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = _connectionUserMap.TryRemove(Context.ConnectionId, out var uid) ? uid : Context.ConnectionId;
        
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user:{userId}");
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "marketplace:global");

        _logger.LogInformation("Client disconnected: {ConnectionId} (User: {UserId})", Context.ConnectionId, userId);
        await base.OnDisconnectedAsync(exception);
    }

    // Plugin events
    public async Task SubscribeToPlugin(string pluginId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"plugin:{pluginId}");
        _logger.LogDebug("Connection {ConnectionId} subscribed to plugin {PluginId}", Context.ConnectionId, pluginId);
    }

    public async Task UnsubscribeFromPlugin(string pluginId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"plugin:{pluginId}");
        _logger.LogDebug("Connection {ConnectionId} unsubscribed from plugin {PluginId}", Context.ConnectionId, pluginId);
    }

    public async Task SubscribeToPublisher(string publisherId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"publisher:{publisherId}");
    }

    public async Task UnsubscribeFromPublisher(string publisherId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"publisher:{publisherId}");
    }

    // Server-to-client methods (called from services)
    public static async Task NotifyPluginPublished(IHubContext<MarketplaceHub> hubContext, string pluginId, object pluginData)
    {
        await hubContext.Clients.Group($"plugin:{pluginId}").SendAsync("PluginPublished", pluginData);
        await hubContext.Clients.Group("marketplace:global").SendAsync("PluginPublished", pluginData);
    }

    public static async Task NotifyVersionReleased(IHubContext<MarketplaceHub> hubContext, string pluginId, string version, object versionData)
    {
        await hubContext.Clients.Group($"plugin:{pluginId}").SendAsync("VersionReleased", new { pluginId, version, data = versionData });
        await hubContext.Clients.Group("marketplace:global").SendAsync("VersionReleased", new { pluginId, version, data = versionData });
    }

    public static async Task NotifyDownloadStarted(IHubContext<MarketplaceHub> hubContext, string pluginId, string version, string userId)
    {
        await hubContext.Clients.Group($"plugin:{pluginId}").SendAsync("DownloadStarted", new { pluginId, version, userId });
        await hubContext.Clients.Group($"user:{userId}").SendAsync("DownloadStarted", new { pluginId, version });
    }

    public static async Task NotifyReviewAdded(IHubContext<MarketplaceHub> hubContext, string pluginId, object reviewData)
    {
        await hubContext.Clients.Group($"plugin:{pluginId}").SendAsync("ReviewAdded", reviewData);
    }

    public static async Task NotifyModerationUpdate(IHubContext<MarketplaceHub> hubContext, string entityType, string entityId, string status)
    {
        await hubContext.Clients.Group("marketplace:moderators").SendAsync("ModerationUpdate", new { entityType, entityId, status });
    }

    public static async Task NotifyPublisherPayout(IHubContext<MarketplaceHub> hubContext, string publisherId, object payoutData)
    {
        await hubContext.Clients.Group($"publisher:{publisherId}").SendAsync("PayoutUpdate", payoutData);
        await hubContext.Clients.Group($"user:{publisherId}").SendAsync("PayoutUpdate", payoutData);
    }

    public static async Task NotifyPluginUpdated(IHubContext<MarketplaceHub> hubContext, string pluginId, object updateData)
    {
        await hubContext.Clients.Group($"plugin:{pluginId}").SendAsync("PluginUpdated", updateData);
    }

    public static async Task NotifyPluginDeleted(IHubContext<MarketplaceHub> hubContext, string pluginId)
    {
        await hubContext.Clients.Group($"plugin:{pluginId}").SendAsync("PluginDeleted", new { pluginId });
        await hubContext.Clients.Group("marketplace:global").SendAsync("PluginDeleted", new { pluginId });
    }
}