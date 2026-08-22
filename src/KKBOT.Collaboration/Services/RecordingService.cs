// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Collaboration.Abstractions;
using KKBOT.Collaboration.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KKBOT.Collaboration.Services;

public sealed class RecordingService : IRecordingService
{
    private readonly CollaborationDbContext _dbContext;
    private readonly ILogger<RecordingService> _logger;

    public RecordingService(CollaborationDbContext dbContext, ILogger<RecordingService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<SessionRecording> StartRecordingAsync(Guid sessionId, StartRecordingRequest request, string userId, CancellationToken ct = default)
    {
        var session = await _dbContext.CollaborationSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session == null)
            throw new InvalidOperationException("Session not found");

        var recording = new SessionRecording
        {
            SessionId = sessionId,
            FilePath = $"/recordings/{sessionId}/{Guid.NewGuid()}.{GetExtension(request.Format)}",
            Format = request.Format,
            Status = RecordingStatus.Recording
        };

        _dbContext.SessionRecordings.Add(recording);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Started recording {RecordingId} for session {SessionId}", recording.Id, sessionId);
        return recording;
    }

    public async Task<SessionRecording?> GetRecordingAsync(Guid recordingId, string userId, CancellationToken ct = default)
    {
        return await _dbContext.SessionRecordings
            .Include(r => r.Events)
            .FirstOrDefaultAsync(r => r.Id == recordingId, ct);
    }

    public async Task<List<SessionRecording>> GetRecordingsAsync(string userId, RecordingFilter filter, CancellationToken ct = default)
    {
        var query = _dbContext.SessionRecordings
            .Where(r => r.Session.OwnerId == userId || r.Session.Participants.Any(p => p.UserId == userId))
            .AsQueryable();

        if (filter.Status.HasValue)
            query = query.Where(r => r.Status == filter.Status.Value);

        if (filter.FromDate.HasValue)
            query = query.Where(r => r.StartedAt >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(r => r.StartedAt <= filter.ToDate.Value);

        if (filter.SessionId.HasValue)
            query = query.Where(r => r.SessionId == filter.SessionId.Value);

        query = query.OrderByDescending(r => r.StartedAt);

        var items = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);

        return items;
    }

    public async Task<SessionRecording> StopRecordingAsync(Guid recordingId, string userId, CancellationToken ct = default)
    {
        var recording = await _dbContext.SessionRecordings.FirstOrDefaultAsync(r => r.Id == recordingId, ct);
        if (recording == null)
            throw new InvalidOperationException("Recording not found");

        recording.Status = RecordingStatus.Completed;
        recording.EndedAt = DateTime.UtcNow;
        recording.Duration = recording.EndedAt.Value - recording.StartedAt;
        recording.FrameCount = 0; // Would be calculated from actual recording

        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("Stopped recording {RecordingId}", recordingId);
        return recording;
    }

    public async Task<bool> DeleteRecordingAsync(Guid recordingId, string userId, CancellationToken ct = default)
    {
        var recording = await _dbContext.SessionRecordings.FirstOrDefaultAsync(r => r.Id == recordingId, ct);
        if (recording == null) return false;

        _dbContext.SessionRecordings.Remove(recording);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<byte[]> ExportRecordingAsync(Guid recordingId, ExportFormat format, CancellationToken ct = default)
    {
        var recording = await _dbContext.SessionRecordings
            .Include(r => r.Events)
            .FirstOrDefaultAsync(r => r.Id == recordingId, ct);

        if (recording == null)
            throw new InvalidOperationException("Recording not found");

        var data = new
        {
            recording.Id,
            recording.SessionId,
            recording.Format,
            recording.Status,
            recording.StartedAt,
            recording.EndedAt,
            recording.Duration,
            recording.FrameCount,
            recording.FileSizeBytes,
            Events = recording.Events.Select(e => new
            {
                e.Id,
                e.Type,
                e.Timestamp,
                e.ActorId,
                e.EventDataJson,
                e.SequenceNumber
            })
        };

        return format switch
        {
            ExportFormat.Json => System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(data, new JsonSerializerOptions { WriteIndented = true }),
            ExportFormat.Csv => System.Text.Encoding.UTF8.GetBytes("CSV export not implemented"),
            ExportFormat.WebM => System.Text.Encoding.UTF8.GetBytes("WebM export not implemented"),
            ExportFormat.MP4 => System.Text.Encoding.UTF8.GetBytes("MP4 export not implemented"),
            _ => throw new System.ArgumentException($"Unsupported format: {format}")
        };
    }

    private string GetExtension(RecordingFormat format)
    {
        return format switch
        {
            RecordingFormat.Custom => "kkrec",
            RecordingFormat.WebM => "webm",
            RecordingFormat.MP4 => "mp4",
            RecordingFormat.JSON => "json",
            _ => "bin"
        };
    }
}