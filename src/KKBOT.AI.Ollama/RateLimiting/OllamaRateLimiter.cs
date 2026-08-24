// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KKBOT.AI.Ollama.RateLimiting;

/// <summary>
/// Rate limiter for Ollama AI endpoints with token bucket algorithm.
/// </summary>
public sealed class OllamaRateLimiter : IDisposable
{
    private readonly OllamaRateLimiterOptions _options;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, TokenBucket> _buckets = new();
    private readonly Timer _refillTimer;
    private readonly CancellationTokenSource _shutdownCts = new();
    private bool _disposed;

    public OllamaRateLimiter(
        ILogger<OllamaRateLimiter> logger,
        IOptions<OllamaRateLimiterOptions> options)
    {
        _logger = logger;
        _options = options.Value;

        _refillTimer = new Timer(RefillBuckets, null, 
            TimeSpan.FromSeconds(1), 
            TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// Tries to acquire tokens for an AI request.
    /// Returns true if tokens were acquired, false if rate limited.
    /// </summary>
    public async ValueTask<bool> TryAcquireAsync(
        string modelName,
        int tokens = 1,
        CancellationToken cancellationToken = default)
    {
        var bucket = _buckets.GetOrAdd(modelName, 
            _ => new TokenBucket(_options.GetModelCapacity(modelName), 
                _options.GetModelRefillRate(modelName)));

        return await bucket.TryConsumeAsync(tokens, cancellationToken);
    }

    /// <summary>
    /// Waits until tokens are available for an AI request.
    /// </summary>
    public async ValueTask WaitForTokensAsync(
        string modelName,
        int tokens = 1,
        CancellationToken cancellationToken = default)
    {
        var bucket = _buckets.GetOrAdd(modelName,
            _ => new TokenBucket(_options.GetModelCapacity(modelName),
                _options.GetModelRefillRate(modelName)));

        await bucket.WaitForTokensAsync(tokens, cancellationToken);
    }

    /// <summary>
    /// Gets current token count for a model.
    /// </summary>
    public int GetAvailableTokens(string modelName)
    {
        if (_buckets.TryGetValue(modelName, out var bucket))
        {
            return bucket.AvailableTokens;
        }
        return _options.GetModelCapacity(modelName);
    }

    /// <summary>
    /// Gets current utilization percentage for a model.
    /// </summary>
    public double GetUtilization(string modelName)
    {
        if (_buckets.TryGetValue(modelName, out var bucket))
        {
            return 1.0 - (double)bucket.AvailableTokens / bucket.Capacity;
        }
        return 0.0;
    }

    private void RefillBuckets(object? state)
    {
        if (_disposed) return;

        foreach (var bucket in _buckets.Values)
        {
            bucket.Refill();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _shutdownCts.Cancel();
        _refillTimer?.Dispose();
    }
}

/// <summary>
/// Token bucket implementation for rate limiting.
/// </summary>
internal sealed class TokenBucket
{
    private readonly object _lock = new();
    private readonly int _capacity;
    private readonly double _refillRatePerSecond;
    private int _tokens;
    private DateTimeOffset _lastRefill = DateTimeOffset.UtcNow;

    public int Capacity => _capacity;
    public int AvailableTokens
    {
        get
        {
            lock (_lock)
            {
                RefillInternal();
                return _tokens;
            }
        }
    }

    public TokenBucket(int capacity, double refillRatePerSecond)
    {
        _capacity = capacity;
        _refillRatePerSecond = refillRatePerSecond;
        _tokens = capacity;
        _lastRefill = DateTimeOffset.UtcNow;
    }

    public bool TryConsume(int tokens, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            RefillInternal();
            
            if (_tokens >= tokens)
            {
                _tokens -= tokens;
                return true;
            }
            return false;
        }
    }

    public async ValueTask<bool> TryConsumeAsync(int tokens, CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            lock (_lock)
            {
                RefillInternal();
                
                if (_tokens >= tokens)
                {
                    _tokens -= tokens;
                    return true;
                }
            }

            // Wait a bit before retrying
            await Task.Delay(50, cancellationToken);
        }
        return false;
    }

    public async ValueTask WaitForTokensAsync(int tokens, CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (TryConsume(tokens, cancellationToken))
            {
                return;
            }
            await Task.Delay(50, cancellationToken);
        }
    }

    private void RefillInternal()
    {
        var now = DateTimeOffset.UtcNow;
        var elapsed = now - _lastRefill;
        var tokensToAdd = (int)(elapsed.TotalSeconds * _refillRatePerSecond);

        if (tokensToAdd > 0)
        {
            _tokens = Math.Min(_capacity, _tokens + tokensToAdd);
            _lastRefill = now;
        }
    }

