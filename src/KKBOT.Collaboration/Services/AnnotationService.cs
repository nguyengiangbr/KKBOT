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

public sealed class AnnotationService : IAnnotationService
{
    private readonly CollaborationDbContext _dbContext;
    private readonly ILogger<AnnotationService> _logger;

    public AnnotationService(CollaborationDbContext dbContext, ILogger<AnnotationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ScreenAnnotation> CreateAnnotationAsync(Guid sessionId, CreateAnnotationRequest request, string userId, CancellationToken ct = default)
    {
        var participant = await GetParticipantAsync(sessionId, userId);
        if (participant == null)
            throw new InvalidOperationException("User not in session");

        var annotation = new ScreenAnnotation
        {
            SessionId = sessionId,
            AuthorId = userId,
            AuthorName = GetParticipantName(sessionId, userId),
            Type = request.Type,
            Color = request.Color,
            StrokeWidth = request.StrokeWidth,
            Points = request.Points,
            Text = request.Text,
            Bounds = request.Bounds,
            IsPersistent = request.IsPersistent
        };

        _dbContext.ScreenAnnotations.Add(annotation);
        await _dbContext.SaveChangesAsync(ct);
        return annotation;
    }

    public async Task<List<ScreenAnnotation>> GetAnnotationsAsync(Guid sessionId, AnnotationFilter filter, CancellationToken ct = default)
    {
        var query = _dbContext.ScreenAnnotations
            .Where(a => a.SessionId == sessionId)
            .AsQueryable();

        if (filter.Type.HasValue)
            query = query.Where(a => a.Type == filter.Type.Value);

        if (!string.IsNullOrWhiteSpace(filter.AuthorId))
            query = query.Where(a => a.AuthorId == filter.AuthorId);

        if (filter.FromDate.HasValue)
            query = query.Where(a => a.CreatedAt >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(a => a.CreatedAt <= filter.ToDate.Value);

        if (filter.IsPersistent.HasValue)
            query = query.Where(a => a.IsPersistent == filter.IsPersistent.Value);

        query = query.OrderBy(a => a.CreatedAt);

        var items = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);

        return items;
    }

    public async Task<ScreenAnnotation> UpdateAnnotationAsync(Guid annotationId, UpdateAnnotationRequest request, string userId, CancellationToken ct = default)
    {
        var annotation = await _dbContext.ScreenAnnotations.FirstOrDefaultAsync(a => a.Id == annotationId, ct);
        if (annotation == null)
            throw new InvalidOperationException("Annotation not found");

        if (annotation.AuthorId != userId)
            throw new UnauthorizedAccessException("Not authorized to update this annotation");

        if (request.Type.HasValue) annotation.Type = request.Type.Value;
        if (request.Color.HasValue) annotation.Color = request.Color.Value;
        if (request.StrokeWidth.HasValue) annotation.StrokeWidth = request.StrokeWidth.Value;
        if (request.Points != null) annotation.Points = request.Points;
        if (request.Text != null) annotation.Text = request.Text;
        if (request.Bounds != null) annotation.Bounds = request.Bounds;
        if (request.IsPersistent.HasValue) annotation.IsPersistent = request.IsPersistent.Value;

        await _dbContext.SaveChangesAsync(ct);
        return annotation;
    }

    public async Task<bool> DeleteAnnotationAsync(Guid annotationId, string userId, CancellationToken ct = default)
    {
        var annotation = await _dbContext.ScreenAnnotations.FirstOrDefaultAsync(a => a.Id == annotationId, ct);
        if (annotation == null) return false;

        if (annotation.AuthorId != userId)
            throw new UnauthorizedAccessException("Not authorized to delete this annotation");

        _dbContext.ScreenAnnotations.Remove(annotation);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ClearAnnotationsAsync(Guid sessionId, string userId, CancellationToken ct = default)
    {
        var annotations = await _dbContext.ScreenAnnotations
            .Where(a => a.SessionId == sessionId)
            .ToListAsync(ct);

        if (!annotations.Any()) return false;

        _dbContext.ScreenAnnotations.RemoveRange(annotations);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    private async Task<SessionParticipant?> GetParticipantAsync(Guid sessionId, string userId)
    {
        return await _dbContext.SessionParticipants
            .FirstOrDefaultAsync(p => p.SessionId == userId && p.UserId == userId);
    }

    private string GetParticipantName(Guid sessionId, string userId)
    {
        var participant = _dbContext.SessionParticipants
            .FirstOrDefault(p => p.SessionId == userId && p.UserId == userId);
        return participant?.UserName ?? userId;
    }
}