// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KKBOT.Plugins.Abstractions;
using KKBOT.Device.Abstractions;
using KKBOT.Perception.Abstractions;
using KKBOT.AI.Abstractions;
using KKBOT.Orchestration;
using KKBOT.Observability;
using KKBOT.Observability.Tracing;
using KKBOT.Plugin.CombatQuest;
using KKBOT.Plugin.LogisticsSurvival;

namespace KKBOT.Plugin.EconomyTrading;

/// <summary>
/// Vending/shop configuration.
/// </summary>
public sealed record VendingConfig
{
    public string ShopName { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty; // Map coordinates or zone name
    public string OpenHotkey { get: init; } = string.Empty; // Hotkey to open shop
    public Dictionary<string, ItemPrice> ItemPrices { get: init; } = new(); // Item name -> price config
    public bool AutoOpenShop { get: init; } = true;
    public TimeSpan ShopDuration { get: init; } = TimeSpan.FromHours(24);
    public bool AutoRestock { get: init; } = true;
    public int MaxItemsPerSlot { get; init; } = 999;
}

/// <summary>
/// Item pricing configuration.
/// </summary>
public sealed record ItemPrice
{
    public string ItemName { get: init; } = string.Empty;
    public long PricePerUnit { get: init; } = 0;
    public int MinStock { get; init; } = 10;
    public int MaxStock { get: init; } = 999;
    public bool AutoPrice { get: init; } = false; // Auto-adjust based on market
    public double PriceAdjustmentPercent { get: init; } = 0; // +/- % from market average
}

/// <summary>
/// Master accumulator configuration.
/// </summary>
public sealed record AccumulatorConfig
{
    public string MasterAccountId { get: init; } = string.Empty;
    public string MasterCharacterName { get: init; } = string.Empty;
    public string MeetingLocation { get: init; } = string.Empty; // Coordinates or zone name
    public TransferMethod Method { get: init; } = TransferMethod.Trade;
    public long GoldThreshold { get: init; } = 100000; // Transfer when gold exceeds this
    public string[] ValuableItemKeywords { get: init; } = Array.Empty<string>(); // Items to always transfer
    public TimeSpan TransferInterval { get: init; } = TimeSpan.FromHours(1);
    public bool RequireMasterOnline { get: init; } = true;
    public int MaxTransferRetries { get; init; } = 3;
}

public enum TransferMethod
{
    Trade,
    Mail,
    Warehouse,
    AuctionHouse
}

/// <summary>
/// Market price monitoring configuration.
/// </summary>
public sealed record MarketMonitorConfig
{
    public bool EnableMarketMonitoring { get: init; } = true;
    public TimeSpan UpdateInterval { get: init; } = TimeSpan.FromMinutes(15);
    public string[] MonitoredItems { get: init; } = Array.Empty<string>();
    public double PriceChangeThresholdPercent { get: init; } = 10; // Alert threshold
    public bool AutoAdjustPrices { get: init; } = false;
}

/// <summary>
/// Economy & Trading module configuration.
/// </summary>
public sealed class EconomyTradingOptions
{
    // Vending
    public bool EnableAutoVending { get; set; } = true;
    public List<VendingConfig> VendingConfigs { get; set; } = new();
    public string VendingHotkey { get; set; } = "V";
    public string VendingLocationHotkey { get; set; } = "M"; // Open map to navigate to vending spot

    // Master Accumulator
    public bool EnableMasterAccumulator { get; set; } = true;
    public AccumulatorConfig Accumulator { get; set; } = new();

    // Market Monitoring
    public MarketMonitorConfig MarketMonitor { get; set; } = new();

    // Trading
    public bool EnableAutoTrading { get; set; } = false; // Advanced: player-to-player trading
    public string[] TrustedTradePartners { get; set; } = Array.Empty<string>();
    public long MaxTradeValue { get; set; } = 1000000;
    public bool RequireConfirmation { get; set; } = true;

