// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using KKBOT.Observability;
using KKBOT.Observability.Tracing;

namespace KKBOT.Performance.Caching;

public sealed class L2Cache : IDistributedCache, IAsyncDisposable
{
    private readonly ILogger<L2Cache> _logger;
    private readonly L2CacheOptions _options;
    private readonly IDistributedCache _redisCache;
    private readonly IMemoryCache _memoryCache;
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private readonly ISubscriber _subscriber;
    private readonly ConcurrentDictionary<string, CacheEntry> _localMetadata = new();
    private readonly SemaphoreSlim _invalidationLock = new(1, 1);
    private bool _disposed;

    public L2Cache(
        ILogger<L2Cache> logger,
        IOptions<L2CacheOptions> options,
        IDistributedCache redisCache,
        IMemoryCache memoryCache,
        IConnectionMultiplexer redis)
    {
        _logger = logger;
        _options = options.Value;
        _redisCache = redisCache;
        _memoryCache = memoryCache;
        _redis = redis;
        _db = redis.GetDatabase();
        _subscriber = redis.GetSubscriber();

        _ = _subscriber.SubscribeAsync("kkbot:cache:invalidate", OnInvalidationReceived);
        _ = _subscriber.SubscribeAsync("kkbot:cache:update", OnUpdateReceived);
    }

    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        using var activity = KkbotActivitySources.Caching.StartActivity("cache.get");
        activity?.SetTag("cache.key", key);
        activity?.SetTag("cache.tier", "L1");

        if (_memoryCache.TryGetValue(key, out byte[]? l1Value))
        {
            _logger.LogDebug("L1 cache HIT for key: {Key}", key);
            UpdateMetadata(key, CacheTier.L1, true);
            return l1Value;
        }

        UpdateMetadata(key, CacheTier.L1, false);

        activity?.SetTag("cache.tier", "L2");
        var l2Value = await _redisCache.GetAsync(key, token);
        
        if (l2Value != null)
        {
            _logger.LogDebug("L2 cache HIT for key: {Key}", key);
            
            _memoryCache.Set(key, l2Value, new MemoryCacheEntryOptions
            {
                Size = l2Value.Length,
                SlidingExpiration = _options.L1SlidingExpiration,
                AbsoluteExpirationRelativeToNow = _options.L1AbsoluteExpiration,
                Priority = CacheItemPriority.Normal
            });
            
            UpdateMetadata(key, CacheTier.L2, true);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return l2Value;
        }

