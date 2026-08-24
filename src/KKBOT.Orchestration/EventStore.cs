// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KKBOT.Domain.Models;
using KKBOT.Observability;
using KKBOT.Observability.Tracing;

namespace KKBOT.Orchestration;

/// <summary>
/// Advanced event store with snapshotting, projection versioning, and event sourcing capabilities.
/// </summary>
public sealed class EventStore : IEventStore, IAsyncDisposable
{
    private readonly ILogger<EventStore> _logger;
    private readonly EventStoreOptions _options;
    private readonly IMetricsRegistry _metrics;
    private readonly ConcurrentQueue<StoredEvent> _eventQueue = new();
    private readonly ConcurrentDictionary<string, AggregateSnapshot> _snapshots = new();
    private readonly ConcurrentDictionary<string, ProjectionState> _projections = new();
    private readonly SemaphoreSlim _snapshotLock = new(1, 1);
    private readonly Timer? _snapshotTimer;
    private readonly CancellationTokenSource _shutdownCts = new();
    private long _eventSequence = 0;
    private bool _disposed;

    public EventStore(
        ILogger<EventStore> logger,
        IOptions<EventStoreOptions> options,
        IMetricsRegistry metrics)
    {
        _logger = logger;
        _options = options.Value;
        _metrics = metrics;

        if (_options.EnableSnapshots && _options.SnapshotInterval > 0)
        {
            _snapshotTimer = new Timer(async _ => await CreateSnapshotsAsync(_shutdownCts.Token), 
                null, _options.SnapshotInterval, _options.SnapshotInterval);
        }

        _logger.LogInformation("EventStore initialized with snapshots: {Enabled}, interval: {Interval}s", 
            _options.EnableSnapshots, _options.SnapshotInterval?.TotalSeconds ?? 0);
    }

