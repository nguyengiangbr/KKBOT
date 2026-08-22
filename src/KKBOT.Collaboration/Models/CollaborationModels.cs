// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KKBOT.Collaboration.Models;

/// <summary>
/// Collaboration session for real-time multi-user interaction
/// </summary>
public sealed class CollaborationSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid InstanceId { get; set; }
    public Guid OwnerId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public SessionStatus Status { get; set; } = SessionStatus.Waiting;
    public SessionType Type { get; set; } = SessionType.CollaborativeControl;
    public SessionSettings Settings { get; set; } = new();
    public List<SessionParticipant> Participants { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? RecordingPath { get; set; }
    public bool IsRecorded { get; set; } = false;
}

public enum SessionStatus
{
    Waiting,
    Active,
    Paused,
    Ended,
    Archived
}

public enum SessionType
{
    CollaborativeControl,    // Multiple users can control
    Observation,             // Read-only observation
    PairProgramming,         // Two users alternating control
    Training,                // Instructor-led training
    Debugging                // Collaborative debugging
}

public sealed class SessionSettings
{
    public int MaxParticipants { get; set; } = 10;
    public bool AllowAnonymousJoin { get; set; } = false;
    public bool RequireApproval { get; set; } = true;
    public bool EnableRecording { get; set; } = true;
    public bool EnableChat { get; set; } = true;
    public bool EnableVoice { get; set; } = false;
    public bool EnableScreenShare { get; set; } = true;
    public bool EnableCursorTracking { get; set; } = true;
    public bool EnableAnnotation { get; set; } = true;
    public ControlPolicy ControlPolicy { get; set; } = ControlPolicy.RequestApproval;
    public int MaxIdleMinutes { get; set; } = 30;
    public Dictionary<string, object> CustomSettings { get; set; } = new();
}

public enum ControlPolicy
{
    OwnerOnly,           // Only session owner has control
    RequestApproval,     // Participants request control, owner approves
    FirstComeFirstServed, // First to request gets control
    RoundRobin,          // Control rotates among participants
    FreeForAll           // Everyone can control simultaneously
}

/// <summary>
/// Participant in a collaboration session
/// </summary>
public sealed class SessionParticipant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public CollaborationSession Session { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string UserAvatar { get; set; } = string.Empty;
    public ParticipantRole Role { get; set; } = ParticipantRole.Viewer;
    public ParticipantStatus Status { get; set; } = ParticipantStatus.Connected;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LeftAt { get; set; }
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
    public ParticipantCapabilities Capabilities { get; set; } = new();
    public ParticipantPermissions Permissions { get; set; } = new();
    public CursorPosition? CursorPosition { get; set; }
    public ViewportInfo? Viewport { get; set; }
    public string? ConnectionId { get; set; }
}

public enum ParticipantRole
{
    Owner,
    Host,
    Presenter,
    Controller,
    Viewer,
    Guest
}

public enum ParticipantStatus
{
    Connecting,
    Connected,
    Active,
    Idle,
    Away,
    Disconnected,
    Reconnecting
}

public sealed class ParticipantCapabilities
{
    public bool CanControl { get; set; } = false;
    public bool CanAnnotate { get; set; } = true;
    public bool CanChat { get; set; } = true;
    public bool CanVoiceChat { get; set; } = false;
    public bool CanScreenShare { get; set; } = false;
    public bool CanRecord { get; set; } = false;
    public bool CanManageParticipants { get; set; } = false;
    public bool CanChangeSettings { get; set; } = false;
}

public sealed class ParticipantPermissions
{
    public bool CanControlInstance { get; set; } = false;
    public bool CanStartStopInstance { get; set; } = false;
    public bool CanConfigureInstance { get; set; } = false;
    public bool CanViewLogs { get; set; } = true;
    public bool CanAccessConsole { get; set; } = false;
    public bool CanManagePlugins { get; set; } = false;
    public bool CanAccessAI { get; set; } = true;
    public bool CanAccessAnalytics { get; set; } = true;
    public Dictionary<string, bool> CustomPermissions { get; set; } = new();
}

