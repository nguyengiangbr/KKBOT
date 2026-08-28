// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Collaboration.Abstractions;
using KKBOT.Collaboration.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KKBOT.Collaboration.Services;

public sealed class CollaborationSessionService : ICollaborationSessionService
{
    private readonly CollaborationDbContext _dbContext;
    private readonly ILogger<CollaborationSessionService> _logger;

    public CollaborationSessionService(CollaborationDbContext dbContext, ILogger<CollaborationSessionService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<CollaborationSession> CreateSessionAsync(CreateSessionRequest request, string userId, CancellationToken ct = default)
    {
        var session = new CollaborationSession
        {
            Name = request.Name,
            Description = request.Description,
            InstanceId = request.InstanceId,
            OwnerId = userId,
            Type = request.Type,
            Settings = request.Settings ?? new SessionSettings()
        };

        _dbContext.CollaborationSessions.Add(session);
        await _dbContext.SaveChangesAsync(ct);

        // Add owner as participant
        var owner = new SessionParticipant
        {
            SessionId = session.Id,
            UserId = userId,
            UserName = "Session Owner", // Would get from user service
            Role = ParticipantRole.Owner,
            Status = ParticipantStatus.Connected,
            Capabilities = new ParticipantCapabilities
            {
                CanControl = true,
                CanAnnotate = true,
                CanChat = true,
                CanRecord = true,
                CanManageParticipants = true,
                CanChangeSettings = true
            },
            Permissions = new ParticipantPermissions
            {
                CanControlInstance = true,
                CanStartStopInstance = true,
                CanConfigureInstance = true,
                CanViewLogs = true,
                CanAccessConsole = true,
                CanManagePlugins = true,
                CanAccessAI = true,
                CanAccessAnalytics = true
            }
        };

        _dbContext.SessionParticipants.Add(owner);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Created collaboration session {SessionId} for user {UserId}", session.Id, userId);
        return session;
    }

    public async Task<CollaborationSession?> GetSessionAsync(Guid sessionId, string userId, CancellationToken ct = default)
    {
        return await _dbContext.CollaborationSessions
            .Include(s => s.Participants)
            .FirstOrDefaultAsync(s => s.Id == sessionId && (s.OwnerId == userId || s.Participants.Any(p => p.UserId == userId)), ct);
    }

    public async Task<List<CollaborationSession>> GetSessionsAsync(string userId, SessionFilter filter, CancellationToken ct = default)
    {
        var query = _dbContext.CollaborationSessions
            .Where(s => s.OwnerId == userId || s.Participants.Any(p => p.UserId == userId))
            .AsQueryable();

        if (filter.Status.HasValue)
            query = query.Where(s => s.Status == filter.Status.Value);

        if (filter.Type.HasValue)
            query = query.Where(s => s.Type == filter.Type.Value);

        if (filter.InstanceId.HasValue)
            query = query.Where(s => s.InstanceId == filter.InstanceId.Value);

        if (filter.FromDate.HasValue)
            query = query.Where(s => s.CreatedAt >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(s => s.CreatedAt <= filter.ToDate.Value);

        query = filter.SortBy?.ToLower() switch
        {
            "name" => filter.SortDescending ? query.OrderByDescending(s => s.Name) : query.OrderBy(s => s.Name),
            "createdAt" => filter.SortDescending ? query.OrderByDescending(s => s.CreatedAt) : query.OrderBy(s => s.CreatedAt),
            _ => filter.SortDescending ? query.OrderByDescending(s => s.UpdatedAt) : query.OrderBy(s => s.UpdatedAt)
        };

        return await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);
    }

    public async Task<CollaborationSession> UpdateSessionAsync(Guid sessionId, UpdateSessionRequest request, string userId, CancellationToken ct = default)
    {
        var session = await _dbContext.CollaborationSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.OwnerId == userId, ct);
        if (session == null)
            throw new InvalidOperationException("Session not found or access denied");

        if (request.Name != null) session.Name = request.Name;
        if (request.Description != null) session.Description = request.Description;
        if (request.Status.HasValue) session.Status = request.Status.Value;
        if (request.Settings != null) session.Settings = request.Settings;

        session.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
        return session;
    }

    public async Task<bool> DeleteSessionAsync(Guid sessionId, string userId, CancellationToken ct = default)
    {
        var session = await _dbContext.CollaborationSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.OwnerId == userId, ct);
        if (session == null) return false;

        _dbContext.CollaborationSessions.Remove(session);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<CollaborationSession> StartSessionAsync(Guid sessionId, string userId, CancellationToken ct = default)
    {
        var session = await _dbContext.CollaborationSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.OwnerId == userId, ct);
        if (session == null) throw new InvalidOperationException("Session not found");

