// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using KKBOT.Agent.Coordination.Abstractions;
using KKBOT.Observability;
using KKBOT.Observability.Tracing;

namespace KKBOT.Agent.Coordination;

/// <summary>
/// Redis-backed message bus for inter-agent communication.
/// </summary>
public sealed class RedisMessageBus : IMessageBus, IAsyncDisposable
{
    private readonly ILogger<RedisMessageBus> _logger;
    private readonly RedisMessageBusOptions _options;
    private readonly IConnectionMultiplexer _redis;
    private readonly ISubscriber _subscriber;
    private readonly IDatabase _db;
    private readonly ConcurrentDictionary<string, MessageHandler> _handlers = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<AgentMessage>> _pendingRequests = new();
    private readonly IMetricsRegistry _metrics;
    private bool _disposed;

    private const string MessageChannelPrefix = "kkbot:messages:";
    private const string BroadcastChannel = "kkbot:messages:broadcast";
    private const string RequestChannelPrefix = "kkbot:requests:";
    private const string ResponseChannelPrefix = "kkbot:responses:";

    public RedisMessageBus(
        ILogger<RedisMessageBus> logger,
        IOptions<RedisMessageBusOptions> options,
        IConnectionMultiplexer redis,
        IMetricsRegistry metrics)
    {
        _logger = logger;
        _options = options.Value;
        _redis = redis;
        _subscriber = redis.GetSubscriber();
        _db = redis.GetDatabase();
        _metrics = metrics;

        // Subscribe to broadcast channel
        _ = _subscriber.SubscribeAsync(BroadcastChannel, OnBroadcastReceived);
    }

    public async Task PublishAsync(AgentMessage message, CancellationToken ct = default)
    {
        using var activity = KkbotActivitySources.Coordination.StartActivity("messagebus.publish");
        activity?.SetTag("message.type", message.Type.ToString());
        activity?.SetTag("message.from", message.FromAgentId);
        activity?.SetTag("message.to", message.ToAgentId);

        try
        {
            var payload = JsonSerializer.Serialize(message);
            var channel = string.IsNullOrEmpty(message.ToAgentId) 
                ? BroadcastChannel 
                : $"{MessageChannelPrefix}{message.ToAgentId}";

            await _subscriber.PublishAsync(channel, JsonSerializer.Serialize(message));

            _metrics.IncrementHttpRequests(); // Reuse metric

            if (_options.EnableTracing)
            {
                _logger.LogDebug("Published message {MessageId} of type {Type} to {Channel}", 
                    message.MessageId, message.Type, channel);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message {MessageId}", message.MessageId);
            throw;
        }
    }

    public async Task SubscribeAsync(
        string agentId, 
        MessageType[] messageTypes, 
        Func<AgentMessage, CancellationToken, Task> handler, 
        CancellationToken ct = default)
    {
        var handlerKey = $"{agentId}:{string.Join(",", messageTypes)}";
        var handler = new MessageHandler
        {
            AgentId = agentId,
            MessageTypes = new HashSet<MessageType>(messageTypes),
            Handler = handler
        };

        _handlers[handlerKey] = handler;

        // Subscribe to agent-specific channel
        var channel = $"{MessageChannelPrefix}{agentId}";
        await _subscriber.SubscribeAsync(channel, (ch, msg) => OnMessageReceived(agentId, msg));

        _logger.LogDebug("Agent {AgentId} subscribed to message types: {Types}", 
            agentId, string.Join(", ", messageTypes));
    }

    public async Task UnsubscribeAsync(string agentId, CancellationToken ct = default)
    {
        var keysToRemove = _handlers.Keys.Where(k => k.StartsWith($"{agentId}:")).ToList();
        foreach (var key in keysToRemove)
        {
            _handlers.TryRemove(key, out _);
        }

        // Unsubscribe from channel
        var channel = $"{MessageChannelPrefix}{agentId}";
        await _subscriber.UnsubscribeAsync(channel);

        _logger.LogDebug("Agent {AgentId} unsubscribed from all message types", agentId);
    }

    public async Task<AgentMessage?> RequestResponseAsync(
        AgentMessage request, 
        TimeSpan timeout, 
        CancellationToken ct = default)
    {
        using var activity = KkbotActivitySources.Coordination.StartActivity("messagebus.request_response");
        activity?.SetTag("request.id", request.MessageId);

        var tcs = new TaskCompletionSource<AgentMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestKey = $"{request.CorrelationId}:{request.MessageId}";
        
        _pendingRequests[requestKey] = tcs;

        // Set up response listener
        var responseChannel = $"{ResponseChannelPrefix}{request.FromAgentId}:{request.CorrelationId}";
        await _subscriber.SubscribeAsync(responseChannel, (ch, msg) => OnResponseReceived(requestKey, msg));

        // Send request
        request.Headers["x-response-channel"] = responseChannel;
        await PublishAsync(request, ct);

        // Wait for response or timeout
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            var response = await tcs.Task.WaitAsync(linkedCts.Token);
            return response;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Request {RequestId} timed out after {Timeout}", request.MessageId, timeout);
            return null;
        }
        finally
        {
            _pendingRequests.TryRemove(requestKey, out _);
            await _subscriber.UnsubscribeAsync($"{ResponseChannelPrefix}{request.FromAgentId}:{request.CorrelationId}");
        }
    }

