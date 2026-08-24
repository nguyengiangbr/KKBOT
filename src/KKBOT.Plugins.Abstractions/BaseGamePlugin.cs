// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KKBOT.Plugins.Abstractions;

/// <summary>
/// Abstract base class for all game plugins.
/// Provides template methods for game-specific lifecycle events.
/// </summary>
public abstract class BaseGamePlugin : IGameModule
{
    private readonly ILogger _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly GameContext _gameContext;
    private bool _initialized;
    private bool _started;
    private readonly object _stateLock = new();

    /// <summary>
    /// Unique identifier for the game (e.g., "volam", "mu", "silkroad").
    /// </summary>
    public abstract string GameId { get; }

    /// <summary>
    /// Display name of the game (e.g., "Võ Lâm Truyền Kỳ").
    /// </summary>
    public abstract string GameName { get; }

    /// <summary>
    /// Version of this plugin.
    /// </summary>
    public abstract string Version { get; }

    /// <summary>
    /// Minimum required Core Framework version.
    /// </summary>
    public virtual string MinimumCoreVersion => "1.0.0";

    /// <summary>
    /// Supported game regions/servers.
    /// </summary>
    public virtual IReadOnlyList<string> SupportedRegions => new[] { "VN" };

    /// <summary>
    /// Supported game versions.
    /// </summary>
    public virtual IReadOnlyList<string> SupportedGameVersions => new[] { "latest" };

    /// <summary>
    /// Required permissions for this plugin.
    /// </summary>
    public virtual IReadOnlyList<string> RequiredPermissions => Array.Empty<string>();

    /// <summary>
    /// Game configuration schema (JSON schema).
    /// </summary>
    public virtual string ConfigSchema => "{}";

    /// <inheritdoc />
    public string Name => GameName;

    /// <inheritdoc />
    public string Id => GameId;

    /// <summary>
    /// Protected constructor for derived classes.
    /// </summary>
    protected BaseGamePlugin()
    {
        _logger = NullLogger.Instance;
        _serviceProvider = new ServiceCollection().BuildServiceProvider();
        _gameContext = new GameContext(_serviceProvider);
    }

    /// <summary>
    /// Constructor with dependency injection.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="serviceProvider">Service provider for dependency injection.</param>
    protected BaseGamePlugin(ILogger logger, IServiceProvider serviceProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _gameContext = new GameContext(_serviceProvider);
    }

    /// <summary>
    /// Gets the logger instance.
    /// </summary>
    protected ILogger Logger => _logger;

    /// <summary>
    /// Gets the game context providing access to Core services.
    /// </summary>
    protected GameContext GameContext => _gameContext;

    /// <summary>
    /// Gets the service provider for dependency injection.
    /// </summary>
    protected IServiceProvider ServiceProvider => _serviceProvider;

    /// <summary>
    /// Gets the plugin configuration.
    /// </summary>
    protected TConfig GetConfig<TConfig>() where TConfig : class, new()
    {
        return _gameContext.Configuration.Get<TConfig>() ?? new TConfig();
    }

    /// <summary>
    /// Gets a service from the DI container.
    /// </summary>
    protected TService GetService<TService>() where TService : class
    {
        return _serviceProvider.GetService<TService>();
    }

    /// <summary>
    /// Gets a required service from the DI container.
    /// </summary>
    protected TService GetRequiredService<TService>() where TService : class
    {
        var service = _serviceProvider.GetService<TService>();
        if (service == null)
        {
            throw new InvalidOperationException($"Required service {typeof(TService).Name} not registered.");
        }
        return service;
    }

    #region IGameModule Implementation

    /// <inheritdoc />
    public virtual async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
        {
            if (_initialized)
            {
                _logger.LogWarning("Plugin {GameId} already initialized.", GameId);
                return;
            }
        }

        _logger.LogInformation("Initializing plugin {GameName} ({GameId}) v{Version}", GameName, GameId, Version);

        try
        {
            // Update service provider and context
            _gameContext.UpdateServiceProvider(services);

            // Validate Core Framework version compatibility
            await ValidateCoreVersionAsync(cancellationToken);

            // Load configuration
            await LoadConfigurationAsync(cancellationToken);

            // Initialize game-specific resources
            await OnInitializeAsync(cancellationToken);

            // Register event handlers
            RegisterEventHandlers();

            lock (_stateLock)
            {
                _initialized = true;
            }

            _logger.LogInformation("Plugin {GameName} initialized successfully.", GameName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize plugin {GameName}.", GameName);
            throw;
        }
    }

