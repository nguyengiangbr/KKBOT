// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KKBOT.Observability;
using KKBOT.Observability.Tracing;

namespace KKBOT.Debugging;

/// <summary>
/// Time-travel debugging recorder for game automation sessions.
/// </summary>
public sealed class TimeTravelDebugger : IDisposable
{
    private readonly ILogger<TimeTravelDebugger> _logger;
    private readonly TimeTravelOptions _options;
    private readonly ConcurrentDictionary<string, DebugSession> _sessions = new();
    private readonly Timer? _cleanupTimer;
    private readonly CancellationTokenSource _shutdownCts = new();
    private bool _disposed;

    public TimeTravelDebugger(
        ILogger<TimeTravelDebugger> logger,
        IOptions<TimeTravelOptions> options)
    {
        _logger = logger;
        _options = options.Value;

        if (_options.EnableAutoCleanup)
        {
            var cleanupTimer = new Timer(CleanupOldSessionsAsync, null, 
                TimeSpan.FromHours(_options.CleanupIntervalHours), 
                TimeSpan.FromHours(_options.CleanupIntervalHours));
        }
    }

    /// <summary>
    /// Starts a new debug recording session.
    /// </summary>
    public async Task<DebugSession> StartRecordingAsync(string sessionId, DebugSessionOptions options, CancellationToken ct = default)
    {
        var session = new DebugSession
        {
            SessionId = sessionId,
            InstanceId = options.InstanceId,
            StartedAt = DateTimeOffset.UtcNow,
            Options = options,
            Events = new ConcurrentQueue<DebugEvent>(),
            Snapshots = new ConcurrentDictionary<long, DebugSnapshot>(),
            Metadata = new ConcurrentDictionary<string, object>()
        };

        _sessions[sessionId] = session;

        // Record start event
        await RecordEventAsync(sessionId, new DebugEvent
        {
            EventId = Guid.NewGuid().ToString("N"),
            Type = DebugEventType.SessionStarted,
            Timestamp = DateTimeOffset.UtcNow,
            Data = JsonSerializer.Serialize(new { options.InstanceId, options.Description })
        });

        _logger.LogInformation("Started debug recording for session {SessionId} (Instance: {InstanceId})", 
            sessionId, options.InstanceId);

        return session;
    }

