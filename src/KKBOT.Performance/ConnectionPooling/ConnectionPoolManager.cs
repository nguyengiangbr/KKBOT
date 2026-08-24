// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Grpc.Net.Client;
using Grpc.Core;
using Polly;
using Polly.Extensions.Http;
using KKBOT.Observability;
using KKBOT.Observability.Tracing;

namespace KKBOT.Performance.ConnectionPooling;

public sealed class ConnectionPoolManager : IAsyncDisposable
{
    private readonly ILogger<ConnectionPoolManager> _logger;
    private readonly ConnectionPoolOptions _options;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<string, HttpClient> _httpClients = new();
    private readonly ConcurrentDictionary<string, IConnectionMultiplexer> _redisConnections = new();
    private readonly ConcurrentDictionary<string, DbConnectionPool> _dbPools = new();
    private readonly ConcurrentDictionary<string, GrpcChannel> _grpcChannels = new();
    private readonly IMetricsRegistry _metrics;
    private bool _disposed;

    public ConnectionPoolManager(
        ILogger<ConnectionPoolManager> logger,
        IOptions<ConnectionPoolOptions> options,
        IServiceProvider serviceProvider,
        IMetricsRegistry metrics)
    {
        _logger = logger;
        _options = options.Value;
        _serviceProvider = serviceProvider;
        _metrics = metrics;
    }

    public HttpClient GetHttpClient(string name)
    {
        return _httpClients.GetOrAdd(name, CreateHttpClient);
    }