    /// <inheritdoc />
    public virtual async Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
        {
            if (!_initialized)
            {
                throw new InvalidOperationException($"Plugin {GameId} not initialized. Call InitializeAsync first.");
            }
            if (_started)
            {
                _logger.LogWarning("Plugin {GameId} already started.", GameId);
                return;
            }
        }

        _logger.LogInformation("Starting plugin {GameName}.", GameName);

        try
        {
            // Start game-specific logic
            await OnStartAsync(cancellationToken);

            lock (_stateLock)
            {
                _started = true;
            }

            _logger.LogInformation("Plugin {GameName} started successfully.", GameName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start plugin {GameName}.", GameName);
            throw;
        }
    }

    /// <inheritdoc />
    public virtual async Task StopAsync(CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
        {
            if (!_started)
            {
                _logger.LogWarning("Plugin {GameId} not started.", GameId);
                return;
            }
        }

        _logger.LogInformation("Stopping plugin {GameName}.", GameName);

        try
        {
            // Stop game-specific logic
            await OnStopAsync(cancellationToken);

            // Unregister event handlers
            UnregisterEventHandlers();

            lock (_stateLock)
            {
                _started = false;
            }

            _logger.LogInformation("Plugin {GameName} stopped successfully.", GameName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping plugin {GameName}.", GameName);
            throw;
        }
    }

    /// <inheritdoc />
    public virtual async Task<ModuleHealth> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var health = new ModuleHealth
        {
            ModuleId = GameId,
            ModuleName = GameName,
            IsHealthy = true,
            Status = HealthStatus.Healthy,
            CheckedAt = DateTimeOffset.UtcNow
        };

        try
        {
            // Check if plugin is initialized and started
            lock (_stateLock)
            {
                if (!_initialized)
                {
                    health.IsHealthy = false;
                    health.Status = HealthStatus.Unhealthy;
                    health.Issues.Add("Plugin not initialized");
                    return health;
                }

                if (!_started)
                {
                    health.Status = HealthStatus.Degraded;
                    health.Issues.Add("Plugin not started");
                }
            }

            // Game-specific health checks
            var gameHealth = await OnHealthCheckAsync(cancellationToken);
            if (!gameHealth.IsHealthy)
            {
                health.IsHealthy = false;
                health.Status = HealthStatus.Unhealthy;
                health.Issues.AddRange(gameHealth.Issues);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed for plugin {GameName}.", GameName);
            health.IsHealthy = false;
            health.Status = HealthStatus.Unhealthy;
            health.Issues.Add($"Health check exception: {ex.Message}");
        }

        return health;
    }

    #endregion

    #region Template Methods - Override in derived classes

    /// <summary>
    /// Called during initialization. Override to initialize game-specific resources.
    /// </summary>
    protected virtual Task OnInitializeAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when the plugin starts. Override to start game-specific logic.
    /// </summary>
    protected virtual Task OnStartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when the plugin stops. Override to cleanup game-specific resources.
    /// </summary>
    protected virtual Task OnStopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called during health check. Override for game-specific health checks.
    /// </summary>
    protected virtual Task<ModuleHealth> OnHealthCheckAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(new ModuleHealth { IsHealthy = true, Status = HealthStatus.Healthy });
    }

    /// <summary>
    /// Called when configuration is loaded. Override for custom config loading.
    /// </summary>
    protected virtual Task LoadConfigurationAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Validates Core Framework version compatibility.
    /// </summary>
    protected virtual Task ValidateCoreVersionAsync(CancellationToken cancellationToken)
    {
        // Default implementation - can be overridden for custom validation
        return Task.CompletedTask;
    }

    #endregion

    #region Game Lifecycle Events - Override in derived classes