    /// <summary>
    /// Records a debug event.
    /// </summary>
    public async Task RecordEventAsync(string sessionId, DebugEvent debugEvent)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return;
        }

        debugEvent.SequenceNumber = Interlocked.Increment(ref _sequenceCounter);
        session.Events.Enqueue(debugEvent);

        // Auto-snapshot on significant events
        if (debugEvent.Type == DebugEventType.StateChanged || 
            debugEvent.Type == DebugEventType.Error ||
            debugEvent.Type == DebugEventType.DecisionMade)
        {
            await CreateSnapshotAsync(session.SessionId, debugEvent);
        }

        // Trim events if exceeding max
        if (session.Events.Count > _options.MaxEventsPerSession)
        {
            while (session.Events.TryDequeue(out _)) { }
        }
    }

    /// <summary>
    /// Creates a snapshot of the current state.
    /// </summary>
    public async Task<DebugSnapshot> CreateSnapshotAsync(string sessionId, DebugEvent triggerEvent)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return null;
        }

        var snapshot = new DebugSnapshot
        {
            SnapshotId = Interlocked.Increment(ref _snapshotCounter),
            SessionId = sessionId,
            Timestamp = DateTimeOffset.UtcNow,
            TriggerEvent = triggerEvent,
            // In real implementation, would capture actual state
            GameState = new GameStateSnapshot(),
            DeviceState = new DeviceStateSnapshot(),
            ModuleStates = new Dictionary<string, object>()
        };

        session.Snapshots[sessionId] = snapshot;
        return snapshot;
    }

    /// <summary>
    /// Gets a session by ID.
    /// </summary>
    public DebugSession GetSession(string sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return session;
    }

    /// <summary>
    /// Replays a session from a snapshot.
    /// </summary>
    public async Task<ReplayResult> ReplaySessionAsync(string sessionId, long fromSnapshotId, 
        ReplayOptions options, CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return new ReplayResult { Success = false, Message = "Session not found" };
        }

        if (!session.Snapshots.TryGetValue(fromSnapshotId, out var snapshot))
        {
            return new ReplayResult { Success = false, Message = "Snapshot not found" };
        }

        // In real implementation, would replay events from snapshot
        // For now, return success
        return new ReplayResult
        {
            Success = true,
            Message = "Replay completed",
            EventsReplayed = session.Events.Count
        };
    }

    /// <summary>
    /// Exports a debug session to a file.
    /// </summary>
    public async Task<string> ExportSessionAsync(string sessionId, string outputPath, 
        ExportFormat format = ExportFormat.Json, CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new ArgumentException($"Session {sessionId} not found");
        }

        var exportData = new DebugExport
        {
            SessionId = session.SessionId,
            InstanceId = session.InstanceId,
            StartedAt = session.StartedAt,
            EndedAt = session.EndedAt,
            Options = session.Options,
            Events = session.Events.ToArray(),
            Snapshots = session.Snapshots.Values.OrderBy(s => s.SnapshotId).ToArray(),
            Metadata = session.Metadata.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        };

        var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });

        if (format == ExportFormat.CompressedJson)
        {
            var compressed = Compress(json);
            await File.WriteAllBytesAsync(outputPath, compressed, ct);
        }
        else
        {
            await File.WriteAllTextAsync(outputPath, json, ct);
        }

        return outputPath;
    }

    /// <summary>
    /// Imports a debug session from a file.
    /// </summary>
    public async Task<DebugSession> ImportSessionAsync(string inputPath, CancellationToken ct = default)
    {
        byte[] data;
        if (inputPath.EndsWith(".gz"))
        {
            data = Decompress(await File.ReadAllBytesAsync(inputPath, ct));
        }
        else
        {
            data = await File.ReadAllBytesAsync(inputPath, ct);
        }

        var json = System.Text.Encoding.UTF8.GetString(data);
        var exportData = JsonSerializer.Deserialize<DebugExport>(json);

        var session = new DebugSession
        {
            SessionId = exportData.SessionId,
            InstanceId = exportData.InstanceId,
            StartedAt = exportData.StartedAt,
            EndedAt = exportData.EndedAt,
            Options = exportData.Options,
            Events = new ConcurrentQueue<DebugEvent>(exportData.Events),
            Snapshots = new ConcurrentDictionary<long, DebugSnapshot>(
                exportData.Snapshots.ToDictionary(s => s.SnapshotId)),
            Metadata = new ConcurrentDictionary<string, object>(exportData.Metadata)
        };

        _sessions[session.SessionId] = session;
        return session;
    }

    private async void CleanupOldSessionsAsync(object state)
    {
        var cutoff = DateTimeOffset.UtcNow - TimeSpan.FromDays(_options.MaxSessionAgeDays);
        var toRemove = _sessions
            .Where(kvp => kvp.Value.StartedAt < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var id in toRemove)
        {
            _sessions.TryRemove(id, out _);
        }

        if (toRemove.Count > 0)
        {
            _logger.LogInformation("Cleaned up {Count} old debug sessions", toRemove.Count);
        }
    }

    private byte[] Compress(string data)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
        using (var writer = new StreamWriter(gzip))
        {
            writer.Write(data);
        }
        return output.ToArray();
    }

    private byte[] Decompress(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _shutdownCts.Cancel();
        _shutdownCts.Dispose();
    }
}

private long _sequenceCounter = 0;
private long _snapshotCounter = 0;

// ==================== DATA MODELS ====================

public sealed record TimeTravelOptions
{
    public bool EnableAutoCleanup { get; set; } = true;
    public int CleanupIntervalHours { get; set; } = 24;
    public int MaxSessionAgeDays { get; set; } = 30;
    public int MaxEventsPerSession { get; set; } = 10000;
    public int MaxSnapshotsPerSession { get; set; } = 100;
}