        UpdateMetadata(key, CacheTier.L2, false);
        activity?.SetStatus(ActivityStatusCode.Ok, "Cache miss");
        return null;
    }

    public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        using var activity = KkbotActivitySources.Caching.StartActivity("cache.set");
        activity?.SetTag("cache.key", key);
        activity?.SetTag("cache.size", value.Length);

        await _redisCache.SetAsync(key, value, options, token);

        var memoryOptions = new MemoryCacheEntryOptions
        {
            Size = value.Length,
            SlidingExpiration = options.SlidingExpiration ?? _options.L1SlidingExpiration,
            AbsoluteExpirationRelativeToNow = options.AbsoluteExpirationRelativeToNow ?? _options.L1AbsoluteExpiration,
            AbsoluteExpiration = options.AbsoluteExpiration,
            Priority = options.Priority ?? CacheItemPriority.Normal
        };

        _memoryCache.Set(key, value, memoryOptions);

        await PublishUpdateEventAsync(key, token);

        _logger.LogDebug("Cache SET for key: {Key} (size: {Size} bytes)", key, value.Length);
    }

    public async Task RefreshAsync(string key, CancellationToken token = default)
    {
        await _redisCache.RefreshAsync(key, token);
        
        if (_memoryCache.TryGetValue(key, out byte[]? value))
        {
            var memoryOptions = new MemoryCacheEntryOptions { Size = value.Length };
            _memoryCache.Set(key, value, memoryOptions);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken token = default)
    {
        using var activity = KkbotActivitySources.Caching.StartActivity("cache.remove");
        activity?.SetTag("cache.key", key);

        await _redisCache.RemoveAsync(key, token);
        _memoryCache.Remove(key);
        await PublishInvalidationEventAsync(key, token);

        _logger.LogDebug("Cache REMOVE for key: {Key}", key);
    }

    public byte[]? Get(string key) => GetAsync(key).GetAwaiter().GetResult();
    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => SetAsync(key, value, options).GetAwaiter().GetResult();
    public void Refresh(string key) => RefreshAsync(key).GetAwaiter().GetResult();
    public void Remove(string key) => RemoveAsync(key).GetAwaiter().GetResult();

    public async Task<CacheGetResult> GetWithTierAsync(string key, CancellationToken token = default)
    {
        if (_memoryCache.TryGetValue(key, out byte[]? l1Value))
        {
            return new CacheGetResult(l1Value, CacheTier.L1, true);
        }

        var l2Value = await _redisCache.GetAsync(key, token);
        if (l2Value != null)
        {
            return new CacheGetResult(l2Value, CacheTier.L2, true);
        }

        return new CacheGetResult(null, CacheTier.None, false);
    }

    public async Task SetInTierAsync(string key, byte[] value, CacheTier tier, DistributedCacheEntryOptions? options = null, CancellationToken token = default)
    {
        var opts = options ?? new DistributedCacheEntryOptions();

        if (tier.HasFlag(CacheTier.L2))
        {
            await _redisCache.SetAsync(key, value, opts, token);
        }

        if (tier.HasFlag(CacheTier.L1))
        {
            var memoryOptions = new MemoryCacheEntryOptions
            {
                Size = value.Length,
                SlidingExpiration = opts.SlidingExpiration ?? _options.L1SlidingExpiration,
                AbsoluteExpirationRelativeToNow = opts.AbsoluteExpirationRelativeToNow ?? _options.L1AbsoluteExpiration
            };
            _memoryCache.Set(key, value, memoryOptions);
        }

        if (tier != CacheTier.None)
        {
            await PublishUpdateEventAsync(key, token);
        }
    }

    public CacheStatistics GetStatistics()
    {
        var metadata = _localMetadata.Values.ToList();
        return new CacheStatistics
        {
            TotalEntries = metadata.Count,
            L1Hits = metadata.Count(m => m.L1Hits > 0),
            L2Hits = metadata.Count(m => m.L2Hits > 0),
            L1Misses = metadata.Count(m => m.L1Misses > 0),
            L2Misses = metadata.Count(m => m.L2Misses > 0),
            TotalSizeBytes = metadata.Sum(m => m.SizeBytes),
            LastAccessed = metadata.Any() ? metadata.Max(m => m.LastAccessed) : null
        };
    }

    public async Task WarmupAsync(IEnumerable<KeyValuePair<string, byte[]>> entries, CancellationToken token = default)
    {
        var tasks = entries.Select(async entry =>
        {
            await SetAsync(entry.Key, entry.Value, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
            }, token);
        });

        await Task.WhenAll(tasks);
        _logger.LogInformation("Cache warmed up with {Count} entries", entries.Count());
    }

    public async Task InvalidatePatternAsync(string pattern, CancellationToken token = default)
    {
        var server = _redis.GetServer(_redis.GetEndPoints()[0]);
        var keys = server.Keys(pattern: pattern).ToArray();

        foreach (var key in keys)
        {
            await RemoveAsync(key!, token);
        }

        _logger.LogInformation("Invalidated {Count} keys matching pattern: {Pattern}", keys.Length, pattern);
    }

    private async Task OnInvalidationReceived(RedisChannel channel, RedisValue message)
    {
        try
        {
            var key = message.ToString();
            _memoryCache.Remove(key);
            _localMetadata.TryRemove(key, out _);
            _logger.LogDebug("Received invalidation for key: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to process invalidation message");
        }
    }

    private async Task OnUpdateReceived(RedisChannel channel, RedisValue message)
    {
        try
        {
            var update = JsonSerializer.Deserialize<CacheUpdateMessage>(message!);
            if (update != null && _memoryCache.TryGetValue(update.Key, out _))
            {
                var value = await _redisCache.GetAsync(update.Key, CancellationToken.None);
                if (value != null)
                {
                    _memoryCache.Set(update.Key, value, new MemoryCacheEntryOptions
                    {
                        SlidingExpiration = _options.L1SlidingExpiration,
                        AbsoluteExpirationRelativeToNow = _options.L1AbsoluteExpiration
                    });
                }
                else
                {
                    _memoryCache.Remove(update.Key);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to process cache update message");
        }
    }

    private async Task PublishInvalidationEventAsync(string key, CancellationToken token)
    {
        await _subscriber.PublishAsync("kkbot:cache:invalidate", key);
    }

    private async Task PublishUpdateEventAsync(string key, CancellationToken token)
    {
        var message = new CacheUpdateMessage { Key = key, Timestamp = DateTimeOffset.UtcNow };
        await _subscriber.PublishAsync("kkbot:cache:update", JsonSerializer.Serialize(message));
    }

    private void UpdateMetadata(string key, CacheTier tier, bool hit)
    {
        var metadata = _localMetadata.GetOrAdd(key, _ => new CacheEntry { Key = key });
        
        switch (tier)
        {
            case CacheTier.L1:
                if (hit) metadata.L1Hits++; else metadata.L1Misses++;
                break;
            case CacheTier.L2:
                if (hit) metadata.L2Hits++; else metadata.L2Misses++;
                break;
        }
        
        metadata.LastAccessed = DateTimeOffset.UtcNow;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await _subscriber.UnsubscribeAllAsync();
        _redis.Dispose();
    }
}

[Flags]
public enum CacheTier
{
    None = 0,
    L1 = 1,
    L2 = 2,
    Both = L1 | L2
}

public sealed record CacheGetResult(
    byte[]? Value,
    CacheTier Tier,
    bool Hit
);

public sealed record CacheStatistics
{
    public int TotalEntries { get; init; }
    public int L1Hits { get; init; }
    public int L2Hits { get; init; }
    public int L1Misses { get; init; }
    public int L2Misses { get; init; }
    public long TotalSizeBytes { get; init; }
    public DateTimeOffset? LastAccessed { get; init; }
    
    public double L1HitRate => L1Hits + L1Misses > 0 ? (double)L1Hits / (L1Hits + L1Misses) : 0;
    public double L2HitRate => L2Hits + L2Misses > 0 ? (double)L2Hits / (L2Hits + L2Misses) : 0;
    public double OverallHitRate => (L1Hits + L2Hits) > 0 ? (double)(L1Hits + L2Hits) / (L1Hits + L2Hits + L1Misses + L2Misses) : 0;
}

internal sealed class CacheEntry
{
    public string Key { get; init; } = string.Empty;
    public int L1Hits { get; set; }
    public int L1Misses { get; set; }
    public int L2Hits { get; set; }
    public int L2Misses { get; set; }
    public long SizeBytes { get; set; }
    public DateTimeOffset LastAccessed { get; set; }
}

internal sealed record CacheUpdateMessage
{
    public string Key { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; }
}

public sealed class L2CacheOptions
{
    public TimeSpan L1SlidingExpiration { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan L1AbsoluteExpiration { get; set; } = TimeSpan.FromMinutes(30);
    public long L1SizeLimitBytes { get; set; } = 100 * 1024 * 1024;
    public bool EnableL1Cache { get; set; } = true;
    public bool EnableL2Cache { get; set; } = true;
    public bool EnableInvalidationEvents { get; set; } = true;
}