public sealed class CursorPosition
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Scale { get; set; } = 1.0;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public sealed class ViewportInfo
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public Height { get; set; }
    public double Scale { get; set; } = 1.0;
}

/// <summary>
/// Real-time presence information
/// </summary>
public sealed class PresenceInfo
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string UserAvatar { get; set; } = string.Empty;
    public PresenceStatus Status { get; set; } = PresenceStatus.Offline;
    public string? CurrentActivity { get; set; }
    public string? CurrentInstanceId { get; set; }
    public string? CurrentSessionId { get; set; }
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> CustomStatus { get; set; } = new();
}

public enum PresenceStatus
{
    Offline,
    Online,
    Away,
    Busy,
    DoNotDisturb,
    InSession,
    InGame
}

/// <summary>
/// Chat message in collaboration session
/// </summary>
public sealed class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public string SenderId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public MessageType Type { get; set; } = MessageType.Text;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public bool IsSystemMessage { get; set; } = false;
    public Dictionary<string, object> Metadata { get; set; } = new();
    public List<MessageReaction> Reactions { get; set; } = new();
}

public enum MessageType
{
    Text,
    Image,
    File,
    Code,
    Command,
    System,
    Annotation
}

public sealed class MessageReaction
{
    public string Emoji { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime ReactedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Annotation on shared screen
/// </summary>
public sealed class ScreenAnnotation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public string AuthorId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public AnnotationType Type { get; set; } = AnnotationType.Pen;
    public AnnotationColor Color { get; set; } = AnnotationColor.Red;
    public double StrokeWidth { get; set; } = 2.0;
    public List<Point2D> Points { get; set; } = new();
    public string? Text { get; set; }
    public Rectangle2D? Bounds { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public bool IsPersistent { get; set; } = true;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public enum AnnotationType
{
    Pen,
    Highlighter,
    Arrow,
    Rectangle,
    Circle,
    Text,
    StickyNote
}

public enum AnnotationColor
{
    Red,
    Blue,
    Green,
    Yellow,
    Orange,
    Purple,
    Black,
    White,
    Custom
}

public sealed class Point2D
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Pressure { get; set; } = 1.0;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public sealed class Rectangle2D
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}

/// <summary>
/// Session recording for replay
/// </summary>
public sealed class SessionRecording
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public RecordingFormat Format { get; set; } = RecordingFormat.Custom;
    public RecordingStatus Status { get; set; } = RecordingStatus.Recording;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public TimeSpan Duration { get; set; }
    public long FrameCount { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public List<RecordingEvent> Events { get; set; } = new();
}

public enum RecordingFormat
{
    Custom,      // Custom binary format
    WebM,        // WebM video
    MP4,         // MP4 video
    JSON         // JSON event stream
}

public enum RecordingStatus
{
    Recording,
    Processing,
    Completed,
    Failed,
    Archived
}

public sealed class RecordingEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RecordingId { get; set; }
    public EventType Type { get; set; }
    public DateTime Timestamp { get; set; }
    public string? ActorId { get; set; }
    public string EventDataJson { get; set; } = string.Empty;
    public int SequenceNumber { get; set; }
}

public enum EventType
{
    SessionStart,
    SessionEnd,
    ParticipantJoin,
    ParticipantLeave,
    ControlRequest,
    ControlGranted,
    ControlReleased,
    ControlTransferred,
    InputCommand,
    ScreenAnnotation,
    ChatMessage,
    ViewportChange,
    CursorMove,
    ScreenCapture,
    Custom
}

/// <summary>
/// Live replay session
/// </summary>
public sealed class LiveReplay
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RecordingId { get; set; }
    public SessionRecording Recording { get; set; } = null!;
    public string HostedByUserId { get; set; } = string.Empty;
    public ReplayStatus Status { get; set; } = ReplayStatus.Preparing;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public double PlaybackSpeed { get; set; } = 1.0;
    public DateTime CurrentPlaybackTime { get; set; }
    public List<LiveReplayParticipant> Viewers { get; set; } = new();
    public ReplaySettings Settings { get; set; } = new();
}

