// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Collaboration.Models;

namespace KKBOT.Collaboration.Abstractions;

public interface ICollaborationSessionService
{
    Task<CollaborationSession> CreateSessionAsync(CreateSessionRequest request, string userId, CancellationToken ct = default);
    Task<CollaborationSession?> GetSessionAsync(Guid sessionId, string userId, CancellationToken ct = default);
    Task<List<CollaborationSession>> GetSessionsAsync(string userId, SessionFilter filter, CancellationToken ct = default);
    Task<CollaborationSession> UpdateSessionAsync(Guid sessionId, UpdateSessionRequest request, string userId, CancellationToken ct = default);
    Task<bool> DeleteSessionAsync(Guid sessionId, string userId, CancellationToken ct = default);
    Task<CollaborationSession> StartSessionAsync(Guid sessionId, string userId, CancellationToken ct = default);
    Task<CollaborationSession> EndSessionAsync(Guid sessionId, string userId, CancellationToken ct = default);
    Task<CollaborationSession> PauseSessionAsync(Guid sessionId, string userId, CancellationToken ct = default);
    Task<CollaborationSession> ResumeSessionAsync(Guid sessionId, string userId, CancellationToken ct = default);
}

public sealed record CreateSessionRequest(
    string Name,
    string Description,
    Guid InstanceId,
    SessionType Type = SessionType.CollaborativeControl,
    SessionSettings? Settings = null,
    List<string>? InvitedUserIds = null
);

public sealed record UpdateSessionRequest(
    string? Name = null,
    string? Description = null,
    SessionStatus? Status = null,
    SessionSettings? Settings = null
);

public sealed record SessionFilter(
    SessionStatus? Status = null,
    SessionType? Type = null,
    Guid? InstanceId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Page = 1,
    int PageSize = 20,
    string SortBy = "createdAt",
    bool SortDescending = true
);

public interface IParticipantService
{
    Task<SessionParticipant> JoinSessionAsync(Guid sessionId, JoinSessionRequest request, CancellationToken ct = default);
    Task<SessionParticipant?> GetParticipantAsync(Guid sessionId, string userId, CancellationToken ct = default);
    Task<List<SessionParticipant>> GetParticipantsAsync(Guid sessionId, CancellationToken ct = default);
    Task<SessionParticipant> UpdateParticipantAsync(Guid sessionId, string userId, UpdateParticipantRequest request, string actorId, CancellationToken ct = default);
    Task<bool> LeaveSessionAsync(Guid sessionId, string userId, CancellationToken ct = default);
    Task<bool> RemoveParticipantAsync(Guid sessionId, string userId, string actorId, CancellationToken ct = default);
    Task<SessionParticipant> RequestControlAsync(Guid sessionId, string userId, CancellationToken ct = default);
    Task<SessionParticipant> GrantControlAsync(Guid sessionId, string targetUserId, string actorId, CancellationToken ct = default);
    Task<SessionParticipant> RevokeControlAsync(Guid sessionId, string targetUserId, string actorId, CancellationToken ct = default);
    Task<SessionParticipant> TransferControlAsync(Guid sessionId, string fromUserId, string toUserId, CancellationToken ct = default);
    Task<SessionParticipant> UpdateCursorAsync(Guid sessionId, string userId, CursorPosition cursor, CancellationToken ct = default);
    Task<SessionParticipant> UpdateViewportAsync(Guid sessionId, string userId, ViewportInfo viewport, CancellationToken ct = default);
    Task<SessionParticipant> UpdateActivityAsync(Guid sessionId, string userId, CancellationToken ct = default);
}

public sealed record JoinSessionRequest(
    string UserName = "",
    string UserAvatar = "",
    string UserId = "",
    ParticipantRole RequestedRole = ParticipantRole.Viewer
);

public sealed record UpdateParticipantRequest(
    ParticipantRole? Role = null,
    ParticipantCapabilities? Capabilities = null,
    ParticipantPermissions? Permissions = null,
    ParticipantStatus? Status = null
);

