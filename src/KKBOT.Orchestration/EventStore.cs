// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KKBOT.Domain.Models;

namespace KKBOT.Orchestration;

/// <summary>
/// Simple in‑memory event store for orchestration events.
/// In a real system this would be persisted (e.g., to a DB or event log).
/// </summary>
public sealed class EventStore
{
    private readonly ConcurrentQueue<IOrchestrationEvent> _events = new();

    public void Append(IOrchestrationEvent evt) => _events.Enqueue(evt);

    public IReadOnlyCollection<IOrchestrationEvent> GetAll() => _events.ToArray();
}

/// <summary>
/// Marker interface for orchestration events.
/// </summary>
public interface IOrchestrationEvent { }

public sealed record InstanceCreatedEvent(Guid InstanceId) : IOrchestrationEvent;
public sealed record JobScheduledEvent(Guid InstanceId, Guid JobId, IReadOnlyCollection<Guid> Dependencies) : IOrchestrationEvent;
public sealed record JobStartedEvent(Guid InstanceId, Guid JobId) : IOrchestrationEvent;
public sealed record JobCompletedEvent(Guid InstanceId, Guid JobId, bool Success) : IOrchestrationEvent;
public sealed record StateEnteredEvent(Guid InstanceId, string StateId) : IOrchestrationEvent;