public enum ReplayStatus
{
    Preparing,
    Playing,
    Paused,
    Ended,
    Cancelled
}

public sealed class LiveReplayParticipant
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LeftAt { get; set; }
    public double CurrentPlaybackPosition { get; set; }
}

public sealed class ReplaySettings
{
    public bool AllowChat { get; set; } = true;
    public bool AllowPause { get; set; } = true;
    public bool AllowSeek { get; set; } = true;
    public bool SyncPlayback { get; set; } = true;
    public double DefaultSpeed { get; set; } = 1.0;
}

/// <summary>
/// Shared workspace for collaborative editing
/// </summary>
public sealed class SharedWorkspace
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid OwnerId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public WorkspaceType Type { get; set; } = WorkspaceType.Document;
    public WorkspaceContent Content { get; set; } = new();
    public List<WorkspaceCollaborator> Collaborators { get; set; } = new();
    public WorkspaceSettings Settings { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastEditedAt { get; set; }
    public string? LastEditedBy { get; set; }
}

public enum WorkspaceType
{
    Document,
    Code,
    Diagram,
    Whiteboard,
    Notebook,
    Configuration
}

public sealed class WorkspaceContent
{
    public string TextContent { get; set; } = string.Empty;
    public string JsonContent { get; set; } = "{}";
    public string BinaryContentBase64 { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string MimeType { get; set; } = "text/plain";
    public int Version { get; set; } = 1;
    public string Checksum { get; set; } = string.Empty;
}

public sealed class WorkspaceCollaborator
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public CollaboratorRole Role { get; set; } = CollaboratorRole.Editor;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastAccessedAt { get; set; }
    public CursorPosition? CursorPosition { get; set; }
    public SelectionRange? Selection { get; set; }
}

public enum CollaboratorRole
{
    Owner,
    Admin,
    Editor,
    Commenter,
    Viewer
}

public sealed class SelectionRange
{
    public int StartLine { get; set; }
    public int StartColumn { get; set; }
    public int EndLine { get; set; }
    public int EndColumn { get; set; }
}

public sealed class WorkspaceSettings
{
    public bool AutoSave { get; set; } = true;
    public int AutoSaveIntervalSeconds { get; set; } = 30;
    public bool RequireApprovalForEdits { get; set; } = false;
    public int MaxHistoryEntries { get; set; } = 1000;
    public bool EnableRealTimeSync { get; set; } = true;
    public ConflictResolutionStrategy ConflictStrategy { get; set; } = ConflictResolutionStrategy.LastWriteWins;
}

public enum ConflictResolutionStrategy
{
    LastWriteWins,
    FirstWriteWins,
    Merge,
    ManualResolution,
    OperationalTransform
}

/// <summary>
/// Activity feed for collaboration
/// </summary>
public sealed class ActivityFeed
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = string.Empty;
    public ActivityType Type { get; set; }
    public string ActorId { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty.
    public string ActorAvatar { get; set; } = string.Empty.
    public string Action { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty.
    public string TargetName { get; set; } = string.Empty.
    public string Description { get; set; } = string.Empty.
    public Dictionary<string, object> Metadata { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow.
    public bool IsPublic { get; set; } = true.
}

public enum ActivityType
{
    SessionCreated,
    SessionJoined,
    SessionLeft,
    ControlGranted,
    ControlReleased,
    AnnotationAdded,
    MessageSent,
    FileShared,
    InstanceStarted,
    InstanceStopped,
    PluginInstalled,
    WorkflowExecuted,
    AlertTriggered,
    Custom
}