public interface IPresenceService
{
    Task<PresenceInfo> UpdatePresenceAsync(string userId, PresenceUpdateRequest request, CancellationToken ct = default);
    Task<PresenceInfo?> GetPresenceAsync(string userId, CancellationToken ct = default);
    Task<List<PresenceInfo>> GetPresencesAsync(string tenantId, PresenceFilter filter, CancellationToken ct = default);
    Task<List<PresenceInfo>> GetOnlineUsersAsync(string tenantId, CancellationToken ct = default);
    Task<bool> SetStatusAsync(string userId, PresenceStatus status, string? customStatus = null, CancellationToken ct = default);
    Task<bool> SetCustomStatusAsync(string userId, Dictionary<string, object> customStatus, CancellationToken ct = default);
}

public sealed record PresenceUpdateRequest(
    PresenceStatus? Status = null,
    string? CurrentActivity = null,
    string? CurrentInstanceId = null,
    string? CurrentSessionId = null,
    Dictionary<string, object>? CustomStatus = null
);

public sealed record PresenceFilter(
    PresenceStatus? Status = null,
    string? Search = null,
    bool? IsOnline = null,
    int Page = 1,
    int PageSize = 50
);

public interface IChatService
{
    Task<ChatMessage> SendMessageAsync(Guid sessionId, SendMessageRequest request, string userId, CancellationToken ct = default);
    Task<List<ChatMessage>> GetMessagesAsync(Guid sessionId, ChatFilter filter, CancellationToken ct = default);
    Task<ChatMessage> UpdateMessageAsync(Guid messageId, UpdateMessageRequest request, string userId, CancellationToken ct = default);
    Task<bool> DeleteMessageAsync(Guid messageId, string userId, CancellationToken ct = default);
    Task<ChatMessage> AddReactionAsync(Guid messageId, string emoji, string userId, CancellationToken ct = default);
    Task<bool> RemoveReactionAsync(Guid messageId, string emoji, string userId, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(Guid sessionId, string userId, CancellationToken ct = default);
    Task<bool> MarkAsReadAsync(Guid sessionId, string userId, CancellationToken ct = default);
}

public sealed record SendMessageRequest(
    string Content = "",
    MessageType Type = MessageType.Text,
    Dictionary<string, object>? Metadata = null
);

public sealed record UpdateMessageRequest(
    string? Content = null
);

public sealed record ChatFilter(
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    MessageType? Type = null,
    string? SenderId = null,
    int Page = 1,
    int PageSize = 50
);

public interface IAnnotationService
{
    Task<ScreenAnnotation> CreateAnnotationAsync(Guid sessionId, CreateAnnotationRequest request, string userId, CancellationToken ct = default);
    Task<List<ScreenAnnotation>> GetAnnotationsAsync(Guid sessionId, AnnotationFilter filter, CancellationToken ct = default);
    Task<ScreenAnnotation> UpdateAnnotationAsync(Guid annotationId, UpdateAnnotationRequest request, string userId, CancellationToken ct = default);
    Task<bool> DeleteAnnotationAsync(Guid annotationId, string userId, CancellationToken ct = default);
    Task<bool> ClearAnnotationsAsync(Guid sessionId, string userId, CancellationToken ct = default);
}

public sealed record CreateAnnotationRequest(
    AnnotationType Type,
    AnnotationColor Color,
    double StrokeWidth,
    List<Point2D> Points,
    string? Text = null,
    Rectangle2D? Bounds = null,
    bool IsPersistent = true
);

public sealed record UpdateAnnotationRequest(
    AnnotationType? Type = null,
    AnnotationColor? Color = null,
    double? StrokeWidth = null,
    List<Point2D>? Points = null,
    string? Text = null,
    Rectangle2D? Bounds = null,
    bool? IsPersistent = null
);

public sealed record AnnotationFilter(
    AnnotationType? Type = null,
    string? AuthorId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    bool? IsPersistent = null,
    int Page = 1,
    int PageSize = 100
);

public interface IRecordingService
{
    Task<SessionRecording> StartRecordingAsync(Guid sessionId, StartRecordingRequest request, string userId, CancellationToken ct = default);
    Task<SessionRecording?> GetRecordingAsync(Guid recordingId, string userId, CancellationToken ct = default);
    Task<List<SessionRecording>> GetRecordingsAsync(string userId, RecordingFilter filter, CancellationToken ct = default);
    Task<SessionRecording> StopRecordingAsync(Guid recordingId, string userId, CancellationToken ct = default);
    Task<bool> DeleteRecordingAsync(Guid recordingId, string userId, CancellationToken ct = default);
    Task<byte[]> ExportRecordingAsync(Guid recordingId, ExportFormat format, CancellationToken ct = default);
}

public sealed record StartRecordingRequest(
    RecordingFormat Format = RecordingFormat.Custom,
    bool IncludeAudio = false,
    bool IncludeChat = true,
    bool IncludeAnnotations = true
);

public sealed record RecordingFilter(
    RecordingStatus? Status = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    Guid? SessionId = null,
    int Page = 1,
    int PageSize = 20
);

public enum ExportFormat
{
    Json,
    Csv,
    WebM,
    MP4
}

public interface IReplayService
{
    Task<LiveReplay> StartReplayAsync(StartReplayRequest request, string userId, CancellationToken ct = default);
    Task<LiveReplay?> GetReplayAsync(Guid replayId, string userId, CancellationToken ct = default);
    Task<List<LiveReplay>> GetReplaysAsync(string userId, ReplayFilter filter, CancellationToken ct = default);
    Task<LiveReplay> ControlReplayAsync(Guid replayId, ReplayControlRequest request, string userId, CancellationToken ct = default);
    Task<bool> EndReplayAsync(Guid replayId, string userId, CancellationToken ct = default);
}

public sealed record StartReplayRequest(
    Guid RecordingId,
    double PlaybackSpeed = 1.0,
    ReplaySettings? Settings = null
);

public sealed record ReplayControlRequest(
    ReplayControlAction Action,
    double? Speed = null,
    DateTime? SeekTo = null
);

public enum ReplayControlAction
{
    Play,
    Pause,
    Stop,
    Seek,
    SpeedUp,
    SlowDown
}

public sealed record ReplayFilter(
    ReplayStatus? Status = null,
    Guid? RecordingId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Page = 1,
    int PageSize = 20
);

public interface IScreenShareService
{
    Task<ScreenShareSession> StartScreenShareAsync(Guid sessionId, StartScreenShareRequest request, string userId, CancellationToken ct = default);
    Task<ScreenShareSession?> GetScreenShareAsync(Guid sessionId, string userId, CancellationToken ct = default);
    Task<bool> StopScreenShareAsync(Guid sessionId, string userId, CancellationToken ct = default);
    Task<bool> UpdateScreenShareAsync(Guid sessionId, UpdateScreenShareRequest request, string userId, CancellationToken ct = default);
}

public sealed record StartScreenShareRequest(
    ScreenShareMode Mode = ScreenShareMode.FullScreen,
    bool IncludeAudio = false,
    int Quality = 80,
    int FrameRate = 30
);

public sealed record UpdateScreenShareRequest(
    int? Quality = null,
    int? FrameRate = null,
    bool? IncludeAudio = null
);

public enum ScreenShareMode
{
    FullScreen,
    Window,
    Region,
    Application
}

public sealed class ScreenShareSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public string PresenterId { get; set; } = string.Empty;
    public ScreenShareMode Mode { get; set; } = ScreenShareMode.FullScreen;
    public bool IsActive { get; set; } = true;
    public int Quality { get; set; } = 80;
    public int FrameRate { get; set; } = 30;
    public bool IncludeAudio { get; set; } = false;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public long BytesSent { get; set; }
    public long BytesReceived { get; set; }
}