    // Security
    public bool VerifyTradePartner { get; set; } = true;
    public bool LogAllTransactions { get: set; } = true;
    public long MaxTransactionValue { get: set; } = 100000;
    public TimeSpan TradeTimeout { get; set; } = TimeSpan.FromMinutes(5);

    // Monitoring
    public int StatusUpdateIntervalMs { get; set; } = 30000;
    public bool LogTransactions { get: set; } = true;
}

/// <summary>
/// Economy & Trading module - Handles auto vending, master accumulation, market monitoring, and trading.
/// </summary>
public sealed class EconomyTradingModule : IGameModule
{
    private readonly ILogger<EconomyTradingModule> _logger;
    private readonly EconomyTradingOptions _options;
    private readonly IDeviceSessionFactory _deviceFactory;
    private readonly IPerceptionPipeline _perceptionPipeline;
    private readonly IAiService _aiService;
    private readonly IInstanceManager _instanceManager;
    private readonly IMetricsRegistry _metrics;
    private readonly IAgentRegistry _agentRegistry;
    private readonly IMessageBus _messageBus;
    private readonly ICoordinationService _coordinationService;

    private readonly ConcurrentDictionary<string, EconomyState> _economyStates = new();
    private readonly ConcurrentDictionary<string, VendingSession> _vendingSessions = new();
    private readonly ConcurrentDictionary<string, MarketData> _marketData = new();
    private readonly Timer? _vendingTimer;
    private readonly Timer? _accumulatorTimer;
    private readonly Timer? _marketTimer;
    private readonly Timer? _tradeTimer;
    private readonly Timer? _monitorTimer;
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly IMetricsRegistry _metrics;
    private bool _disposed;

    public string Id => "economy-trading";
    public string Version => "1.0.0";