    private HttpClient CreateHttpClient(string name)
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = _options.HttpPooledConnectionLifetime,
            PooledConnectionIdleTimeout = _options.HttpPooledConnectionIdleTimeout,
            MaxConnectionsPerServer = _options.HttpMaxConnectionsPerServer,
            EnableMultipleHttp2Connections = true,
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
            UseCookies = false,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
        };

        var client = new HttpClient(handler)
        {
            Timeout = _options.HttpDefaultTimeout
        };

        client.DefaultRequestHeaders.Add("User-Agent", $"KKBOT/{GetType().Assembly.GetName().Version}");
        client.DefaultRequestHeaders.ConnectionClose = false;

        _logger.LogDebug("Created HTTP client pool: {Name}", name);
        return client;
    }

    public IConnectionMultiplexer GetRedisConnection(string name = "default")
    {
        return _redisConnections.GetOrAdd(name, CreateRedisConnection);
    }

    private IConnectionMultiplexer CreateRedisConnection(string name)
    {
        var config = _options.GetRedisConfiguration(name);
        
        var options = ConfigurationOptions.Parse(config.ConnectionString);
        options.AbortOnConnectFail = false;
        options.ConnectRetry = config.ConnectRetry;
        options.ReconnectRetryPolicy = new ExponentialRetry(config.ReconnectRetryDelay);
        options.KeepAlive = config.KeepAliveInterval;
        options.DefaultVersion = new Version(7, 2, 0);
        
        options.MinPoolSize = config.MinPoolSize;
        options.MaxPoolSize = config.MaxPoolSize;
        options.SocketManager = SocketManager.ThreadPool;

        var multiplexer = ConnectionMultiplexer.Connect(options);
        
        multiplexer.ConnectionFailed += (sender, args) =>
        {
            _logger.LogError(args.Exception, "Redis connection failed: {Endpoint}", args.EndPoint);
        };
        
        multiplexer.ConnectionRestored += (sender, args) =>
        {
            _logger.LogInformation("Redis connection restored: {Endpoint}", args.EndPoint);
        };
        
        _logger.LogDebug("Created Redis connection pool: {Name}", name);
        return multiplexer;
    }

    public DbConnectionPool GetDatabasePool(string name = "default")
    {
        return _dbPools.GetOrAdd(name, CreateDatabasePool);
    }

    private DbConnectionPool CreateDatabasePool(string name)
    {
        var config = _options.GetDatabaseConfiguration(name);
        
        var pool = new DbConnectionPool
        {
            Name = name,
            ConnectionString = config.ConnectionString,
            MinPoolSize = config.MinPoolSize,
            MaxPoolSize = config.MaxPoolSize,
            ConnectionTimeout = config.ConnectionTimeout,
            EnableRetry = config.EnableRetry,
            MaxRetryCount = config.MaxRetryCount,
            RetryDelay = config.RetryDelay
        };

        _logger.LogDebug("Created database connection pool: {Name}", name);
        return pool;
    }

    public void ConfigureEfCorePooling<TContext>(DbContextOptionsBuilder optionsBuilder) where TContext : DbContext
    {
        var config = _options.GetDatabaseConfiguration(typeof(TContext).Name);
        
        optionsBuilder.UseSqlite(config.ConnectionString, sqliteOptions =>
        {
            sqliteOptions.CommandTimeout((int)config.CommandTimeout.TotalSeconds);
        });

        optionsBuilder.EnableServiceProviderCaching();
        optionsBuilder.EnableSensitiveDataLogging(false);
    }

    public GrpcChannel GetGrpcChannel(string name)
    {
        return _grpcChannels.GetOrAdd(name, CreateGrpcChannel);
    }

    private GrpcChannel CreateGrpcChannel(string name)
    {
        var config = _options.GetGrpcConfiguration(name);
        
        var credentials = config.UseTls 
            ? new SslCredentials() 
            : ChannelCredentials.Insecure;

        var channelOptions = new GrpcChannelOptions
        {
            Credentials = credentials,
            MaxReceiveMessageSize = config.MaxReceiveMessageSize,
            MaxSendMessageSize = config.MaxSendMessageSize,
            DisposeHttpClient = true,
            HttpHandler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = _options.GrpcPooledConnectionLifetime,
                PooledConnectionIdleTimeout = _options.GrpcPooledConnectionIdleTimeout,
                MaxConnectionsPerServer = config.MaxConnectionsPerServer,
                EnableMultipleHttp2Connections = true,
                KeepAlivePingDelay = TimeSpan.FromSeconds(30),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(10),
                KeepAlivePingPolicy = HttpKeepAlivePolicy.Always
            },
            LoggerFactory = _serviceProvider.GetService<ILoggerFactory>()
        };

        var channel = GrpcChannel.ForAddress(config.Address, channelOptions);
        _logger.LogDebug("Created gRPC channel: {Name} -> {Address}", name, config.Address);
        return channel;
    }

    public async Task<ConnectionPoolHealthReport> CheckHealthAsync(CancellationToken ct = default)
    {
        var report = new ConnectionPoolHealthReport
        {
            Timestamp = DateTimeOffset.UtcNow
        };

        foreach (var kvp in _httpClients)
        {
            try
            {
                var client = kvp.Value;
                using var request = new HttpRequestMessage(HttpMethod.Head, "/health");
                request.Headers.Add("X-Health-Check", "true");
                
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(5));
                
                var response = await client.SendAsync(request, cts.Token);
                report.HttpClients[kvp.Key] = response.IsSuccessStatusCode ? HealthStatus.Healthy : HealthStatus.Degraded;
            }
            catch
            {
                report.HttpClients[kvp.Key] = HealthStatus.Unhealthy;
            }
        }

        foreach (var kvp in _redisConnections)
        {
            try
            {
                var db = kvp.Value.GetDatabase();
                await db.PingAsync();
                report.RedisConnections[kvp.Key] = HealthStatus.Healthy;
            }
            catch
            {
                report.RedisConnections[kvp.Key] = HealthStatus.Unhealthy;
            }
        }

        foreach (var kvp in _dbPools)
        {
            report.DatabasePools[kvp.Key] = HealthStatus.Healthy;
        }

        foreach (var kvp in _grpcChannels)
        {
            try
            {
                var state = kvp.Value.State;
                report.GrpcChannels[kvp.Key] = state == ChannelState.Ready ? HealthStatus.Healthy : HealthStatus.Degraded;
            }
            catch
            {
                report.GrpcChannels[kvp.Key] = HealthStatus.Unhealthy;
            }
        }

        report.OverallStatus = report.HttpClients.Values.All(s => s == HealthStatus.Healthy) &&
                              report.RedisConnections.Values.All(s => s == HealthStatus.Healthy) &&
                              report.DatabasePools.Values.All(s => s == HealthStatus.Healthy) &&
                              report.GrpcChannels.Values.All(s => s == HealthStatus.Healthy)
            ? HealthStatus.Healthy : HealthStatus.Degraded;

        return report;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var client in _httpClients.Values)
        {
            client.Dispose();
        }

        foreach (var redis in _redisConnections.Values)
        {
            await redis.DisposeAsync();
        }

        foreach (var pool in _dbPools.Values)
        {
            pool.Dispose();
        }

        foreach (var channel in _grpcChannels.Values)
        {
            await channel.ShutdownAsync();
        }

        _logger.LogInformation("Disposed all connection pools");
    }
}