        session.Status = SessionStatus.Active;
        session.StartedAt = DateTime.UtcNow;
        session.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
        return session;
    }

    public async Task<CollaborationSession> EndSessionAsync(Guid sessionId, string userId, CancellationToken ct = default)
    {
        var session = await _dbContext.CollaborationSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.OwnerId == userId, ct);
        if (session == null) throw new InvalidOperationException("Session not found");

        session.Status = SessionStatus.Ended;
        session.EndedAt = DateTime.UtcNow;
        session.UpdatedAt = DateTime.UtcNow;

        // End all participants
        var participants = await _dbContext.SessionParticipants.Where(p => p.SessionId == sessionId).ToListAsync(ct);
        foreach (var p in participants)
        {
            p.Status = ParticipantStatus.Disconnected;
            p.LeftAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(ct);
        return session;
    }

    public async Task<CollaborationSession> PauseSessionAsync(Guid sessionId, string userId, CancellationToken ct = default)
    {
        var session = await _dbContext.CollaborationSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.OwnerId == userId, ct);
        if (session == null) throw new InvalidOperationException("Session not found");

        session.Status = SessionStatus.Paused;
        session.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
        return session;
    }

    public async Task<CollaborationSession> ResumeSessionAsync(Guid sessionId, string userId, CancellationToken ct = default)
    {
        var session = await _dbContext.CollaborationSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.OwnerId == userId, ct);
        if (session == null) throw new InvalidOperationException("Session not found");

        session.Status = SessionStatus.Active;
        session.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
        return session;
    }
}

public sealed class ParticipantService : IParticipantService
{
    private readonly CollaborationDbContext _dbContext;
    private readonly ILogger<ParticipantService> _logger;

