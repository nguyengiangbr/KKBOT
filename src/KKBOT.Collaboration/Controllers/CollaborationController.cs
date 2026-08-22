// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Collaboration.Abstractions;
using KKBOT.Collaboration.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Swashbuckle.AspNetCore.Annotations;

namespace KKBOT.Collaboration.Controllers;

[ApiController]
[Route("api/v1/collaboration")]
[Authorize]
[Produces("application/json")]
[SwaggerTag("Real-time Collaboration")]
public sealed class CollaborationController : ControllerBase
{
    private readonly ICollaborationSessionService _sessionService;
    private readonly IParticipantService _participantService;
    private readonly IPresenceService _presenceService;
    private readonly IChatService _chatService;
    private readonly IAnnotationService _annotationService;
    private readonly IRecordingService _recordingService;
    private readonly IScreenShareService _screenShareService;
    private readonly ISharedWorkspaceService _workspaceService;
    private readonly IPresenceService _presence;
    private readonly IChatService _chat;
    private readonly IAnnotationService _annotation;
    private readonly IRecordingService _recording;
    private readonly IScreenShareService _screenShare;
    private readonly ISharedWorkspaceService _workspace;
    private readonly ILogger<CollaborationController> _logger;
    private readonly IHubContext<CollaborationHub> _hubContext;

    public CollaborationController(
        ICollaborationSessionService sessionService,
        IParticipantService participantService,
        IPresenceService presenceService,
        IChatService chatService,
        IAnnotationService annotationService,
        IRecordingService recordingService,
        IScreenShareService screenShareService,
        ISharedWorkspaceService workspaceService,
        IPresenceService presence,
        IChatService chat,
        IAnnotationService annotation,
        IRecordingService recording,
        IScreenShareService screenShare,
        ISharedWorkspaceService workspace,
        ILogger<CollaborationController> logger,
        IHubContext<CollaborationHub> hubContext)
    {
        _sessionService = sessionService;
        _participantService = participantService;
        _presenceService = presenceService;
        _chatService = chatService;
        _annotationService = annotationService;
        _recordingService = recordingService;
        _screenShareService = screenShareService;
        _workspaceService = workspaceService;
        _presence = presenceService;
        _chat = chatService;
        _annotation = annotationService;
        _recording = recordingService;
        _screenShare = screenShareService;
        _workspace = workspaceService;
        _presence = presenceService;
        _chat = chatService;
        _annotation = annotationService;
        _recording = recordingService;
        _screenShare = screenShareService;
        _workspace = workspaceService;
        _logger = logger;
        _hubContext = hubContext;
    }

    // Session endpoints
    [HttpGet("sessions")]
    [ProducesResponseType(typeof(List<CollaborationSession>), 200)]
    [SwaggerOperation("List collaboration sessions")]
    public async Task<ActionResult<List<CollaborationSession>>> GetSessions(
        [FromQuery] SessionStatus? status,
        [FromQuery] SessionType? type,
        [FromQuery] Guid? instanceId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string sortBy = "updatedAt",
        [FromQuery] bool sortDescending = true,
        CancellationToken ct = default)
    {
        var filter = new SessionFilter(status, type, instanceId, fromDate, toDate, page, pageSize, sortBy, !sortDescending);
        var sessions = await _sessionService.GetSessionsAsync(GetCurrentUserId(), filter, ct);
        return Ok(sessions);
    }