public interface ISharedWorkspaceService
{
    Task<SharedWorkspace> CreateWorkspaceAsync(CreateWorkspaceRequest request, string userId, CancellationToken ct = default);
    Task<SharedWorkspace?> GetWorkspaceAsync(Guid workspaceId, string userId, CancellationToken ct = default);
    Task<List<SharedWorkspace>> GetWorkspacesAsync(string userId, WorkspaceFilter filter, CancellationToken ct = default);
    Task<SharedWorkspace> UpdateWorkspaceAsync(Guid workspaceId, UpdateWorkspaceRequest request, string userId, CancellationToken ct = default);
    Task<bool> DeleteWorkspaceAsync(Guid workspaceId, string userId, CancellationToken ct = default);
    Task<SharedWorkspace> AddCollaboratorAsync(Guid workspaceId, AddCollaboratorRequest request, string userId, CancellationToken ct = default);
    Task<bool> RemoveCollaboratorAsync(Guid workspaceId, string userId, string targetUserId, CancellationToken ct = default);
    Task<WorkspaceOperationResult> ApplyOperationAsync(Guid workspaceId, WorkspaceOperation operation, string userId, CancellationToken ct = default);
    Task<List<WorkspaceOperation>> GetHistoryAsync(Guid workspaceId, HistoryFilter filter, CancellationToken ct = default);
}

