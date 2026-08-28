// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using KKBOT.Orchestration.EventSourcing;

namespace KKBOT.Orchestration;

/// <summary>
/// Marker interface for events that can be persisted in the orchestration event store.
/// </summary>
public interface IOrchestrationEvent
{
}

/// <summary>
/// Raised when a new instance is created.
/// </summary>
public sealed record InstanceCreatedEvent : EventBase, IOrchestrationEvent
{
    public Guid InstanceId { get; init; }

    public InstanceCreatedEvent(Guid instanceId) : this()
    {
        InstanceId = instanceId;
        AggregateId = instanceId;
    }

    public InstanceCreatedEvent() { }
}

/// <summary>
/// Raised when an instance enters a state.
/// </summary>
public sealed record StateEnteredEvent : EventBase, IOrchestrationEvent
{
    public Guid InstanceId { get; init; }
    public string StateId { get; init; } = string.Empty;

    public StateEnteredEvent(Guid instanceId, string stateId) : this()
    {
        InstanceId = instanceId;
        StateId = stateId;
        AggregateId = instanceId;
    }

    public StateEnteredEvent() { }
}

/// <summary>
/// Raised when a job is scheduled for an instance.
/// </summary>
public sealed record JobScheduledEvent : EventBase, IOrchestrationEvent
{
    public Guid InstanceId { get; init; }
    public Guid JobId { get; init; }
    public List<Guid> Dependencies { get; init; } = new();

    public JobScheduledEvent(Guid instanceId, Guid jobId) : this()
    {
        InstanceId = instanceId;
        JobId = jobId;
        AggregateId = instanceId;
    }

    public JobScheduledEvent() { }
}

/// <summary>
/// Raised when a job starts executing for an instance.
/// </summary>
public sealed record JobStartedEvent : EventBase, IOrchestrationEvent
{
    public Guid InstanceId { get; init; }
    public Guid JobId { get; init; }

    public JobStartedEvent(Guid instanceId, Guid jobId) : this()
    {
        InstanceId = instanceId;
        JobId = jobId;
        AggregateId = instanceId;
    }

    public JobStartedEvent() { }
}

/// <summary>
/// Raised when a job completes for an instance.
/// </summary>
public sealed record JobCompletedEvent : EventBase, IOrchestrationEvent
{
    public Guid InstanceId { get; init; }
    public Guid JobId { get; init; }
    public bool Success { get; init; }

    public JobCompletedEvent(Guid instanceId, Guid jobId, bool success) : this()
    {
        InstanceId = instanceId;
        JobId = jobId;
        Success = success;
        AggregateId = instanceId;
    }

    public JobCompletedEvent() { }
}
