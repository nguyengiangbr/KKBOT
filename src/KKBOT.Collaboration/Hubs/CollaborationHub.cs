// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KKBOT.Collaboration.Abstractions;
using KKBOT.Collaboration.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace KKBOT.Collaboration.Hubs;

public sealed class CollaborationHub : Hub, ICollaborationHub
{
    private readonly ILogger<CollaborationHub> _logger;
    private readonly ICollaborationSessionService _sessionService;
    private readonly IParticipantService _participantService;
    private readonly IPresenceService _presenceService;
    private readonly IChatService _chatService;
    private readonly IAnnotationService _annotationService;
    private readonly IRecordingService _recordingService;
    private readonly IScreenShareService _screenShareService;
    private readonly ISharedWorkspaceService _workspaceService;

    // Connection tracking
    private static readonly ConcurrentDictionary<string, ConnectionInfo> _connections = new();
    private static readonly ConcurrentDictionary<string, HashSet<string>> _userConnections = new();
    private static readonly ConcurrentDictionary<Guid, HashSet<string>> _sessionConnections = new();

    public CollaborationHub(
        ILogger<CollaborationHub> logger,
        ICollaborationSessionService sessionService,
        IParticipantService participantService,
        IPresenceService presenceService,
        IChatService chatService,
        IAnnotationService annotationService,
        IRecordingService recordingService,
        IScreenShareService screenShareService,
        ISharedWorkspaceService workspaceService)
    {
        _logger = logger;
        _sessionService = sessionService;
        _participantService = participantService;
        _presenceService = presenceService;
        _chatService = chatService;
        _annotationService = annotationService;
        _recordingService = recordingService;
        _screenShareService = screenShareService;
        _workspaceService = workspaceService;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst("sub")?.Value ?? Context.ConnectionId;
        var connectionId = Context.ConnectionId;

        var connectionInfo = new ConnectionInfo
        {
            ConnectionId = connectionId,
            UserId = userId,
            ConnectedAt = DateTime.UtcNow
        };

        _connections[connectionId] = connectionInfo;
        _userConnections.AddOrUpdate(userId, 
            _ => new HashSet<string> { connectionId },
            (_, existing) => { existing.Add(connectionId); return existing; });

        // Update presence
        await _presenceService.UpdatePresenceAsync(userId, new PresenceUpdateRequest
        {
            Status = PresenceStatus.Online
        });

        _logger.LogInformation("Client connected: {ConnectionId} (User: {UserId})", connectionId, userId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;
        var userId = Context.User?.FindFirst("sub")?.Value ?? Context.ConnectionId;

        if (_connections.TryRemove(connectionId, out var connectionInfo))
        {
            if (_userConnections.TryGetValue(userId, out var connections))
            {
                connections.Remove(connectionId);
                if (connections.Count == 0)
                {
                    _userConnections.TryRemove(userId, out _);
                    
                    // Update presence to offline if no more connections
                    await _presenceService.UpdatePresenceAsync(userId, new PresenceUpdateRequest
                    {
                        Status = PresenceStatus.Offline
                    });
                }
            }
        }

        // Leave any active sessions
        foreach (var sessionId in _sessionConnections.Keys)
        {
            if (_sessionConnections[sessionId].Contains(Context.ConnectionId))
            {
                await LeaveSessionInternalAsync(sessionId);
                break;
            }
        }

        _logger.LogInformation("Client disconnected: {ConnectionId} (User: {UserId})", Context.ConnectionId, userId);
        await base.OnDisconnectedAsync(exception);
    }

    // ICollaborationHub implementation
    public Task BroadcastToSessionAsync(Guid sessionId, string method, object payload, CancellationToken ct = default)
        => Clients.Group($"session:{sessionId}").SendAsync(method, payload, ct);

    public Task SendToUserAsync(string userId, string method, object payload, CancellationToken ct = default)
        => Clients.User(userId).SendAsync(method, payload, ct);

    public Task SendToConnectionAsync(string connectionId, string method, object payload, CancellationToken ct = default)
        => Clients.Client(connectionId).SendAsync(method, payload, ct);

    public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken ct = default)
        => Groups.AddToGroupAsync(connectionId, groupName, ct);

