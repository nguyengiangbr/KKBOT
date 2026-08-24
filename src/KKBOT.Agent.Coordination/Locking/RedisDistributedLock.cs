// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using KKBOT.Agent.Coordination.Abstractions;
using KKBOT.Observability;

namespace KKBOT.Agent.Coordination;

/// <summary>
/// Redis-based distributed lock implementation using Redlock algorithm.
/// </summary>
public sealed class RedisDistributedLock : IDistributedLock, IAsyncDisposable
{
    private readonly ILogger<RedisDistributedLock> _logger;
    private readonly RedisDistributedLockOptions _options;
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private readonly IMetricsRegistry _metrics;
    private bool _disposed;

    private const string LockKeyPrefix = "kkbot:locks:";

    public RedisDistributedLock(
        ILogger<RedisDistributedLock> logger,
        IOptions<RedisDistributedLockOptions> options,
        IConnectionMultiplexer redis,
        IMetricsRegistry metrics)
    {
        _logger = logger;
        _options = options.Value;
        _redis = redis;
        _db = redis.GetDatabase();
        _metrics = metrics;
    }

    public async Task<bool> AcquireAsync(string resourceId, string ownerId, TimeSpan ttl, CancellationToken ct = default)
    {
        using var activity = KkbotActivitySources.Coordination.StartActivity("lock.acquire");
        activity?.SetTag("resource.id", resourceId);
        activity?.SetTag("owner.id", ownerId);

        var key = $"{LockKeyPrefix}{resourceId}";
        var lockValue = JsonSerializer.Serialize(new LockValue
        {
            OwnerId = ownerId,
            AcquiredAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.Add(ttl)
        });

        // Use SET with NX (only if not exists) and EX (expiry)
        var acquired = await _db.StringSetAsync(
            key, 
            lockValue, 
            ttl, 
            When.NotExists);

        if (acquired)
        {
            _logger.LogDebug("Acquired lock {ResourceId} for owner {OwnerId}", resourceId, ownerId);
            _metrics.IncrementHttpRequests(); // Reuse metric
        }
        else
        {
            _logger.LogDebug("Failed to acquire lock {ResourceId} (already held)", resourceId);
        }

        return acquired;
    }

    public async Task<bool> ReleaseAsync(string resourceId, string ownerId, CancellationToken ct = default)
    {
        using var activity = KkbotActivitySources.Coordination.StartActivity("lock.release");
        activity?.SetTag("resource.id", resourceId);
        activity?.SetTag("owner.id", ownerId);

        var key = $"{LockKeyPrefix}{resourceId}";

        // Use Lua script for atomic check-and-delete
        var script = @"
            if redis.call('get', KEYS[1]) == ARGV[1] then
                return redis.call('del', KEYS[1])
            else
                return 0
            end";

        var lockValue = JsonSerializer.Serialize(new LockValue { OwnerId = ownerId });
        var result = await _db.ScriptEvaluateAsync(script, new RedisKey[] { key }, new RedisValue[] { lockValue });

        var released = (int)result == 1;

        if (released)
        {
            _logger.LogDebug("Released lock {ResourceId} for owner {OwnerId}", resourceId, ownerId);
        }
        else
        {
            _logger.LogDebug("Failed to release lock {ResourceId} (not owner or expired)", resourceId);
        }

        return released;
    }

    public async Task<bool> ExtendAsync(string resourceId, string ownerId, TimeSpan ttl, CancellationToken ct = default)
    {
        using var activity = KkbotActivitySources.Coordination.StartActivity("lock.extend");
        activity?.SetTag("resource.id", resourceId);
        activity?.SetTag("owner.id", ownerId);

        var key = $"{LockKeyPrefix}{resourceId}";
        var lockValue = JsonSerializer.Serialize(new LockValue { OwnerId = ownerId });

        // Lua script for atomic check-and-extend
        var script = @"
            if redis.call('get', KEYS[1]) == ARGV[1] then
                return redis.call('expire', KEYS[1], ARGV[2])
            else
                return 0
            end";

        var result = await _db.ScriptEvaluateAsync(
            script, 
            new RedisKey[] { key }, 
            new RedisValue[] { lockValue, (int)ttl.TotalSeconds });

        var extended = (int)result == 1;

        if (extended)
        {
            _logger.LogDebug("Extended lock {ResourceId} for owner {OwnerId} by {Ttl}", resourceId, ownerId, ttl);
        }

        return extended;
    }

    public async Task<LockInfo?> GetLockInfoAsync(string resourceId, CancellationToken ct = default)
    {
        var key = $"{LockKeyPrefix}{resourceId}";
        var value = await _db.StringGetAsync(key);

        if (value.IsNullOrEmpty)
            return null;

        var lockValue = JsonSerializer.Deserialize<LockValue>(value!);
        if (lockValue == null)
            return null;

        return new LockInfo
        {
            ResourceId = resourceId,
            OwnerId = lockValue.OwnerId,
            AcquiredAt = lockValue.AcquiredAt,
            ExpiresAt = lockValue.ExpiresAt
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _redis.Dispose();
    }

    private sealed record LockValue
    {
        public string OwnerId { get; init; } = string.Empty;
        public DateTimeOffset AcquiredAt { get; init; }
        public DateTimeOffset ExpiresAt { get; init; }
    }
}

/// <summary>
/// Options for distributed lock.
/// </summary>
public sealed class RedisDistributedLockOptions
{
    public int DefaultTtlSeconds { get; set; } = 30;
    public int MaxRetryAttempts { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(100);
}