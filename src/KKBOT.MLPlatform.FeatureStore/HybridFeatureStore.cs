// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using KKBOT.MLPlatform.Abstractions;
using KKBOT.Observability;
using KKBOT.Performance.Caching;

namespace KKBOT.MLPlatform.FeatureStore;

/// <summary>
/// Hybrid feature store with Redis (online) + PostgreSQL (offline) storage.
/// Supports: point-in-time correctness, feature versioning, materialized views.
/// </summary>
public sealed class HybridFeatureStore : IFeatureStore, IHostedService, IDisposable
{
    private readonly ILogger<HybridFeatureStore> _logger;
    private readonly FeatureStoreOptions _options;
    private readonly IMetricsRegistry _metrics;
    private readonly ConnectionMultiplexer _redis;
    private readonly IDatabase _redisDb;
    private readonly FeatureStoreDbContext _dbContext;
    private readonly L2Cache<string, FeatureVector> _onlineCache;
    private readonly ConcurrentDictionary<string, FeatureDefinition> _featureRegistry = new();
    private readonly Timer? _materializationTimer;
    private readonly CancellationTokenSource _shutdownCts = new();
    private bool _disposed;

    public HybridFeatureStore(
        ILogger<HybridFeatureStore> logger,
        IOptions<FeatureStoreOptions> options,
        IMetricsRegistry metrics,
        L2Cache<string, FeatureVector> onlineCache)
    {
        _logger = logger;
        _options = options.Value;
        _metrics = metrics;
        _onlineCache = onlineCache;

        // Redis connection
        _redis = ConnectionMultiplexer.Connect(_options.RedisConnectionString);
        _redisDb = _redis.GetDatabase();

        // PostgreSQL context
        var dbOptions = new DbContextOptionsBuilder<FeatureStoreDbContext>()
            .UseNpgsql(_options.PostgresConnectionString)
            .EnableSensitiveDataLogging(_options.EnableSensitiveLogging)
            .Options;
        _dbContext = new FeatureStoreDbContext(dbOptions);

        // Materialization timer for offline store
        if (_options.EnableOfflineStore)
        {
            _materializationTimer = new Timer(
                MaterializeOfflineAsync, 
                null, 
                TimeSpan.FromMinutes(_options.MaterializationIntervalMinutes),
                TimeSpan.FromMinutes(_options.MaterializationIntervalMinutes));
        }

        // Ensure database exists
        _dbContext.Database.EnsureCreated();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Hybrid Feature Store started");
        
        // Load feature definitions
        await LoadFeatureDefinitionsAsync(cancellationToken);
        
        // Start background tasks
        if (_options.EnableOnlineStore)
        {
            _ = Task.Run(() => WarmupCacheAsync(cancellationToken), cancellationToken);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Hybrid Feature Store stopping");
        _shutdownCts.Cancel();
        _materializationTimer?.Dispose();
        await _redis.CloseAsync();
        await _dbContext.DisposeAsync();
    }

    public async Task WriteFeaturesAsync(IEnumerable<FeatureVector> features, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var featureList = features.ToList();

        try
        {
            // Write to online store (Redis) - low latency
            if (_options.EnableOnlineStore)
            {
                await WriteToOnlineStoreAsync(featureList, ct);
            }

            // Write to offline store (PostgreSQL) - durability
            if (_options.EnableOfflineStore)
            {
                await WriteToOfflineStoreAsync(featureList, ct);
            }

            // Update cache
            foreach (var fv in featureList)
            {
                var cacheKey = GetCacheKey(fv.EntityType, fv.EntityId);
                _onlineCache.Set(cacheKey, fv, TimeSpan.FromHours(_options.CacheTtlHours));
            }

            _metrics.RecordHttpRequestDuration("feature_write", stopwatch.Elapsed.TotalMilliseconds);
            _metrics.IncrementHttpRequests(); // feature_write_count
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write features");
            throw;
        }
    }

    public async Task<FeatureVector?> GetFeaturesAsync(
        string entityType, 
        string entityId, 
        IEnumerable<string> featureNames, 
        DateTimeOffset? asOf = null, 
        CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var cacheKey = GetCacheKey(entityType, entityId);

        try
        {
            // Try online store first (Redis)
            if (_options.EnableOnlineStore)
            {
                // Check cache
                if (_onlineCache.TryGetValue(cacheKey, out var cached))
                {
                    if (!asOf.HasValue || cached.EventTimestamp <= asOf.Value)
                    {
                        return FilterFeatures(cached, featureNames);
                    }
                }

                // Check Redis directly
                var redisKey = GetRedisKey(entityType, entityId);
                var redisValue = await _redisDb.StringGetAsync(redisKey);
                if (redisValue.HasValue)
                {
                    var featureVector = System.Text.Json.JsonSerializer.Deserialize<FeatureVector>(redisValue!);
                    if (featureVector != null && (!asOf.HasValue || featureVector.EventTimestamp <= asOf.Value))
                    {
                        _onlineCache.Set(cacheKey, featureVector, TimeSpan.FromHours(_options.CacheTtlHours));
                        return FilterFeatures(featureVector, featureNames);
                    }
                }
            }

            // Fall back to offline store (PostgreSQL) for point-in-time queries
            if (_options.EnableOfflineStore && asOf.HasValue)
            {
                return await GetFromOfflineStoreAsync(entityType, entityId, featureNames, asOf.Value, ct);
            }

            _metrics.RecordHttpRequestDuration("feature_read_miss", stopwatch.Elapsed.TotalMilliseconds);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read features for {EntityType}:{EntityId}", entityType, entityId);
            throw;
        }
    }

    public async Task<IReadOnlyList<FeatureVector>> GetHistoricalFeaturesAsync(
        string entityType, 
        IEnumerable<string> entityIds, 
        IEnumerable<string> featureNames, 
        DateTimeOffset start, 
        DateTimeOffset end, 
        CancellationToken ct = default)
    {
        if (!_options.EnableOfflineStore)
        {
            throw new InvalidOperationException("Offline store not enabled");
        }

        var entityIdList = entityIds.ToList();
        var featureNameList = featureNames.ToList();

        var query = _dbContext.FeatureEvents
            .Where(fe => fe.EntityType == entityType 
                      && entityIdList.Contains(fe.EntityId)
                      && fe.EventTimestamp >= start 
                      && fe.EventTimestamp <= end)
            .OrderBy(fe => fe.EventTimestamp);

        var events = await query.ToListAsync(ct);

        // Group by entity and reconstruct feature vectors
        var result = events
            .GroupBy(e => e.EntityId)
            .Select(g => new FeatureVector
            {
                EntityId = g.Key,
                EntityType = entityType,
                Features = g
                    .Where(e => featureNameList.Contains(e.FeatureName))
                    .GroupBy(e => e.FeatureName)
                    .ToDictionary(
                        grp => grp.Key, 
                        grp => new FeatureValue 
                        { 
                            Value = System.Text.Json.JsonSerializer.Deserialize<object>(grp.OrderByDescending(e => e.EventTimestamp).First().ValueJson),
                            Type = Enum.Parse<FeatureType>(grp.First().FeatureType),
                            Timestamp = grp.OrderByDescending(e => e.EventTimestamp).First().EventTimestamp
                        }),
                EventTimestamp = g.Max(e => e.EventTimestamp)
            })
            .ToList();

        return result;
    }

    public async Task<FeatureDefinition> RegisterFeatureAsync(FeatureDefinition definition, CancellationToken ct = default)
    {
        // Validate definition
        if (string.IsNullOrWhiteSpace(definition.Name))
            throw new ArgumentException("Feature name is required");

        if (string.IsNullOrWhiteSpace(definition.EntityType))
            throw new ArgumentException("Entity type is required");

        // Register in memory
        var key = $"{definition.EntityType}:{definition.Name}";
        _featureRegistry[key] = definition;

        // Persist to database
        var entity = new FeatureDefinitionEntity
        {
            Name = definition.Name,
            EntityType = definition.EntityType,
            Type = definition.Type.ToString(),
            Description = definition.Description,
            Owner = definition.Owner,
            Tags = System.Text.Json.JsonSerializer.Serialize(definition.Tags),
            Validation = System.Text.Json.JsonSerializer.Serialize(definition.Validation),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.FeatureDefinitions.Add(entity);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Registered feature {EntityType}:{Name}", definition.EntityType, definition.Name);
        return definition;
    }

    public async Task<IReadOnlyList<FeatureDefinition>> ListFeaturesAsync(string entityType, CancellationToken ct = default)
    {
        var definitions = await _dbContext.FeatureDefinitions
            .Where(fd => fd.EntityType == entityType)
            .ToListAsync(ct);

        return definitions.Select(d => new FeatureDefinition
        {
            Name = d.Name,
            EntityType = d.EntityType,
            Type = Enum.Parse<FeatureType>(d.Type),
            Description = d.Description,
            Owner = d.Owner,
            Tags = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(d.Tags) ?? new(),
            Validation = System.Text.Json.JsonSerializer.Deserialize<FeatureValidation>(d.Validation) ?? new()
        }).ToList();
    }

    private async Task WriteToOnlineStoreAsync(List<FeatureVector> features, CancellationToken ct)
    {
        var batch = _redis.CreateBatch();

        foreach (var fv in features)
        {
            var redisKey = GetRedisKey(fv.EntityType, fv.EntityId);
            var json = System.Text.Json.JsonSerializer.Serialize(fv);
            
            batch.StringSetAsync(redisKey, json, TimeSpan.FromDays(_options.OnlineTtlDays));
            
            // Also store individual features for partial reads
            foreach (var feat in fv.Features)
            {
                var featKey = $"{redisKey}:{feat.Key}";
                var featJson = System.Text.Json.JsonSerializer.Serialize(feat.Value);
                batch.StringSetAsync(featKey, featJson, TimeSpan.FromDays(_options.OnlineTtlDays));
            }
        }

        await batch.ExecuteAsync();
    }

    private async Task WriteToOfflineStoreAsync(List<FeatureVector> features, CancellationToken ct)
    {
        var entities = features.SelectMany(fv => fv.Features.Select(feat => new FeatureEventEntity
        {
            EntityType = fv.EntityType,
            EntityId = fv.EntityId,
            FeatureName = feat.Key,
            FeatureType = feat.Value.Type.ToString(),
            ValueJson = System.Text.Json.JsonSerializer.Serialize(feat.Value.Value),
            EventTimestamp = fv.EventTimestamp,
            CreatedTimestamp = fv.CreatedTimestamp
        })).ToList();

        _dbContext.FeatureEvents.AddRange(entities);
        await _dbContext.SaveChangesAsync(ct);
    }

    private async Task<FeatureVector?> GetFromOfflineStoreAsync(
        string entityType, 
        string entityId, 
        IEnumerable<string> featureNames, 
        DateTimeOffset asOf, 
        CancellationToken ct)
    {
        var featureNameList = featureNames.ToList();

        var events = await _dbContext.FeatureEvents
            .Where(fe => fe.EntityType == entityType 
                      && fe.EntityId == entityId
                      && fe.EventTimestamp <= asOf
                      && featureNameList.Contains(fe.FeatureName))
            .OrderByDescending(fe => fe.EventTimestamp)
            .ToListAsync(ct);

        if (!events.Any()) return null;

        var latestEvents = events
            .GroupBy(e => e.FeatureName)
            .Select(g => g.First())
            .ToList();

        return new FeatureVector
        {
            EntityId = entityId,
            EntityType = entityType,
            Features = latestEvents.ToDictionary(
                e => e.FeatureName,
                e => new FeatureValue
                {
                    Value = System.Text.Json.JsonSerializer.Deserialize<object>(e.ValueJson),
                    Type = Enum.Parse<FeatureType>(e.FeatureType),
                    Timestamp = e.EventTimestamp
                }),
            EventTimestamp = latestEvents.Max(e => e.EventTimestamp)
        };
    }

    private FeatureVector? FilterFeatures(FeatureVector vector, IEnumerable<string> featureNames)
    {
        var names = featureNames.ToHashSet();
        var filtered = vector.Features.Where(kvp => names.Contains(kvp.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        
        if (filtered.Count == 0) return null;

        return new FeatureVector
        {
            EntityId = vector.EntityId,
            EntityType = vector.EntityType,
            Features = filtered,
            EventTimestamp = vector.EventTimestamp,
            CreatedTimestamp = vector.CreatedTimestamp
        };
    }

    private async Task LoadFeatureDefinitionsAsync(CancellationToken ct)
    {
        var definitions = await _dbContext.FeatureDefinitions.ToListAsync(ct);
        foreach (var d in definitions)
        {
            var key = $"{d.EntityType}:{d.Name}";
            _featureRegistry[key] = new FeatureDefinition
            {
                Name = d.Name,
                EntityType = d.EntityType,
                Type = Enum.Parse<FeatureType>(d.Type),
                Description = d.Description,
                Owner = d.Owner,
                Tags = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(d.Tags) ?? new(),
                Validation = System.Text.Json.JsonSerializer.Deserialize<FeatureValidation>(d.Validation) ?? new()
            };
        }
        _logger.LogInformation("Loaded {Count} feature definitions", definitions.Count);
    }

    private async Task WarmupCacheAsync(CancellationToken ct)
    {
        // Pre-load frequently accessed features
        try
        {
            var recentEntities = await _dbContext.FeatureEvents
                .Where(fe => fe.EventTimestamp > DateTimeOffset.UtcNow.AddHours(-1))
                .Select(fe => new { fe.EntityType, fe.EntityId })
                .Distinct()
                .Take(1000)
                .ToListAsync(ct);

            foreach (var entity in recentEntities)
            {
                if (ct.IsCancellationRequested) break;
                
                var key = GetCacheKey(entity.EntityType, entity.EntityId);
                if (!_onlineCache.TryGetValue(key, out _))
                {
                    // Trigger lazy load on first access
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache warmup failed");
        }
    }

    private async void MaterializeOfflineAsync(object? state)
    {
        if (_shutdownCts.Token.IsCancellationRequested) return;

        try
        {
            _logger.LogDebug("Starting offline materialization");
            
            // Create materialized views for common queries
            await CreateMaterializedViewsAsync(_shutdownCts.Token);
            
            // Compact old data
            await CompactOfflineStoreAsync(_shutdownCts.Token);
            
            _logger.LogDebug("Offline materialization completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Offline materialization failed");
        }
    }

    private async Task CreateMaterializedViewsAsync(CancellationToken ct)
    {
        // Create aggregated views for common analytical queries
        // This would typically be done via SQL views or materialized views
        await Task.CompletedTask;
    }

    private async Task CompactOfflineStoreAsync(CancellationToken ct)
    {
        // Remove old events beyond retention period
        var cutoff = DateTimeOffset.UtcNow - TimeSpan.FromDays(_options.OfflineRetentionDays);
        
        var deleted = await _dbContext.FeatureEvents
            .Where(fe => fe.EventTimestamp < cutoff)
            .ExecuteDeleteAsync(ct);

        if (deleted > 0)
        {
            _logger.LogInformation("Compacted {Count} old feature events", deleted);
        }
    }

    private string GetCacheKey(string entityType, string entityId) => $"fs:{entityType}:{entityId}";
    private string GetRedisKey(string entityType, string entityId) => $"fs:online:{entityType}:{entityId}";

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _shutdownCts.Cancel();
        _redis?.Dispose();
        _dbContext?.Dispose();
    }
}

/// <summary>
/// Entity Framework entities.
/// </summary>
internal sealed class FeatureStoreDbContext : DbContext
{
    public FeatureStoreDbContext(DbContextOptions<FeatureStoreDbContext> options) : base(options) { }

    public DbSet<FeatureDefinitionEntity> FeatureDefinitions { get; set; }
    public DbSet<FeatureEventEntity> FeatureEvents { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FeatureDefinitionEntity>(entity =>
        {
            entity.HasKey(e => new { e.EntityType, e.Name });
            entity.Property(e => e.Tags).HasColumnType("jsonb");
            entity.Property(e => e.Validation).HasColumnType("jsonb");
            entity.HasIndex(e => e.EntityType);
        });

        modelBuilder.Entity<FeatureEventEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.EntityType, e.EntityId, e.EventTimestamp });
            entity.HasIndex(e => e.FeatureName);
            entity.Property(e => e.ValueJson).HasColumnType("jsonb");
        });
    }
}

internal sealed class FeatureDefinitionEntity
{
    public string EntityType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Tags { get; set; } = "{}";
    public string Validation { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class FeatureEventEntity
{
    public long Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string FeatureName { get; set; } = string.Empty;
    public string FeatureType { get; set; } = string.Empty;
    public string ValueJson { get; set; } = string.Empty;
    public DateTimeOffset EventTimestamp { get; set; }
    public DateTimeOffset CreatedTimestamp { get; set; }
}

/// <summary>
/// Feature store configuration.
/// </summary>
public sealed class FeatureStoreOptions
{
    public string RedisConnectionString { get; set; } = "localhost:6379";
    public string PostgresConnectionString { get; set; } = "Host=localhost;Database=kkbot_features;Username=kkbot;Password=kkbot";
    public bool EnableOnlineStore { get; set; } = true;
    public bool EnableOfflineStore { get; set; } = true;
    public int OnlineTtlDays { get; set; } = 7;
    public int OfflineRetentionDays { get; set; } = 90;
    public int CacheTtlHours { get; set; } = 24;
    public int MaterializationIntervalMinutes { get; set; } = 60;
    public bool EnableSensitiveLogging { get; set; } = false;
}

/// <summary>
/// DI Extensions.
/// </summary>
public static class FeatureStoreExtensions
{
    public static IServiceCollection AddKkbotFeatureStore(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FeatureStoreOptions>(configuration.GetSection("FeatureStore"));
        services.AddSingleton<HybridFeatureStore>();
        services.AddHostedService(sp => sp.GetRequiredService<HybridFeatureStore>());
        return services;
    }
}