    /// <inheritdoc />
    public async Task AppendAsync(IOrchestrationEvent @event, CancellationToken cancellationToken = default)
    {
        using var activity = KkbotActivitySources.Orchestration.StartActivity("eventstore.append", ActivityKind.Internal);
        
        var sequence = Interlocked.Increment(ref _eventSequence);
        var storedEvent = new StoredEvent
        {
            Sequence = sequence,
            EventId = Guid.NewGuid(),
            EventType = @event.GetType().Name,
            EventData = JsonSerializer.Serialize(@event, EventJsonContext.Default),
            Timestamp = DateTimeOffset.UtcNow,
            CorrelationId = GetCorrelationId(@event),
            CausationId = GetCausationId(@event)
        };

        _eventQueue.Enqueue(storedEvent);
        
        _metrics.IncrementHttpRequests(); // Reuse metric for event count

        // Update projections
        await UpdateProjectionsAsync(storedEvent, cancellationToken);

        // Check if snapshot needed
        if (_options.EnableSnapshots && sequence % _options.SnapshotEveryNEvents == 0)
        {
            _ = Task.Run(() => CreateSnapshotsAsync(cancellationToken), cancellationToken);
        }

        activity?.SetStatus(ActivityStatusCode.Ok);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StoredEvent>> GetEventsAsync(
        string aggregateId, 
        long fromSequence = 0, 
        CancellationToken cancellationToken = default)
    {
        // In real implementation, query from persistent storage
        // For now, filter from in-memory queue
        var events = _eventQueue
            .Where(e => e.CorrelationId == aggregateId && e.Sequence > fromSequence)
            .OrderBy(e => e.Sequence)
            .ToList();

        return await Task.FromResult(events.AsReadOnly());
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StoredEvent>> GetEventsByCorrelationIdAsync(
        string correlationId, 
        CancellationToken cancellationToken = default)
    {
        var events = _eventQueue
            .Where(e => e.CorrelationId == correlationId)
            .OrderBy(e => e.Sequence)
            .ToList();

        return await Task.FromResult(events.AsReadOnly());
    }

    /// <inheritdoc />
    public async Task<ProjectionState?> GetProjectionAsync(string projectionName, CancellationToken cancellationToken = default)
    {
        _projections.TryGetValue(projectionName, out var state);
        return await Task.FromResult(state);
    }

    /// <inheritdoc />
    public async Task<ProjectionState?> GetProjectionAtAsync(
        string projectionName, 
        long sequence, 
        CancellationToken cancellationToken = default)
    {
        // In real implementation, reconstruct projection state at specific sequence
        // using snapshots + replay
        var current = await GetProjectionAsync(projectionName, cancellationToken);
        return current;
    }

    /// <inheritdoc />
    public async Task<AggregateSnapshot?> GetSnapshotAsync(string aggregateId, CancellationToken cancellationToken = default)
    {
        _snapshots.TryGetValue(aggregateId, out var snapshot);
        return await Task.FromResult(snapshot);
    }

    /// <inheritdoc />
    public async Task CreateSnapshotAsync(string aggregateId, CancellationToken cancellationToken = default)
    {
        await _snapshotLock.WaitAsync(cancellationToken);
        try
        {
            var events = await GetEventsAsync(aggregateId, cancellationToken);
            if (events.Count == 0) return;

            var lastEvent = events.Last();
            var snapshot = new AggregateSnapshot
            {
                AggregateId = aggregateId,
                Sequence = lastEvent.Sequence,
                StateJson = JsonSerializer.Serialize(events, EventJsonContext.Default),
                Timestamp = DateTimeOffset.UtcNow,
                EventCount = events.Count
            };

            _snapshots[aggregateId] = snapshot;
            
            _logger.LogDebug("Created snapshot for aggregate {AggregateId} at sequence {Sequence}", 
                aggregateId, snapshot.Sequence);
        }
        finally
        {
            _snapshotLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task RegisterProjectionAsync(
        string name, 
        IEventProjection projection, 
        CancellationToken cancellationToken = default)
    {
        var state = new ProjectionState
        {
            Name = name,
            Version = projection.Version,
            LastSequence = 0,
            StateJson = "{}",
            LastUpdated = DateTimeOffset.UtcNow
        };

        _projections[name] = state;
        _logger.LogInformation("Registered projection: {Name} v{Version}", name, projection.Version);
    }

    /// <inheritdoc />
    public async Task<EventStoreStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(new EventStoreStats
        {
            TotalEvents = _eventQueue.Count,
            TotalSnapshots = _snapshots.Count,
            TotalProjections = _projections.Count,
            CurrentSequence = _eventSequence,
            OldestEvent = _eventQueue.TryPeek(out var first) ? first.Timestamp : null,
            NewestEvent = _eventQueue.LastOrDefault()?.Timestamp
        });
    }

    private async Task UpdateProjectionsAsync(StoredEvent storedEvent, CancellationToken ct)
    {
        // In real implementation, dispatch to registered projections
        // For now, update a default "orchestration" projection
        if (_projections.TryGetValue("orchestration", out var state))
        {
            state.LastSequence = storedEvent.Sequence;
            state.LastUpdated = DateTimeOffset.UtcNow;
            
            // Simple state accumulation (in real impl, use projection logic)
            var currentState = JsonSerializer.Deserialize<Dictionary<string, object>>(state.StateJson);
            currentState ??= new();
            currentState[$"last_{storedEvent.EventType.ToLowerInvariant()}"] = storedEvent.EventData;
            state.StateJson = JsonSerializer.Serialize(currentState);
        }
    }

    private async Task CreateSnapshotsAsync(CancellationToken ct)
    {
        if (!_snapshotLock.Wait(0)) return; // Skip if already running

        try
        {
            _logger.LogDebug("Creating periodic snapshots");
            
            // Group events by correlation ID (aggregate)
            var aggregates = _eventQueue
                .GroupBy(e => e.CorrelationId)
                .Where(g => !string.IsNullOrEmpty(g.Key))
                .ToList();

            foreach (var aggregate in aggregates)
            {
                await CreateSnapshotAsync(aggregate.Key, ct);
            }

            _metrics.IncrementHttpRequests(); // Reuse metric
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating snapshots");
        }
        finally
        {
            _snapshotLock.Release();
        }
    }

    private string GetCorrelationId(IOrchestrationEvent @event)
    {
        // Extract correlation ID from event properties
        var props = @event.GetType().GetProperties();
        var corrProp = props.FirstOrDefault(p => p.Name.EndsWith("CorrelationId", StringComparison.OrdinalIgnoreCase));
        return corrProp?.GetValue(@event)?.ToString() ?? string.Empty;
    }

    private string GetCausationId(IOrchestrationEvent @event)
    {
        var props = @event.GetType().GetProperties();
        var causProp = props.FirstOrDefault(p => p.Name.EndsWith("CausationId", StringComparison.OrdinalIgnoreCase));
        return causProp?.GetValue(@event)?.ToString() ?? string.Empty;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _shutdownCts.Cancel();
        _snapshotTimer?.Dispose();
        
        // Final snapshot
        await CreateSnapshotsAsync(CancellationToken.None);
        
        _logger.LogInformation("EventStore disposed. Events: {Count}, Snapshots: {Snapshots}", 
            _eventQueue.Count, _snapshots.Count);
    }
}

/// <summary>
/// Configuration options for EventStore.
/// </summary>
public sealed class EventStoreOptions
{
    public bool EnableSnapshots { get; set; } = true;
    public TimeSpan? SnapshotInterval { get; set; } = TimeSpan.FromMinutes(5);
    public int SnapshotEveryNEvents { get; set; } = 1000;
    public int MaxEventsInMemory { get; set; } = 100000;
    public string? PersistencePath { get; set; }
}

/// <summary>
/// Stored event with metadata.
/// </summary>
public sealed record StoredEvent
{
    public long Sequence { get; init; }
    public Guid EventId { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string EventData { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public string CausationId { get; init; } = string.Empty;
    public int Version { get; init; } = 1; // For event versioning
}

/// <summary>
/// Aggregate snapshot for fast state reconstruction.
/// </summary>
public sealed record AggregateSnapshot
{
    public string AggregateId { get; init; } = string.Empty;
    public long Sequence { get; init; }
    public string StateJson { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; }
    public int EventCount { get; init; }
    public string? SchemaVersion { get; init; }
}

/// <summary>
/// Projection state for read models.
/// </summary>
public sealed record ProjectionState
{
    public string Name { get; init; } = string.Empty;
    public int Version { get; init; } = 1;
    public long LastSequence { get; init; }
    public string StateJson { get; init; } = "{}";
    public DateTimeOffset LastUpdated { get; init; }
    public bool IsHealthy { get; init; } = true;
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Event store statistics.
/// </summary>
public sealed record EventStoreStats
{
    public long TotalEvents { get; init; }
    public int TotalSnapshots { get; init; }
    public int TotalProjections { get; init; }
    public long CurrentSequence { get; init; }
    public DateTimeOffset? OldestEvent { get; init; }
    public DateTimeOffset? NewestEvent { get; init; }
}

/// <summary>
/// Interface for event projections.
/// </summary>
public interface IEventProjection
{
    string Name { get; }
    int Version { get; }
    Task ProjectAsync(StoredEvent @event, CancellationToken ct);
    Task<object?> GetStateAsync(CancellationToken ct);
}

/// <summary>
/// Event upcaster for version migration.
/// </summary>
public interface IEventUpcaster
{
    bool CanUpcast(string eventType, int fromVersion, int toVersion);
    Task<string> UpcastAsync(string eventJson, int fromVersion, int toVersion, CancellationToken ct);
}

/// <summary>
/// JSON serialization context for event store.
/// </summary>
[JsonSerializable(typeof(InstanceCreatedEvent))]
[JsonSerializable(typeof(JobScheduledEvent))]
[JsonSerializable(typeof(JobStartedEvent))]
[JsonSerializable(typeof(JobCompletedEvent))]
[JsonSerializable(typeof(StateEnteredEvent))]
[JsonSerializable(typeof(StoredEvent))]
[JsonSerializable(typeof(AggregateSnapshot))]
[JsonSerializable(typeof(ProjectionState))]
internal partial class EventJsonContext : JsonSerializerContext { }

/// <summary>
/// Event store interface.
/// </summary>
public interface IEventStore
{
    Task AppendAsync(IOrchestrationEvent @event, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StoredEvent>> GetEventsAsync(string aggregateId, long fromSequence = 0, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StoredEvent>> GetEventsByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default);
    Task<ProjectionState?> GetProjectionAsync(string projectionName, CancellationToken cancellationToken = default);
    Task<ProjectionState?> GetProjectionAtAsync(string projectionName, long sequence, CancellationToken cancellationToken = default);
    Task<AggregateSnapshot?> GetSnapshotAsync(string aggregateId, CancellationToken cancellationToken = default);
    Task CreateSnapshotAsync(string aggregateId, CancellationToken cancellationToken = default);
    Task RegisterProjectionAsync(string name, IEventProjection projection, CancellationToken cancellationToken = default);
    Task<EventStoreStats> GetStatsAsync(CancellationToken cancellationToken = default);
}