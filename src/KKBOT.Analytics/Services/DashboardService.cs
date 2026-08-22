// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Analytics.Abstractions;
using KKBOT.Analytics.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KKBOT.Analytics.Services;

public sealed class DashboardService : IDashboardService
{
    private readonly AnalyticsDbContext _dbContext;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(AnalyticsDbContext dbContext, ILogger<DashboardService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Dashboard> CreateDashboardAsync(CreateDashboardRequest request, string userId, CancellationToken ct = default)
    {
        var dashboard = new Dashboard
        {
            Name = request.Name,
            Description = request.Description,
            IsPublic = request.IsPublic,
            Tags = request.Tags,
            LayoutJson = request.LayoutJson,
            Theme = request.Theme,
            OwnerId = userId,
            TenantId = userId // Simplified: use userId as tenant
        };

        _dbContext.Dashboards.Add(dashboard);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Created dashboard {DashboardId} for user {UserId}", dashboard.Id, userId);
        return dashboard;
    }

    public async Task<Dashboard?> GetDashboardAsync(Guid dashboardId, string userId, CancellationToken ct = default)
    {
        return await _dbContext.Dashboards
            .Include(d => d.Widgets)
            .Include(d => d.Shares)
            .Include(d => d.Subscriptions)
            .FirstOrDefaultAsync(d => d.Id == dashboardId && (d.OwnerId == userId || d.IsPublic || d.Shares.Any(s => s.SharedWithUserId == userId)), ct);
    }

    public async Task<List<Dashboard>> GetDashboardsAsync(string userId, DashboardFilter filter, CancellationToken ct = default)
    {
        var query = _dbContext.Dashboards
            .Where(d => d.OwnerId == userId || d.IsPublic || d.Shares.Any(s => s.SharedWithUserId == userId))
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.ToLower();
            query = query.Where(d => d.Name.ToLower().Contains(search) || d.Description.ToLower().Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(filter.Tag))
        {
            query = query.Where(d => d.Tags.Contains(filter.Tag));
        }

        if (filter.IsPublic.HasValue)
        {
            query = query.Where(d => d.IsPublic == filter.IsPublic.Value);
        }

        if (filter.IsFavorite.HasValue && filter.IsFavorite.Value)
        {
            query = query.Where(d => d.IsFavorite);
        }

        if (!string.IsNullOrWhiteSpace(filter.OwnerId))
        {
            query = query.Where(d => d.OwnerId == filter.OwnerId);
        }

        query = filter.SortBy?.ToLower() switch
        {
            "name" => filter.SortDescending ? query.OrderByDescending(d => d.Name) : query.OrderBy(d => d.Name),
            "createdAt" => filter.SortDescending ? query.OrderByDescending(d => d.CreatedAt) : query.OrderBy(d => d.CreatedAt),
            _ => filter.SortDescending ? query.OrderByDescending(d => d.UpdatedAt) : query.OrderBy(d => d.UpdatedAt)
        };

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);

        return items;
    }

    public async Task<Dashboard> UpdateDashboardAsync(Guid dashboardId, UpdateDashboardRequest request, string userId, CancellationToken ct = default)
    {
        var dashboard = await _dbContext.Dashboards.FirstOrDefaultAsync(d => d.Id == dashboardId && d.OwnerId == userId, ct);
        if (dashboard == null)
            throw new InvalidOperationException("Dashboard not found or access denied");

        if (request.Name != null) dashboard.Name = request.Name;
        if (request.Description != null) dashboard.Description = request.Description;
        if (request.IsPublic.HasValue) dashboard.IsPublic = request.IsPublic.Value;
        if (request.Tags != null) dashboard.Tags = request.Tags;
        if (request.LayoutJson != null) dashboard.LayoutJson = request.LayoutJson;
        if (request.Theme != null) dashboard.Theme = request.Theme;

        dashboard.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
        return dashboard;
    }