public sealed class ConnectionPoolOptions
{
    public TimeSpan HttpPooledConnectionLifetime { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan HttpPooledConnectionIdleTimeout { get; set; } = TimeSpan.FromMinutes(2);
    public int HttpMaxConnectionsPerServer { get; set; } = 100;
    public TimeSpan HttpDefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public int HttpRetryCount { get; set; } = 3;
    public int HttpCircuitBreakerThreshold { get; set; } = 5;
    public TimeSpan HttpCircuitBreakerDuration { get; set; } = TimeSpan.FromSeconds(30);

    public RedisPoolConfig GetRedisConfiguration(string name) => 
        _redisConfigs.GetValueOrDefault(name, new RedisPoolConfig());
    
    private readonly Dictionary<string, RedisPoolConfig> _redisConfigs = new()
    {
        ["default"] = new RedisPoolConfig
        {
            ConnectionString = "localhost:6379,abortConnect=false",
            MinPoolSize = 10,
            MaxPoolSize = 100,
            ConnectRetry = 3,
            ReconnectRetryDelay = TimeSpan.FromSeconds(1),
            KeepAliveInterval = TimeSpan.FromSeconds(60)
        }
    };

    public DbPoolConfig GetDatabaseConfiguration(string name) =>
        _dbConfigs.GetValueOrDefault(name, new DbPoolConfig());
    
    private readonly Dictionary<string, DbPoolConfig> _dbConfigs = new()
    {
        ["default"] = new DbPoolConfig
        {
            ConnectionString = "DataSource=kkbot.db;Cache=Shared;Foreign Keys=True",
            MinPoolSize = 5,
            MaxPoolSize = 50,
            ConnectionTimeout = TimeSpan.FromSeconds(15),
            CommandTimeout = TimeSpan.FromSeconds(30),
            EnableRetry = true,
            MaxRetryCount = 3,
            RetryDelay = TimeSpan.FromSeconds(1)
        }
    };

    public GrpcPoolConfig GetGrpcConfiguration(string name) =>
        _grpcConfigs.GetValueOrDefault(name, new GrpcPoolConfig());
    
    private readonly Dictionary<string, GrpcPoolConfig> _grpcConfigs = new()
    {
        ["coordination"] = new GrpcPoolConfig
        {
            Address = "http://localhost:5000",
            UseTls = false,
            MaxReceiveMessageSize = 4 * 1024 * 1024,
            MaxSendMessageSize = 4 * 1024 * 1024,
            MaxConnectionsPerServer = 50
        }
    };

    public TimeSpan GrpcPooledConnectionLifetime { get; set; } = TimeSpan.FromMinutes(10);
    public TimeSpan GrpcPooledConnectionIdleTimeout { get; set; } = TimeSpan.FromMinutes(5);
}

public sealed class RedisPoolConfig
{
    public string ConnectionString { get; set; } = string.Empty;
    public int MinPoolSize { get; set; } = 10;
    public int MaxPoolSize { get; set; } = 100;
    public int ConnectRetry { get; set; } = 3;
    public TimeSpan ReconnectRetryDelay { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(60);
}

public sealed class DbPoolConfig
{
    public string ConnectionString { get; set; } = string.Empty;
    public int MinPoolSize { get; set; } = 5;
    public int MaxPoolSize { get; set; } = 50;
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(15);
    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public bool EnableRetry { get; set; } = true;
    public int MaxRetryCount { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
}

public sealed class GrpcPoolConfig
{
    public string Address { get; set; } = string.Empty;
    public bool UseTls { get; set; } = false;
    public int MaxReceiveMessageSize { get; set; } = 4 * 1024 * 1024;
    public int MaxSendMessageSize { get; set; } = 4 * 1024 * 1024;
    public int MaxConnectionsPerServer { get; set; } = 50;
}

public sealed class DbConnectionPool : IDisposable
{
    public string Name { get; init; } = string.Empty;
    public string ConnectionString { get; init; } = string.Empty;
    public int MinPoolSize { get; init; }
    public int MaxPoolSize { get; init; }
    public TimeSpan ConnectionTimeout { get; init; }
    public bool EnableRetry { get; init; }
    public int MaxRetryCount { get; init; }
    public TimeSpan RetryDelay { get; init; }
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}

public enum HealthStatus
{
    Healthy,
    Degraded,
    Unhealthy
}

public sealed record ConnectionPoolHealthReport
{
    public DateTimeOffset Timestamp { get; init; }
    public HealthStatus OverallStatus { get; init; }
    public Dictionary<string, HealthStatus> HttpClients { get; init; } = new();
    public Dictionary<string, HealthStatus> RedisConnections { get; init; } = new();
    public Dictionary<string, HealthStatus> DatabasePools { get; init; } = new();
    public Dictionary<string, HealthStatus> GrpcChannels { get; init; } = new();
}