    public EconomyTradingModule(
        ILogger<EconomyTradingModule> logger,
        IOptions<EconomyTradingOptions> options,
        IDeviceSessionFactory deviceFactory,
        IPerceptionPipeline perceptionPipeline,
        IAiService aiService,
        IInstanceManager instanceManager,
        IMetricsRegistry metrics,
        IAgentRegistry agentRegistry,
        IMessageBus messageBus,
        ICoordinationService coordinationService)
    {
        _logger = logger;
        _options = options.Value;
        _deviceFactory = deviceFactory;
        _perceptionPipeline = perceptionPipeline;
        _aiService = aiService;
        _instanceManager = instanceManager;
        _metrics = metrics;
        _agentRegistry = agentRegistry;
        _messageBus = messageBus;
        _coordinationService = coordinationService;

        _vendingTimer = new Timer(VendingLoopAsync, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
        _accumulatorTimer = new Timer(AccumulatorLoopAsync, null, TimeSpan.Zero, TimeSpan.FromMinutes(5));
        _marketTimer = new Timer(MarketMonitorLoopAsync, null, TimeSpan.Zero, TimeSpan.FromMinutes(_options.MarketMonitor.UpdateInterval.TotalMinutes));
        _tradeTimer = new Timer(TradeLoopAsync, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
        _monitorTimer = new Timer(ReportMetricsAsync, null, TimeSpan.FromMilliseconds(_options.StatusUpdateIntervalMs), TimeSpan.FromMilliseconds(_options.StatusUpdateIntervalMs));
    }

    public ModuleManifest GetManifest()
    {
        return new ModuleManifest
        {
            Id = Id,
            Name = "Economy & Trading",
            Version = Version,
            Author = "KKBOT Team",
            Description = "Auto vending, master accumulator, market monitoring, and automated trading",
            SupportedResolutions = new[] { new Resolution(1920, 1080), new Resolution(1280, 720) },
            RequiredPermissions = new[] { "DEVICE_READ", "DEVICE_WRITE", "INPUT_WRITE", "SCREENSHOT_CAPTURE", "NETWORK_ACCESS", "TRADE_MANAGE" },
            ConfigSchema = JsonSerializer.Serialize(new EconomyTradingOptions()),
            DefaultConfig = JsonSerializer.Serialize(new EconomyTradingOptions()),
            EntryAssembly = "KKBOT.Plugin.EconomyTrading.dll",
            Assets = new[] 
            { 
                "templates/vending_ui/",
                "templates/trade_window/",
                "templates/market_prices/",
                "templates/mail_ui/"
            }
        };
    }

    public IReadOnlyCollection<IGameDetector> GetDetectors() => Array.Empty<IGameDetector>();
    public IReadOnlyCollection<IGameTaskDefinition> GetTasks() => new[] 
    { 
        new AutoVendingTask(), 
        new MasterAccumulatorTask(), 
        new MarketMonitorTask(),
        new AutoTradeTask()
    };
    public IReadOnlyCollection<IStateDefinition> GetStates() => new[] 
    { 
        new IdleState(), 
        new VendingState(), 
        new AccumulatingState(), 
        new TradingState(),
        new MarketAnalysisState()
    };

    public async Task<ModuleValidationResult> ValidateAsync(ModuleContext context, CancellationToken ct)
    {
        var errors = new List<ModuleValidationError>();

        if (_options.EnableAutoVending && _options.VendingConfigs.Count == 0)
        {
            errors.Add(new ModuleValidationError("NO_VENDING_CONFIG", "Auto vending enabled but no vending configurations"));
        }

        if (_options.EnableMasterAccumulator && string.IsNullOrEmpty(_options.Accumulator.MasterAccountId))
        {
            errors.Add(new ModuleValidationError("NO_MASTER_ACCOUNT", "Master accumulator enabled but no master account configured"));
        }

        if (_options.MarketMonitor.EnableMarketMonitoring && _options.MarketMonitor.MonitoredItems.Length == 0)
        {
            errors.Add(new ModuleValidationError("NO_MONITORED_ITEMS", "Market monitoring enabled but no items specified"));
        }

        return new ModuleValidationResult(errors.Count == 0, errors);
    }

    /// <summary>
    /// Registers an instance for economy management.
    /// </summary>
    public void RegisterInstance(string accountId, IDeviceSession session)
    {
        _economyStates[accountId] = new EconomyState
        {
            AccountId = accountId,
            Session = session,
            Gold = 0,
            InventoryValue = 0,
            LastUpdate = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Unregisters an instance.
    /// </summary>
    public void UnregisterInstance(string accountId)
    {
        _economyStates.TryRemove(accountId, out _);
        _vendingSessions.TryRemove(accountId, out _);
    }

    // ==================== VENDING ====================

    private async void VendingLoopAsync(object? state)
    {
        if (!_options.EnableAutoVending) return;
        if (_shutdownCts.Token.IsCancellationRequested) return;

        try
        {
            foreach (var config in _options.VendingConfigs.Where(c => c.AutoOpenShop))
            {
                foreach (var kvp in _economyStates)
                {
                    var accountId = kvp.Key;
                    var state = kvp.Value;

                    if (state.Session == null) continue;

                    var session = _vendingSessions.GetOrAdd(accountId, _ => new VendingSession
                    {
                        AccountId = accountId,
                        Config = config,
                        IsOpen = false,
                        StartedAt = DateTimeOffset.UtcNow
                    });

                    if (!session.IsOpen && DateTimeOffset.UtcNow - session.StartedAt > config.ShopDuration)
                    {
                        await OpenShopAsync(accountId, config, CancellationToken.None);
                    }
                    else if (session.IsOpen && DateTimeOffset.UtcNow - session.StartedAt > config.ShopDuration)
                    {
                        await CloseShopAsync(accountId, CancellationToken.None);
                    }
                    else if (session.IsOpen && config.AutoRestock)
                    {
                        await RestockShopAsync(accountId, config, CancellationToken.None);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in vending loop");
        }
    }

    private async Task OpenShopAsync(string accountId, VendingConfig config, CancellationToken ct)
    {
        if (!_economyStates.TryGetValue(accountId, out var state) || state.Session == null)
            return;

        var session = _vendingSessions.GetOrAdd(accountId, _ => new VendingSession { AccountId = accountId, Config = _options.VendingConfigs.First() });
        
        _logger.LogInformation("Opening shop for account {AccountId} at {Location}", accountId, config.Location);

        // Navigate to vending location
        await NavigateToVendingSpotAsync(accountId, config.Location, CancellationToken.None);

        // Open shop UI
        await state.Session!.SendInputAsync(new InputCommand
        {
            Type = InputType.Key,
            Text = config.OpenHotkey
        }, CancellationToken.None);
        await Task.Delay(1000, CancellationToken.None);

        session.IsOpen = true;
        session.StartedAt = DateTimeOffset.UtcNow;
    }

    private async Task CloseShopAsync(string accountId, CancellationToken ct)
    {
        if (!_vendingSessions.TryGetValue(accountId, out var session) || !session.IsOpen)
            return;

        if (!_economyStates.TryGetValue(accountId, out var state) || state.Session == null)
            return;

        _logger.LogInformation("Closing shop for account {AccountId}", accountId);

        await state.Session.SendInputAsync(new InputCommand
        {
            Type = InputType.Key,
            Text = "Escape" // Close shop UI
        }, CancellationToken.None);

        session.IsOpen = false;
    }

    private async Task RestockShopAsync(string accountId, VendingConfig config, CancellationToken ct)
    {
        if (!_economyStates.TryGetValue(accountId, out var state) || state.Session == null)
            return;

        _logger.LogDebug("Restocking shop for account {AccountId}", accountId);

        // Check inventory and restock items
        foreach (var itemPrice in config.ItemPrices)
        {
            var currentStock = await GetItemStockAsync(accountId, itemPrice.ItemName, CancellationToken.None);
            
            if (currentStock < itemPrice.MinStock)
            {
                var needed = itemPrice.MaxStock - currentStock;
                await RestockItemAsync(accountId, itemPrice.ItemName, needed, CancellationToken.None);
            }
        }
    }

    private async Task NavigateToVendingSpotAsync(string accountId, string location, CancellationToken ct)
    {
        // Navigate to vending location
        await Task.Delay(5000, ct);
    }

    private async Task<int> GetItemStockAsync(string accountId, string itemName, CancellationToken ct)
    {
        // Scan inventory for item count
        return 0;
    }

    private async Task RestockItemAsync(string accountId, string itemName, int quantity, CancellationToken ct)
    {
        // Restock from warehouse or craft
    }

    // ==================== MASTER ACCUMULATOR ====================

    private async void AccumulatorLoopAsync(object? state)
    {
        if (!_options.EnableMasterAccumulator) return;
        if (_shutdownCts.Token.IsCancellationRequested) return;

        try
        {
            var config = _options.Accumulator;
            
            foreach (var kvp in _economyStates)
            {
                var accountId = kvp.Key;
                var state = kvp.Value;

                if (state.Session == null) continue;

                // Check gold threshold
                if (state.Gold >= config.GoldThreshold)
                {
                    await TransferGoldToMasterAsync(accountId, state, CancellationToken.None);
                }

                // Check valuable items
                await TransferValuableItemsAsync(accountId, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in accumulator loop");
        }
    }

    private async Task TransferGoldToMasterAsync(string accountId, EconomyState state, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_options.Accumulator.MasterAccountId))
            return;

        var masterState = _economyStates.GetValueOrDefault(_options.Accumulator.MasterAccountId);
        if (masterState?.Session == null && _options.Accumulator.RequireMasterOnline)
        {
            _logger.LogWarning("Master account {MasterId} not online, skipping gold transfer", _options.Accumulator.MasterAccountId);
            return;
        }

        var amountToTransfer = state.Gold - _options.Accumulator.GoldThreshold;
        if (amountToTransfer <= 0) return;

        _logger.LogInformation("Transferring {Amount} gold from {AccountId} to master {MasterId}", 
            amountToTransfer, accountId, _options.Accumulator.MasterAccountId);

        await ExecuteTransferAsync(accountId, _options.Accumulator.MasterAccountId, 
            new TransferPayload { Type = TransferMethod.Mail, GoldAmount = amountToTransfer }, CancellationToken.None);
    }

    private async Task TransferValuableItemsAsync(string accountId, CancellationToken ct)
    {
        // Scan inventory for valuable items
        // Transfer items matching keywords
    }

    private async Task ExecuteTransferAsync(string fromAccountId, string toAccountId, TransferPayload payload, CancellationToken ct)
    {
        switch (payload.Type)
        {
            case TransferMethod.Trade:
                await ExecuteTradeTransferAsync(fromAccountId, toAccountId, payload, ct);
                break;
            case TransferMethod.Mail:
                await SendMailAsync(fromAccountId, toAccountId, payload, ct);
                break;
            case TransferMethod.Warehouse:
                await DepositToWarehouseAsync(fromAccountId, payload, ct);
                break;
            case TransferMethod.AuctionHouse:
                await ListOnAuctionHouseAsync(fromAccountId, payload, ct);
                break;
        }
    }

    private async Task ExecuteTradeTransferAsync(string fromAccountId, string toAccountId, TransferPayload payload, CancellationToken ct)
    {
        // Initiate trade with master account
        // Wait for acceptance
        // Transfer gold/items
    }

    private async Task SendMailAsync(string fromAccountId, string toAccountId, TransferPayload payload, CancellationToken ct)
    {
        // Send in-game mail with gold/items
    }

    private async Task DepositToWarehouseAsync(string fromAccountId, TransferPayload payload, CancellationToken ct)
    {
        // Deposit to shared warehouse
    }

    private async Task ListOnAuctionHouseAsync(string fromAccountId, TransferPayload payload, CancellationToken ct)
    {
        // List items on auction house
    }

    // ==================== MARKET MONITORING ====================

    private async void MarketMonitorLoopAsync(object? state)
    {
        if (!_options.MarketMonitor.EnableMarketMonitoring) return;
        if (_shutdownCts.Token.IsCancellationRequested) return;

        try
        {
            foreach (var itemName in _options.MarketMonitor.MonitoredItems)
            {
                var price = await FetchMarketPriceAsync(itemName, CancellationToken.None);
                
                if (price > 0)
                {
                    _marketData[itemName] = new MarketData
                    {
                        ItemName = itemName,
                        CurrentPrice = price,
                        LastUpdated = DateTimeOffset.UtcNow,
                        PriceHistory = new List<PricePoint>
                        {
                            new PricePoint { Price = price, Timestamp = DateTimeOffset.UtcNow }
                        }
                    };

                    // Check for significant price changes
                    await CheckPriceAlertAsync(itemName, price);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in market monitor loop");
        }
    }

    private async Task<long> FetchMarketPriceAsync(string itemName, CancellationToken ct)
    {
        // In real implementation, would query game API or scrape market
        // For now, return simulated price
        return new Random().Next(100, 10000);
    }

    private async Task CheckPriceAlertAsync(string itemName, long currentPrice)
    {
        if (!_marketData.TryGetValue(itemName, out var oldData))
            return;

        var changePercent = Math.Abs((double)(currentPrice - oldData.CurrentPrice) / oldData.CurrentPrice * 100);
        
        if (changePercent > _options.MarketMonitor.PriceChangeThresholdPercent)
        {
            _logger.LogWarning("Price alert for {ItemName}: {OldPrice} -> {NewPrice} ({ChangePercent:F1}%)", 
                itemName, oldData.CurrentPrice, currentPrice, changePercent);
            
            if (_options.MarketMonitor.AutoAdjustPrices)
            {
                await AdjustVendingPricesAsync(itemName, currentPrice);
            }
        }
    }

    private async Task AdjustVendingPricesAsync(string itemName, long newPrice)
    {
        // Adjust vending prices based on market
    }

    // ==================== TRADING ====================

    private async void TradeLoopAsync(object? state)
    {
        if (!_options.EnableAutoTrading) return;
        if (_shutdownCts.Token.IsCancellationRequested) return;

        // Process pending trades
        // Verify trade partners
        // Execute trades
    }

    private async Task<bool> InitiateTradeAsync(string fromAccountId, string toAccountId, CancellationToken ct)
    {
        // Initiate trade window with target player
        return true;
    }

    private async Task<bool> ExecuteTradeAsync(string fromAccountId, string toAccountId, TradePayload payload, CancellationToken ct)
    {
        // Execute trade with items/gold
        return true;
    }

    // ==================== MONITORING ====================

    private async void ReportMetricsAsync(object? state)
    {
        if (_shutdownCts.Token.IsCancellationRequested) return;

        foreach (var kvp in _economyStates)
        {
            var state = kvp.Value;
            _metrics.RecordHttpRequests(); // Reuse for economy metrics
        }
    }

    // ==================== HELPER METHODS ====================

    private async Task<bool> NavigateToCoordinatesAsync(string accountId, string coordinates, CancellationToken ct)
    {
        // Navigate to coordinates
        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _vendingTimer?.Dispose();
        _accumulatorTimer?.Dispose();
        _marketTimer?.Dispose();
        _tradeTimer?.Dispose();
        _monitorTimer?.Dispose();
        _shutdownCts.Cancel();
        _shutdownCts.Dispose();
    }
}

// ==================== STATE CLASSES ====================

public sealed class EconomyState
{
    public string AccountId { get; set; } = string.Empty;
    public IDeviceSession? Session { get; set; }
    public long Gold { get; set; } = 0;
    public long InventoryValue { get; set; } = 0;
    public long TotalEarned { get; set; } = 0;
    public long TotalSpent { get; set; } = 0;
    public DateTimeOffset LastUpdate { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, int> ItemCounts { get; set; } = new();
}

public sealed class VendingSession
{
    public string AccountId { get; set; } = string.Empty;
    public VendingConfig Config { get; set; } = new();
    public bool IsOpen { get; set; } = false;
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public long TotalSales { get; set; } = 0;
    public Dictionary<string, int> ItemsSold { get; set; } = new();
}

public sealed class MarketData
{
    public string ItemName { get; set; } = string.Empty;
    public long CurrentPrice { get; set; } = 0;
    public long PreviousPrice { get; set; } = 0;
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;
    public List<PricePoint> PriceHistory { get; set; } = new();
}

public sealed record PricePoint
{
    public long Price { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}

public sealed record TransferPayload
{
    public TransferMethod Type { get; init; } = TransferMethod.Mail;
    public long GoldAmount { get; init; } = 0;
    public string[] ItemNames { get; init; } = Array.Empty<string>();
    public int[] Quantities { get; init; } = Array.Empty<int>();
    public string Note { get: init; } = string.Empty;
}

public sealed record TradePayload
{
    public string FromAccountId { get: init; } = string.Empty;
    public string ToAccountId { get: init; } = string.Empty;
    public long GoldAmount { get: init; } = 0;
    public string[] ItemNames { get: init; } = Array.Empty<string>();
    public int[] Quantities { get: init; } = Array.Empty<int>();
}

// ==================== TASKS & STATES ====================

public sealed class AutoVendingTask : IGameTaskDefinition
{
    public string Id => "auto-vending";
    public string Name => "Auto Vending";
    public string? Description => "Automatically manages vending shops";
    public TaskType Type => TaskType.Repeating;
    public TimeSpan DefaultTimeout => TimeSpan.FromMinutes(5);
    public RetryPolicy DefaultRetryPolicy => new(2, 30000);
    public bool RequiresApproval => false;

    public async Task<IStateDefinition> ExecuteAsync(TaskContext context, CancellationToken ct)
    {
        return new VendingState();
    }
}

public sealed class MasterAccumulatorTask : IGameTaskDefinition
{
    public string Id => "master-accumulator";
    public string Name => "Master Accumulator";
    public string? Description => "Transfers gold and valuable items to master account";
    public TaskType Type => TaskType.Repeating;
    public TimeSpan DefaultTimeout => TimeSpan.FromMinutes(10);
    public RetryPolicy DefaultRetryPolicy => new(1, 60000);
    public bool RequiresApproval => false;

    public async Task<IStateDefinition> ExecuteAsync(TaskContext context, CancellationToken ct)
    {
        return new AccumulatingState();
    }
}

public sealed class MarketMonitorTask : IGameTaskDefinition
{
    public string Id => "market-monitor";
    public string Name => "Market Monitor";
    public string? Description => "Monitors market prices and adjusts pricing";
    public TaskType Type => TaskType.Repeating;
    public TimeSpan DefaultTimeout => TimeSpan.FromMinutes(5);
    public RetryPolicy DefaultRetryPolicy => new(1, 60000);
    public bool RequiresApproval => false;

    public async Task<IStateDefinition> ExecuteAsync(TaskContext context, CancellationToken ct)
    {
        return new MarketAnalysisState();
    }
}

public sealed class AutoTradeTask : IGameTaskDefinition
{
    public string Id => "auto-trade";
    public string Name => "Auto Trade";
    public string? Description => "Automates trading with other players";
    public TaskType Type => TaskType.Repeating;
    public TimeSpan DefaultTimeout => TimeSpan.FromMinutes(5);
    public RetryPolicy DefaultRetryPolicy => new(1, 30000);
    public bool RequiresApproval => true; // Trading requires approval

    public async Task<IStateDefinition> ExecuteAsync(TaskContext context, CancellationToken ct)
    {
        return new TradingState();
    }
}

// States
public sealed class IdleState : IStateDefinition
{
    public string Id => "idle";
    public string Name => "Idle";
    public string Category => "economy";
    public Func<PerceptionContext?, bool> EntryCondition => _ => true;
    public IStateAction? Action => null;
    public IReadOnlyList<string> NextStateIds => new[] { "vending", "accumulating", "trading", "market_analysis" };
    public TimeSpan Timeout => TimeSpan.FromMinutes(5);
    public RetryPolicy RetryPolicy => new(0, 0);
    public EvidencePolicy EvidencePolicy => new(false, false);
}

public sealed class VendingState : IStateDefinition
{
    public string Id => "vending";
    public string Name => "Vending";
    public string Category => "economy";
    public Func<PerceptionContext?, bool> EntryCondition => ctx => 
        ctx?.Detections.Any(d => d.DetectorName.Contains("vending") && d.IsMatch) ?? false;
    public IStateAction? Action => null;
    public IReadOnlyList<string> NextStateIds => new[] { "idle", "accumulating" };
    public TimeSpan Timeout => TimeSpan.FromMinutes(30);
    public RetryPolicy RetryPolicy => new(2, 60000);
    public EvidencePolicy EvidencePolicy => new(true, false);
}

public sealed class AccumulatingState : IStateDefinition
{
    public string Id => "accumulating";
    public string Name => "Accumulating";
    public string Category => "economy";
    public Func<PerceptionContext?, bool> EntryCondition => ctx => false; // Triggered by timer
    public IStateAction? Action => null;
    public IReadOnlyList<string> NextStateIds => new[] { "idle", "trading" };
    public TimeSpan Timeout => TimeSpan.FromMinutes(10);
    public RetryPolicy RetryPolicy => new(2, 60000);
    public EvidencePolicy EvidencePolicy => new(true, false);
}

public sealed class TradingState : IStateDefinition
{
    public string Id => "trading";
    public string Name => "Trading";
    public string Category => "economy";
    public Func<PerceptionContext?, bool> EntryCondition => ctx => 
        ctx?.Detections.Any(d => d.DetectorName.Contains("trade") && d.IsMatch) ?? false;
    public IStateAction? Action => null;
    public IReadOnlyList<string> NextStateIds => new[] { "idle", "vending" };
    public TimeSpan Timeout => TimeSpan.FromMinutes(5);
    public RetryPolicy RetryPolicy => new(1, 30000);
    public EvidencePolicy EvidencePolicy => new(true, false);
}

public sealed class MarketAnalysisState : IStateDefinition
{
    public string Id => "market_analysis";
    public string Name => "Market Analysis";
    public string Category => "economy";
    public Func<PerceptionContext?, bool> EntryCondition => ctx => false; // Timer triggered
    public IStateAction? Action => null;
    public IReadOnlyList<string> NextStateIds => new[] { "idle", "vending" };
    public TimeSpan Timeout => TimeSpan.FromMinutes(15);
    public RetryPolicy RetryPolicy => new(1, 300000);
    public EvidencePolicy EvidencePolicy => new(false, false);
}

/// <summary>
/// DI Extensions for Economy Trading module.
/// </summary>
public static class EconomyTradingExtensions
{
    public static IServiceCollection AddKkbotEconomyTrading(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EconomyTradingOptions>(configuration.GetSection("EconomyTrading"));

        services.AddSingleton<EconomyTradingModule>();
        services.AddScoped<IEconomyTradingService, EconomyTradingService>();

        return services;
    }
}

/// <summary>
/// Service interface for economy trading operations.
/// </summary>
public interface IEconomyTradingService
{
    void RegisterInstance(string accountId, IDeviceSession session);
    void UnregisterInstance(string accountId);
    Task<bool> OpenShopAsync(string accountId, CancellationToken ct = default);
    Task<bool> CloseShopAsync(string accountId, CancellationToken ct = default);
    Task<bool> RestockShopAsync(string accountId, CancellationToken ct = default);
    Task<bool> TransferToMasterAsync(string accountId, CancellationToken ct = default);
    Task<bool> InitiateTradeAsync(string fromAccountId, string toAccountId, CancellationToken ct = default);
    Task<bool> SendMailAsync(string fromAccountId, string toAccountId, TransferPayload payload, CancellationToken ct = default);
    Task<MarketData?> GetMarketDataAsync(string itemName);
    EconomyState? GetEconomyState(string accountId);
}

/// <summary>
/// Service implementation for economy trading operations.
/// </summary>
public sealed class EconomyTradingService : IEconomyTradingService
{
    private readonly EconomyTradingModule _module;

    public EconomyTradingService(EconomyTradingModule module)
    {
        _module = module;
    }

    public void RegisterInstance(string accountId, IDeviceSession session)
        => _module.RegisterInstance(accountId, session);

    public void UnregisterInstance(string accountId)
        => _module.UnregisterInstance(accountId);

    public async Task<bool> OpenShopAsync(string accountId, CancellationToken ct = default)
    {
        // Implementation would open shop
        return true;
    }

    public async Task<bool> CloseShopAsync(string accountId, CancellationToken ct = default)
    {
        return true;
    }

    public async Task<bool> RestockShopAsync(string accountId, CancellationToken ct = default)
    {
        return true;
    }

    public async Task<bool> TransferToMasterAsync(string accountId, CancellationToken ct = default)
    {
        return true;
    }

    public async Task<bool> InitiateTradeAsync(string fromAccountId, string toAccountId, CancellationToken ct = default)
    {
        return true;
    }

    public async Task<bool> SendMailAsync(string fromAccountId, string toAccountId, TransferPayload payload, CancellationToken ct = default)
    {
        return true;
    }

    public Task<MarketData?> GetMarketDataAsync(string itemName)
    {
        return Task.FromResult<MarketData?>(null);
    }

    public EconomyState? GetEconomyState(string accountId)
    {
        return null;
    }
}