    public async Task BroadcastAsync(AgentMessage message, CancellationToken ct = default)
    {
        var broadcastMessage = message with { ToAgentId = string.Empty };
        await PublishAsync(broadcastMessage, ct);
    }

    private async Task OnBroadcastReceived(RedisChannel channel, RedisValue message)
    {
        try
        {
            var message = JsonSerializer.Deserialize<AgentMessage>(message!);
            if (message == null) return;

            // Deliver to all matching handlers
            foreach (var handler in _handlers.Values)
            {
                if (handler.MessageTypes.Contains(message.Type) || handler.MessageTypes.Count == 0)
                {
                    try
                    {
                        await handler.Handler(message, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in message handler for agent {AgentId}", handler.AgentId);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process broadcast message");
        }
    }

    private async Task OnMessageReceived(string agentId, RedisValue message)
    {
        try
        {
            var message = JsonSerializer.Deserialize<AgentMessage>(message!);
            if (message == null) return;

            // Check if we have a handler for this agent and message type
            var handlerKey = $"{agentId}:{message.Type}";
            if (_handlers.TryGetValue(handlerKey, out var handler) || 
                _handlers.TryGetValue($"{agentId}:*", out handler) ||
                _handlers.TryGetValue("*", out handler))
            {
                if (handler.MessageTypes.Contains(message.Type) || handler.MessageTypes.Count == 0)
                {
                    await handler.Handler(message, CancellationToken.None);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process message for agent {AgentId}", agentId);
        }
    }

    private async Task OnResponseReceived(string requestKey, RedisValue message)
    {
        try
        {
            var response = JsonSerializer.Deserialize<AgentMessage>(message!);
            if (response == null) return;

            if (_pendingRequests.TryGetValue(requestKey, out var tcs))
            {
                tcs.TrySetResult(response);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process response for request {RequestKey}", requestKey);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        // Unsubscribe all
        await _subscriber.UnsubscribeAllAsync();
        _redis.Dispose();
    }
}

/// <summary>
/// Message handler registration.
/// </summary>
internal sealed class MessageHandler
{
    public string AgentId { get; init; } = string.Empty;
    public HashSet<MessageType> MessageTypes { get; init; } = new();
    public required Func<AgentMessage, CancellationToken, Task> Handler { get; init; }
}

/// <summary>
/// Options for Redis message bus.
/// </summary>
public sealed class RedisMessageBusOptions
{
    public bool EnableTracing { get; set; } = true;
    public int DefaultTimeoutSeconds { get; set; } = 30;
    public int MaxRetries { get; set; } = 3;
}