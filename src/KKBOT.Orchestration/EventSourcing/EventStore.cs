// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Orchestration.EventSourcing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KKBOT.Orchestration.EventSourcing;

public sealed class EventStore : IEventStore
{
    private readonly EventStoreDbContext _context;
    private readonly ILogger<EventStore> _logger;

    public EventStore(EventStoreDbContext context, ILogger<EventStore> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task AppendAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : IEvent
    {
        var entity = new EventEntity
        {
            EventId = @event.EventId,
            AggregateId = @event.AggregateId,
            EventType = @event.EventType,
            Timestamp = @event.Timestamp,
            Version = @event.Version,
            Payload = JsonSerializer.Serialize(@event)
        };

        _context.Events.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        
        _logger.LogDebug("Appended event {EventType} for aggregate {AggregateId} v{Version}", 
            @event.EventType, @event.AggregateId, @event.Version);
    }

    public async Task<IReadOnlyList<TEvent>> GetEventsAsync<TEvent>(Guid aggregateId, CancellationToken cancellationToken = default) where TEvent : IEvent
    {
        var entities = await _context.Events
            .Where(e => e.AggregateId == aggregateId && e.EventType == typeof(TEvent).Name)
            .OrderBy(e => e.Version)
            .ToListAsync(cancellationToken);

        return entities.Select(e => JsonSerializer.Deserialize<TEvent>(e.Payload)!).ToList();
    }

    public async Task<IReadOnlyList<IEvent>> GetAllEventsAsync(Guid aggregateId, CancellationToken cancellationToken = default)
    {
        var entities = await _context.Events
            .Where(e => e.AggregateId == aggregateId)
            .OrderBy(e => e.Version)
            .ToListAsync(cancellationToken);

        var events = new List<IEvent>();
        foreach (var entity in entities)
        {
            var type = Type.GetType($"KKBOT.Orchestration.EventSourcing.{entity.EventType}, KKBOT.Orchestration");
            if (type != null)
            {
                var evt = JsonSerializer.Deserialize(entity.Payload, type) as IEvent;
                if (evt != null) events.Add(evt);
            }
        }
        return events;
    }

    public async Task<long> GetEventCountAsync(Guid aggregateId, CancellationToken cancellationToken = default)
    {
        return await _context.Events
            .Where(e => e.AggregateId == aggregateId)
            .LongCountAsync(cancellationToken);
    }
}

public sealed class EventEntity
{
    public Guid EventId { get; set; }
    public Guid AggregateId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public int Version { get; set; }
    public string Payload { get; set; } = string.Empty;
}

public sealed class EventStoreDbContext : DbContext
{
    public DbSet<EventEntity> Events => Set<EventEntity>();

    public EventStoreDbContext(DbContextOptions<EventStoreDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EventEntity>(e =>
        {
            e.HasKey(x => x.EventId);
            e.HasIndex(x => new { x.AggregateId, x.Version }).IsUnique();
            e.HasIndex(x => x.AggregateId);
            e.HasIndex(x => x.EventType);
            e.Property(x => x.Payload).HasColumnType("TEXT");
        });
    }
}