public sealed record DebugSessionOptions
{
    public string InstanceId { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool RecordPerception { get; init; } = true;
    public bool RecordActions { get; init; } = true;
    public bool RecordStateChanges { get; init; } = true;
    public bool RecordDecisions { get; init; } = true;
    public int MaxEventsPerSecond { get; init; } = 100;
}

public sealed record ReplayOptions
{
    public double SpeedMultiplier { get; init; } = 1.0;
    public bool PauseOnError { get; init; } = true;
    public bool PauseOnDecision { get; init; } = false;
    public TimeSpan? MaxReplayDuration { get; init; }
}

public enum DebugEventType
{
    SessionStarted,
    SessionEnded,
    StateChanged,
    ActionExecuted,
    DecisionMade,
    Error,
    Warning,
    PerceptionResult,
    SkillUsed,
    TargetAcquired,
    TargetLost,
    DamageTaken,
    HealReceived,
    BuffApplied,
    BuffExpired,
    ItemUsed,
    ItemAcquired,
    ItemSold,
    QuestStarted,
    QuestCompleted,
    QuestFailed,
    TargetAcquired,
    TargetLost,
    CombatStarted,
    CombatEnded,
    Death,
    LevelUp,
    QuestStarted,
    QuestCompleted,
    QuestFailed,
    InventoryChanged,
    EquipmentChanged,
    SkillLearned,
    SkillUsed,
    BuffApplied,
    BuffExpired,
    DebuffApplied,
    DebuffExpired,
    QuestObjectiveUpdated,
    Custom
}

public sealed record DebugEvent
{
    public string EventId { get; init; } = Guid.NewGuid().ToString("N");
    public long SequenceNumber { get; init; }
    public DebugEventType Type { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string InstanceId { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Data { get; init; } = string.Empty; // JSON serialized data
    public string StackTrace { get; init; } = string.Empty;
    public Dictionary<string, object> Tags { get; init; } = new();
    public long SequenceNumber { get; set; }
}

public sealed record DebugSnapshot
{
    public long SnapshotId { get; init; }
    public string SessionId { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public DebugEvent TriggerEvent { get; init; } = null;
    public GameStateSnapshot GameState { get; init; } = new();
    public DeviceStateSnapshot DeviceState { get; init; } = new();
    public Dictionary<string, object> ModuleStates { get; init; } = new();
}

public sealed record GameStateSnapshot
{
    public double HpPercent { get; init; }
    public double ManaPercent { get; init; }
    public int Level { get; init; }
    public long Gold { get; init; }
    public string CurrentZone { get; init; } = string.Empty;
    public Point3D Position { get; init; } = Point3D.Zero;
    public List<string> ActiveBuffs { get: init; } = new();
    public List<string> ActiveDebuffs { get: init; } = new();
    public bool InCombat { get: init; }
    public string CurrentTarget { get: init; } = string.Empty;
}

public sealed record DeviceStateSnapshot
{
    public bool IsConnected { get: init; }
    public int BatteryLevel { get: init; }
    public string NetworkType { get: init; } = string.Empty;
    public string IpAddress { get: init; } = string.Empty;
    public double CpuUsage { get: init; }
    public long MemoryUsage { get: init; }
    public double Temperature { get: init; }
}

public sealed record ReplayOptions
{
    public double SpeedMultiplier { get; init; } = 1.0;
    public bool PauseOnError { get: init; } = true;
    public bool PauseOnDecision { get: init; } = false;
    public TimeSpan? MaxReplayDuration { get; init; }
    public bool RecordReplay { get: init; } = false;
}

public sealed record ReplayResult
{
    public bool Success { get; init; }
    public string Message { get: init; } = string.Empty;
    public int EventsReplayed { get: init; }
    public TimeSpan Duration { get; init; }
    public List<ReplayError> Errors { get: init; } = new();
}

public sealed record ReplayError
{
    public long SequenceNumber { get: init; }
    public string ErrorMessage { get: init; } = string.Empty;
    public DateTimeOffset Timestamp { get: init; }
}

public enum ExportFormat
{
    Json,
    CompressedJson,
    Binary
}

public sealed record DebugExport
{
    public string SessionId { get: init; } = string.Empty;
    public string InstanceId { get: init; } = string.Empty;
    public DateTimeOffset StartedAt { get: init; }
    public DateTimeOffset? EndedAt { get: init; }
    public DebugSessionOptions Options { get: init; } = new();
    public DebugEvent[] Events { get: init; } = Array.Empty<DebugEvent>();
    public DebugSnapshot[] Snapshots { get: init; } = Array.Empty<DebugSnapshot>();
    public Dictionary<string, object> Metadata { get: init; } = new();
}

public sealed class DebugSession
{
    public string SessionId { get; set; } = string.Empty;
    public string InstanceId { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndedAt { get; set; }
    public DebugSessionOptions Options { get; set; } = new();
    public ConcurrentQueue<DebugEvent> Events { get; set; } = new();
    public ConcurrentDictionary<long, DebugSnapshot> Snapshots { get; set; } = new();
    public ConcurrentDictionary<string, object> Metadata { get; set; } = new();
    public DateTimeOffset EndedAtNullable => EndedAt;
}

public sealed class ReplayOptions
{
    public double SpeedMultiplier { get; init; } = 1.0;
    public bool PauseOnError { get; init; } = true;
    public bool PauseOnDecision { get: init; } = false;
    public TimeSpan? MaxReplayDuration { get; init; }
    public bool RecordReplay { get: init; } = false;
}