    public ParticipantService(CollaborationDbContext dbContext, ILogger<ParticipantService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<SessionParticipant> JoinSessionAsync(Guid sessionId, JoinSessionRequest request, CancellationToken ct = default)
    {
        var session = await _dbContext.CollaborationSessions.FirstOrDefaultAsync(s => s.Id == sessionId, CancellationToken.None);
        if (session == null)
            throw new InvalidOperationException("Session not found");

        if (session.Settings.RequireApproval)
        {
            // In a real implementation, this would notify the owner for approval
        }

        var existing = await _dbContext.SessionParticipants
            .FirstOrDefaultAsync(p => p.SessionId == sessionId && p.UserId == request.UserId, CancellationToken.None);

        if (existing != null)
        {
            existing.Status = ParticipantStatus.Connected;
            existing.LastActivityAt = DateTime.UtcNow;
            existing.ConnectionId = ""; // Would be set from SignalR context
        }
        else
        {
            var newParticipant = new SessionParticipant
            {
                SessionId = sessionId,
                UserId = request.UserId,
                UserName = request.UserName,
                UserAvatar = request.UserAvatar,
                Role = request.RequestedRole,
                Status = ParticipantStatus.Connected,
                Capabilities = GetDefaultCapabilities(request.RequestedRole),
                Permissions = GetDefaultPermissions(request.RequestedRole)
            };

            _dbContext.SessionParticipants.Add(newParticipant);
        }

        await _dbContext.SaveChangesAsync(CancellationToken.None);
        
        var participant = await _dbContext.SessionParticipants
            .FirstOrDefaultAsync(p => p.SessionId == sessionId && p.UserId == request.UserId, CancellationToken.None);
        
        return participant!;
    }

    public async Task<SessionParticipant?> GetParticipantAsync(Guid sessionId, string userId, CancellationToken ct = default)
    {
        return await _dbContext.SessionParticipants
            .FirstOrDefaultAsync(p => p.SessionId == sessionId && p.UserId == userId, ct);
    }

    public async Task<List<SessionParticipant>> GetParticipantsAsync(Guid sessionId, CancellationToken ct = default)
    {
        return await _dbContext.SessionParticipants
            .Where(p => p.SessionId == sessionId)
            .OrderBy(p => p.JoinedAt)
            .ToListAsync(ct);
    }

    public async Task<SessionParticipant> UpdateParticipantAsync(Guid sessionId, string userId, UpdateParticipantRequest request, string actorId, CancellationToken ct = default)
    {
        var participant = await _dbContext.SessionParticipants
            .FirstOrDefaultAsync(p => p.SessionId == sessionId && p.UserId == userId, CancellationToken.None);

        if (participant == null)
            throw new InvalidOperationException("Participant not found");

        // Check permissions
        var actor = await _dbContext.SessionParticipants.FirstOrDefaultAsync(p => p.SessionId == participant.SessionId && p.UserId == actorId, CancellationToken.None);
        if (actor == null || !actor.Capabilities.CanManageParticipants)
            throw new UnauthorizedAccessException("Not authorized to update participant");

        if (request.Role.HasValue) participant.Role = request.Role.Value;
        if (request.Capabilities != null) participant.Capabilities = request.Capabilities;
        if (request.Permissions != null) participant.Permissions = request.Permissions;
        if (request.Status.HasValue) participant.Status = request.Status.Value;

        participant.LastActivityAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(CancellationToken.None);
        return participant;
    }

    public async Task<bool> LeaveSessionAsync(Guid sessionId, string userId, CancellationToken ct = default)
    {
        var participant = await _dbContext.SessionParticipants
            .FirstOrDefaultAsync(p => p.SessionId == sessionId && p.UserId == userId, ct);
        
        if (participant == null) return false;

        participant.Status = ParticipantStatus.Disconnected;
        participant.LeftAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> RemoveParticipantAsync(Guid sessionId, string userId, string actorId, CancellationToken ct = default)
    {
        var actor = await _dbContext.SessionParticipants.FirstOrDefaultAsync(p => p.SessionId == sessionId && p.UserId == actorId, CancellationToken.None);
        if (actor == null || !actor.Capabilities.CanManageParticipants)
            throw new UnauthorizedAccessException("Not authorized to remove participant");

        var participant = await _dbContext.SessionParticipants
            .FirstOrDefaultAsync(p => p.SessionId == sessionId && p.UserId == userId, ct);
        
        if (participant == null) return false;

        _dbContext.SessionParticipants.Remove(participant);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<SessionParticipant> RequestControlAsync(Guid sessionId, string userId, CancellationToken ct = default)
    {
        var participant = await _dbContext.SessionParticipants
            .FirstOrDefaultAsync(p => p.SessionId == sessionId && p.UserId == userId, ct);

        if (participant == null)
            throw new InvalidOperationException("Participant not found");

        var session = await _dbContext.CollaborationSessions.FirstOrDefaultAsync(s => s.Id == participant.SessionId, CancellationToken.None);
        if (session == null)
            throw new InvalidOperationException("Session not found");

        if (session.Settings.ControlPolicy == ControlPolicy.OwnerOnly)
        {
            throw new InvalidOperationException("Control requests not allowed in this session");
        }

        // In a real implementation, this would notify the owner for approval
        // For now, we'll just log the request
        return participant;
    }

    public async Task<SessionParticipant> GrantControlAsync(Guid sessionId, string targetUserId, string actorId, CancellationToken ct = default)
    {
        var actor = await _dbContext.SessionParticipants.FirstOrDefaultAsync(p => p.SessionId == sessionId && p.UserId == actorId, CancellationToken.None);
        if (actor == null || !actor.Capabilities.CanManageParticipants)
            throw new UnauthorizedAccessException("Not authorized to grant control");

        var target = await _dbContext.SessionParticipants
            .FirstOrDefaultAsync(p => p.SessionId == sessionId && p.UserId == targetUserId, CancellationToken.None);
        
        if (target == null)
            throw new InvalidOperationException("Target participant not found");

        target.Capabilities.CanControl = true;
        target.Role = ParticipantRole.Controller;
        target.LastActivityAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(CancellationToken.None);
        return target;
    }

    public async Task<SessionParticipant> RevokeControlAsync(Guid sessionId, string targetUserId, string actorId, CancellationToken ct = default)
    {
        var actor = await _dbContext.SessionParticipants.FirstOrDefaultAsync(p => p.SessionId == sessionId && p.UserId == actorId, CancellationToken.None);
        if (actor == null || !actor.Capabilities.CanManageParticipants)
            throw new UnauthorizedAccessException("Not authorized to revoke control");

        var target = await _dbContext.SessionParticipants
            .FirstOrDefaultAsync(p => p.SessionId == sessionId && p.UserId == targetUserId, ct);
        
        if (target == null)
            throw new InvalidOperationException("Target participant not found");

        target.Capabilities.CanControl = false;
        target.Role = ParticipantRole.Viewer;
        target.LastActivityAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
        return target;
    }

    public async Task<SessionParticipant> TransferControlAsync(Guid sessionId, string fromUserId, string toUserId, CancellationToken ct = default)
    {
        var from = await _dbContext.SessionParticipants
            .FirstOrDefaultAsync(p => p.SessionId == sessionId && p.UserId == fromUserId, ct);
        
        var to = await _dbContext.SessionParticipants
            .FirstOrDefaultAsync(p => p.SessionId == sessionId && p.UserId == toUserId, ct);

        if (from == null || to == null)
            throw new InvalidOperationException("Participant not found");

        from.Capabilities.CanControl = false;
        from.Role = ParticipantRole.Viewer;
        
        to.Capabilities.CanControl = true;
        to.Role = ParticipantRole.Controller;

        await _dbContext.SaveChangesAsync(ct);
        return to;
    }

    public async Task<SessionParticipant> UpdateCursorAsync(Guid sessionId, string userId, CursorPosition cursor, CancellationToken ct = default)
    {
        var participant = await _dbContext.SessionParticipants
            .FirstOrDefaultAsync(p => p.SessionId == sessionId && p.UserId == userId, ct);

        if (participant == null)
            throw new InvalidOperationException("Participant not found");

        participant.CursorPosition = cursor;
        participant.LastActivityAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
        return participant;
    }

    public async Task<SessionParticipant> UpdateViewportAsync(Guid sessionId, string userId, ViewportInfo viewport, CancellationToken ct = default)
    {
        var participant = await _dbContext.SessionParticipants
            .FirstOrDefaultAsync(p => p.SessionId == sessionId && p.UserId == userId, ct);

        if (participant == null)
            throw new InvalidOperationException("Participant not found");

        participant.Viewport = viewport;
        participant.LastActivityAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
        return participant;
    }

    public async Task<SessionParticipant> UpdateActivityAsync(Guid sessionId, string userId, CancellationToken ct = default)
    {
        var participant = await _dbContext.SessionParticipants
            .FirstOrDefaultAsync(p => p.SessionId == sessionId && p.UserId == userId, ct);

        if (participant == null) return null!;

        participant.LastActivityAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
        return participant;
    }

    private ParticipantCapabilities GetDefaultCapabilities(ParticipantRole role)
    {
        return role switch
        {
            ParticipantRole.Owner => new ParticipantCapabilities
            {
                CanControl = true,
                CanAnnotate = true,
                CanChat = true,
                CanVoiceChat = true,
                CanScreenShare = true,
                CanRecord = true,
                CanManageParticipants = true,
                CanChangeSettings = true
            },
            ParticipantRole.Host => new ParticipantCapabilities
            {
                CanControl = true,
                CanAnnotate = true,
                CanChat = true,
                CanVoiceChat = true,
                CanScreenShare = true,
                CanRecord = true,
                CanManageParticipants = true,
                CanChangeSettings = false
            },
            ParticipantRole.Presenter => new ParticipantCapabilities
            {
                CanControl = false,
                CanAnnotate = true,
                CanChat = true,
                CanVoiceChat = true,
                CanScreenShare = true,
                CanRecord = false,
                CanManageParticipants = false,
                CanChangeSettings = false
            },
            _ => new ParticipantCapabilities
            {
                CanControl = false,
                CanAnnotate = true,
                CanChat = true,
                CanVoiceChat = false,
                CanScreenShare = false,
                CanRecord = false,
                CanManageParticipants = false,
                CanChangeSettings = false
            }
        };
    }

    private ParticipantPermissions GetDefaultPermissions(ParticipantRole role)
    {
        return role switch
        {
            ParticipantRole.Owner => new ParticipantPermissions
            {
                CanControlInstance = true,
                CanStartStopInstance = true,
                CanConfigureInstance = true,
                CanViewLogs = true,
                CanAccessConsole = true,
                CanManagePlugins = true,
                CanAccessAI = true,
                CanAccessAnalytics = true
            },
            ParticipantRole.Host => new ParticipantPermissions
            {
                CanControlInstance = true,
                CanStartStopInstance = true,
                CanConfigureInstance = true,
                CanViewLogs = true,
                CanAccessConsole = true,
                CanManagePlugins = false,
                CanAccessAI = true,
                CanAccessAnalytics = true
            },
            ParticipantRole.Presenter => new ParticipantPermissions
            {
                CanControlInstance = false,
                CanStartStopInstance = false,
                CanConfigureInstance = false,
                CanViewLogs = true,
                CanAccessConsole = false,
                CanManagePlugins = false,
                CanAccessAI = true,
                CanAccessAnalytics = true
            },
            _ => new ParticipantPermissions
            {
                CanControlInstance = false,
                CanStartStopInstance = false,
                CanConfigureInstance = false,
                CanViewLogs = true,
                CanAccessConsole = false,
                CanManagePlugins = false,
                CanAccessAI = true,
                CanAccessAnalytics = true
            }
        };
    }
}