    [HttpGet("sessions/{sessionId}")]
    [ProducesResponseType(typeof(CollaborationSession), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get session by ID")]
    public async Task<ActionResult<CollaborationSession>> GetSession(Guid sessionId, CancellationToken ct)
    {
        var session = await _sessionService.GetSessionAsync(sessionId, GetCurrentUserId(), ct);
        return session == null ? NotFound() : Ok(session);
    }

    [HttpPost("sessions")]
    [Authorize(Policy = "RequirePermission:collaboration.manage")]
    [ProducesResponseType(typeof(CollaborationSession), 201)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Create a new collaboration session")]
    public async Task<ActionResult<CollaborationSession>> CreateSession([FromBody] CreateSessionRequest request, CancellationToken ct)
    {
        var session = await _sessionService.CreateSessionAsync(request, GetCurrentUserId(), ct);
        return CreatedAtAction(nameof(GetSession), new { sessionId = session.Id }, session);
    }

    [HttpPut("sessions/{sessionId}")]
    [Authorize(Policy = "RequirePermission:collaboration.manage")]
    [ProducesResponseType(typeof(CollaborationSession), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(403)]
    [SwaggerOperation("Update session")]
    public async Task<ActionResult<CollaborationSession>> UpdateSession(Guid sessionId, [FromBody] UpdateSessionRequest request, CancellationToken ct)
    {
        try
        {
            var session = await _sessionService.UpdateSessionAsync(sessionId, request, GetCurrentUserId(), ct);
            return Ok(session);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpDelete("sessions/{sessionId}")]
    [Authorize(Policy = "RequirePermission:collaboration.manage")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(403)]
    [SwaggerOperation("Delete a session")]
    public async Task<IActionResult> DeleteSession(Guid sessionId, CancellationToken ct)
    {
        var success = await _sessionService.DeleteSessionAsync(sessionId, GetCurrentUserId(), ct);
        return success ? NoContent() : NotFound();
    }

    [HttpPost("sessions/{sessionId}/start")]
    [Authorize(Policy = "RequirePermission:collaboration.manage")]
    [ProducesResponseType(typeof(CollaborationSession), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Start a session")]
    public async Task<ActionResult<CollaborationSession>> StartSession(Guid sessionId, CancellationToken ct)
    {
        var session = await _sessionService.StartSessionAsync(sessionId, GetCurrentUserId(), ct);
        return Ok(session);
    }

    [HttpPost("sessions/{sessionId}/end")]
    [Authorize(Policy = "RequirePermission:collaboration.manage")]
    [ProducesResponseType(typeof(CollaborationSession), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("End a session")]
    public async Task<ActionResult<CollaborationSession>> EndSession(Guid sessionId, CancellationToken ct)
    {
        var session = await _sessionService.EndSessionAsync(sessionId, GetCurrentUserId(), ct);
        return Ok(session);
    }

    [HttpPost("sessions/{sessionId}/pause")]
    [ProducesResponseType(typeof(CollaborationSession), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Pause a session")]
    public async Task<ActionResult<CollaborationSession>> PauseSession(Guid sessionId, CancellationToken ct)
    {
        var session = await _sessionService.PauseSessionAsync(sessionId, GetCurrentUserId(), ct);
        return Ok(session);
    }

    [HttpPost("sessions/{sessionId}/resume")]
    [ProducesResponseType(typeof(CollaborationSession), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Resume a session")]
    public async Task<ActionResult<CollaborationSession>> ResumeSession(Guid sessionId, CancellationToken ct)
    {
        var session = await _sessionService.ResumeSessionAsync(sessionId, GetCurrentUserId(), ct);
        return Ok(session);
    }

    // Participant endpoints
    [HttpPost("sessions/{sessionId}/join")]
    [ProducesResponseType(typeof(SessionParticipant), 201)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Join a session")]
    public async Task<ActionResult<SessionParticipant>> JoinSession(Guid sessionId, [FromBody] JoinSessionRequest request, CancellationToken ct)
    {
        var participant = await _participantService.JoinSessionAsync(sessionId, request, ct);
        return CreatedAtAction(nameof(GetParticipant), new { sessionId, userId = request.UserId }, participant);
    }

    [HttpGet("sessions/{sessionId}/participants")]
    [ProducesResponseType(typeof(List<SessionParticipant>), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get session participants")]
    public async Task<ActionResult<List<SessionParticipant>>> GetParticipants(Guid sessionId, CancellationToken ct)
    {
        var participants = await _participantService.GetParticipantsAsync(sessionId, ct);
        return Ok(participants);
    }

    [HttpGet("sessions/{sessionId}/participants/{userId}")]
    [ProducesResponseType(typeof(SessionParticipant), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get participant")]
    public async Task<ActionResult<SessionParticipant>> GetParticipant(Guid sessionId, string userId, CancellationToken ct)
    {
        var participant = await _participantService.GetParticipantAsync(sessionId, userId, ct);
        return participant == null ? NotFound() : Ok(participant);
    }

    [HttpPut("sessions/{sessionId}/participants/{userId}")]
    [ProducesResponseType(typeof(SessionParticipant), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(403)]
    [SwaggerOperation("Update participant")]
    public async Task<ActionResult<SessionParticipant>> UpdateParticipant(Guid sessionId, string userId, [FromBody] UpdateParticipantRequest request, CancellationToken ct)
    {
        try
        {
            var actorId = User.FindFirst("sub")?.Value ?? "unknown";
            var participant = await _participantService.UpdateParticipantAsync(sessionId, userId, request, actorId, CancellationToken.None);
            return Ok(participant);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost("sessions/{sessionId}/leave")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Leave a session")]
    public async Task<IActionResult> LeaveSession(Guid sessionId, CancellationToken ct)
    {
        var success = await _participantService.LeaveSessionAsync(sessionId, GetCurrentUserId(), ct);
        return success ? NoContent() : NotFound();
    }

    [HttpDelete("sessions/{sessionId}/participants/{userId}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(403)]
    [SwaggerOperation("Remove participant")]
    public async Task<IActionResult> RemoveParticipant(Guid sessionId, string userId, CancellationToken ct)
    {
        var actorId = User.FindFirst("sub")?.Value ?? "unknown";
        var success = await _participantService.RemoveParticipantAsync(sessionId, userId, actorId, CancellationToken.None);
        return success ? NoContent() : NotFound();
    }

    [HttpPost("sessions/{sessionId}/control/request")]
    [ProducesResponseType(typeof(SessionParticipant), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Request control of session")]
    public async Task<ActionResult<SessionParticipant>> RequestControl(Guid sessionId, CancellationToken ct)
    {
        var participant = await _participantService.RequestControlAsync(sessionId, GetCurrentUserId(), ct);
        return Ok(participant);
    }

    [HttpPost("sessions/{sessionId}/control/grant")]
    [ProducesResponseType(typeof(SessionParticipant), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(403)]
    [SwaggerOperation("Grant control to participant")]
    public async Task<ActionResult<SessionParticipant>> GrantControl(Guid sessionId, [FromBody] GrantControlRequest request, CancellationToken ct)
    {
        try
        {
            var participant = await _participantService.GrantControlAsync(sessionId, request.TargetUserId, GetCurrentUserId(), ct);
            return Ok(participant);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost("sessions/{sessionId}/control/revoke")]
    [ProducesResponseType(typeof(SessionParticipant), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(403)]
    [SwaggerOperation("Revoke control from participant")]
    public async Task<ActionResult<SessionParticipant>> RevokeControl(Guid sessionId, [FromBody] RevokeControlRequest request, CancellationToken ct)
    {
        try
        {
            var participant = await _participantService.RevokeControlAsync(sessionId, request.TargetUserId, GetCurrentUserId(), ct);
            return Ok(participant);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost("sessions/{sessionId}/control/transfer")]
    [ProducesResponseType(typeof(SessionParticipant), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(403)]
    [SwaggerOperation("Transfer control between participants")]
    public async Task<ActionResult<SessionParticipant>> TransferControl(Guid sessionId, [FromBody] TransferControlRequest request, CancellationToken ct)
    {
        try
        {
            var participant = await _participantService.TransferControlAsync(sessionId, request.FromUserId, request.ToUserId, ct);
            return Ok(participant);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPut("sessions/{sessionId}/cursor")]
    [ProducesResponseType(typeof(SessionParticipant), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Update cursor position")]
    public async Task<ActionResult<SessionParticipant>> UpdateCursor(Guid sessionId, [FromBody] CursorPosition cursor, CancellationToken ct)
    {
        var participant = await _participantService.UpdateCursorAsync(sessionId, GetCurrentUserId(), cursor, ct);
        return Ok(participant);
    }

    [HttpPut("sessions/{sessionId}/viewport")]
    [ProducesResponseType(typeof(SessionParticipant), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Update viewport")]
    public async Task<ActionResult<SessionParticipant>> UpdateViewport(Guid sessionId, [FromBody] ViewportInfo viewport, CancellationToken ct)
    {
        var participant = await _participantService.UpdateViewportAsync(sessionId, GetCurrentUserId(), viewport, ct);
        return Ok(participant);
    }

    // Chat endpoints
    [HttpPost("sessions/{sessionId}/chat")]
    [ProducesResponseType(typeof(ChatMessage), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Send chat message")]
    public async Task<ActionResult<ChatMessage>> SendMessage(Guid sessionId, [FromBody] SendMessageRequest request, CancellationToken ct)
    {
        var message = await _chatService.SendMessageAsync(sessionId, request, GetCurrentUserId(), ct);
        return CreatedAtAction(nameof(GetMessages), new { sessionId, messageId = message.Id }, message);
    }

    [HttpGet("sessions/{sessionId}/chat")]
    [ProducesResponseType(typeof(List<ChatMessage>), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get chat messages")]
    public async Task<ActionResult<List<ChatMessage>>> GetMessages(
        Guid sessionId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] MessageType? type,
        [FromQuery] string? senderId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var filter = new ChatFilter(fromDate, toDate, type, senderId, page, pageSize);
        var messages = await _chatService.GetMessagesAsync(sessionId, filter, ct);
        return Ok(messages);
    }

    [HttpPost("chat/{messageId}/reactions")]
    [ProducesResponseType(typeof(ChatMessage), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Add reaction to message")]
    public async Task<ActionResult<ChatMessage>> AddReaction(Guid messageId, [FromBody] AddReactionRequest request, CancellationToken ct)
    {
        var message = await _chatService.AddReactionAsync(messageId, request.Emoji, GetCurrentUserId(), ct);
        return Ok(message);
    }

    [HttpDelete("chat/{messageId}/reactions/{emoji}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Remove reaction from message")]
    public async Task<IActionResult> RemoveReaction(Guid messageId, string emoji, CancellationToken ct)
    {
        var success = await _chatService.RemoveReactionAsync(messageId, emoji, GetCurrentUserId(), ct);
        return success ? NoContent() : NotFound();
    }

    // Annotation endpoints
    [HttpPost("sessions/{sessionId}/annotations")]
    [ProducesResponseType(typeof(ScreenAnnotation), 201)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Create annotation")]
    public async Task<ActionResult<ScreenAnnotation>> CreateAnnotation(Guid sessionId, [FromBody] CreateAnnotationRequest request, CancellationToken ct)
    {
        var annotation = await _annotationService.CreateAnnotationAsync(sessionId, request, GetCurrentUserId(), ct);
        return CreatedAtAction(nameof(GetAnnotation), new { sessionId, annotationId = annotation.Id }, annotation);
    }

    [HttpGet("sessions/{sessionId}/annotations")]
    [ProducesResponseType(typeof(List<ScreenAnnotation>), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get annotations")]
    public async Task<ActionResult<List<ScreenAnnotation>>> GetAnnotations(
        Guid sessionId,
        [FromQuery] AnnotationType? type,
        [FromQuery] string? authorId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] bool? isPersistent,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken ct = default)
    {
        var filter = new AnnotationFilter(type, authorId, fromDate, toDate, isPersistent, page, pageSize);
        var annotations = await _annotationService.GetAnnotationsAsync(sessionId, filter, ct);
        return Ok(annotations);
    }

    [HttpPut("annotations/{annotationId}")]
    [ProducesResponseType(typeof(ScreenAnnotation), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(403)]
    [SwaggerOperation("Update annotation")]
    public async Task<ActionResult<ScreenAnnotation>> UpdateAnnotation(Guid annotationId, [FromBody] UpdateAnnotationRequest request, CancellationToken ct)
    {
        try
        {
            var annotation = await _annotationService.UpdateAnnotationAsync(annotationId, request, GetCurrentUserId(), ct);
            return Ok(annotation);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpDelete("annotations/{annotationId}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(403)]
    [SwaggerOperation("Delete annotation")]
    public async Task<IActionResult> DeleteAnnotation(Guid annotationId, CancellationToken ct)
    {
        var success = await _annotationService.DeleteAnnotationAsync(annotationId, GetCurrentUserId(), ct);
        return success ? NoContent() : NotFound();
    }

    [HttpDelete("sessions/{sessionId}/annotations")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(403)]
    [SwaggerOperation("Clear all annotations")]
    public async Task<IActionResult> ClearAnnotations(Guid sessionId, CancellationToken ct)
    {
        var success = await _annotationService.ClearAnnotationsAsync(sessionId, GetCurrentUserId(), ct);
        return success ? NoContent() : NotFound();
    }

    // Recording endpoints
    [HttpPost("sessions/{sessionId}/recordings")]
    [ProducesResponseType(typeof(SessionRecording), 201)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Start recording")]
    public async Task<ActionResult<SessionRecording>> StartRecording(Guid sessionId, [FromBody] StartRecordingRequest request, CancellationToken ct)
    {
        var recording = await _recordingService.StartRecordingAsync(sessionId, request, GetCurrentUserId(), ct);
        return CreatedAtAction(nameof(GetRecording), new { recordingId = recording.Id }, recording);
    }

    [HttpGet("recordings/{recordingId}")]
    [ProducesResponseType(typeof(SessionRecording), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get recording")]
    public async Task<ActionResult<SessionRecording>> GetRecording(Guid recordingId, CancellationToken ct)
    {
        var recording = await _recordingService.GetRecordingAsync(recordingId, GetCurrentUserId(), ct);
        return recording == null ? NotFound() : Ok(recording);
    }

    [HttpPost("recordings/{recordingId}/stop")]
    [ProducesResponseType(typeof(SessionRecording), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Stop recording")]
    public async Task<ActionResult<SessionRecording>> StopRecording(Guid recordingId, CancellationToken ct)
    {
        var recording = await _recordingService.StopRecordingAsync(recordingId, GetCurrentUserId(), ct);
        return Ok(recording);
    }

    [HttpGet("sessions/{sessionId}/recordings")]
    [ProducesResponseType(typeof(List<SessionRecording>), 200)]
    [SwaggerOperation("List recordings for session")]
    public async Task<ActionResult<List<SessionRecording>>> GetRecordings(
        Guid sessionId,
        [FromQuery] RecordingStatus? status,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var filter = new RecordingFilter(status, fromDate, toDate, sessionId, 1, 50);
        var recordings = await _recordingService.GetRecordingsAsync(GetCurrentUserId(), filter, ct);
        return Ok(recordings);
    }

    [HttpDelete("recordings/{recordingId}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Delete recording")]
    public async Task<IActionResult> DeleteRecording(Guid recordingId, CancellationToken ct)
    {
        var success = await _recordingService.DeleteRecordingAsync(recordingId, GetCurrentUserId(), ct);
        return success ? NoContent() : NotFound();
    }

    [HttpGet("recordings/{recordingId}/export")]
    [ProducesResponseType(typeof(FileContentResult), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Export recording")]
    public async Task<IActionResult> ExportRecording(
        Guid recordingId,
        [FromQuery] ExportFormat format = ExportFormat.Json,
        CancellationToken ct = default)
    {
        var bytes = await _recordingService.ExportRecordingAsync(recordingId, format, ct);
        var contentType = format switch
        {
            ExportFormat.Json => "application/json",
            ExportFormat.Csv => "text/csv",
            ExportFormat.WebM => "video/webm",
            ExportFormat.MP4 => "video/mp4",
            _ => "application/octet-stream"
        };
        return File(bytes, contentType, $"recording-{recordingId}.{format.ToString().ToLower()}");
    }

    // Screen share
    [HttpPost("sessions/{sessionId}/screen-share")]
    [ProducesResponseType(typeof(ScreenShareSession), 201)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Start screen sharing")]
    public async Task<ActionResult<ScreenShareSession>> StartScreenShare(Guid sessionId, [FromBody] StartScreenShareRequest request, CancellationToken ct)
    {
        var screenShare = await _screenShareService.StartScreenShareAsync(sessionId, request, GetCurrentUserId(), ct);
        return CreatedAtAction(nameof(GetScreenShare), new { sessionId, shareId = screenShare.Id }, screenShare);
    }

    [HttpGet("sessions/{sessionId}/screen-share")]
    [ProducesResponseType(typeof(ScreenShareSession), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get screen share info")]
    public async Task<ActionResult<ScreenShareSession>> GetScreenShare(Guid sessionId, CancellationToken ct)
    {
        var screenShare = await _screenShareService.GetScreenShareAsync(sessionId, GetCurrentUserId(), ct);
        return screenShare == null ? NotFound() : Ok(screenShare);
    }

    [HttpPost("sessions/{sessionId}/screen-share/stop")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Stop screen sharing")]
    public async Task<IActionResult> StopScreenShare(Guid sessionId, CancellationToken ct)
    {
        var success = await _screenShareService.StopScreenShareAsync(sessionId, GetCurrentUserId(), ct);
        return success ? NoContent() : NotFound();
    }

    // Presence
    [HttpGet("presence")]
    [ProducesResponseType(typeof(List<PresenceInfo>), 200)]
    [SwaggerOperation("Get online users")]
    public async Task<ActionResult<List<PresenceInfo>>> GetPresences(
        [FromQuery] PresenceStatus? status,
        [FromQuery] string? search,
        [FromQuery] bool? isOnline,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var filter = new PresenceFilter(status, search, isOnline, page, pageSize);
        var presences = await _presenceService.GetPresencesAsync(GetCurrentUserId(), filter, ct);
        return Ok(presences);
    }

    [HttpPut("presence")]
    [ProducesResponseType(typeof(PresenceInfo), 200)]
    [SwaggerOperation("Update presence")]
    public async Task<ActionResult<PresenceInfo>> UpdatePresence([FromBody] PresenceUpdateRequest request, CancellationToken ct)
    {
        var presence = await _presenceService.UpdatePresenceAsync(GetCurrentUserId(), request, ct);
        return Ok(presence);
    }

    // Workspace
    [HttpPost("workspaces")]
    [Authorize(Policy = "RequirePermission:workspace.manage")]
    [ProducesResponseType(typeof(SharedWorkspace), 201)]
    [ProducesResponseType(400)]
    [SwaggerOperation("Create workspace")]
    public async Task<ActionResult<SharedWorkspace>> CreateWorkspace([FromBody] CreateWorkspaceRequest request, CancellationToken ct)
    {
        var workspace = await _workspaceService.CreateWorkspaceAsync(request, GetCurrentUserId(), ct);
        return CreatedAtAction(nameof(GetWorkspace), new { workspaceId = workspace.Id }, workspace);
    }

    [HttpGet("workspaces/{workspaceId}")]
    [ProducesResponseType(typeof(SharedWorkspace), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Get workspace")]
    public async Task<ActionResult<SharedWorkspace>> GetWorkspace(Guid workspaceId, CancellationToken ct)
    {
        var workspace = await _workspaceService.GetWorkspaceAsync(workspaceId, GetCurrentUserId(), ct);
        return workspace == null ? NotFound() : Ok(workspace);
    }

    [HttpPost("workspaces/{workspaceId}/operations")]
    [ProducesResponseType(typeof(WorkspaceOperationResult), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation("Apply workspace operation")]
    public async Task<ActionResult<WorkspaceOperationResult>> ApplyOperation(Guid workspaceId, [FromBody] WorkspaceOperation operation, CancellationToken ct)
    {
        var result = await _workspaceService.ApplyOperationAsync(workspaceId, operation, GetCurrentUserId(), ct);
        return Ok(result);
    }

    private string GetCurrentUserId() => User.FindFirst("sub")?.Value ?? "system";
}

// Request DTOs
public sealed record GrantControlRequest(string TargetUserId);
public sealed record RevokeControlRequest(string TargetUserId);
public sealed record TransferControlRequest(string FromUserId, string ToUserId);
public sealed record AddReactionRequest(string Emoji);