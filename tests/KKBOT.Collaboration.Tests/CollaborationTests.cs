using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KKBOT.Collaboration.Abstractions;
using KKBOT.Collaboration.Models;
using KKBOT.Collaboration.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace KKBOT.Collaboration.Tests;

public class CollaborationTests
{
    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole());
        
        services.AddDbContext<CollaborationDbContext>(options => 
            options.UseSqlite("DataSource=:memory:"));
        
        services.AddScoped<ICollaborationSessionService, CollaborationSessionService>();
        services.AddScoped<IParticipantService, ParticipantService>();
        services.AddScoped<IPresenceService, PresenceService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IAnnotationService, AnnotationService>();
        services.AddScoped<IRecordingService, RecordingService>();

        var provider = services.BuildServiceProvider();
        
        // Ensure database created
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CollaborationDbContext>();
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        
        return provider;
    }

    [Fact]
    public async Task Session_CreateAndGet_Works()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var sessionService = scope.ServiceProvider.GetRequiredService<ICollaborationSessionService>();

        var request = new CreateSessionRequest(
            "Test Session",
            "A test collaboration session",
            Guid.NewGuid(),
            SessionType.CollaborativeControl,
            null,
            null
        );

        var session = await sessionService.CreateSessionAsync(request, "user-123", CancellationToken.None);

        Assert.NotNull(session);
        Assert.Equal("Test Session", session.Name);
        Assert.Equal(SessionStatus.Waiting, session.Status);
        Assert.Equal("user-123", session.OwnerId);

        var retrieved = await sessionService.GetSessionAsync(session.Id, "user-123", CancellationToken.None);
        Assert.NotNull(retrieved);
        Assert.Equal(session.Id, retrieved.Id);
    }

    [Fact]
    public async Task Session_StartAndEnd_Works()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var sessionService = scope.ServiceProvider.GetRequiredService<ICollaborationSessionService>();

        var request = new CreateSessionRequest(
            "Lifecycle Test",
            "Testing session lifecycle",
            Guid.NewGuid(),
            SessionType.CollaborativeControl
        );

        var session = await sessionService.CreateSessionAsync(request, "user-456", CancellationToken.None);

        // Start session
        var started = await sessionService.StartSessionAsync(session.Id, "user-456", CancellationToken.None);
        Assert.Equal(SessionStatus.Active, started.Status);
        Assert.NotNull(started.StartedAt);

        // End session
        var ended = await sessionService.EndSessionAsync(session.Id, "user-456", CancellationToken.None);
        Assert.Equal(SessionStatus.Ended, ended.Status);
        Assert.NotNull(ended.EndedAt);
    }

    [Fact]
    public async Task Participant_JoinAndLeave_Works()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var sessionService = scope.ServiceProvider.GetRequiredService<ICollaborationSessionService>();
        var participantService = scope.ServiceProvider.GetRequiredService<IParticipantService>();

        var request = new CreateSessionRequest(
            "Participant Test",
            "Testing participants",
            Guid.NewGuid()
        );

        var session = await sessionService.CreateSessionAsync(request, "owner-123", CancellationToken.None);

        // Join session
        var joinRequest = new JoinSessionRequest("Test User", "https://avatar.com/user.png", ParticipantRole.Viewer);
        var participant = await participantService.JoinSessionAsync(session.Id, joinRequest, CancellationToken.None);

        Assert.NotNull(participant);
        Assert.Equal("Test User", participant.UserName);
        Assert.Equal(ParticipantRole.Viewer, participant.Role);
        Assert.Equal(ParticipantStatus.Connected, participant.Status);

        // Leave session
        var left = await participantService.LeaveSessionAsync(session.Id, "user-123", CancellationToken.None);
        Assert.True(left);

        var leftParticipant = await participantService.GetParticipantAsync(session.Id, "user-123", CancellationToken.None);
        Assert.Equal(ParticipantStatus.Disconnected, leftParticipant.Status);
        Assert.NotNull(leftParticipant.LeftAt);
    }

    [Fact]
    public async Task Participant_ControlManagement_Works()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var sessionService = scope.ServiceProvider.GetRequiredService<ICollaborationSessionService>();
        var participantService = scope.ServiceProvider.GetRequiredService<IParticipantService>();

        var session = await sessionService.CreateSessionAsync(
            new CreateSessionRequest("Control Test", "Test", Guid.NewGuid()),
            "owner-123", CancellationToken.None);

        // Add participants
        await participantService.JoinSessionAsync(session.Id, 
            new JoinSessionRequest("Controller", "avatar.png", ParticipantRole.Controller), 
            CancellationToken.None);
        await participantService.JoinSessionAsync(session.Id, 
            new JoinSessionRequest("Viewer", "avatar.png", ParticipantRole.Viewer), 
            CancellationToken.None);

        // Grant control
        var viewer = await participantService.GetParticipantAsync(session.Id, "viewer", CancellationToken.None);
        var granted = await participantService.GrantControlAsync(session.Id, "viewer", "owner", CancellationToken.None);
        
        Assert.Equal(ParticipantRole.Controller, granted.Role);
        Assert.True(granted.Capabilities.CanControl);

        // Revoke control
        var revoked = await participantService.RevokeControlAsync(session.Id, "viewer", "owner", CancellationToken.None);
        Assert.Equal(ParticipantRole.Viewer, revoked.Role);
        Assert.False(revoked.Capabilities.CanControl);
    }

    [Fact]
    public async Task Chat_MessagesAndReactions_Work()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var sessionService = scope.ServiceProvider.GetRequiredService<ICollaborationSessionService>();
        var chatService = scope.ServiceProvider.GetRequiredService<IChatService>();

        var session = await sessionService.CreateSessionAsync(
            new CreateSessionRequest("Chat Test", "Test", Guid.NewGuid()),
            "user-123", CancellationToken.None);

        // Send message
        var message = await chatService.SendMessageAsync(session.Id, 
            new SendMessageRequest("Hello, world!", MessageType.Text), 
            "user-123", CancellationToken.None);

        Assert.Equal("Hello, world!", message.Content);
        Assert.Equal(MessageType.Text, message.Type);
        Assert.Equal("user-123", message.SenderId);

        // Add reaction
        var reacted = await chatService.AddReactionAsync(message.Id, "👍", "user-456", CancellationToken.None);
        var reaction = assertedMessage.Reactions.FirstOrDefault(r => r.Emoji == "👍");
        Assert.NotNull(reaction);
        Assert.Equal("👍", reaction.Emoji);
        Assert.Equal("user-456", reaction.UserId);

        // Remove reaction
        var removed = await chatService.RemoveReactionAsync(message.Id, "👍", "user-456", CancellationToken.None);
        Assert.True(removed);
    }

    [Fact]
    public async Task Annotations_CreateAndManage_Works()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var sessionService = scope.ServiceProvider.GetRequiredService<ICollaborationSessionService>();
        var annotationService = scope.ServiceProvider.GetRequiredService<IAnnotationService>();

        var session = await sessionService.CreateSessionAsync(
            new CreateSessionRequest("Annotation Test", "Test", Guid.NewGuid()),
            "user-123", CancellationToken.None);

        // Create annotation
        var points = new List<Point2D>
        {
            new Point2D { X = 100, Y = 100 },
            new Point2D { X = 200, Y = 200 }
        };

        var annotation = await annotationService.CreateAnnotationAsync(session.Id,
            new CreateAnnotationRequest(
                AnnotationType.Pen,
                AnnotationColor.Red,
                2.0,
                points
            ), "user-123", CancellationToken.None);

        Assert.NotNull(annotation);
        Assert.Equal(AnnotationType.Pen, annotation.Type);
        Assert.Equal(AnnotationColor.Red, annotation.Color);
        Assert.Equal(2, annotation.Points.Count);

        // Update annotation
        var updated = await annotationService.UpdateAnnotationAsync(annotation.Id,
            new UpdateAnnotationRequest { Color = AnnotationColor.Blue },
            "user-123", CancellationToken.None);

        Assert.Equal(AnnotationColor.Blue, updated.Color);

        // Delete annotation
        var deleted = await annotationService.DeleteAnnotationAsync(annotation.Id, "user-123", CancellationToken.None);
        Assert.True(deleted);
    }

    [Fact]
    public async Task Recording_Lifecycle_Works()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var sessionService = scope.ServiceProvider.GetRequiredService<ICollaborationSessionService>();
        var recordingService = scope.ServiceProvider.GetRequiredService<IRecordingService>();

        var session = await sessionService.CreateSessionAsync(
            new CreateSessionRequest("Recording Test", "Test", Guid.NewGuid()),
            "user-123", CancellationToken.None);

        // Start recording
        var recording = await recordingService.StartRecordingAsync(session.Id,
            new StartRecordingRequest(RecordingFormat.Custom, false, true, true),
            "user-123", CancellationToken.None);

        Assert.Equal(RecordingStatus.Recording, recording.Status);
        Assert.NotNull(recording.FilePath);

        // Stop recording
        var stopped = await recordingService.StopRecordingAsync(recording.Id, "user-123", CancellationToken.None);
        Assert.Equal(RecordingStatus.Completed, stopped.Status);
        Assert.NotNull(stopped.EndedAt);
        Assert.NotNull(stopped.Duration);
    }

    [Fact]
    public async Task Presence_Updates_Work()
    {
        var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var presenceService = scope.ServiceProvider.GetRequiredService<IPresenceService>();

        var presence = await presenceService.UpdatePresenceAsync("user-123",
            new PresenceUpdateRequest
            {
                Status = PresenceStatus.Online,
                CurrentActivity = "Testing collaboration",
                CurrentInstanceId = "instance-123"
            }, CancellationToken.None);

        Assert.Equal(PresenceStatus.Online, presence.Status);
        Assert.Equal("Testing collaboration", presence.CurrentActivity);
        Assert.Equal("instance-123", presence.CurrentInstanceId);

        // Get presence
        var retrieved = await presenceService.GetPresenceAsync("user-123", CancellationToken.None);
        Assert.NotNull(retrieved);
        Assert.Equal(PresenceStatus.Online, retrieved.Status);
    }
}