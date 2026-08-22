// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Collaboration.Abstractions;
using KKBOT.Collaboration.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KKBOT.Collaboration.Services;

public sealed class PresenceService : IPresenceService
{
    private readonly CollaborationDbContext _dbContext;
    private readonly ILogger<PresenceService> _logger;
    private readonly ConcurrentDictionary<string, PresenceInfo> _presences = new();

    public PresenceService(CollaborationDbContext dbContext, ILogger<PresenceService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<PresenceInfo> UpdatePresenceAsync(string userId, PresenceUpdateRequest request, CancellationToken ct = default)
    {
        var presence = _presences.AddOrUpdate(userId,
            _ => new PresenceInfo
            {
                UserId = userId,
                UserName = request.CurrentActivity ?? userId,
                Status = request.Status ?? PresenceStatus.Online,
                CurrentActivity = request.CurrentActivity,
                CurrentInstanceId = request.CurrentInstanceId,
                CurrentSessionId = request.CurrentSessionId,
                CustomStatus = request.CustomStatus ?? new Dictionary<string, object>(),
                LastSeenAt = DateTime.UtcNow,
                LastActiveAt = DateTime.UtcNow
            },
            (_, existing) =>
            {
                if (request.Status.HasValue) existing.Status = request.Status.Value;
                if (request.CurrentActivity != null) existing.CurrentActivity = request.CurrentActivity;
                if (request.CurrentInstanceId != null) existing.CurrentInstanceId = request.CurrentInstanceId;
                if (request.CurrentSessionId != null) existing.CurrentSessionId = request.CurrentSessionId;
                if (request.CustomStatus != null) existing.CustomStatus = request.CustomStatus;
                existing.LastActiveAt = DateTime.UtcNow;
                return existing;
            });

        return presence;
    }

    public async Task<PresenceInfo?> GetPresenceAsync(string userId, CancellationToken ct = default)
    {
        _presences.TryGetValue(userId, out var presence);
        return presence;
    }

    public async Task<List<PresenceInfo>> GetPresencesAsync(string tenantId, PresenceFilter filter, CancellationToken ct = default)
    {
        var query = _presences.Values.AsEnumerable();

        if (filter.Status.HasValue)
            query = query.Where(p => p.Status == filter.Status.Value);

        if (filter.IsOnline.HasValue)
        {
            if (filter.IsOnline.Value)
                query = query.Where(p => p.Status != PresenceStatus.Offline);
            else
                query = query.Where(p => p.Status == PresenceStatus.Offline);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.ToLowerInvariant();
            query = query.Where(p => p.UserName.ToLower().Contains(search) || p.UserId.ToLower().Contains(search));
        }

        query = query.OrderByDescending(p => p.LastActiveAt);

        return query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToList();
    }

    public async Task<List<PresenceInfo>> GetOnlineUsersAsync(string tenantId, CancellationToken ct = default)
    {
        return _presences.Values
            .Where(p => p.Status != PresenceStatus.Offline)
            .OrderBy(p => p.UserName)
            .ToList();
    }

    public async Task<bool> SetStatusAsync(string userId, PresenceStatus status, string? customStatus = null, CancellationToken ct = default)
    {
        if (_presences.TryGetValue(userId, out var presence))
        {
            presence.Status = status;
            if (customStatus != null)
                presence.CustomStatus["custom"] = customStatus;
            return true;
        }
        return false;
    }

    public async Task<bool> SetCustomStatusAsync(string userId, Dictionary<string, object> customStatus, CancellationToken ct = default)
    {
        if (_presences.TryGetValue(userId, out var presence))
        {
            presence.CustomStatus = customStatus;
            return true;
        }
        return false;
    }
}