    public async Task<bool> DeleteDashboardAsync(Guid dashboardId, string userId, CancellationToken ct = default)
    {
        var dashboard = await _dbContext.Dashboards.FirstOrDefaultAsync(d => d.Id == dashboardId && d.OwnerId == userId, ct);
        if (dashboard == null) return false;

        _dbContext.Dashboards.Remove(dashboard);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<Dashboard> DuplicateDashboardAsync(Guid dashboardId, string userId, CancellationToken ct = default)
    {
        var original = await GetDashboardAsync(dashboardId, userId, ct);
        if (original == null)
            throw new InvalidOperationException("Dashboard not found");

        var duplicate = new Dashboard
        {
            Name = $"{original.Name} (Copy)",
            Description = original.Description,
            IsPublic = false,
            Tags = original.Tags,
            LayoutJson = original.LayoutJson,
            Theme = original.Theme,
            OwnerId = userId,
            TenantId = userId
        };

        _dbContext.Dashboards.Add(duplicate);
        await _dbContext.SaveChangesAsync(ct);

        // Copy widgets
        foreach (var widget in original.Widgets)
        {
            var newWidget = new DashboardWidget
            {
                DashboardId = duplicate.Id,
                WidgetType = widget.WidgetType,
                Title = widget.Title,
                Description = widget.Description,
                ConfigJson = widget.ConfigJson,
                QueryJson = widget.QueryJson,
                PositionX = widget.PositionX,
                PositionY = widget.PositionY,
                Width = widget.Width,
                Height = widget.Height,
                ZIndex = widget.ZIndex,
                IsVisible = widget.IsVisible,
                Options = widget.Options
            };
            _dbContext.DashboardWidgets.Add(newWidget);
        }

        // Copy shares (only for owner)
        foreach (var share in original.Shares)
        {
            _dbContext.DashboardShares.Add(new DashboardShare
            {
                DashboardId = duplicate.Id,
                SharedWithUserId = share.SharedWithUserId,
                SharedWithRole = share.SharedWithRole,
                Permission = share.Permission,
                SharedByUserId = userId
            });
        }

        await _dbContext.SaveChangesAsync(ct);
        return duplicate;
    }

    public async Task<DashboardWidget> AddWidgetAsync(Guid dashboardId, CreateWidgetRequest request, string userId, CancellationToken ct = default)
    {
        var dashboard = await _dbContext.Dashboards.FirstOrDefaultAsync(d => d.Id == dashboardId && (d.OwnerId == userId || d.Shares.Any(s => s.SharedWithUserId == userId && s.Permission >= SharePermission.Edit)), ct);
        if (dashboard == null)
            throw new InvalidOperationException("Dashboard not found or access denied");

        var widget = new DashboardWidget
        {
            DashboardId = dashboardId,
            WidgetType = request.WidgetType,
            Title = request.Title,
            Description = request.Description,
            ConfigJson = request.ConfigJson,
            QueryJson = request.QueryJson,
            PositionX = request.PositionX,
            PositionY = request.PositionY,
            Width = request.Width,
            Height = request.Height,
            Options = request.Options
        };

        _dbContext.DashboardWidgets.Add(widget);
        await _dbContext.SaveChangesAsync(ct);
        return widget;
    }

    public async Task<DashboardWidget> UpdateWidgetAsync(Guid widgetId, UpdateWidgetRequest request, string userId, CancellationToken ct = default)
    {
        var widget = await _dbContext.DashboardWidgets
            .Include(w => w.Dashboard)
            .FirstOrDefaultAsync(w => w.Id == widgetId && (w.Dashboard.OwnerId == userId || w.Dashboard.Shares.Any(s => s.SharedWithUserId == userId && s.Permission >= SharePermission.Edit)), ct);
        
        if (widget == null)
            throw new InvalidOperationException("Widget not found or access denied");

        if (request.Title != null) widget.Title = request.Title;
        if (request.Description != null) widget.Description = request.Description;
        if (request.ConfigJson != null) widget.ConfigJson = request.ConfigJson;
        if (request.QueryJson != null) widget.QueryJson = request.QueryJson;
        if (request.PositionX.HasValue) widget.PositionX = request.PositionX.Value;
        if (request.PositionY.HasValue) widget.PositionY = request.PositionY.Value;
        if (request.Width.HasValue) widget.Width = request.Width.Value;
        if (request.Height.HasValue) widget.Height = request.Height.Value;
        if (request.IsVisible.HasValue) widget.IsVisible = request.IsVisible.Value;
        if (request.Options != null) widget.Options = request.Options;

        widget.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
        return widget;
    }

    public async Task<bool> RemoveWidgetAsync(Guid widgetId, string userId, CancellationToken ct = default)
    {
        var widget = await _dbContext.DashboardWidgets
            .Include(w => w.Dashboard)
            .FirstOrDefaultAsync(w => w.Id == widgetId && (w.Dashboard.OwnerId == userId || w.Dashboard.Shares.Any(s => s.SharedWithUserId == userId && s.Permission >= SharePermission.Edit)), ct);
        
        if (widget == null) return false;

        _dbContext.DashboardWidgets.Remove(widget);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ReorderWidgetsAsync(Guid dashboardId, List<WidgetPosition> positions, string userId, CancellationToken ct = default)
    {
        var dashboard = await _dbContext.Dashboards.FirstOrDefaultAsync(d => d.Id == dashboardId && d.OwnerId == userId, ct);
        if (dashboard == null) return false;

        var widgets = await _dbContext.DashboardWidgets.Where(w => w.DashboardId == dashboardId).ToListAsync(ct);
        var widgetDict = widgets.ToDictionary(w => w.Id);

        foreach (var pos in positions)
        {
            if (widgetDict.TryGetValue(pos.WidgetId, out var widget))
            {
                widget.PositionX = pos.PositionX;
                widget.PositionY = pos.PositionY;
                widget.Width = pos.Width;
                widget.Height = pos.Height;
                widget.ZIndex = pos.ZIndex;
            }
        }

        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<DashboardShare> ShareDashboardAsync(Guid dashboardId, ShareDashboardRequest request, string userId, CancellationToken ct = default)
    {
        var dashboard = await _dbContext.Dashboards.FirstOrDefaultAsync(d => d.Id == dashboardId && d.OwnerId == userId, ct);
        if (dashboard == null)
            throw new InvalidOperationException("Dashboard not found or access denied");

        var existingShare = await _dbContext.DashboardShares
            .FirstOrDefaultAsync(s => s.DashboardId == dashboardId && s.SharedWithUserId == request.SharedWithUserId, ct);
        
        if (existingShare != null)
        {
            existingShare.Permission = request.Permission;
            existingShare.SharedWithRole = request.SharedWithRole;
            await _dbContext.SaveChangesAsync(ct);
            return existingShare;
        }

        var share = new DashboardShare
        {
            DashboardId = dashboardId,
            SharedWithUserId = request.SharedWithUserId,
            SharedWithRole = request.SharedWithRole,
            Permission = request.Permission,
            SharedByUserId = userId
        };

        _dbContext.DashboardShares.Add(share);
        await _dbContext.SaveChangesAsync(ct);
        return share;
    }

    public async Task<bool> UpdateShareAsync(Guid shareId, UpdateShareRequest request, string userId, CancellationToken ct = default)
    {
        var share = await _dbContext.DashboardShares
            .Include(s => s.Dashboard)
            .FirstOrDefaultAsync(s => s.Id == shareId && s.Dashboard.OwnerId == userId, ct);
        
        if (share == null) return false;

        if (request.Permission.HasValue) share.Permission = request.Permission.Value;
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> RemoveShareAsync(Guid shareId, string userId, CancellationToken ct = default)
    {
        var share = await _dbContext.DashboardShares
            .Include(s => s.Dashboard)
            .FirstOrDefaultAsync(s => s.Id == shareId && s.Dashboard.OwnerId == userId, ct);
        
        if (share == null) return false;

        _dbContext.DashboardShares.Remove(share);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<DashboardSubscription> CreateSubscriptionAsync(Guid dashboardId, CreateSubscriptionRequest request, string userId, CancellationToken ct = default)
    {
        var dashboard = await _dbContext.Dashboards.FirstOrDefaultAsync(d => d.Id == dashboardId && (d.OwnerId == userId || d.Shares.Any(s => s.SharedWithUserId == userId)), ct);
        if (dashboard == null)
            throw new InvalidOperationException("Dashboard not found or access denied");

        var subscription = new DashboardSubscription
        {
            DashboardId = dashboardId,
            SubscriberId = userId,
            Frequency = request.Frequency,
            Format = request.Format,
            DeliveryMethod = request.DeliveryMethod,
            DeliveryConfigJson = request.DeliveryConfigJson
        };

        _dbContext.DashboardSubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync(ct);
        return subscription;
    }

    public async Task<bool> UpdateSubscriptionAsync(Guid subscriptionId, UpdateSubscriptionRequest request, string userId, CancellationToken ct = default)
    {
        var subscription = await _dbContext.DashboardSubscriptions
            .FirstOrDefaultAsync(s => s.Id == subscriptionId && s.SubscriberId == userId, ct);
        
        if (subscription == null) return false;

        if (request.Frequency.HasValue) subscription.Frequency = request.Frequency.Value;
        if (request.Format != null) subscription.Format = request.Format;
        if (request.DeliveryMethod != null) subscription.DeliveryMethod = request.DeliveryMethod;
        if (request.DeliveryConfigJson != null) subscription.DeliveryConfigJson = request.DeliveryConfigJson;
        if (request.IsActive.HasValue) subscription.IsActive = request.IsActive.Value;

        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteSubscriptionAsync(Guid subscriptionId, string userId, CancellationToken ct = default)
    {
        var subscription = await _dbContext.DashboardSubscriptions.FirstOrDefaultAsync(s => s.Id == subscriptionId && s.SubscriberId == userId, ct);
        if (subscription == null) return false;

        _dbContext.DashboardSubscriptions.Remove(subscription);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }
}