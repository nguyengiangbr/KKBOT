// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using KKBOT.Observability;

namespace KKBOT.Orchestration;

/// <summary>
/// Event upcaster for migrating events between versions.
/// Uses JSON Node API for flexible schema migration.
/// </summary>
public sealed class EventUpcaster : IEventUpcaster
{
    private readonly ILogger<EventUpcaster> _logger;
    private readonly Dictionary<string, List<UpcastStep>> _upcastChains = new();
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public EventUpcaster(ILogger<EventUpcaster> logger)
    {
        _logger = logger;
        RegisterDefaultUpcasters();
    }

    public bool CanUpcast(string eventType, int fromVersion, int toVersion)
    {
        if (fromVersion >= toVersion) return false;
        
        var key = $"{eventType}:{fromVersion}->{toVersion}";
        return _upcastChains.ContainsKey(key) || _upcastChains.ContainsKey(eventType);
    }

    public async Task<string> UpcastAsync(string eventJson, int fromVersion, int toVersion, CancellationToken ct)
    {
        if (fromVersion >= toVersion) return eventJson;

        var currentJson = eventJson;
        var currentVersion = fromVersion;

        while (currentVersion < toVersion)
        {
            var nextVersion = currentVersion + 1;
            var key = $"{GetEventType(currentJson)}:{currentVersion}->{nextVersion}";
            
            if (!_upcastChains.TryGetValue(key, out var steps) && !_upcastChains.TryGetValue(GetEventType(currentJson), out steps))
            {
                _logger.LogWarning("No upcaster found for {EventType} v{From} -> v{To}", GetEventType(currentJson), currentVersion, nextVersion);
                break;
            }

            foreach (var step in steps)
            {
                currentJson = await step.UpcastAsync(currentJson, ct);
                currentVersion = nextVersion;
            }
        }

        return currentJson;
    }

    public void RegisterUpcaster(string eventType, int fromVersion, int toVersion, UpcastStep step)
    {
        var key = $"{eventType}:{fromVersion}->{toVersion}";
        if (!_upcastChains.ContainsKey(key))
            _upcastChains[key] = new List<UpcastStep>();
        
        _upcastChains[key].Add(step);
        _logger.LogDebug("Registered upcaster: {Key}", key);
    }

    private void RegisterDefaultUpcasters()
    {
        // InstanceCreatedEvent v1 -> v2: Added ModuleVersion field
        RegisterUpcaster("InstanceCreatedEvent", 1, 2, new JsonUpcastStep((json, ct) =>
        {
            var node = JsonNode.Parse(json);
            if (node?["ModuleVersion"] == null)
            {
                node!["ModuleVersion"] = "1.0.0";
            }
            return Task.FromResult(node!.ToJsonString());
        }));

        // JobCompletedEvent v1 -> v2: Added DurationMs field
        RegisterUpcaster("JobCompletedEvent", 1, 2, new JsonUpcastStep((json, ct) =>
        {
            var node = JsonNode.Parse(json);
            if (node?["DurationMs"] == null)
            {
                node!["DurationMs"] = 0;
            }
            return Task.FromResult(node!.ToJsonString());
        }));

        // StateEnteredEvent v1 -> v2: Added PerceptionData field
        RegisterUpcaster("StateEnteredEvent", 1, 2, new JsonUpcastStep((json, ct) =>
        {
            var node = JsonNode.Parse(json);
            if (node?["PerceptionData"] == null)
            {
                node!["PerceptionData"] = new JsonObject();
            }
            return Task.FromResult(node!.ToJsonString());
        }));

        _logger.LogInformation("Registered {Count} default event upcasters", 3);
    }

    private string GetEventType(string json)
    {
        try
        {
            var node = JsonNode.Parse(json);
            return node?["$type"]?.ToString() ?? node?["EventType"]?.ToString() ?? "Unknown";
        }
        catch { return "Unknown"; }
    }
}

/// <summary>
/// Single upcast step.
/// </summary>
public sealed record UpcastStep
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public Func<string, CancellationToken, Task<string>> UpcastAsync { get; init; } = null!;
}

/// <summary>
/// JSON-based upcast step using JsonNode for manipulation.
/// </summary>
public sealed class JsonUpcastStep : UpcastStep
{
    private readonly Func<JsonNode, CancellationToken, Task<JsonNode>> _transform;

    public JsonUpcastStep(Func<JsonNode, CancellationToken, Task<JsonNode>> transform)
    {
        _transform = transform;
        UpcastAsync = async (json, ct) =>
        {
            var node = JsonNode.Parse(json);
            var transformed = await _transform(node, ct);
            return transformed.ToJsonString();
        };
    }
}

/// <summary>
/// Pipeline for upcasting events through multiple versions.
/// </summary>
public sealed class UpcastPipeline
{
    private readonly IEventUpcaster _upcaster;
    private readonly ILogger<UpcastPipeline> _logger;

    public UpcastPipeline(IEventUpcaster upcaster, ILogger<UpcastPipeline> logger)
    {
        _upcaster = upcaster;
        _logger = logger;
    }

    public async Task<string> UpcastToLatestAsync(string eventJson, CancellationToken ct)
    {
        var node = JsonNode.Parse(eventJson);
        var eventType = node?["$type"]?.ToString() ?? node?["EventType"]?.ToString() ?? "Unknown";
        var currentVersion = node?["Version"]?.GetValue<int>() ?? 1;
        var latestVersion = GetLatestVersion(eventType);

        if (currentVersion >= latestVersion) return eventJson;

        _logger.LogDebug("Upcasting {EventType} from v{Current} to v{Latest}", eventType, currentVersion, latestVersion);

        return await _upcaster.UpcastAsync(eventJson, currentVersion, latestVersion, ct);
    }

    private int GetLatestVersion(string eventType)
    {
        // In real implementation, read from version registry
        return eventType switch
        {
            "InstanceCreatedEvent" => 2,
            "JobCompletedEvent" => 2,
            "StateEnteredEvent" => 2,
            _ => 1
        };
    }
}

/// <summary>
/// Event version registry.
/// </summary>
public sealed class EventVersionRegistry
{
    private readonly Dictionary<string, EventVersionInfo> _versions = new();

    public void Register(string eventType, int latestVersion, string schemaJson)
    {
        _versions[eventType] = new EventVersionInfo
        {
            EventType = eventType,
            LatestVersion = latestVersion,
            SchemaJson = schemaJson,
            RegisteredAt = DateTimeOffset.UtcNow
        };
    }

    public EventVersionInfo? GetVersion(string eventType)
    {
        _versions.TryGetValue(eventType, out var info);
        return info;
    }

    public IEnumerable<EventVersionInfo> GetAllVersions()
    {
        return _versions.Values;
    }
}

public sealed record EventVersionInfo
{
    public string EventType { get; init; } = string.Empty;
    public int LatestVersion { get; init; }
    public string SchemaJson { get; init; } = string.Empty;
    public DateTimeOffset RegisteredAt { get; init; }
}