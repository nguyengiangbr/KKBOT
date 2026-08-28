// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KKBOT.Plugins.Abstractions;

/// <summary>
/// Event bus for publishing and subscribing to game events.
/// </summary>
public interface IGameEventBus
{
    /// <summary>
    /// Subscribes to an event type.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="handler">The event handler.</param>
    /// <returns>A subscription token for unsubscribing.</returns>
    IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : GameEventArgs;

    /// <summary>
    /// Unsubscribes from an event type.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="handler">The event handler to remove.</param>
    void Unsubscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : GameEventArgs;

    /// <summary>
    /// Publishes an event to all subscribers.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="event">The event to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : GameEventArgs;
}