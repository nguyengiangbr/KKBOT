// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KKBOT.Plugins.Abstractions;
using KKBOT.Device.Abstractions;
using KKBOT.Perception.Abstractions;
using KKBOT.AI.Abstractions;
using KKBOT.Orchestration;
using KKBOT.Observability;
using KKBOT.Observability.Tracing;
using KKBOT.Agent.Coordination;

namespace KKBOT.Plugin.NetworkProxy;

/// <summary>
/// Proxy protocol types.
/// </summary>
public enum ProxyProtocol
{
    HTTP,
    HTTPS,
    SOCKS4,
    SOCKS5
}

/// <summary>
/// Proxy configuration for a specific instance.
/// </summary>
public sealed record ProxyConfig
{
    public string ProxyId { get; init; } = Guid.NewGuid().ToString("N");
    public string InstanceId { get; init; } = string.Empty;
    public ProxyProtocol Protocol { get; init; } = ProxyProtocol.SOCKS5;
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 1080;
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string? Domain { get; init; }
    public bool Enabled { get; init; } = true;
    public int ConnectionTimeoutMs { get; init; } = 10000;
    public int ReadWriteTimeoutMs { get; init; } = 30000;
    public Dictionary<string, string> Metadata { get; init; } = new();
}

/// <summary>
/// Proxy pool configuration.
/// </summary>
public sealed record ProxyPoolConfig
{
    public string PoolId { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = string.Empty;
    public List<ProxyConfig> Proxies { get; init; } = new();
    public RotationStrategy RotationStrategy { get; init; } = RotationStrategy.RoundRobin;
    public int HealthCheckIntervalSeconds { get; init; } = 60;
    public int MaxConsecutiveFailures { get; init; } = 3;
    public TimeSpan HealthCheckTimeout { get; init; } = TimeSpan.FromSeconds(10);
    public bool AutoFailover { get; init; } = true;
    public int MaxConcurrentConnectionsPerProxy { get; init; } = 100;
}

public enum RotationStrategy
{
    RoundRobin,
    Random,
    LeastUsed,
    LowestLatency,
    Sticky
}

/// <summary>
/// Proxy health status.
/// </summary>
public sealed record ProxyHealth
{
    public string ProxyId { get; init; } = string.Empty;
    public bool IsHealthy { get; init; }
    public int ConsecutiveFailures { get; init; }
    public int ConsecutiveSuccesses { get; init; }
    public double AverageLatencyMs { get; init; }
    public DateTimeOffset LastCheck { get; init; }
    public DateTimeOffset LastSuccess { get; init; }
    public DateTimeOffset? LastFailure { get; init; }
    public string? LastError { get; init; }
    public int ActiveConnections { get; init; }
}

/// <summary>
/// Network isolation configuration for an instance.
/// </summary>
public sealed record NetworkIsolationConfig
{
    public string InstanceId { get; init; } = string.Empty;
    public IsolationMode Mode { get; init; } = IsolationMode.ProxyOnly;
    public string? NetworkNamespace { get; init; }
    public List<string> AllowedDestinations { get; init; } = new(); // CIDR or domain allowlist
    public List<string> BlockedDestinations { get; init; } = new(); // CIDR or domain blocklist
    public bool BlockWebRTC { get; init; } = true;
    public bool BlockDirectSockets { get; init; } = true;
    public bool ForceProxyForAllTraffic { get; init; } = true;
    public Dictionary<string, string> CustomRoutes { get; init; } = new(); // destination -> proxyId
}

public enum IsolationMode
{
    None,           // No isolation, direct connection
    ProxyOnly,      // All traffic through proxy
    NetworkNamespace, // Linux network namespace isolation
    VPN,            // VPN tunnel (future)
    Custom          // Custom routing rules
}

/// <summary>
/// Proxy rotation event.
/// </summary>
public sealed record ProxyRotationEvent
{
    public string ProxyId { get; init; } = string.Empty;
    public string PoolId { get; init; } = string.Empty;
    public RotationReason Reason { get; init; }
    public string OldProxyId { get; init; } = string.Empty;
    public string NewProxyId { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public Dictionary<string, object> Metadata { get; init; } = new();
}

public enum RotationReason
{
    Scheduled,
    HealthCheckFailed,
    MaxFailuresExceeded,
    ManualTrigger,
    LatencyThreshold,
    GeoLocationChange
}

/// <summary>
/// Module configuration for Network Proxy.
/// </summary>
public sealed class NetworkProxyModuleOptions
{
    // Proxy Binding
    public bool EnableProxyBinding { get; set; } = true;
    public string DefaultProxyPoolId { get; set; } = "default";
    public int DefaultConnectionTimeoutMs { get; set; } = 10000;
    public int DefaultReadWriteTimeoutMs { get; set; } = 30000;

    // Proxy Pools
    public List<ProxyPoolConfig> ProxyPools { get; set; } = new();

    // Network Isolation
    public bool EnableNetworkIsolation { get; set; } = false;
    public IsolationMode DefaultIsolationMode { get; set; } = IsolationMode.ProxyOnly;
    public bool BlockWebRTCByDefault { get; set; } = true;
    public bool BlockDirectSocketsByDefault { get; set; } = true;

    // Proxy Health
    public int HealthCheckIntervalSeconds { get; set; } = 60;
    public int MaxConsecutiveFailures { get; set; } = 3;
    public int HealthCheckTimeoutSeconds { get; set; } = 10;
    public double MaxLatencyThresholdMs { get; set; } = 5000;