    public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken ct = default)
        => Groups.RemoveFromGroupAsync(connectionId, groupName, ct);

    // Session management
    public async Task JoinSessionAsync(string connectionId, Guid sessionId, string userId, CancellationToken ct = default)
    {
        var participant = await _participantService.JoinSessionAsync(sessionId, new JoinSessionRequest
        {
            UserId = userId,
            UserName = Context.User?.FindFirst("name")?.Value ?? userId,
            UserAvatar = Context.User?.FindFirst("picture")?.Value ?? ""
        }, ct);

        // Track connection in session
        _sessionConnections.AddOrUpdate(sessionId,
            _ => new HashSet<string> { Context.ConnectionId },
            (_, existing) => { existing.Add(Context.ConnectionId); return existing; });

        // Add to SignalR group
        await Groups.AddToGroupAsync(Context.ConnectionId, $"session:{sessionId}");

        // Notify others
        await Clients.Group($"session:{sessionId}").SendAsync("ParticipantJoined", new
        {
            participantId = participant.Id,
            userId = participant.UserId,
            userName = participant.UserName,
            userAvatar = participant.UserAvatar,
            role = participant.Role,
            joinedAt = participant.JoinedAt
        });

        // Send current participants to new user
        var participants = await _participantService.GetParticipantsAsync(sessionId, CancellationToken.None);
        await Clients.Caller.SendAsync("ParticipantsList", participants);

        // Send current annotations
        var annotations = await _annotationService.GetAnnotationsAsync(sessionId, new AnnotationFilter(), CancellationToken.None);
        await Clients.Caller.SendAsync("AnnotationsList", annotations);
    }

    public async Task LeaveSessionAsync(string connectionId, Guid sessionId, string userId, CancellationToken ct = default)
    {
        await LeaveSessionInternalAsync(sessionId);
        
        await _participantService.LeaveSessionAsync(sessionId, userId, CancellationToken.None);
        
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"session:{sessionId}");
        
        await Clients.Group($"session:{sessionId}").SendAsync("ParticipantLeft", new
        {
            userId,
            leftAt = DateTime.UtcNow
        });
    }

    private async Task LeaveSessionInternalAsync(Guid sessionId)
    {
        if (_sessionConnections.TryGetValue(sessionId, out var connections))
        {
            connections.Remove(Context.ConnectionId);
            if (connections.Count == 0)
            {
                _sessionConnections.TryRemove(sessionId, out _);
            }
        }
    }

    // Participant actions
    public async Task RequestControl(Guid sessionId, CancellationToken ct = default)
    {
        var userId = Context.User?.FindFirst("sub")?.Value ?? Context.ConnectionId;
        var participant = await _participantService.RequestControlAsync(GetSessionId(), userId, CancellationToken.None);
        
        await Clients.Group($"session:{GetSessionId()}").SendAsync("ControlRequested", new
        {
            userId = participant.UserId,
            userName = participant.UserName,
            requestedAt = DateTime.UtcNow
        });
    }

    public async Task GrantControl(string targetUserId, CancellationToken ct = default)
    {
        var actorId = Context.User?.FindFirst("sub")?.Value ?? Context.ConnectionId;
        var participant = await _participantService.GrantControlAsync(GetSessionId(), targetUserId, Context.User?.FindFirst("sub")?.Value ?? Context.ConnectionId, CancellationToken.None);
        
        await Clients.Group($"session:{GetSessionId()}").SendAsync("ControlGranted", new
        {
            userId = participant.UserId,
            userName = participant.UserName,
            grantedBy = Context.User?.FindFirst("name")?.Value ?? "Unknown",
            grantedAt = DateTime.UtcNow
        });
    }

    public async Task RevokeControl(string targetUserId, CancellationToken ct = default)
    {
        var actorId = Context.User?.FindFirst("sub")?.Value ?? Context.ConnectionId;
        var participant = await _participantService.RevokeControlAsync(GetSessionId(), targetUserId, actorId, CancellationToken.None);
        
        await Clients.Group($"session:{GetSessionId()}").SendAsync("ControlRevoked", new
        {
            userId = participant.UserId,
            userName = participant.UserName,
            revokedBy = Context.User?.FindFirst("name")?.Value ?? "Unknown",
            revokedAt = DateTime.UtcNow
        });
    }

    public async Task TransferControl(string fromUserId, string toUserId, CancellationToken ct = default)
    {
        var participant = await _participantService.TransferControlAsync(GetSessionId(), fromUserId, toUserId, CancellationToken.None);
        
        await Clients.Group($"session:{GetSessionId()}").SendAsync("ControlTransferred", new
        {
            fromUserId,
            toUserId,
            transferredBy = Context.User?.FindFirst("name")?.Value ?? "Unknown",
            transferredAt = DateTime.UtcNow
        });
    }

    public async Task UpdateCursor(CursorPosition cursor, CancellationToken ct = default)
    {
        var userId = Context.User?.FindFirst("sub")?.Value ?? Context.ConnectionId;
        var sessionId = GetSessionId();
        
        var participant = await _participantService.UpdateCursorAsync(sessionId, userId, cursor, CancellationToken.None);
        
        // Broadcast to others in session (excluding sender)
        await Clients.OthersInGroup($"session:{sessionId}").SendAsync("CursorMoved", new
        {
            userId,
            userName = participant.UserName,
            cursor = new
            {
                cursor.X,
                cursor.Y,
                cursor.Scale,
                cursor.Timestamp
            }
        });
    }

    public async Task UpdateViewport(ViewportInfo viewport, CancellationToken ct = default)
    {
        var userId = Context.User?.FindFirst("sub")?.Value ?? Context.ConnectionId;
        var sessionId = GetSessionId();
        
        var participant = await _participantService.UpdateViewportAsync(sessionId, userId, viewport, CancellationToken.None);
        
        await Clients.OthersInGroup($"session:{sessionId}").SendAsync("ViewportChanged", new
        {
            userId,
            userName = participant.UserName,
            viewport = new
            {
                viewport.X,
                viewport.Y,
                viewport.Width,
                viewport.Height,
                viewport.Scale
            }
        });
    }

    // Chat
    public async Task SendChatMessage(SendMessageRequest request, CancellationToken ct = default)
    {
        var userId = Context.User?.FindFirst("sub")?.Value ?? Context.ConnectionId;
        var sessionId = GetSessionId();
        
        var message = await _chatService.SendMessageAsync(GetSessionId(), new SendMessageRequest
        {
            Content = request.Content,
            Type = request.Type,
            Metadata = request.Metadata
        }, userId, CancellationToken.None);

        await Clients.Group($"session:{sessionId}").SendAsync("ChatMessage", new
        {
            message.Id,
            message.SenderId,
            message.SenderName,
            message.Content,
            message.Type,
            message.SentAt,
            message.Metadata
        });
    }

    public async Task AddReaction(Guid messageId, string emoji, CancellationToken ct = default)
    {
        var userId = Context.User?.FindFirst("sub")?.Value ?? Context.ConnectionId;
        var message = await _chatService.AddReactionAsync(messageId, emoji, userId, CancellationToken.None);
        
        await Clients.Group($"session:{GetSessionId()}").SendAsync("ReactionAdded", new
        {
            messageId,
            emoji,
            userId,
            reactedAt = DateTime.UtcNow
        });
    }

    // Annotations
    public async Task CreateAnnotation(CreateAnnotationRequest request, CancellationToken ct = default)
    {
        var userId = Context.User?.FindFirst("sub")?.Value ?? Context.ConnectionId;
        var sessionId = GetSessionId();
        
        var annotation = await _annotationService.CreateAnnotationAsync(sessionId, request, userId, CancellationToken.None);
        
        await Clients.Group($"session:{sessionId}").SendAsync("AnnotationCreated", annotation);
    }

    public async Task UpdateAnnotation(Guid annotationId, UpdateAnnotationRequest request, CancellationToken ct = default)
    {
        var userId = Context.User?.FindFirst("sub")?.Value ?? Context.ConnectionId;
        var annotation = await _annotationService.UpdateAnnotationAsync(annotationId, request, Context.User?.FindFirst("sub")?.Value ?? Context.ConnectionId, CancellationToken.None);
        
        await Clients.Group($"session:{GetSessionId()}").SendAsync("AnnotationUpdated", annotation);
    }

    public async Task DeleteAnnotation(Guid annotationId, CancellationToken ct = default)
    {
        var userId = Context.User?.FindFirst("sub")?.Value ?? Context.ConnectionId;
        var success = await _annotationService.DeleteAnnotationAsync(annotationId, userId, CancellationToken.None);
        
        if (success)
        {
            await Clients.Group($"session:{GetSessionId()}").SendAsync("AnnotationDeleted", new { annotationId });
        }
    }

    // Recording
    public async Task StartRecording(StartRecordingRequest request, CancellationToken ct = default)
    {
        var userId = Context.User?.FindFirst("sub")?.Value ?? Context.ConnectionId;
        var recording = await _recordingService.StartRecordingAsync(GetSessionId(), request, Context.User?.FindFirst("sub")?.Value ?? Context.ConnectionId, CancellationToken.None);
        
        await Clients.Group($"session:{GetSessionId()}").SendAsync("RecordingStarted", new
        {
            recording.Id,
            recording.Format,
            recording.StartedAt,
            recordedBy = Context.User?.FindFirst("name")?.Value ?? "Unknown"
        });
    }

    public async Task StopRecording(Guid recordingId, CancellationToken ct = default)
    {
        var userId = Context.User?.FindFirst("sub")?.Value ?? Context.ConnectionId;
        var recording = await _recordingService.StopRecordingAsync(recordingId, userId, CancellationToken.None);
        
        await Clients.Group($"session:{GetSessionId()}").SendAsync("RecordingStopped", new
        {
            recordingId,
            stoppedAt = DateTime.UtcNow,
            stoppedBy = Context.User?.FindFirst("name")?.Value ?? "Unknown"
        });
    }

    // Screen Share
    public async Task StartScreenShare(StartScreenShareRequest request, CancellationToken ct = default)
    {
        var sessionId = GetSessionId();
        var userId = Context.User?.FindFirst("sub")?.Value ?? Context.ConnectionId;
        
        var screenShare = await _screenShareService.StartScreenShareAsync(sessionId, request, userId, CancellationToken.None);
        
        await Clients.Group($"session:{sessionId}").SendAsync("ScreenShareStarted", new
        {
            screenShare.Id,
            screenShare.PresenterId,
            screenShare.Mode,
            screenShare.Quality,
            screenShare.FrameRate,
            screenShare.IncludeAudio,
            screenShare.StartedAt
        });
    }

    public async Task StopScreenShare(CancellationToken ct = default)
    {
        var sessionId = GetSessionId();
        var userId = Context.User?.FindFirst("sub")?.Value ?? Context.ConnectionId;
        
        await _screenShareService.StopScreenShareAsync(sessionId, userId, CancellationToken.None);
        
        await Clients.Group($"session:{GetSessionId()}").SendAsync("ScreenShareStopped", new
        {
            stoppedBy = Context.User?.FindFirst("name")?.Value ?? "Unknown",
            stoppedAt = DateTime.UtcNow
        });
    }

    // Workspace
    public async Task ApplyWorkspaceOperation(WorkspaceOperation operation, CancellationToken ct = default)
    {
        var userId = Context.User?.FindFirst("sub")?.Value ?? Context.ConnectionId;
        var workspaceId = GetWorkspaceId();
        
        var result = await _workspaceService.ApplyOperationAsync(workspaceId, operation, userId, CancellationToken.None);
        
        if (result.Success)
        {
            await Clients.Group($"workspace:{workspaceId}").SendAsync("WorkspaceOperationApplied", new
            {
                operation,
                appliedBy = Context.User?.FindFirst("name")?.Value ?? "Unknown",
                appliedAt = DateTime.UtcNow,
                newVersion = result.NewVersion
            });
        }
        else
        {
            await Clients.Caller.SendAsync("WorkspaceOperationFailed", new
            {
                operation,
                error = result.ErrorMessage
            });
        }
    }

    // Presence
    public async Task UpdatePresence(PresenceUpdateRequest request, CancellationToken ct = default)
    {
        var userId = Context.User?.FindFirst("sub")?.Value ?? Context.ConnectionId;
        await _presenceService.UpdatePresenceAsync(userId, request, CancellationToken.None);
    }

    // Helper methods
    private Guid GetSessionId()
    {
        // In a real implementation, this would come from the connection context or query string
        // For now, we'll get it from the connection's session mapping
        foreach (var kvp in _sessionConnections)
        {
            if (kvp.Value.Contains(Context.ConnectionId))
            {
                return kvp.Key;
            }
        }

        // Fallback: get from query string or context
        return Guid.TryParse(Context.GetHttpContext()?.Request.Query["sessionId"], out var sid) ? sid : Guid.Empty;
    }

    private Guid GetWorkspaceId()
    {
        return Guid.TryParse(Context.GetHttpContext()?.Request.Query["workspaceId"], out var wid) ? wid : Guid.Empty;
    }

    private sealed class ConnectionInfo
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public DateTime ConnectedAt { get; set; }
    }
}