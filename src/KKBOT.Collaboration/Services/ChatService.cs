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

public sealed class ChatService : IChatService
{
    private readonly CollaborationDbContext _dbContext;
    private readonly ILogger<ChatService> _logger;

    public ChatService(CollaborationDbContext dbContext, ILogger<ChatService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ChatMessage> SendMessageAsync(Guid sessionId, SendMessageRequest request, string userId, CancellationToken ct = default)
    {
        var participant = await GetParticipantAsync(sessionId, userId);
        if (participant == null)
            throw new InvalidOperationException("User not in session");

        var message = new ChatMessage
        {
            SessionId = sessionId,
            SenderId = userId,
            SenderName = GetParticipantName(sessionId, userId),
            Content = request.Content,
            Type = request.Type,
            Metadata = request.Metadata ?? new Dictionary<string, object>()
        };

        _dbContext.ChatMessages.Add(message);
        await _dbContext.SaveChangesAsync(ct);
        return message;
    }

    public async Task<List<ChatMessage>> GetMessagesAsync(Guid sessionId, ChatFilter filter, CancellationToken ct = default)
    {
        var query = _dbContext.ChatMessages
            .Where(m => m.SessionId == sessionId)
            .AsQueryable();

        if (filter.FromDate.HasValue)
            query = query.Where(m => m.SentAt >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(m => m.SentAt <= filter.ToDate.Value);

        if (filter.Type.HasValue)
            query = query.Where(m => m.Type == filter.Type.Value);

        if (!string.IsNullOrWhiteSpace(filter.SenderId))
            query = query.Where(m => m.SenderId == filter.SenderId);

        query = query.OrderBy(m => m.SentAt);

        var items = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);

        return items;
    }

    public async Task<ChatMessage> UpdateMessageAsync(Guid messageId, UpdateMessageRequest request, string userId, CancellationToken ct = default)
    {
        var message = await _dbContext.ChatMessages.FirstOrDefaultAsync(m => m.Id == messageId && m.SenderId == userId, ct);
        if (message == null)
            throw new InvalidOperationException("Message not found or not authorized");

        if (request.Content != null)
        {
            message.Content = request.Content;
            message.IsEdited = true;
        }

        await _dbContext.SaveChangesAsync(ct);
        return message;
    }

    public async Task<bool> DeleteMessageAsync(Guid messageId, string userId, CancellationToken ct = default)
    {
        var message = await _dbContext.ChatMessages.FirstOrDefaultAsync(m => m.Id == messageId && m.SenderId == userId, ct);
        if (message == null) return false;

        _dbContext.ChatMessages.Remove(message);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ChatMessage> AddReactionAsync(Guid messageId, string emoji, string userId, CancellationToken ct = default)
    {
        var message = await _dbContext.ChatMessages.FirstOrDefaultAsync(m => m.Id == messageId, ct);
        if (message == null)
            throw new InvalidOperationException("Message not found");

        var existing = message.Reactions.FirstOrDefault(r => r.Emoji == emoji && r.UserId == userId);
        if (existing == null)
        {
            message.Reactions.Add(new MessageReaction
            {
                Emoji = emoji,
                UserId = userId,
                ReactedAt = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync(ct);
        }
        return message;
    }

    public async Task<bool> RemoveReactionAsync(Guid messageId, string emoji, string userId, CancellationToken ct = default)
    {
        var message = await _dbContext.ChatMessages.FirstOrDefaultAsync(m => m.Id == messageId, ct);
        if (message == null) return false;

        var reaction = message.Reactions.FirstOrDefault(r => r.Emoji == emoji && r.UserId == userId);
        if (reaction == null) return false;

        message.Reactions.Remove(reaction);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> GetUnreadCountAsync(Guid sessionId, string userId, CancellationToken ct = default)
    {
        // In a real implementation, this would track read receipts
        return 0;
    }

    public async Task<bool> MarkAsReadAsync(Guid sessionId, string userId, CancellationToken ct = default)
    {
        // In a real implementation, this would update read receipts
        return true;
    }

    private async Task<SessionParticipant?> GetParticipantAsync(Guid sessionId, string userId)
    {
        return await _dbContext.SessionParticipants
            .FirstOrDefaultAsync(p => p.SessionId == sessionId && p.UserId == userId);
    }

    private string GetParticipantName(Guid sessionId, string userId)
    {
        var participant = _dbContext.SessionParticipants
            .FirstOrDefault(p => p.SessionId == sessionId && p.UserId == userId);
        return participant?.UserName ?? userId;
    }
}