    // Rotation
    public RotationStrategy DefaultRotationStrategy { get; set; } = RotationStrategy.LeastUsed;
    public TimeSpan MinRotationInterval { get; set; } = TimeSpan.FromMinutes(5);

    // WebRTC/Direct Socket Blocking
    public bool EnableWebRTCBlocking { get; set; } = true;
    public bool EnableDirectSocketBlocking { get; set; } = true;

    // Monitoring
    public int MetricsIntervalSeconds { get; set; } = 30;
}

/// <summary>
/// Network Proxy Module - Module 2: Network & Proxy Binding
/// </summary>
public sealed class NetworkProxyModule : IGameModule
{
    private readonly ILogger<NetworkProxyModule> _logger;
    private readonly NetworkProxyModuleOptions _options;
    private readonly IDeviceSessionFactory _deviceFactory;
    private readonly IPerceptionPipeline _perceptionPipeline;
    private readonly IAiService _aiService;
    private readonly IAgentRegistry _agentRegistry;
    private readonly IMessageBus _messageBus;
    private readonly ICoordinationService _coordinationService;
    private readonly IDistributedLock _distributedLock;
    private readonly IMetricsRegistry _metrics;

    private readonly ConcurrentDictionary<string, ProxyConfig> _proxyConfigs = new();
    private readonly ConcurrentDictionary<string, ProxyPoolConfig> _proxyPools = new();
    private readonly ConcurrentDictionary<string, ProxyHealth> _proxyHealth = new();
    private readonly ConcurrentDictionary<string, NetworkIsolationConfig> _isolationConfigs = new();
    private readonly ConcurrentDictionary<string, string> _instanceToProxy = new(); // instanceId -> proxyId
    private readonly ConcurrentDictionary<string, HttpClient> _httpClients = new();
    private readonly Timer? _healthCheckTimer;
    private readonly Timer? _rotationTimer;
    private readonly Timer? _metricsTimer;
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly Random _random = new();
    private bool _disposed;

    public string Id => "network-proxy";
    public string Version => "1.0.0";

