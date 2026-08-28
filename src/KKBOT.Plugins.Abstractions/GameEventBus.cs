// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KKBOT.Plugins.Abstractions;

/// <summary>
/// In-memory implementation of the game event bus.
/// </summary>
public sealed class GameEventBus : IGameEventBus
{
    private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();

    /// <inheritdoc />
    public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : GameEventArgs
    {
        var eventType = typeof(TEvent);
        var handlerList = _handlers.GetOrAdd(eventType, _ => new List<Delegate>());
        lock (handlerList)
        {
            handlerList.Add(handler);
        }

        return new Subscription(() =>
        {
            lock (handlerList)
            {
                handlerList.Remove(handler);
            }
        });
    }

    /// <inheritdoc />
    public void Unsubscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : GameEventArgs
    {
        var eventType = typeof(TEvent);
        if (_handlers.TryGetValue(eventType, out var handlerList))
        {
            lock (handlerList)
            {
                handlerList.Remove(handler);
            }
        }
    }

    /// <inheritdoc />
    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : GameEventArgs
    {
        var eventType = typeof(TEvent);
        if (_handlers.TryGetValue(eventType, out var handlerList))
        {
            List<Delegate> handlersToInvoke;
            lock (handlerList)
            {
                handlersToInvoke = new List<Delegate>(handlerList);
            }

            foreach (var handler in handlersToInvoke)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    if (handler is Func<TEvent, CancellationToken, Task> typedHandler)
                    {
                        await typedHandler(@event, cancellationToken);
                    }
                }
                catch (Exception)
                {
                    // Log error but continue with other handlers
                }
            }
        }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly Action _unsubscribe;

        public Subscription(Action unsubscribe)
        {
            _unsubscribe = unsubscribe;
        }

        public void Dispose() => _unsubscribe();
    }
}