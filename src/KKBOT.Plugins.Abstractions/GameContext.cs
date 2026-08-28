// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using KKBOT.AI.Abstractions;
using KKBOT.Device.Abstractions;
using KKBOT.Perception.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KKBOT.Plugins.Abstractions;

/// <summary>
/// Game context providing access to core framework services for plugins.
/// </summary>
public sealed class GameContext
{
    private IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameContext"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for dependency injection.</param>
    public GameContext(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// Updates the service provider.
    /// </summary>
    /// <param name="serviceProvider">The new service provider.</param>
    public void UpdateServiceProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// Gets the device session for capturing frames and sending input.
    /// </summary>
    public IDeviceSession Device => GetRequiredService<IDeviceSession>();

    /// <summary>
    /// Gets the perception pipeline for frame processing.
    /// </summary>
    public IPerceptionPipeline Perception => GetRequiredService<IPerceptionPipeline>();

    /// <summary>
    /// Gets the AI service for text generation.
    /// </summary>
    public IAiService AI => GetRequiredService<IAiService>();

    /// <summary>
    /// Gets the input dispatcher for sending input commands.
    /// </summary>
    public IInputDispatcher Input => GetRequiredService<IInputDispatcher>();

    /// <summary>
    /// Gets the scheduler for job scheduling.
    /// </summary>
    public IScheduler Scheduler => GetRequiredService<IScheduler>();

    /// <summary>
    /// Gets the configuration.
    /// </summary>
    public IConfiguration Configuration => GetRequiredService<IConfiguration>();

    /// <summary>
    /// Gets the game event bus for publishing and subscribing to game events.
    /// </summary>
    public IGameEventBus EventBus => GetRequiredService<IGameEventBus>();

    /// <summary>
    /// Gets the navigation service for pathfinding and movement along a game map.
    /// </summary>
    public INavigationService Navigation => GetRequiredService<INavigationService>();

    /// <summary>
    /// Gets a service from the DI container.
    /// </summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <returns>The service instance, or null if not registered.</returns>
    public T GetService<T>() where T : class
    {
        return _serviceProvider.GetService<T>() ?? throw new InvalidOperationException($"Service {typeof(T).Name} not registered.");
    }

    /// <summary>
    /// Gets a required service from the DI container.
    /// </summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <returns>The service instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the service is not registered.</exception>
    public T GetRequiredService<T>() where T : class
    {
        var service = _serviceProvider.GetService<T>();
        if (service == null)
        {
            throw new InvalidOperationException($"Required service {typeof(T).Name} not registered.");
        }
        return service;
    }
}