    public NetworkProxyModule(
        ILogger<NetworkProxyModule> logger,
        IOptions<NetworkProxyModuleOptions> options,
        IDeviceSessionFactory deviceFactory,
        IPerceptionPipeline perceptionPipeline,
        IAiService aiService,
        IAgentRegistry agentRegistry,
        IMessageBus messageBus,
        ICoordinationService coordinationService,
        IDistributedLock distributedLock,
        IMetricsRegistry metrics)
    {
        _logger = logger;
        _options = options.Value;
        _deviceFactory = deviceFactory;
        _perceptionPipeline = perceptionPipeline;
        _aiService = aiService;
        _agentRegistry = agentRegistry;
        _messageBus = messageBus;
        _coordinationService = coordinationService;
        _distributedLock = distributedLock;
        _metrics = metrics;

        // Initialize proxy pools from config
        foreach (var pool in _options.ProxyPools)
        {
            _proxyPools[pool.PoolId] = pool;
            foreach (var proxy in pool.Proxies)
            {
                _proxyConfigs[proxy.ProxyId] = proxy;
                _proxyHealth[proxy.ProxyId] = new ProxyHealth
                {
                    ProxyId = proxy.ProxyId,
                    IsHealthy = true,
                    ConsecutiveFailures = 0,
                    ConsecutiveSuccesses = 0,
                    AverageLatencyMs = 0,
                    LastCheck = DateTimeOffset.UtcNow
                };
            }
        }

        // Timers
        _healthCheckTimer = new Timer(HealthCheckAsync, null, TimeSpan.Zero, TimeSpan.FromSeconds(_options.HealthCheckIntervalSeconds));
        _rotationTimer = new Timer(CheckRotationAsync, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        _metricsTimer = new Timer(ReportMetricsAsync, null, TimeSpan.FromSeconds(_options.MetricsIntervalSeconds), TimeSpan.FromSeconds(_options.MetricsIntervalSeconds));

        _logger.LogInformation("Network Proxy Module initialized with {PoolCount} pools, {ProxyCount} proxies", 
            _proxyPools.Count, _proxyConfigs.Count);
    }

    public ModuleManifest GetManifest()
    {
        return new ModuleManifest
        {
            Id = Id,
            Name = "Network & Proxy Binding",
            Version = Version,
            Author = "KKBOT Team",
            Description = "Per-instance proxy binding, network isolation, proxy rotation, health monitoring, and traffic routing",
            SupportedResolutions = Array.Empty<Resolution>(),
            RequiredPermissions = new[] { "NETWORK_ACCESS", "PROXY_MANAGE", "DEVICE_READ", "DEVICE_WRITE" },
            ConfigSchema = JsonSerializer.Serialize(new NetworkProxyModuleOptions()),
            DefaultConfig = JsonSerializer.Serialize(new NetworkProxyModuleOptions()),
            EntryAssembly = "KKBOT.Plugin.NetworkProxy.dll",
            Assets = Array.Empty<string>()
        };
    }

    public IReadOnlyCollection<IGameDetector> GetDetectors() => Array.Empty<IGameDetector>();
    public IReadOnlyCollection<IGameTaskDefinition> GetTasks() => new[] { new ProxyHealthCheckTask(), new ProxyRotationTask(), new NetworkIsolationTask() };
    public IReadOnlyCollection<IStateDefinition> GetStates() => new[] { new ProxyHealthyState(), new ProxyRotatingState(), new NetworkIsolatedState() };

    public async Task<ModuleValidationResult> ValidateAsync(ModuleContext context, CancellationToken ct)
    {
        var errors = new List<ModuleValidationError>();

        if (_options.EnableProxyBinding && _proxyPools.Count == 0)
        {
            errors.Add(new ModuleValidationError("NO_PROXY_POOLS", "Proxy binding enabled but no proxy pools configured"));
        }

        foreach (var pool in _proxyPools.Values)
        {
            if (pool.Proxies.Count == 0)
            {
                errors.Add(new ModuleValidationError("EMPTY_PROXY_POOL", $"Proxy pool {pool.PoolId} has no proxies"));
            }

            foreach (var proxy in pool.Proxies)
            {
                if (string.IsNullOrEmpty(proxy.Host) || proxy.Port <= 0)
                {
                    errors.Add(new ModuleValidationError("INVALID_PROXY", $"Proxy {proxy.ProxyId} has invalid host/port"));
                }
            }
        }

        return new ModuleValidationResult(errors.Count == 0, errors);
    }

    // ==================== PROXY BINDING ====================

    /// <summary>
    /// Binds a proxy to an instance.
    /// </summary>
    public async Task<BindResult> BindProxyAsync(string instanceId, string proxyId, CancellationToken ct = default)
    {
        using var activity = KkbotActivitySources.Plugins.StartActivity("proxy.bind");
        activity?.SetTag("instance.id", instanceId);
        activity?.SetTag("proxy.id", proxyId);

        if (!_proxyConfigs.TryGetValue(proxyId, out var proxy))
        {
            return BindResult.Failed($"Proxy {proxyId} not found");
        }

        if (!proxy.Enabled)
        {
            return BindResult.Failed($"Proxy {proxyId} is disabled");
        }

        // Check if instance already has a proxy
        if (_instanceToProxy.TryGetValue(instanceId, out var existingProxyId))
        {
            if (existingProxyId == proxyId)
            {
                return BindResult.Success($"Instance {instanceId} already bound to proxy {proxyId}");
            }

            // Unbind from current proxy first
            await UnbindProxyAsync(instanceId, ct);
        }

        // Apply network isolation if enabled
        if (_options.EnableNetworkIsolation)
        {
            await ApplyNetworkIsolationAsync(instanceId, proxyId, ct);
        }

        // Create HTTP client with proxy
        var httpClient = CreateHttpClientWithProxy(proxy);
        _httpClients[instanceId] = httpClient;

        _instanceToProxy[instanceId] = proxyId;

        // Update proxy health
        if (_proxyHealth.TryGetValue(proxyId, out var health))
        {
            _proxyHealth[proxyId] = health with { ActiveConnections = health.ActiveConnections + 1 };
        }

        _logger.LogInformation("Bound proxy {ProxyId} to instance {InstanceId}", proxyId, instanceId);
        return BindResult.Success($"Proxy {proxyId} bound to instance {instanceId}");
    }

    /// <summary>
    /// Unbinds proxy from instance.
    /// </summary>
    public async Task<UnbindResult> UnbindProxyAsync(string instanceId, CancellationToken ct = default)
    {
        if (!_instanceToProxy.TryRemove(instanceId, out var proxyId))
        {
            return UnbindResult.Success($"Instance {instanceId} was not bound to any proxy");
        }

        if (_proxyHealth.TryGetValue(proxyId, out var health))
        {
            _proxyHealth[proxyId] = health with { ActiveConnections = Math.Max(0, health.ActiveConnections - 1) };
        }

        // Remove HTTP client
        if (_httpClients.TryRemove(instanceId, out var httpClient))
        {
            httpClient.Dispose();
        }

        // Remove network isolation
        if (_options.EnableNetworkIsolation)
        {
            await RemoveNetworkIsolationAsync(instanceId, ct);
        }

        _logger.LogInformation("Unbound proxy {ProxyId} from instance {InstanceId}", proxyId, instanceId);
        return UnbindResult.Success($"Proxy {proxyId} unbound from instance {instanceId}");
    }

    /// <summary>
    /// Gets the proxy bound to an instance.
    /// </summary>
    public ProxyConfig? GetBoundProxy(string instanceId)
    {
        if (_instanceToProxy.TryGetValue(instanceId, out var proxyId))
        {
            _proxyConfigs.TryGetValue(proxyId, out var proxy);
            return proxy;
        }
        return null;
    }

    /// <summary>
    /// Gets HTTP client for an instance (with proxy).
    /// </summary>
    public HttpClient? GetHttpClient(string instanceId)
    {
        _httpClients.TryGetValue(instanceId, out var client);
        return client;
    }

    // ==================== PROXY POOLS ====================

    /// <summary>
    /// Creates a new proxy pool.
    /// </summary>
    public async Task<PoolResult> CreateProxyPoolAsync(ProxyPoolConfig pool, CancellationToken ct = default)
    {
        if (_proxyPools.ContainsKey(pool.PoolId))
        {
            return PoolResult.Failed($"Pool {pool.PoolId} already exists");
        }

        _proxyPools[pool.PoolId] = pool;

        foreach (var proxy in pool.Proxies)
        {
            _proxyConfigs[proxy.ProxyId] = proxy;
            _proxyHealth[proxy.ProxyId] = new ProxyHealth
            {
                ProxyId = proxy.ProxyId,
                IsHealthy = true,
                ConsecutiveFailures = 0,
                ConsecutiveSuccesses = 0,
                AverageLatencyMs = 0,
                LastCheck = DateTimeOffset.UtcNow
            };
        }

        _logger.LogInformation("Created proxy pool {PoolId} with {ProxyCount} proxies", pool.PoolId, pool.Proxies.Count);
        return PoolResult.Success($"Pool {pool.PoolId} created");
    }

    /// <summary>
    /// Adds a proxy to a pool.
    /// </summary>
    public async Task<ProxyResult> AddProxyToPoolAsync(string poolId, ProxyConfig proxy, CancellationToken ct = default)
    {
        if (!_proxyPools.TryGetValue(poolId, out var pool))
        {
            return ProxyResult.Failed($"Pool {poolId} not found");
        }

        if (pool.Proxies.Any(p => p.ProxyId == proxy.ProxyId))
        {
            return ProxyResult.Failed($"Proxy {proxy.ProxyId} already exists in pool {poolId}");
        }

        var updatedPool = pool with { Proxies = pool.Proxies.Append(proxy).ToList() };
        _proxyPools[poolId] = updatedPool;
        _proxyConfigs[proxy.ProxyId] = proxy;
        _proxyHealth[proxy.ProxyId] = new ProxyHealth
        {
            ProxyId = proxy.ProxyId,
            IsHealthy = true,
            ConsecutiveFailures = 0,
            ConsecutiveSuccesses = 0,
            AverageLatencyMs = 0,
            LastCheck = DateTimeOffset.UtcNow
        };

        _logger.LogInformation("Added proxy {ProxyId} to pool {PoolId}", proxy.ProxyId, poolId);
        return ProxyResult.Success($"Proxy {proxy.ProxyId} added to pool {poolId}");
    }

    /// <summary>
    /// Gets the next proxy from a pool based on rotation strategy.
    /// </summary>
    public ProxyConfig? GetNextProxy(string poolId)
    {
        if (!_proxyPools.TryGetValue(poolId, out var pool) || pool.Proxies.Count == 0)
        {
            return null;
        }

        var healthyProxies = pool.Proxies
            .Where(p => _proxyHealth.TryGetValue(p.ProxyId, out var h) && h.IsHealthy)
            .ToList();

        if (!healthyProxies.Any())
        {
            _logger.LogWarning("No healthy proxies in pool {PoolId}", poolId);
            return null;
        }

        return pool.RotationStrategy switch
        {
            RotationStrategy.RoundRobin => healthyProxies[_random.Next(healthyProxies.Count)],
            RotationStrategy.Random => healthyProxies[_random.Next(healthyProxies.Count)],
            RotationStrategy.LeastUsed => healthyProxies.OrderBy(p => _proxyHealth.TryGetValue(p.ProxyId, out var h) ? h.ActiveConnections : 0).First(),
            RotationStrategy.LowestLatency => healthyProxies.OrderBy(p => _proxyHealth.TryGetValue(p.ProxyId, out var h) ? h.AverageLatencyMs : double.MaxValue).First(),
            RotationStrategy.Sticky => healthyProxies.First(),
            _ => healthyProxies.First()
        };
    }

    /// <summary>
    /// Auto-assigns a proxy from the default pool to an instance.
    /// </summary>
    public async Task<BindResult> AutoBindProxyAsync(string instanceId, CancellationToken ct = default)
    {
        var proxy = GetNextProxy(_options.DefaultProxyPoolId);
        if (proxy == null)
        {
            return BindResult.Failed("No available proxies in default pool");
        }

        return await BindProxyAsync(instanceId, proxy.ProxyId, ct);
    }

    // ==================== NETWORK ISOLATION ====================

    /// <summary>
    /// Applies network isolation for an instance.
    /// </summary>
    public async Task<IsolationResult> ApplyNetworkIsolationAsync(string instanceId, string proxyId, CancellationToken ct = default)
    {
        using var activity = KkbotActivitySources.Plugins.StartActivity("network.isolation.apply");
        activity?.SetTag("instance.id", instanceId);
        activity?.SetTag("proxy.id", proxyId);

        if (!_proxyConfigs.TryGetValue(proxyId, out var proxy))
        {
            return IsolationResult.Failed($"Proxy {proxyId} not found");
        }

        var isolationConfig = new NetworkIsolationConfig
        {
            InstanceId = instanceId,
            Mode = _options.DefaultIsolationMode,
            NetworkNamespace = $"kkbot-{instanceId}",
            AllowedDestinations = new List<string> { proxy.Host },
            BlockWebRTC = _options.BlockWebRTCByDefault,
            BlockDirectSockets = _options.BlockDirectSocketsByDefault,
            ForceProxyForAllTraffic = true
        };

        _isolationConfigs[instanceId] = isolationConfig;

        // In real implementation, this would:
        // 1. Create Linux network namespace (if Mode == NetworkNamespace)
        // 2. Configure iptables/nftables rules to route through proxy
        // 3. Block WebRTC via browser settings or system-level blocking
        // 4. Block direct socket connections via firewall rules
        // 5. Configure routing tables

        _logger.LogInformation("Applied network isolation for instance {InstanceId} with proxy {ProxyId} (Mode: {Mode})", 
            instanceId, proxyId, _options.DefaultIsolationMode);

        return IsolationResult.Success($"Network isolation applied for instance {instanceId}");
    }

    /// <summary>
    /// Removes network isolation for an instance.
    /// </summary>
    public async Task<IsolationResult> RemoveNetworkIsolationAsync(string instanceId, CancellationToken ct = default)
    {
        if (!_isolationConfigs.TryRemove(instanceId, out _))
        {
            return IsolationResult.Success("No isolation config to remove");
        }

        // In real implementation, this would:
        // 1. Remove iptables/nftables rules
        // 2. Delete network namespace
        // 3. Restore default routing

        _logger.LogInformation("Removed network isolation for instance {InstanceId}", instanceId);
        return IsolationResult.Success($"Network isolation removed for instance {instanceId}");
    }

    /// <summary>
    /// Configures isolation for an instance.
    /// </summary>
    public async Task<IsolationResult> ConfigureIsolationAsync(string instanceId, NetworkIsolationConfig config, CancellationToken ct = default)
    {
        _isolationConfigs[instanceId] = config;
        _logger.LogInformation("Updated isolation config for instance {InstanceId}: Mode={Mode}", instanceId, config.Mode);
        return IsolationResult.Success($"Isolation config updated for instance {instanceId}");
    }

    // ==================== PROXY HEALTH & ROTATION ====================

    /// <summary>
    /// Performs health check on all proxies.
    /// </summary>
    private async void HealthCheckAsync(object? state)
    {
        if (_shutdownCts.Token.IsCancellationRequested) return;

        var tasks = _proxyConfigs.Keys.Select(async proxyId =>
        {
            if (!_proxyConfigs.TryGetValue(proxyId, out var proxy))
                return;

            var stopwatch = Stopwatch.StartNew();
            bool isHealthy = false;
            string? error = null;

            try
            {
                var httpClient = CreateHttpClientWithProxy(_proxyConfigs[proxyId]);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(_shutdownCts.Token);
                cts.CancelAfter(TimeSpan.FromSeconds(_options.HealthCheckTimeoutSeconds));

                var response = await httpClient.GetAsync("http://httpbin.org/ip", cts.Token);
                isHealthy = response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                isHealthy = false;
                error = ex.Message;
            }
            finally
            {
                stopwatch.Stop();
            }

            await UpdateProxyHealthAsync(proxyId, isHealthy, stopwatch.ElapsedMilliseconds, error);
        });

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Updates proxy health status.
    /// </summary>
    private async Task UpdateProxyHealthAsync(string proxyId, bool isHealthy, double latencyMs, string? error)
    {
        _proxyHealth.AddOrUpdate(proxyId,
            new ProxyHealth
            {
                ProxyId = proxyId,
                IsHealthy = isHealthy,
                ConsecutiveFailures = isHealthy ? 0 : 1,
                ConsecutiveSuccesses = isHealthy ? 1 : 0,
                AverageLatencyMs = latencyMs,
                LastCheck = DateTimeOffset.UtcNow,
                LastSuccess = isHealthy ? DateTimeOffset.UtcNow : DateTimeOffset.MinValue,
                LastFailure = isHealthy ? DateTimeOffset.MinValue : DateTimeOffset.UtcNow,
                LastError = error
            },
            (key, existing) => existing with
            {
                IsHealthy = isHealthy,
                ConsecutiveFailures = isHealthy ? 0 : existing.ConsecutiveFailures + 1,
                ConsecutiveSuccesses = isHealthy ? existing.ConsecutiveSuccesses + 1 : 0,
                AverageLatencyMs = (existing.AverageLatencyMs * existing.ConsecutiveSuccesses + latencyMs) / (existing.ConsecutiveSuccesses + 1),
                LastCheck = DateTimeOffset.UtcNow,
                LastSuccess = isHealthy ? DateTimeOffset.UtcNow : existing.LastSuccess,
                LastFailure = isHealthy ? existing.LastFailure : DateTimeOffset.UtcNow,
                LastError = error ?? existing.LastError
            });

        // Check if proxy should be marked unhealthy
        var health = _proxyHealth[proxyId];
        if (!isHealthy && health.ConsecutiveFailures >= _options.MaxConsecutiveFailures)
        {
            _logger.LogWarning("Proxy {ProxyId} marked unhealthy after {Failures} consecutive failures", proxyId, health.ConsecutiveFailures);
            await TriggerRotationAsync(proxyId, RotationReason.HealthCheckFailed);
        }
        else if (latencyMs > _options.MaxLatencyThresholdMs && isHealthy)
        {
            _logger.LogWarning("Proxy {ProxyId} latency {Latency}ms exceeds threshold {Threshold}ms", proxyId, latencyMs, _options.MaxLatencyThresholdMs);
            await TriggerRotationAsync(proxyId, RotationReason.LatencyThreshold);
        }
    }

    /// <summary>
    /// Checks if any proxies need rotation.
    /// </summary>
    private async void CheckRotationAsync(object? state)
    {
        if (_shutdownCts.Token.IsCancellationRequested) return;

        foreach (var pool in _proxyPools.Values)
        {
            if (pool.RotationStrategy == RotationStrategy.Scheduled)
            {
                // Check if minimum rotation interval has passed
                // In real implementation, track last rotation time per pool
            }
        }
    }

    /// <summary>
    /// Triggers proxy rotation for a specific proxy.
    /// </summary>
    public async Task<RotationResult> TriggerRotationAsync(string proxyId, RotationReason reason, CancellationToken ct = default)
    {
        using var activity = KkbotActivitySources.Plugins.StartActivity("proxy.rotate");
        activity?.SetTag("proxy.id", proxyId);
        activity?.SetTag("reason", reason.ToString());

        if (!_proxyConfigs.TryGetValue(proxyId, out var oldProxy))
        {
            return RotationResult.Failed($"Proxy {proxyId} not found");
        }

        // Find the pool this proxy belongs to
        var pool = _proxyPools.Values.FirstOrDefault(p => p.Proxies.Any(p => p.ProxyId == proxyId));
        if (pool == null)
        {
            return RotationResult.Failed($"Pool for proxy {proxyId} not found");
        }

        // Get next healthy proxy
        var newProxy = GetNextProxy(pool.PoolId);
        if (newProxy == null || newProxy.ProxyId == proxyId)
        {
            return RotationResult.Failed("No alternative proxy available");
        }

        // Update all instances using this proxy
        var affectedInstances = _instanceToProxy
            .Where(kvp => kvp.Value == proxyId)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var instanceId in affectedInstances)
        {
            await UnbindProxyAsync(instanceId, ct);
            await BindProxyAsync(instanceId, newProxy.ProxyId, ct);
        }

        // Publish rotation event
        var rotationEvent = new ProxyRotationEvent
        {
            ProxyId = proxyId,
            PoolId = pool.PoolId,
            Reason = reason,
            OldProxyId = proxyId,
            NewProxyId = newProxy.ProxyId
        };

        await _messageBus.PublishAsync(new AgentMessage
        {
            Type = MessageType.ProxyRotated,
            PayloadType = nameof(ProxyRotationEvent),
            Payload = JsonSerializer.Serialize(rotationEvent)
        }, ct);

        _logger.LogInformation("Rotated proxy {OldProxyId} -> {NewProxyId} for {InstanceCount} instances (Reason: {Reason})",
            proxyId, newProxy.ProxyId, affectedInstances.Count, reason);

        return RotationResult.Success($"Rotated to {newProxy.ProxyId}", newProxy.ProxyId);
    }

    /// <summary>
    /// Manually triggers rotation for an instance.
    /// </summary>
    public async Task<BindResult> RotateInstanceProxyAsync(string instanceId, CancellationToken ct = default)
    {
        if (!_instanceToProxy.TryGetValue(instanceId, out var currentProxyId))
        {
            return BindResult.Failed($"Instance {instanceId} not bound to any proxy");
        }

        var pool = _proxyPools.Values.FirstOrDefault(p => p.Proxies.Any(p => p.ProxyId == currentProxyId));
        if (pool == null)
        {
            return BindResult.Failed($"No pool found for current proxy {currentProxyId}");
        }

        var newProxy = GetNextProxy(pool.PoolId);
        if (newProxy == null || newProxy.ProxyId == currentProxyId)
        {
            return BindResult.Failed("No alternative proxy available");
        }

        await UnbindProxyAsync(instanceId, ct);
        return await BindProxyAsync(instanceId, newProxy.ProxyId, ct);
    }

    // ==================== METRICS & MONITORING ====================

    private async void ReportMetricsAsync(object? state)
    {
        if (_shutdownCts.Token.IsCancellationRequested) return;

        foreach (var health in _proxyHealth.Values)
        {
            _metrics.RecordHttpRequestDuration(health.AverageLatencyMs);
            if (!health.IsHealthy)
            {
                _metrics.IncrementHttpRequestsFailed();
            }
        }

        _metrics.RecordHttpRequests(_proxyConfigs.Count);
        _metrics.RecordHttpRequests(_instanceToProxy.Count);
    }

    // ==================== HTTP CLIENT CREATION ====================

    private HttpClient CreateHttpClientWithProxy(ProxyConfig proxy)
    {
        var handler = new SocketsHttpHandler
        {
            Proxy = new WebProxy($"{proxy.Protocol.ToString().ToLower()}://{proxy.Host}:{proxy.Port}")
            {
                Credentials = !string.IsNullOrEmpty(proxy.Username) 
                    ? new NetworkCredential(proxy.Username, proxy.Password, proxy.Domain ?? string.Empty)
                    : null,
                BypassProxyOnLocal = false
            },
            UseProxy = true,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 100,
            EnableMultipleHttp2Connections = true,
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMilliseconds(proxy.ReadWriteTimeoutMs)
        };

        return client;
    }

    // ==================== NETWORK ISOLATION HELPERS ====================

    /// <summary>
    /// Checks if a destination is allowed for an instance.
    /// </summary>
    public bool IsDestinationAllowed(string instanceId, string destination)
    {
        if (!_isolationConfigs.TryGetValue(instanceId, out var config))
        {
            return true; // No isolation = allow all
        }

        if (config.BlockedDestinations.Any(d => IsMatch(destination, d)))
        {
            return false;
        }

        if (config.AllowedDestinations.Count > 0 && !config.AllowedDestinations.Any(d => IsMatch(destination, d)))
        {
            return false;
        }

        return true;
    }

    private bool IsMatch(string destination, string pattern)
    {
        // Simple CIDR or domain matching
        if (pattern.Contains("/"))
        {
            // CIDR notation
            return IPAddress.TryParse(destination, out var ip) && IPNetwork.Parse(pattern).Contains(ip);
        }
        
        // Domain matching (wildcard support)
        if (pattern.StartsWith("*."))
        {
            var domain = pattern.Substring(2);
            return destination.EndsWith(domain, StringComparison.OrdinalIgnoreCase) || 
                   destination.Equals(domain.Substring(1), StringComparison.OrdinalIgnoreCase);
        }

        return destination.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }

    // ==================== MESSAGE HANDLING ====================

    public async Task HandleMessageAsync(AgentMessage message, CancellationToken ct)
    {
        switch (message.Type)
        {
            case MessageType.ProxyBindRequest:
                var bindReq = JsonSerializer.Deserialize<ProxyBindRequest>(message.Payload);
                if (bindReq != null)
                {
                    var result = await BindProxyAsync(bindReq.InstanceId, bindReq.ProxyId, ct);
                    // Send response
                }
                break;

            case MessageType.ProxyRotateRequest:
                var rotateReq = JsonSerializer.Deserialize<ProxyRotateRequest>(message.Payload);
                if (rotateReq != null)
                {
                    if (!string.IsNullOrEmpty(rotateReq.InstanceId))
                    {
                        await RotateInstanceProxyAsync(rotateReq.InstanceId, ct);
                    }
                    else if (!string.IsNullOrEmpty(rotateReq.ProxyId))
                    {
                        await TriggerRotationAsync(rotateReq.ProxyId, RotationReason.ManualTrigger, ct);
                    }
                }
                break;

            case MessageType.ProxyUnbindRequest:
                var unbindReq = JsonSerializer.Deserialize<ProxyUnbindRequest>(message.Payload);
                if (unbindReq != null)
                {
                    await UnbindProxyAsync(unbindReq.InstanceId, ct);
                }
                break;

            case MessageType.IsolationConfigRequest:
                var isoReq = JsonSerializer.Deserialize<IsolationConfigRequest>(message.Payload);
                if (isoReq != null)
                {
                    await ConfigureIsolationAsync(isoReq.InstanceId, isoReq.Config, ct);
                }
                break;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _healthCheckTimer?.Dispose();
        _rotationTimer?.Dispose();
        _metricsTimer?.Dispose();
        _shutdownCts.Cancel();
        _shutdownCts.Dispose();

        foreach (var client in _httpClients.Values)
        {
            client.Dispose();
        }
    }
}

// ==================== RESULT TYPES ====================

public sealed record BindResult(bool Success, string? Message, string? ProxyId = null)
{
    public static BindResult Success(string message, string? proxyId = null) => new(true, message, proxyId);
    public static BindResult Failed(string message) => new(false, message);
}

public sealed record UnbindResult(bool Success, string? Message)
{
    public static UnbindResult Success(string message) => new(true, message);
    public static UnbindResult Failed(string message) => new(false, message);
}

public sealed record PoolResult(bool Success, string? Message)
{
    public static PoolResult Success(string message) => new(true, message);
    public static PoolResult Failed(string message) => new(false, message);
}

public sealed record ProxyResult(bool Success, string? Message)
{
    public static ProxyResult Success(string message) => new(true, message);
    public static ProxyResult Failed(string message) => new(false, message);
}

public sealed record IsolationResult(bool Success, string? Message)
{
    public static IsolationResult Success(string message) => new(true, message);
    public static IsolationResult Failed(string message) => new(false, message);
}

public sealed record RotationResult(bool Success, string? Message, string? NewProxyId = null)
{
    public static RotationResult Success(string message, string? newProxyId = null) => new(true, message, newProxyId);
    public static RotationResult Failed(string message) => new(false, message);
}

// ==================== REQUEST/RESPONSE TYPES ====================

public sealed record ProxyBindRequest(string InstanceId, string ProxyId);
public sealed record ProxyRotateRequest(string? InstanceId, string? ProxyId);
public sealed record ProxyUnbindRequest(string InstanceId);
public sealed record IsolationConfigRequest(string InstanceId, NetworkIsolationConfig Config);

// ==================== TASKS & STATES ====================

public sealed class ProxyHealthCheckTask : IGameTaskDefinition
{
    public string Id => "proxy-health-check";
    public string Name => "Proxy Health Check";
    public string? Description => "Performs health check on all proxies";
    public TaskType Type => TaskType.Repeating;
    public TimeSpan DefaultTimeout => TimeSpan.FromSeconds(30);
    public RetryPolicy DefaultRetryPolicy => new(1, 5000);
    public bool RequiresApproval => false;

    public async Task<IStateDefinition> ExecuteAsync(TaskContext context, CancellationToken ct)
    {
        return new ProxyHealthyState();
    }
}

public sealed class ProxyRotationTask : IGameTaskDefinition
{
    public string Id => "proxy-rotation";
    public string Name => "Proxy Rotation";
    public string? Description => "Rotates proxies based on health and strategy";
    public TaskType Type => TaskType.Repeating;
    public TimeSpan DefaultTimeout => TimeSpan.FromMinutes(5);
    public RetryPolicy DefaultRetryPolicy => new(1, 30000);
    public bool RequiresApproval => false;

    public async Task<IStateDefinition> ExecuteAsync(TaskContext context, CancellationToken ct)
    {
        return new ProxyHealthyState();
    }
}

public sealed class NetworkIsolationTask : IGameTaskDefinition
{
    public string Id => "network-isolation";
    public string Name => "Network Isolation";
    public string? Description => "Applies network isolation for instances";
    public TaskType Type => TaskType.Repeating;
    public TimeSpan DefaultTimeout => TimeSpan.FromMinutes(1);
    public RetryPolicy DefaultRetryPolicy => new(1, 10000);
    public bool RequiresApproval => false;

    public async Task<IStateDefinition> ExecuteAsync(TaskContext context, CancellationToken ct)
    {
        return new NetworkIsolatedState();
    }
}

public sealed class ProxyHealthyState : IStateDefinition
{
    public string Id => "proxy-healthy";
    public string Name => "Proxy Healthy";
    public string Category => "network";
    public Func<PerceptionContext?, bool> EntryCondition => _ => true;
    public IStateAction? Action => null;
    public IReadOnlyList<string> NextStateIds => new[] { "proxy-unhealthy", "proxy-rotating" };
    public TimeSpan Timeout => TimeSpan.FromMinutes(5);
    public RetryPolicy RetryPolicy => new(0, 0);
    public EvidencePolicy EvidencePolicy => new(false, false);
}

public sealed class ProxyRotatingState : IStateDefinition
{
    public string Id => "proxy-rotating";
    public string Name => "Proxy Rotating";
    public string Category => "network";
    public Func<PerceptionContext?, bool> EntryCondition => _ => false; // Triggered by coordinator
    public IStateAction? Action => null;
    public IReadOnlyList<string> NextStateIds => new[] { "proxy-healthy" };
    public TimeSpan Timeout => TimeSpan.FromMinutes(2);
    public RetryPolicy RetryPolicy => new(0, 0);
    public EvidencePolicy EvidencePolicy => new(false, false);
}

public sealed class NetworkIsolatedState : IStateDefinition
{
    public string Id => "network-isolated";
    public string Name => "Network Isolated";
    public string Category => "network";
    public Func<PerceptionContext?, bool> EntryCondition => _ => true;
    public IStateAction? Action => null;
    public IReadOnlyList<string> NextStateIds => Array.Empty<string>();
    public TimeSpan Timeout => TimeSpan.FromHours(1);
    public RetryPolicy RetryPolicy => new(0, 0);
    public EvidencePolicy EvidencePolicy => new(false, false);
}

// ==================== DI EXTENSIONS ====================

public static class NetworkProxyExtensions
{
    public static IServiceCollection AddKkbotNetworkProxy(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<NetworkProxyModuleOptions>(configuration.GetSection("NetworkProxy"));

        services.AddSingleton<NetworkProxyModule>();
        services.AddScoped<INetworkProxyService, NetworkProxyService>();

        return services;
    }
}

public interface INetworkProxyService
{
    Task<BindResult> BindProxyAsync(string instanceId, string proxyId, CancellationToken ct = default);
    Task<UnbindResult> UnbindProxyAsync(string instanceId, CancellationToken ct = default);
    Task<BindResult> AutoBindProxyAsync(string instanceId, CancellationToken ct = default);
    Task<BindResult> RotateInstanceProxyAsync(string instanceId, CancellationToken ct = default);
    Task<IsolationResult> ApplyNetworkIsolationAsync(string instanceId, string proxyId, CancellationToken ct = default);
    Task<IsolationResult> RemoveNetworkIsolationAsync(string instanceId, CancellationToken ct = default);
    Task<RotationResult> TriggerRotationAsync(string proxyId, RotationReason reason, CancellationToken ct = default);
    ProxyConfig? GetBoundProxy(string instanceId);
    HttpClient? GetHttpClient(string instanceId);
    bool IsDestinationAllowed(string instanceId, string destination);
}

public sealed class NetworkProxyService : INetworkProxyService
{
    private readonly NetworkProxyModule _module;

    public NetworkProxyService(NetworkProxyModule module)
    {
        _module = module;
    }

    public Task<BindResult> BindProxyAsync(string instanceId, string proxyId, CancellationToken ct = default)
        => _module.BindProxyAsync(instanceId, proxyId, ct);

    public Task<UnbindResult> UnbindProxyAsync(string instanceId, CancellationToken ct = default)
        => _module.UnbindProxyAsync(instanceId, ct);

    public Task<BindResult> AutoBindProxyAsync(string instanceId, CancellationToken ct = default)
        => _module.AutoBindProxyAsync(instanceId, ct);

    public Task<BindResult> RotateInstanceProxyAsync(string instanceId, CancellationToken ct = default)
        => _module.RotateInstanceProxyAsync(instanceId, ct);

    public Task<IsolationResult> ApplyNetworkIsolationAsync(string instanceId, string proxyId, CancellationToken ct = default)
        => _module.ApplyNetworkIsolationAsync(instanceId, proxyId, ct);

    public Task<IsolationResult> RemoveNetworkIsolationAsync(string instanceId, CancellationToken ct = default)
        => _module.RemoveNetworkIsolationAsync(instanceId, ct);

    public Task<RotationResult> TriggerRotationAsync(string proxyId, RotationReason reason, CancellationToken ct = default)
        => _module.TriggerRotationAsync(proxyId, reason, ct);

    public ProxyConfig? GetBoundProxy(string instanceId)
        => _module.GetBoundProxy(instanceId);

    public HttpClient? GetHttpClient(string instanceId)
        => _module.GetHttpClient(instanceId);

    public bool IsDestinationAllowed(string instanceId, string destination)
        => _module.IsDestinationAllowed(instanceId, destination);
}