    public void Refill()
    {
        lock (_lock)
        {
            RefillInternal();
        }
    }
}

/// <summary>
/// Options for Ollama rate limiter.
/// </summary>
public sealed class OllamaRateLimiterOptions
{
    private readonly Dictionary<string, ModelRateLimitConfig> _modelConfigs = new();

    public OllamaRateLimiterOptions()
    {
        // Default configurations for common models
        _modelConfigs["llama2"] = new ModelRateLimitConfig { Capacity = 100, RefillRatePerSecond = 10 };
        _modelConfigs["llama2:7b"] = new ModelRateLimitConfig { Capacity = 100, RefillRatePerSecond = 10 };
        _modelConfigs["llama2:13b"] = new ModelRateLimitConfig { Capacity = 50, RefillRatePerSecond = 5 };
        _modelConfigs["llama2:70b"] = new ModelRateLimitConfig { Capacity = 20, RefillRatePerSecond = 2 };
        _modelConfigs["codellama"] = new ModelRateLimitConfig { Capacity = 50, RefillRatePerSecond = 5 };
        _modelConfigs["mistral"] = new ModelRateLimitConfig { Capacity = 100, RefillRatePerSecond = 10 };
        _modelConfigs["mistral:7b"] = new ModelRateLimitConfig { Capacity = 100, RefillRatePerSecond = 10 };
        _modelConfigs["mixtral"] = new ModelRateLimitConfig { Capacity = 50, RefillRatePerSecond = 5 };
        _modelConfigs["phi"] = new ModelRateLimitConfig { Capacity = 100, RefillRatePerSecond = 10 };
        _modelConfigs["default"] = new ModelRateLimitConfig { Capacity = 50, RefillRatePerSecond = 5 };
    }

    public void SetModelConfig(string modelName, ModelRateLimitConfig config)
    {
        _modelConfigs[modelName.ToLowerInvariant()] = config;
    }

    public int GetModelCapacity(string modelName)
    {
        var key = modelName.ToLowerInvariant();
        return _modelConfigs.TryGetValue(key, out var config) ? config.Capacity : _modelConfigs["default"].Capacity;
    }

    public double GetModelRefillRate(string modelName)
    {
        var key = modelName.ToLowerInvariant();
        return _modelConfigs.TryGetValue(key, out var config) ? config.RefillRatePerSecond : _modelConfigs["default"].RefillRatePerSecond;
    }
}

/// <summary>
/// Model-specific rate limit configuration.
/// </summary>
public sealed class ModelRateLimitConfig
{
    public int Capacity { get; set; } = 50;
    public double RefillRatePerSecond { get; set; } = 5.0;
}

/// <summary>
/// Extension methods for registering Ollama rate limiter.
/// </summary>
public static class OllamaRateLimiterExtensions
{
    public static IServiceCollection AddOllamaRateLimiter(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OllamaRateLimiterOptions>(configuration.GetSection("OllamaRateLimiter"));
        services.AddSingleton<OllamaRateLimiter>();
        services.AddHostedService(sp => sp.GetRequiredService<OllamaRateLimiter>());
        return services;
    }
}

/// <summary>
/// Circuit breaker for Ollama AI calls.
/// </summary>
public sealed class OllamaCircuitBreaker
{
    private readonly CircuitBreaker _circuitBreaker;
    private readonly ILogger _logger;

    public OllamaCircuitBreaker(
        ILogger<OllamaCircuitBreaker> logger,
        IOptions<CircuitBreakerOptions> options)
    {
        _logger = logger;
        _circuitBreaker = new CircuitBreaker(
            loggerFactory.CreateLogger<CircuitBreaker>(),
            options);
    }

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        return await _circuitBreaker.ExecuteAsync(operation, cancellationToken);
    }

    public async Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
    {
        await _circuitBreaker.ExecuteAsync(operation, cancellationToken);
    }

    public CircuitBreakerState State => _circuitBreaker.State;

    public void Dispose() { }
}

/// <summary>
/// Extension methods for registering Ollama circuit breaker.
/// </summary>
public static class OllamaCircuitBreakerExtensions
{
    public static IServiceCollection AddOllamaCircuitBreaker(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CircuitBreakerOptions>(configuration.GetSection("OllamaCircuitBreaker"));
        services.AddSingleton<OllamaCircuitBreaker>();
        services.AddHostedService(sp => sp.GetRequiredService<OllamaCircuitBreaker>());
        return services;
    }
}