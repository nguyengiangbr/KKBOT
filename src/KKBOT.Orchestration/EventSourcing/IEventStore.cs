// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KKBOT.Orchestration.EventSourcing;

public interface IEventStore
{
    Task AppendAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : IEvent;
    Task<IReadOnlyList<TEvent>> GetEventsAsync<TEvent>(Guid aggregateId, CancellationToken cancellationToken = default) where TEvent : IEvent;
    Task<IReadOnlyList<IEvent>> GetAllEventsAsync(Guid aggregateId, CancellationToken cancellationToken = default);
    Task<long> GetEventCountAsync(Guid aggregateId, CancellationToken cancellationToken = default);
}

public interface IEvent
{
    Guid EventId { get; }
    Guid AggregateId { get; }
    DateTime Timestamp { get; }
    int Version { get; }
    string EventType { get; }
}

public abstract record EventBase : IEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public Guid AggregateId { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public int Version { get; init; }
    public string EventType => GetType().Name;
}