    /// <summary>
    /// Called when the game client starts login process.
    /// </summary>
    protected virtual Task OnLoginAsync(LoginEventArgs args, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when login succeeds.
    /// </summary>
    protected virtual Task OnLoginSuccessAsync(LoginSuccessEventArgs args, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when login fails.
    /// </summary>
    protected virtual Task OnLoginFailedAsync(LoginFailedEventArgs args, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when character enters game world.
    /// </summary>
    protected virtual Task OnCharacterEnterWorldAsync(CharacterEnterWorldEventArgs args, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when combat starts.
    /// </summary>
    protected virtual Task OnCombatStartAsync(CombatStartEventArgs args, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when combat ends.
    /// </summary>
    protected virtual Task OnCombatEndAsync(CombatEndEventArgs args, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when character uses a skill.
    /// </summary>
    protected virtual Task OnSkillUsedAsync(SkillUsedEventArgs args, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when character takes damage.
    /// </summary>
    protected virtual Task OnDamageTakenAsync(DamageTakenEventArgs args, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when character dies.
    /// </summary>
    protected virtual Task OnDeadAsync(DeadEventArgs args, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when character respawns/revives.
    /// </summary>
    protected virtual Task OnReviveAsync(ReviveEventArgs args, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when character levels up.
    /// </summary>
    protected virtual Task OnLevelUpAsync(LevelUpEventArgs args, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when a quest is completed.
    /// </summary>
    protected virtual Task OnQuestCompleteAsync(QuestCompleteEventArgs args, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when inventory is full.
    /// </summary>
    protected virtual Task OnInventoryFullAsync(InventoryFullEventArgs args, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when stamina/energy is low.
    /// </summary>
    protected virtual Task OnStaminaLowAsync(StaminaLowEventArgs args, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when character is stuck (not moving).
    /// </summary>
    protected virtual Task OnStuckAsync(StuckEventArgs args, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when a boss spawns.
    /// </summary>
    protected virtual Task OnBossSpawnAsync(BossSpawnEventArgs args, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when another player is nearby.
    /// </summary>
    protected virtual Task OnPlayerNearbyAsync(PlayerNearbyEventArgs args, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when an item drops.
    /// </summary>
    protected virtual Task OnItemDropAsync(ItemDropEventArgs args, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when a chat message is received.
    /// </summary>
    protected virtual Task OnChatMessageAsync(ChatMessageEventArgs args, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when a party/guild invite is received.
    /// </summary>
    protected virtual Task OnPartyInviteAsync(PartyInviteEventArgs args, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when a trade request is received.
    /// </summary>
    protected virtual Task OnTradeRequestAsync(TradeRequestEventArgs args, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when the game window loses focus.
    /// </summary>
    protected virtual Task OnWindowFocusLostAsync(WindowFocusLostEventArgs args, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when the game window gains focus.
    /// </summary>
    protected virtual Task OnWindowFocusGainedAsync(WindowFocusGainedEventArgs args, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when network connection is lost.
    /// </summary>
    protected virtual Task OnDisconnectedAsync(DisconnectedEventArgs args, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when network connection is restored.
    /// </summary>
    protected virtual Task OnReconnectedAsync(ReconnectedEventArgs args, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called periodically for periodic tasks (every tick).
    /// </summary>
    protected virtual Task OnTickAsync(TickEventArgs args, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    #endregion

    #region Event Handler Registration

    private void RegisterEventHandlers()
    {
        // Subscribe to game events via GameContext event bus
        var eventBus = _gameContext.GetService<IGameEventBus>();
        if (eventBus != null)
        {
            eventBus.Subscribe<LoginEventArgs>(OnLoginAsync);
            eventBus.Subscribe<LoginSuccessEventArgs>(OnLoginSuccessAsync);
            eventBus.Subscribe<LoginFailedEventArgs>(OnLoginFailedAsync);
            eventBus.Subscribe<CharacterEnterWorldEventArgs>(OnCharacterEnterWorldAsync);
            eventBus.Subscribe<CombatStartEventArgs>(OnCombatStartAsync);
            eventBus.Subscribe<CombatEndEventArgs>(OnCombatEndAsync);
            eventBus.Subscribe<SkillUsedEventArgs>(OnSkillUsedAsync);
            eventBus.Subscribe<DamageTakenEventArgs>(OnDamageTakenAsync);
            eventBus.Subscribe<DeadEventArgs>(OnDeadAsync);
            eventBus.Subscribe<ReviveEventArgs>(OnReviveAsync);
            eventBus.Subscribe<LevelUpEventArgs>(OnLevelUpAsync);
            eventBus.Subscribe<QuestCompleteEventArgs>(OnQuestCompleteAsync);
            eventBus.Subscribe<InventoryFullEventArgs>(OnInventoryFullAsync);
            eventBus.Subscribe<StaminaLowEventArgs>(OnStaminaLowAsync);
            eventBus.Subscribe<StuckEventArgs>(OnStuckAsync);
            eventBus.Subscribe<BossSpawnEventArgs>(OnBossSpawnAsync);
            eventBus.Subscribe<PlayerNearbyEventArgs>(OnPlayerNearbyAsync);
            eventBus.Subscribe<ItemDropEventArgs>(OnItemDropAsync);
            eventBus.Subscribe<ChatMessageEventArgs>(OnChatMessageAsync);
            eventBus.Subscribe<PartyInviteEventArgs>(OnPartyInviteAsync);
            eventBus.Subscribe<TradeRequestEventArgs>(OnTradeRequestAsync);
            eventBus.Subscribe<WindowFocusLostEventArgs>(OnWindowFocusLostAsync);
            eventBus.Subscribe<WindowFocusGainedEventArgs>(OnWindowFocusGainedAsync);
            eventBus.Subscribe<DisconnectedEventArgs>(OnDisconnectedAsync);
            eventBus.Subscribe<ReconnectedEventArgs>(OnReconnectedAsync);
            eventBus.Subscribe<TickEventArgs>(OnTickAsync);
        }
    }

    private void UnregisterEventHandlers()
    {
        var eventBus = _gameContext.GetService<IGameEventBus>();
        if (eventBus != null)
        {
            eventBus.Unsubscribe<LoginEventArgs>(OnLoginAsync);
            eventBus.Unsubscribe<LoginSuccessEventArgs>(OnLoginSuccessAsync);
            eventBus.Unsubscribe<LoginFailedEventArgs>(OnLoginFailedAsync);
            eventBus.Unsubscribe<CharacterEnterWorldEventArgs>(OnCharacterEnterWorldAsync);
            eventBus.Unsubscribe<CombatStartEventArgs>(OnCombatStartAsync);
            eventBus.Unsubscribe<CombatEndEventArgs>(OnCombatEndAsync);
            eventBus.Unsubscribe<SkillUsedEventArgs>(OnSkillUsedAsync);
            eventBus.Unsubscribe<DamageTakenEventArgs>(OnDamageTakenAsync);
            eventBus.Unsubscribe<DeadEventArgs>(OnDeadAsync);
            eventBus.Unsubscribe<ReviveEventArgs>(OnReviveAsync);
            eventBus.Unsubscribe<LevelUpEventArgs>(OnLevelUpAsync);
            eventBus.Unsubscribe<QuestCompleteEventArgs>(OnQuestCompleteAsync);
            eventBus.Unsubscribe<InventoryFullEventArgs>(OnInventoryFullAsync);
            eventBus.Unsubscribe<StaminaLowEventArgs>(OnStaminaLowAsync);
            eventBus.Unsubscribe<StuckEventArgs>(OnStuckAsync);
            eventBus.Unsubscribe<BossSpawnEventArgs>(OnBossSpawnAsync);
            eventBus.Unsubscribe<PlayerNearbyEventArgs>(OnPlayerNearbyAsync);
            eventBus.Unsubscribe<ItemDropEventArgs>(OnItemDropAsync);
            eventBus.Unsubscribe<ChatMessageEventArgs>(OnChatMessageAsync);
            eventBus.Unsubscribe<PartyInviteEventArgs>(OnPartyInviteAsync);
            eventBus.Unsubscribe<TradeRequestEventArgs>(OnTradeRequestAsync);
            eventBus.Unsubscribe<WindowFocusLostEventArgs>(OnWindowFocusLostAsync);
            eventBus.Unsubscribe<WindowFocusGainedEventArgs>(OnWindowFocusGainedAsync);
            eventBus.Unsubscribe<DisconnectedEventArgs>(OnDisconnectedAsync);
            eventBus.Unsubscribe<ReconnectedEventArgs>(OnReconnectedAsync);
            eventBus.Unsubscribe<TickEventArgs>(OnTickAsync);
        }
    }

    #endregion

    public void Dispose()
    {
        if (_started)
        {
            StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
        _initialized = false;
    }
}

/// <summary>
/// Null logger implementation for parameterless constructor.
/// </summary>
internal sealed class NullLogger : ILogger
{
    public static readonly NullLogger Instance = new NullLogger();

    private NullLogger() { }

    public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => false;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new NullScope();
        public void Dispose() { }
    }
}