public sealed record CreateWorkspaceRequest(
    string Name,
    string Description,
    WorkspaceType Type,
    WorkspaceContent? InitialContent = null,
    WorkspaceSettings? Settings = null,
    List<AddCollaboratorRequest>? InitialCollaborators = null
);

public sealed record UpdateWorkspaceRequest(
    string? Name = null,
    string? Description = null,
    WorkspaceSettings? Settings = null
);

public sealed record AddCollaboratorRequest(
    string UserId,
    string UserName,
    CollaboratorRole Role
);

public sealed record WorkspaceFilter(
    WorkspaceType? Type = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20
);

public sealed record WorkspaceOperation(
    OperationType Type,
    string? Path = null,
    int? Position = null,
    string? Text = null,
    int? Length = null,
    Dictionary<string, object>? Attributes = null
)
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public SharedWorkspace Workspace { get; set; } = null!;
}

public enum OperationType
{
    Insert,
    Delete,
    Update,
    Move,
    Copy,
    Format,
    Comment
}

public sealed class WorkspaceOperationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int NewVersion { get; set; }
    public WorkspaceContent? Content { get; set; }
    public List<WorkspaceOperation> AppliedOperations { get; set; } = new();
}

public sealed record HistoryFilter(
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    string? UserId = null,
    OperationType? Type = null,
    int Page = 1,
    int PageSize = 100
);

public interface IActivityFeedService
{
    Task<ActivityFeed> AddActivityAsync(ActivityFeed activity, CancellationToken ct = default);
    Task<List<ActivityFeed>> GetActivityFeedAsync(string userId, ActivityFeedFilter filter, CancellationToken ct = default);
    Task<List<ActivityFeed>> GetTenantActivityFeedAsync(string tenantId, ActivityFeedFilter filter, CancellationToken ct = default);
    Task<bool> MarkAsReadAsync(Guid activityId, string userId, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(string userId, CancellationToken ct = default);
}

public sealed record ActivityFeedFilter(
    ActivityType? Type = null,
    string? ActorId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Page = 1,
    int PageSize = 50
);

public interface ICollaborationHub
{
    Task JoinSessionAsync(string connectionId, Guid sessionId, string userId, CancellationToken ct = default);
    Task LeaveSessionAsync(string connectionId, Guid sessionId, string userId, CancellationToken ct = default);
    Task BroadcastToSessionAsync(Guid sessionId, string method, object payload, CancellationToken ct = default);
    Task SendToUserAsync(string userId, string method, object payload, CancellationToken ct = default);
    Task SendToConnectionAsync(string connectionId, string method, object payload, CancellationToken ct = default);
    Task AddToGroupAsync(string connectionId, string groupName, CancellationToken ct = default);
    Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken ct = default);
}