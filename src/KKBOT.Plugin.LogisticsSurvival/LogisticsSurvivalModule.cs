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

namespace KKBOT.Plugin.LogisticsSurvival;

/// <summary>
/// Potion types for healing and mana restoration.
/// </summary>
public enum PotionType
{
    Health,
    Mana,
    Stamina,
    Elixir,
    Custom
}

/// <summary>
/// Potion configuration for auto-consumption.
/// </summary>
public sealed record PotionConfig
{
    public string Name { get; init; } = string.Empty;
    public PotionType Type { get; init; } = PotionType.Health;
    public string Hotkey { get; init; } = string.Empty; // e.g., "1", "F1", "Ctrl+1"
    public int SlotIndex { get; init; } = 0; // Inventory slot (0-based)
    public double ThresholdPercent { get; init; } = 0.4; // Use when below this %
    public int CooldownMs { get; init; } = 2000; // Cooldown between uses
    public bool Enabled { get; init; } = true;
    public string ItemName { get; init; } = string.Empty; // For inventory detection
    public int MinQuantity { get; init; } = 5; // Alert when below this
}

/// <summary>
/// Buff skill configuration.
/// </summary>
public sealed record BuffConfig
{
    public string Name { get; init; } = string.Empty;
    public string SkillHotkey { get; init; } = string.Empty;
    public TimeSpan Cooldown { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan Duration { get; init; } = TimeSpan.FromMinutes(30);
    public string BuffIconTemplate { get; init; } = string.Empty; // Template for detection
    public BuffCondition Condition { get; init; } = BuffCondition.Always;
    public bool Enabled { get; init; } = true;
}

public enum BuffCondition
{
    Always,
    InCombat,
    OutOfCombat,
    LowHealth,
    LowMana,
    Custom
}

/// <summary>
/// NPC interaction configuration.
/// </summary>
public sealed record NPCConfig
{
    public string Name { get; init; } = string.Empty;
    public NPCType Type { get; init; } = NPCType.Merchant;
    public string Location { get; init; } = string.Empty; // Map coordinates or zone name
    public string InteractionHotkey { get; init; } = "F"; // Default interaction key
    public string[] DialogKeywords { get; init; } = Array.Empty<string>(); // Keywords to detect in dialog
    public Dictionary<string, int> BuyList { get; init; } = new(); // Item name -> quantity
    public string[] RepairSlots { get; init; } = Array.Empty<string>(); // Equipment slots to repair
    public double MaxRepairCost { get; init; } = 10000; // Max gold for repairs
}

public enum NPCType
{
    Merchant,
    Blacksmith,
    Warehouse,
    Healer,
    QuestGiver,
    Teleporter
}

/// <summary>
/// Inventory item filter rule.
/// </summary>
public sealed record ItemFilterRule
{
    public string Name { get; init; } = string.Empty;
    public FilterAction Action { get; init; } = FilterAction.Keep;
    public ItemQuality? MinQuality { get; init; }
    public ItemQuality? MaxQuality { get; init; }
    public ItemType? ItemType { get; init; }
    public string[] RequiredStats { get; init; } = Array.Empty<string>(); // e.g., "STR>50", "INT>30"
    public string[] ForbiddenStats { get; init; } = Array.Empty<string>();
    public int MinItemLevel { get; init; } = 0;
    public int MaxItemLevel { get; init; } = int.MaxValue;
    public int MinValue { get; init; } = 0;
    public string[] RequiredKeywords { get; init; } = Array.Empty<string>();
    public string[] ForbiddenKeywords { get; init; } = Array.Empty<string>();
    public int Priority { get; init; } = 0; // Higher = evaluated first
}

public enum FilterAction
{
    Keep,
    Sell,
    Destroy,
    Store,
    MoveToWarehouse
}

public enum ItemQuality
{
    Junk = 0,
    Common = 1,
    Uncommon = 2,
    Rare = 3,
    Epic = 4,
    Legendary = 5,
    Mythic = 6,
    Artifact = 7
}

public enum ItemType
{
    Weapon,
    Armor,
    Accessory,
    Consumable,
    Material,
    Quest,
    Recipe,
    Gem,
    Scroll,
    Other
}

/// <summary>
/// Logistics & Survival module configuration.
/// </summary>
public sealed class LogisticsSurvivalOptions
{
    // Auto Heal/Buff
    public bool EnableAutoHeal { get; set; } = true;
    public bool EnableAutoBuff { get; set; } = true;
    public double GlobalHealThresholdPercent { get; set; } = 0.4;
    public double GlobalManaThresholdPercent { get; set; } = 0.3;
    public List<PotionConfig> Potions { get; set; } = new();
    public List<BuffConfig> Buffs { get; set; } = new();
    public int HealCheckIntervalMs { get; set; } = 500;
    public int BuffCheckIntervalMs { get; set; } = 5000;

    // Auto Refill & Repair
    public bool EnableAutoRefill { get; set; } = true;
    public bool EnableAutoRepair { get; set; } = true;
    public int RefillThresholdPercent { get; set; } = 20; // % of max quantity
    public double RepairDurabilityThreshold { get; set; } = 0.2; // 20% durability
    public double MaxRepairCost { get; set; } = 10000;
    public string TownPortalHotkey { get; set; } = "T"; // Town portal scroll/skill
    public string RecallHotkey { get; set; } = "B"; // Recall/hearthstone
    public int RefillCheckIntervalMs { get; set; } = 30000;
    public List<NPCConfig> KnownNPCs { get; set; } = new();

    // Smart Inventory
    public bool EnableSmartInventory { get; set; } = true;
    public int InventoryCheckIntervalMs { get; set; } = 10000;
    public List<ItemFilterRule> FilterRules { get; set; } = new();
    public bool AutoEquipBetterGear { get; set; } = true;
    public bool AutoSellJunk { get; set; } = true;
    public bool AutoDestroyWorthless { get; set; } = false;
    public int MaxInventoryUsagePercent { get; set; } = 80;

    // Navigation
    public string GrindSpotCoordinates { get; set; } = string.Empty; // x,y,z or zone name
    public string SafeZoneCoordinates { get; set; } = string.Empty;
    public double MaxDistanceFromGrindSpot { get; set; } = 500; // Max distance before return

    // Monitoring
    public int HealthCheckIntervalMs { get; set; } = 1000;
    public int StuckDetectionTimeoutMs { get; set; } = 30000;
    public bool EnableStuckDetection { get; set; } = true;
}

/// <summary>
/// Module 4: Logistics & Survival - Handles auto heal/buff, refill/repair, inventory management.
/// </summary>
public sealed class LogisticsSurvivalModule : IGameModule
{
    private readonly ILogger<LogisticsSurvivalModule> _logger;
    private readonly LogisticsSurvivalOptions _options;
    private readonly IDeviceSessionFactory _deviceFactory;
    private readonly IPerceptionPipeline _perceptionPipeline;
    private readonly IAiService _aiService;
    private readonly IInstanceManager _instanceManager;
    private readonly IMetricsRegistry _metrics;

    private readonly ConcurrentDictionary<string, LogisticsState> _instanceStates = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastHealTime = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastBuffTime = new();
    private readonly ConcurrentDictionary<string, Dictionary<string, DateTimeOffset>> _potionCooldowns = new();
    private readonly ConcurrentDictionary<string, Dictionary<string, DateTimeOffset>> _buffCooldowns = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastInventoryCheck = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastRefillCheck = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastPosition = new();
    private readonly ConcurrentDictionary<string, Point3D> _lastKnownPosition = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastMovement = new();
    private readonly Timer? _healthMonitorTimer;
    private readonly Timer? _buffMonitorTimer;
    private readonly Timer? _inventoryTimer;
    private readonly Timer? _refillTimer;
    private readonly Timer? _stuckDetectionTimer;
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly Random _random = new();
    private readonly IMetricsRegistry _metrics;
    private bool _disposed;

    public string Id => "logistics-survival";
    public string Version => "1.0.0";

    public LogisticsSurvivalModule(
        ILogger<LogisticsSurvivalModule> logger,
        IOptions<LogisticsSurvivalOptions> options,
        IDeviceSessionFactory deviceFactory,
        IPerceptionPipeline perceptionPipeline,
        IAiService aiService,
        IInstanceManager instanceManager,
        IMetricsRegistry metrics)
    {
        _logger = logger;
        _options = options.Value;
        _deviceFactory = deviceFactory;
        _perceptionPipeline = perceptionPipeline;
        _aiService = aiService;
        _instanceManager = instanceManager;
        _metrics = metrics;

        _healthMonitorTimer = new Timer(MonitorHealthAsync, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(_options.HealthCheckIntervalMs));
        _buffMonitorTimer = new Timer(MonitorBuffsAsync, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(_options.BuffCheckIntervalMs));
        _inventoryTimer = new Timer(MonitorInventoryAsync, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(_options.InventoryCheckIntervalMs));
        _refillTimer = new Timer(CheckRefillAsync, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(_options.RefillCheckIntervalMs));
        _stuckDetectionTimer = new Timer(CheckStuckAsync, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(_options.StuckDetectionTimeoutMs));
    }

    public ModuleManifest GetManifest()
    {
        return new ModuleManifest
        {
            Id = Id,
            Name = "Logistics & Survival",
            Version = Version,
            Author = "KKBOT Team",
            Description = "Auto heal/buff, auto refill/repair, smart inventory management, and survival utilities",
            SupportedResolutions = new[] { new Resolution(1920, 1080), new Resolution(1280, 720) },
            RequiredPermissions = new[] { "DEVICE_READ", "DEVICE_WRITE", "INPUT_WRITE", "SCREENSHOT_CAPTURE", "PERCEPTION_USE" },
            ConfigSchema = JsonSerializer.Serialize(new LogisticsSurvivalOptions()),
            DefaultConfig = JsonSerializer.Serialize(new LogisticsSurvivalOptions()),
            EntryAssembly = "KKBOT.Plugin.LogisticsSurvival.dll",
            Assets = new[] 
            { 
                "templates/hp_bar.png",
                "templates/mana_bar.png",
                "templates/stamina_bar.png",
                "templates/buff_icons/",
                "templates/inventory/",
                "templates/npc_dialogs/",
                "templates/item_icons/"
            }
        };
    }

    public IReadOnlyCollection<IGameDetector> GetDetectors() => Array.Empty<IGameDetector>();
    public IReadOnlyCollection<IGameTaskDefinition> GetTasks() => new[] 
    { 
        new AutoHealTask(), 
        new AutoBuffTask(), 
        new AutoRefillTask(), 
        new AutoRepairTask(), 
        new SmartInventoryTask(),
        new ReturnToGrindSpotTask()
    };
    public IReadOnlyCollection<IStateDefinition> GetStates() => new[] 
    { 
        new IdleState(), 
        new InCombatState(), 
        new HealingState(), 
        new BuffingState(), 
        new RefillingState(), 
        new RepairingState(), 
        new InventoryManagementState(), 
        new ReturningToGrindState(),
        new FleeingState()
    };

    public async Task<ModuleValidationResult> ValidateAsync(ModuleContext context, CancellationToken ct)
    {
        var errors = new List<ModuleValidationError>();

        if (_options.EnableAutoHeal && _options.Potions.Count == 0)
        {
            errors.Add(new ModuleValidationError("NO_POTIONS", "Auto heal enabled but no potions configured"));
        }

        if (_options.EnableAutoBuff && _options.Buffs.Count == 0)
        {
            errors.Add(new ModuleValidationError("NO_BUFFS", "Auto buff enabled but no buffs configured"));
        }

        if (_options.EnableAutoRefill && _options.KnownNPCs.Count == 0)
        {
            errors.Add(new ModuleValidationError("NO_NPCS", "Auto refill enabled but no NPCs configured"));
        }

        if (string.IsNullOrEmpty(_options.GrindSpotCoordinates))
        {
            errors.Add(new ModuleValidationError("NO_GRIND_SPOT", "Grind spot coordinates not configured"));
        }

        return new ModuleValidationResult(errors.Count == 0, errors);
    }

    /// <summary>
    /// Registers an instance for logistics management.
    /// </summary>
    public void RegisterInstance(string accountId, IDeviceSession session)
    {
        _instanceStates[accountId] = new LogisticsState
        {
            AccountId = accountId,
            Session = session,
            CurrentState = LogisticsStateType.Idle,
            LastUpdated = DateTimeOffset.UtcNow
        };
        _lastPosition[accountId] = DateTimeOffset.UtcNow;
        _lastMovement[accountId] = DateTimeOffset.UtcNow;
        _logger.LogInformation("Registered instance {AccountId} for logistics management", accountId);
    }

    /// <summary>
    /// Unregisters an instance.
    /// </summary>
    public void UnregisterInstance(string accountId)
    {
        _instanceStates.TryRemove(accountId, out _);
        _lastHealTime.TryRemove(accountId, out _);
        _lastBuffTime.TryRemove(accountId, out _);
        _potionCooldowns.TryRemove(accountId, out _);
        _buffCooldowns.TryRemove(accountId, out _);
        _lastInventoryCheck.TryRemove(accountId, out _);
        _lastRefillCheck.TryRemove(accountId, out _);
        _lastPosition.TryRemove(accountId, out _);
        _lastKnownPosition.TryRemove(accountId, out _);
        _lastMovement.TryRemove(accountId, out _);
    }

    /// <summary>
    /// Updates the current position of an instance.
    /// </summary>
    public void UpdatePosition(string accountId, Point3D position)
    {
        var now = DateTimeOffset.UtcNow;
        _lastKnownPosition[accountId] = position;
        _lastPosition[accountId] = now;
        
        if (_lastKnownPosition.TryGetValue(accountId, out var lastPos))
        {
            var distance = CalculateDistance(lastPos, position);
            if (distance > 1.0) // Moved more than 1 unit
            {
                _lastMovement[accountId] = DateTimeOffset.UtcNow;
            }
        }
    }

    // ==================== HEALTH MONITORING ====================

    private async void MonitorHealthAsync(object? state)
    {
        if (_shutdownCts.Token.IsCancellationRequested) return;

        try
        {
            foreach (var kvp in _instanceStates)
            {
                var accountId = kvp.Key;
                var state = kvp.Value;
                
                if (state.Session == null) continue;

                var frame = await state.Session.CaptureAsync(new CaptureOptions(), CancellationToken.None);
                var results = await _perceptionPipeline.ProcessAsync(frame, CancellationToken.None);

                // Check HP
                var hpResult = results.FirstOrDefault(r => r.DetectorName.Contains("hp") || r.DetectorName.Contains("health"));
                if (hpResult != null && hpResult.IsMatch)
                {
                    var hpPercent = hpResult.Confidence; // Assuming confidence maps to HP%
                    await CheckAndHealAsync(accountId, hpPercent, CancellationToken.None);
                }

                // Check Mana
                var manaResult = results.FirstOrDefault(r => r.DetectorName.Contains("mana") || r.DetectorName.Contains("mp"));
                if (manaResult != null && manaResult.IsMatch)
                {
                    var manaPercent = manaResult.Confidence;
                    await CheckAndUseManaPotionAsync(accountId, manaPercent, CancellationToken.None);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error monitoring health");
        }
    }

    private async Task CheckAndHealAsync(string accountId, double hpPercent, CancellationToken ct)
    {
        if (!_options.EnableAutoHeal) return;

        var threshold = _options.GlobalHealThresholdPercent;
        var potions = _options.Potions.Where(p => p.Type == PotionType.Health && p.Enabled).ToList();

        foreach (var potion in potions)
        {
            var effectiveThreshold = potion.ThresholdPercent > 0 ? potion.ThresholdPercent : _options.GlobalHealThresholdPercent;
            
            if (hpPercent <= effectiveThreshold)
            {
                // Check cooldown
                var cooldownKey = $"potion_{potion.Name}";
                if (_potionCooldowns.TryGetValue(accountId, out var cooldowns) &&
                    cooldowns.TryGetValue(cooldownKey, out var lastUse) &&
                    DateTimeOffset.UtcNow - lastUse < TimeSpan.FromMilliseconds(potion.CooldownMs))
                {
                    continue; // On cooldown
                }

                _logger.LogInformation("Account {AccountId} HP at {HpPercent}%, using {PotionName}", accountId, hpPercent * 100, potion.Name);

                // Use potion
                if (state.Session != null)
                {
                    await state.Session.SendInputAsync(new InputCommand
                    {
                        Type = InputType.Key,
                        Text = potion.Hotkey
                    }, CancellationToken.None);

                    // Update cooldown
                    _potionCooldowns.GetOrAdd(accountId, _ => new())[cooldownKey] = DateTimeOffset.UtcNow;
                    _metrics.IncrementHttpRequests(); // Reuse metric
                    
                    await Task.Delay(potion.CooldownMs, ct);
                    break; // Use one potion at a time
                }
            }
        }
    }

    private async Task CheckAndUseManaPotionAsync(string accountId, double manaPercent, CancellationToken ct)
    {
        if (!_options.EnableAutoHeal) return; // Reuse heal config for mana

        var threshold = _options.GlobalManaThresholdPercent;
        var potions = _options.Potions.Where(p => p.Type == PotionType.Mana && p.Enabled).ToList();

        foreach (var potion in potions)
        {
            var effectiveThreshold = potion.ThresholdPercent > 0 ? potion.ThresholdPercent : _options.GlobalManaThresholdPercent;
            
            if (manaPercent <= effectiveThreshold)
            {
                var cooldownKey = $"potion_{potion.Name}";
                if (_potionCooldowns.TryGetValue(accountId, out var cooldowns) &&
                    cooldowns.TryGetValue(cooldownKey, out var lastUse) &&
                    DateTimeOffset.UtcNow - lastUse < TimeSpan.FromMilliseconds(potion.CooldownMs))
                {
                    continue;
                }

                _logger.LogInformation("Account {AccountId} Mana at {ManaPercent}%, using {PotionName}", accountId, manaPercent * 100, potion.Name);

                if (state.Session != null)
                {
                    await state.Session.SendInputAsync(new InputCommand
                    {
                        Type = InputType.Key,
                        Text = potion.Hotkey
                    }, CancellationToken.None);

                    _potionCooldowns.GetOrAdd(accountId, _ => new())[cooldownKey] = DateTimeOffset.UtcNow;
                    _metrics.IncrementHttpRequests();
                    
                    await Task.Delay(potion.CooldownMs, ct);
                    break;
                }
            }
        }
    }

    // ==================== BUFF MONITORING ====================

    private async void MonitorBuffsAsync(object? state)
    {
        if (!_options.EnableAutoBuff) return;
        if (_shutdownCts.Token.IsCancellationRequested) return;

        try
        {
            foreach (var kvp in _instanceStates)
            {
                var accountId = kvp.Key;
                var state = kvp.Value;

                if (state.Session == null) continue;

                foreach (var buff in _options.Buffs.Where(b => b.Enabled))
                {
                    var cooldownKey = $"buff_{buff.Name}";
                    
                    if (_buffCooldowns.TryGetValue(accountId, out var cooldowns) &&
                        cooldowns.TryGetValue(cooldownKey, out var lastUse) &&
                        DateTimeOffset.UtcNow - lastUse < buff.Cooldown)
                    {
                        continue; // On cooldown
                    }

                    // Check if buff is active (via perception)
                    var frame = await state.Session.CaptureAsync(new CaptureOptions(), CancellationToken.None);
                    var results = await _perceptionPipeline.ProcessAsync(frame, CancellationToken.None);

                    var buffActive = results.Any(r => 
                        r.DetectorName.Contains(buff.Name, StringComparison.OrdinalIgnoreCase) && r.IsMatch);

                    if (!buffActive)
                    {
                        // Check condition
                        if (ShouldApplyBuff(buff, state))
                        {
                            _logger.LogInformation("Applying buff {BuffName} for account {AccountId}", buff.Name, accountId);

                            if (state.Session != null)
                            {
                                await state.Session.SendInputAsync(new InputCommand
                                {
                                    Type = InputType.Key,
                                    Text = buff.SkillHotkey
                                }, CancellationToken.None);

                                _buffCooldowns.GetOrAdd(accountId, _ => new())[cooldownKey] = DateTimeOffset.UtcNow;
                                _metrics.IncrementHttpRequests();
                                
                                await Task.Delay(1000); // Wait for cast
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error monitoring buffs");
        }
    }

    private bool ShouldApplyBuff(BuffConfig buff, LogisticsState state)
    {
        return buff.Condition switch
        {
            BuffCondition.Always => true,
            BuffCondition.InCombat => state.CurrentState == LogisticsStateType.InCombat,
            BuffCondition.OutOfCombat => state.CurrentState != LogisticsStateType.InCombat,
            BuffCondition.LowHealth => state.CurrentHpPercent < 0.5,
            BuffCondition.LowMana => state.CurrentManaPercent < 0.3,
            BuffCondition.Custom => true, // Would need custom logic
            _ => false
        };
    }

    // ==================== REFILL & REPAIR ====================

    private async void CheckRefillAsync(object? state)
    {
        if (!_options.EnableAutoRefill && !_options.EnableAutoRepair) return;
        if (_shutdownCts.Token.IsCancellationRequested) return;

        try
        {
            foreach (var kvp in _instanceStates)
            {
                var accountId = kvp.Key;
                var state = kvp.Value;

                if (state.Session == null) continue;

                // Check potion quantities
                if (_options.EnableAutoRefill)
                {
                    await CheckAndRefillPotionsAsync(accountId, CancellationToken.None);
                }

                // Check equipment durability
                if (_options.EnableAutoRepair)
                {
                    await CheckAndRepairEquipmentAsync(accountId, CancellationToken.None);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking refill/repair");
        }
    }

    private async Task CheckAndRefillPotionsAsync(string accountId, CancellationToken ct)
    {
        var state = _instanceStates.GetValueOrDefault(accountId);
        if (state?.Session == null) return;

        // Check inventory for potion quantities
        // This would use perception to read inventory counts
        var frame = await state.Session.CaptureAsync(new CaptureOptions(), ct);
        var results = await _perceptionPipeline.ProcessAsync(frame, ct);

        // Check each configured potion
        foreach (var potion in _options.Potions.Where(p => p.Enabled))
        {
            // In real implementation, would detect quantity from inventory
            // For now, simulate checking
            var quantity = await GetItemQuantityAsync(state.Session, potion.ItemName, ct);
            
            if (quantity <= potion.MinQuantity)
            {
                _logger.LogWarning("Account {AccountId} low on {ItemName}: {Quantity}", accountId, potion.ItemName, quantity);
                await GoToTownAndBuyAsync(accountId, potion, ct);
            }
        }
    }

    private async Task<int> GetItemQuantityAsync(IDeviceSession session, string itemName, CancellationToken ct)
    {
        // In real implementation, would use OCR or template matching to read inventory
        // For now, return a simulated value
        await Task.Delay(100, ct);
        return _random.Next(0, 20);
    }

    private async Task GoToTownAndBuyAsync(string accountId, PotionConfig potion, CancellationToken ct)
    {
        var state = _instanceStates.GetValueOrDefault(accountId);
        if (state?.Session == null) return;

        var originalState = state.CurrentState;
        state.CurrentState = LogisticsStateType.Refilling;

        try
        {
            // 1. Use town portal / recall
            await state.Session.SendInputAsync(new InputCommand
            {
                Type = InputType.Key,
                Text = _options.TownPortalHotkey
            }, ct);
            await Task.Delay(3000, ct);

            // 2. Navigate to NPC (simplified - would use navigation system)
            var npc = _options.KnownNPCs.FirstOrDefault(n => n.Type == NPCType.Merchant);
            if (npc != null)
            {
                // Navigate to NPC location
                await NavigateToNPCAsync(state.Session, npc, ct);
                
                // Interact with NPC
                await state.Session.SendInputAsync(new InputCommand
                {
                    Type = InputType.Key,
                    Text = npc.InteractionHotkey
                }, ct);
                await Task.Delay(1000, ct);

                // Buy potions (simplified)
                await BuyItemsAsync(state.Session, new Dictionary<string, int> { [potion.ItemName] = 50 }, ct);
            }

            // 3. Return to grind spot
            await ReturnToGrindSpotAsync(accountId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refill potions for {AccountId}", accountId);
        }
        finally
        {
            state.CurrentState = originalState;
        }
    }

    private async Task NavigateToNPCAsync(IDeviceSession session, NPCConfig npc, CancellationToken ct)
    {
        // Simplified navigation - would use pathfinding in real implementation
        _logger.LogInformation("Navigating to NPC {NpcName} at {Location}", npc.Name, npc.Location);
        await Task.Delay(5000, ct); // Simulate travel time
    }

    private async Task BuyItemsAsync(IDeviceSession session, Dictionary<string, int> items, CancellationToken ct)
    {
        // Simplified - would interact with shop UI
        foreach (var item in items)
        {
            _logger.LogInformation("Buying {Quantity}x {ItemName}", item.Value, item.Key);
            await Task.Delay(500, ct);
        }
    }

    private async Task CheckAndRepairEquipmentAsync(string accountId, CancellationToken ct)
    {
        var state = _instanceStates.GetValueOrDefault(accountId);
        if (state?.Session == null) return;

        // Check equipment durability via perception
        var frame = await state.Session.CaptureAsync(new CaptureOptions(), ct);
        var results = await _perceptionPipeline.ProcessAsync(frame, ct);

        var durabilityResults = results.Where(r => r.DetectorName.Contains("durability") || r.DetectorName.Contains("equipment")).ToList();
        
        foreach (var result in durabilityResults)
        {
            if (result.IsMatch && result.Confidence < _options.RepairDurabilityThreshold)
            {
                _logger.LogWarning("Equipment durability low: {Detector} at {Confidence}%", result.DetectorName, result.Confidence * 100);
                await GoToBlacksmithAndRepairAsync(accountId, ct);
                break;
            }
        }
    }

    private async Task GoToBlacksmithAndRepairAsync(string accountId, CancellationToken ct)
    {
        var state = _instanceStates.GetValueOrDefault(accountId);
        if (state?.Session == null) return;

        var originalState = state.CurrentState;
        state.CurrentState = LogisticsStateType.Repairing;

        try
        {
            // Use town portal
            await state.Session.SendInputAsync(new InputCommand
            {
                Type = InputType.Key,
                Text = _options.TownPortalHotkey
            }, ct);
            await Task.Delay(3000, ct);

            // Navigate to blacksmith
            var blacksmith = _options.KnownNPCs.FirstOrDefault(n => n.Type == NPCType.Blacksmith);
            if (blacksmith != null)
            {
                await NavigateToNPCAsync(state.Session, blacksmith, ct);
                await state.Session.SendInputAsync(new InputCommand
                {
                    Type = InputType.Key,
                    Text = blacksmith.InteractionHotkey
                }, ct);
                await Task.Delay(1000, ct);

                // Repair all
                await RepairAllAsync(state.Session, ct);
            }

            await ReturnToGrindSpotAsync(accountId, ct);
        }
        finally
        {
            state.CurrentState = originalState;
        }
    }

    private async Task RepairAllAsync(IDeviceSession session, CancellationToken ct)
    {
        // Click repair all button in blacksmith UI
        await session.SendInputAsync(new InputCommand
        {
            Type = InputType.Key,
            Text = "R" // Repair hotkey
        }, ct);
        await Task.Delay(2000, ct);
    }

    private async Task ReturnToGrindSpotAsync(string accountId, CancellationToken ct)
    {
        var state = _instanceStates.GetValueOrDefault(accountId);
        if (state?.Session == null) return;

        state.CurrentState = LogisticsStateType.ReturningToGrind;

        // Use recall or walk back
        await state.Session.SendInputAsync(new InputCommand
        {
            Type = InputType.Key,
            Text = _options.RecallHotkey
        }, CancellationToken.None);
        await Task.Delay(3000, ct);

        // Navigate to grind spot coordinates
        if (!string.IsNullOrEmpty(_options.GrindSpotCoordinates))
        {
            await NavigateToCoordinatesAsync(state.Session, _options.GrindSpotCoordinates, ct);
        }

        state.CurrentState = LogisticsStateType.Idle;
    }

    private async Task NavigateToCoordinatesAsync(IDeviceSession session, string coordinates, CancellationToken ct)
    {
        // Parse coordinates and navigate
        _logger.LogInformation("Navigating to coordinates: {Coordinates}", coordinates);
        await Task.Delay(5000, ct); // Simulate travel
    }

    // ==================== INVENTORY MANAGEMENT ====================

    private async void MonitorInventoryAsync(object? state)
    {
        if (!_options.EnableSmartInventory) return;
        if (_shutdownCts.Token.IsCancellationRequested) return;

        try
        {
            foreach (var kvp in _instanceStates)
            {
                var accountId = kvp.Key;
                var state = kvp.Value;

                if (state.Session == null) continue;

                // Check if inventory is full
                var frame = await state.Session.CaptureAsync(new CaptureOptions(), CancellationToken.None);
                var results = await _perceptionPipeline.ProcessAsync(frame, CancellationToken.None);

                var inventoryFull = results.Any(r => 
                    r.DetectorName.Contains("inventory") && r.IsMatch && r.Confidence > 0.9);

                if (inventoryFull || ShouldManageInventory(accountId))
                {
                    await ManageInventoryAsync(accountId, CancellationToken.None);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error monitoring inventory");
        }
    }

    private bool ShouldManageInventory(string accountId)
    {
        if (!_lastInventoryCheck.TryGetValue(accountId, out var lastCheck))
            return true;

        return DateTimeOffset.UtcNow - lastCheck > TimeSpan.FromMilliseconds(_options.InventoryCheckIntervalMs);
    }

    private async Task ManageInventoryAsync(string accountId, CancellationToken ct)
    {
        var state = _instanceStates.GetValueOrDefault(accountId);
        if (state?.Session == null) return;

        _lastInventoryCheck[accountId] = DateTimeOffset.UtcNow;
        state.CurrentState = LogisticsStateType.InventoryManagement;

        try
        {
            var frame = await state.Session.CaptureAsync(new CaptureOptions(), ct);
            var results = await _perceptionPipeline.ProcessAsync(frame, ct);

            // Apply filter rules
            foreach (var rule in _options.FilterRules.OrderByDescending(r => r.Priority))
            {
                var matchingItems = FindItemsMatchingRule(results, rule);
                
                foreach (var item in matchingItems)
                {
                    await ExecuteFilterActionAsync(state.Session, item, rule.Action, ct);
                }
            }

            // Auto-equip better gear
            if (_options.AutoEquipBetterGear)
            {
                await AutoEquipBetterGearAsync(state.Session, ct);
            }

            // Auto-sell junk
            if (_options.AutoSellJunk)
            {
                await AutoSellJunkAsync(state.Session, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error managing inventory for {AccountId}", accountId);
        }
        finally
        {
            if (_instanceStates.TryGetValue(accountId, out var state))
            {
                state.CurrentState = LogisticsStateType.Idle;
            }
        }
    }

    private List<DetectedItem> FindItemsMatchingRule(List<DetectionResult> results, ItemFilterRule rule)
    {
        // In real implementation, would match items against rule criteria
        // For now, return empty
        return new List<DetectedItem>();
    }

    private async Task ExecuteFilterActionAsync(IDeviceSession session, DetectedItem item, FilterAction action, CancellationToken ct)
    {
        switch (action)
        {
            case FilterAction.Sell:
                await SellItemAsync(session, item, ct);
                break;
            case FilterAction.Destroy:
                await DestroyItemAsync(session, item, ct);
                break;
            case FilterAction.Store:
                await StoreItemAsync(session, item, ct);
                break;
            case FilterAction.MoveToWarehouse:
                await MoveToWarehouseAsync(session, item, ct);
                break;
            case FilterAction.Keep:
            default:
                break;
        }
    }

    private async Task SellItemAsync(IDeviceSession session, DetectedItem item, CancellationToken ct)
    {
        // Right-click item, select sell, confirm
        await session.SendInputAsync(new InputCommand
        {
            Type = InputType.RightClick,
            X = item.BoundingBox?.X ?? 0,
            Y = item.BoundingBox?.Y ?? 0
        }, ct);
        await Task.Delay(500, ct);
        
        await session.SendInputAsync(new InputCommand
        {
            Type = InputType.Key,
            Text = "S" // Sell hotkey
        }, ct);
    }

    private async Task DestroyItemAsync(IDeviceSession session, DetectedItem item, CancellationToken ct)
    {
        // Drag to trash or use destroy hotkey
        await session.SendInputAsync(new InputCommand
        {
            Type = InputType.Key,
            Text = "Delete"
        }, ct);
    }

    private async Task StoreItemAsync(IDeviceSession session, DetectedItem item, CancellationToken ct)
    {
        // Move to warehouse/storage
    }

    private async Task MoveToWarehouseAsync(IDeviceSession session, DetectedItem item, CancellationToken ct)
    {
        // Move to warehouse
    }

    private async Task AutoEquipBetterGearAsync(IDeviceSession session, CancellationToken ct)
    {
        // Compare equipped vs inventory, equip better
        await Task.Delay(100, ct);
    }

    private async Task AutoSellJunkAsync(IDeviceSession session, CancellationToken ct)
    {
        // Sell gray/white items to vendor
        await Task.Delay(100, ct);
    }

    // ==================== STUCK DETECTION ====================

    private async void CheckStuckAsync(object? state)
    {
        if (!_options.EnableStuckDetection) return;
        if (_shutdownCts.Token.IsCancellationRequested) return;

        try
        {
            foreach (var kvp in _instanceStates)
            {
                var accountId = kvp.Key;
                var state = kvp.Value;

                if (!_lastMovement.TryGetValue(accountId, out var lastMove))
                    continue;

                var stuckDuration = DateTimeOffset.UtcNow - lastMove;
                if (stuckDuration > TimeSpan.FromMilliseconds(_options.StuckDetectionTimeoutMs))
                {
                    _logger.LogWarning("Account {AccountId} appears stuck for {Duration}, attempting to unstuck", 
                        accountId, stuckDuration);
                    
                    await HandleStuckAsync(kvp.Value, CancellationToken.None);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking stuck");
        }
    }

    private async Task HandleStuckAsync(LogisticsState state, CancellationToken ct)
    {
        if (state.Session == null) return;

        var originalState = state.CurrentState;
        state.CurrentState = LogisticsStateType.Fleeing;

        try
        {
            // Try to move randomly
            var randomDirection = _random.Next(4);
            var moveKey = randomDirection switch
            {
                0 => "W",
                1 => "A",
                2 => "S",
                3 => "D",
                _ => "W"
            };

            await state.Session.SendInputAsync(new InputCommand
            {
                Type = InputType.Key,
                Text = moveKey,
                Duration = TimeSpan.FromSeconds(_random.Next(1, 3))
            }, ct);

            await Task.Delay(2000, ct);

            // Try jumping
            await state.Session.SendInputAsync(new InputCommand
            {
                Type = InputType.Key,
                Text = "Space"
            }, ct);
        }
        finally
        {
            state.CurrentState = LogisticsStateType.Idle;
        }
    }

    // ==================== HELPER METHODS ====================

    private double CalculateDistance(Point3D a, Point3D b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        var dz = a.Z - b.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private async Task BuyItemsAsync(IDeviceSession session, Dictionary<string, int> items, CancellationToken ct)
    {
        foreach (var item in items)
        {
            _logger.LogInformation("Buying {Quantity}x {ItemName}", item.Value, item.Key);
            await Task.Delay(500, ct);
        }
    }

    private async Task NavigateToNPCAsync(IDeviceSession session, NPCConfig npc, CancellationToken ct)
    {
        _logger.LogInformation("Navigating to NPC {NpcName} at {Location}", npc.Name, npc.Location);
        await Task.Delay(5000, ct);
    }

    private async Task RepairAllAsync(IDeviceSession session, CancellationToken ct)
    {
        await session.SendInputAsync(new InputCommand
        {
            Type = InputType.Key,
            Text = "R"
        }, ct);
        await Task.Delay(2000, ct);
    }

    // ==================== TASKS & STATES ====================

    public sealed class AutoHealTask : IGameTaskDefinition
    {
        public string Id => "auto-heal";
        public string Name => "Auto Heal";
        public string? Description => "Automatically uses health potions when HP is low";
        public TaskType Type => TaskType.Repeating;
        public TimeSpan DefaultTimeout => TimeSpan.FromMinutes(1);
        public RetryPolicy DefaultRetryPolicy => new(3, 5000);
        public bool RequiresApproval => false;

        public async Task<IStateDefinition> ExecuteAsync(TaskContext context, CancellationToken ct)
        {
            return new HealingState();
        }
    }

    public sealed class AutoBuffTask : IGameTaskDefinition
    {
        public string Id => "auto-buff";
        public string Name => "Auto Buff";
        public string? Description => "Automatically maintains buff skills";
        public TaskType Type => TaskType.Repeating;
        public TimeSpan DefaultTimeout => TimeSpan.FromMinutes(5);
        public RetryPolicy DefaultRetryPolicy => new(2, 10000);
        public bool RequiresApproval => false;

        public async Task<IStateDefinition> ExecuteAsync(TaskContext context, CancellationToken ct)
        {
            return new BuffingState();
        }
    }

    public sealed class AutoRefillTask : IGameTaskDefinition
    {
        public string Id => "auto-refill";
        public string Name => "Auto Refill";
        public string? Description => "Automatically refills potions and supplies";
        public TaskType Type => TaskType.Repeating;
        public TimeSpan DefaultTimeout => TimeSpan.FromMinutes(10);
        public RetryPolicy DefaultRetryPolicy => new(2, 30000);
        public bool RequiresApproval => false;

        public async Task<IStateDefinition> ExecuteAsync(TaskContext context, CancellationToken ct)
        {
            return new RefillingState();
        }
    }

    public sealed class AutoRepairTask : IGameTaskDefinition
    {
        public string Id => "auto-repair";
        public string Name => "Auto Repair";
        public string? Description => "Automatically repairs equipment when durability is low";
        public TaskType Type => TaskType.Repeating;
        public TimeSpan DefaultTimeout => TimeSpan.FromMinutes(15);
        public RetryPolicy DefaultRetryPolicy => new(2, 60000);
        public bool RequiresApproval => false;

        public async Task<IStateDefinition> ExecuteAsync(TaskContext context, CancellationToken ct)
        {
            return new RepairingState();
        }
    }

    public sealed class SmartInventoryTask : IGameTaskDefinition
    {
        public string Id => "smart-inventory";
        public string Name => "Smart Inventory";
        public string? Description => "Manages inventory: sells junk, equips better gear, organizes";
        public TaskType Type => TaskType.Repeating;
        public TimeSpan DefaultTimeout => TimeSpan.FromMinutes(5);
        public RetryPolicy DefaultRetryPolicy => new(1, 30000);
        public bool RequiresApproval => false;

        public async Task<IStateDefinition> ExecuteAsync(TaskContext context, CancellationToken ct)
        {
            return new InventoryManagementState();
        }
    }

    public sealed class ReturnToGrindSpotTask : IGameTaskDefinition
    {
        public string Id => "return-to-grind";
        public string Name => "Return to Grind Spot";
        public string? Description => "Returns to the configured grind spot after death or town visit";
        public TaskType Type => TaskType.OneShot;
        public TimeSpan DefaultTimeout => TimeSpan.FromMinutes(5);
        public RetryPolicy DefaultRetryPolicy => new(2, 30000);
        public bool RequiresApproval => false;

        public async Task<IStateDefinition> ExecuteAsync(TaskContext context, CancellationToken ct)
        {
            return new ReturningToGrindState();
        }
    }

    // States
    public sealed class IdleState : IStateDefinition
    {
        public string Id => "idle";
        public string Name => "Idle";
        public string Category => "logistics";
        public Func<PerceptionContext?, bool> EntryCondition => _ => true;
        public IStateAction? Action => null;
        public IReadOnlyList<string> NextStateIds => new[] { "in_combat", "healing", "buffing", "refilling", "inventory_management" };
        public TimeSpan Timeout => TimeSpan.FromMinutes(10);
        public RetryPolicy RetryPolicy => new(0, 0);
        public EvidencePolicy EvidencePolicy => new(false, false);
    }

    public sealed class InCombatState : IStateDefinition
    {
        public string Id => "in_combat";
        public string Name => "In Combat";
        public string Category => "combat";
        public Func<PerceptionContext?, bool> EntryCondition => ctx => 
            ctx?.Detections.Any(d => d.DetectorName.Contains("enemy") && d.IsMatch) ?? false;
        public IStateAction? Action => null;
        public IReadOnlyList<string> NextStateIds => new[] { "idle", "healing", "fleeing" };
        public TimeSpan Timeout => TimeSpan.FromMinutes(10);
        public RetryPolicy RetryPolicy => new(0, 0);
        public EvidencePolicy EvidencePolicy => new(true, true);
    }

    public sealed class HealingState : IStateDefinition
    {
        public string Id => "healing";
        public string Name => "Healing";
        public string Category => "survival";
        public Func<PerceptionContext?, bool> EntryCondition => ctx => 
            ctx?.Detections.Any(d => (d.DetectorName.Contains("hp") || d.DetectorName.Contains("health")) && d.Confidence < 0.5) ?? false;
        public IStateAction? Action => null;
        public IReadOnlyList<string> NextStateIds => new[] { "idle", "in_combat", "fleeing" };
        public TimeSpan Timeout => TimeSpan.FromSeconds(30);
        public RetryPolicy RetryPolicy => new(3, 2000);
        public EvidencePolicy EvidencePolicy => new(true, false);
    }

    public sealed class BuffingState : IStateDefinition
    {
        public string Id => "buffing";
        public string Name => "Buffing";
        public string Category => "survival";
        public Func<PerceptionContext?, bool> EntryCondition => ctx => false; // Triggered by buff monitor
        public IStateAction? Action => null;
        public IReadOnlyList<string> NextStateIds => new[] { "idle", "in_combat" };
        public TimeSpan Timeout => TimeSpan.FromMinutes(1);
        public RetryPolicy RetryPolicy => new(2, 5000);
        public EvidencePolicy EvidencePolicy => new(true, false);
    }

    public sealed class RefillingState : IStateDefinition
    {
        public string Id => "refilling";
        public string Name => "Refilling";
        public string Category => "logistics";
        public Func<PerceptionContext?, bool> EntryCondition => ctx => false; // Triggered by refill monitor
        public IStateAction? Action => null;
        public IReadOnlyList<string> NextStateIds => new[] { "idle", "returning_to_grind" };
        public TimeSpan Timeout => TimeSpan.FromMinutes(5);
        public RetryPolicy RetryPolicy => new(2, 30000);
        public EvidencePolicy EvidencePolicy => new(true, false);
    }

    public sealed class RepairingState : IStateDefinition
    {
        public string Id => "repairing";
        public string Name => "Repairing";
        public string Category => "logistics";
        public Func<PerceptionContext?, bool> EntryCondition => ctx => 
            ctx?.Detections.Any(d => d.DetectorName.Contains("durability") && d.Confidence < 0.3) ?? false;
        public IStateAction? Action => null;
        public IReadOnlyList<string> NextStateIds => new[] { "idle", "returning_to_grind" };
        public TimeSpan Timeout => TimeSpan.FromMinutes(5);
        public RetryPolicy RetryPolicy => new(2, 30000);
        public EvidencePolicy EvidencePolicy => new(true, false);
    }

    public sealed class InventoryManagementState : IStateDefinition
    {
        public string Id => "inventory_management";
        public string Name => "Inventory Management";
        public string Category => "logistics";
        public Func<PerceptionContext?, bool> EntryCondition => ctx => false; // Triggered by inventory monitor
        public IStateAction? Action => null;
        public IReadOnlyList<string> NextStateIds => new[] { "idle", "refilling" };
        public TimeSpan Timeout => TimeSpan.FromMinutes(3);
        public RetryPolicy RetryPolicy => new(1, 30000);
        public EvidencePolicy EvidencePolicy => new(true, false);
    }

    public sealed class ReturningToGrindState : IStateDefinition
    {
        public string Id => "returning_to_grind";
        public string Name => "Returning to Grind Spot";
        public string Category => "navigation";
        public Func<PerceptionContext?, bool> EntryCondition => ctx => false;
        public IStateAction? Action => null;
        public IReadOnlyList<string> NextStateIds => new[] { "idle", "in_combat" };
        public TimeSpan Timeout => TimeSpan.FromMinutes(5);
        public RetryPolicy RetryPolicy => new(2, 30000);
        public EvidencePolicy EvidencePolicy => new(false, false);
    }

    public sealed class FleeingState : IStateDefinition
    {
        public string Id => "fleeing";
        public string Name => "Fleeing";
        public string Category => "survival";
        public Func<PerceptionContext?, bool> EntryCondition => ctx => 
            ctx?.Detections.Any(d => d.DetectorName.Contains("hp") && d.Confidence < 0.15) ?? false;
        public IStateAction? Action => null;
        public IReadOnlyList<string> NextStateIds => new[] { "healing", "idle", "refilling" };
        public TimeSpan Timeout => TimeSpan.FromMinutes(2);
        public RetryPolicy RetryPolicy => new(1, 5000);
        public EvidencePolicy EvidencePolicy => new(true, false);
    }
}

/// <summary>
/// Logistics state for an instance.
/// </summary>
public sealed class LogisticsState
{
    public string AccountId { get; set; } = string.Empty;
    public IDeviceSession? Session { get; set; }
    public LogisticsStateType CurrentState { get; set; } = LogisticsStateType.Idle;
    public double CurrentHpPercent { get; set; } = 1.0;
    public double CurrentManaPercent { get; set; } = 1.0;
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, int> InventoryItems { get; set; } = new();
    public Dictionary<string, double> EquipmentDurability { get; set; } = new();
    public List<string> ActiveBuffs { get; set; } = new();
    public Point3D? CurrentPosition { get; set; }
    public bool IsInCombat { get; set; }
    public DateTimeOffset LastCombatTime { get; set; }
}

public enum LogisticsStateType
{
    Idle,
    InCombat,
    Healing,
    Buffing,
    Refilling,
    Repairing,
    InventoryManagement,
    ReturningToGrind,
    Fleeing
}

/// <summary>
/// 3D point for position tracking.
/// </summary>
public sealed record Point3D(double X, double Y, double Z = 0)
{
    public static Point3D Zero => new(0, 0, 0);
}

/// <summary>
/// Detected item in inventory.
/// </summary>
public sealed record DetectedItem
{
    public string Name { get; init; } = string.Empty;
    public ItemQuality Quality { get; init; } = ItemQuality.Common;
    public ItemType Type { get; init; } = ItemType.Other;
    public int Level { get; init; } = 1;
    public Dictionary<string, int> Stats { get; init; } = new();
    public Rectangle? BoundingBox { get; init; }
    public int Quantity { get; init; } = 1;
    public long Value { get; init; } = 0;
}

/// <summary>
/// 3D rectangle for bounding boxes.
/// </summary>
public sealed record Rectangle(int X, int Y, int Width, int Height)
{
    public static Rectangle Empty => new(0, 0, 0, 0);
}

/// <summary>
/// DI Extensions for Logistics Survival module.
/// </summary>
public static class LogisticsSurvivalExtensions
{
    public static IServiceCollection AddKkbotLogisticsSurvival(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LogisticsSurvivalOptions>(configuration.GetSection("LogisticsSurvival"));

        services.AddSingleton<LogisticsSurvivalModule>();
        services.AddScoped<ILogisticsSurvivalService, LogisticsSurvivalService>();

        return services;
    }
}

/// <summary>
/// Service interface for logistics survival operations.
/// </summary>
public interface ILogisticsSurvivalService
{
    void RegisterInstance(string accountId, IDeviceSession session);
    void UnregisterInstance(string accountId);
    void UpdatePosition(string accountId, Point3D position);
    Task<bool> UsePotionAsync(string accountId, string potionName, CancellationToken ct = default);
    Task<bool> ApplyBuffAsync(string accountId, string buffName, CancellationToken ct = default);
    Task<bool> TriggerRefillAsync(string accountId, CancellationToken ct = default);
    Task<bool> TriggerRepairAsync(string accountId, CancellationToken ct = default);
    Task<bool> TriggerInventoryManagementAsync(string accountId, CancellationToken ct = default);
    Task<bool> ReturnToGrindSpotAsync(string accountId, CancellationToken ct = default);
    LogisticsState? GetInstanceState(string accountId);
}

/// <summary>
/// Service implementation for logistics survival operations.
/// </summary>
public sealed class LogisticsSurvivalService : ILogisticsSurvivalService
{
    private readonly LogisticsSurvivalModule _module;

    public LogisticsSurvivalService(LogisticsSurvivalModule module)
    {
        _module = module;
    }

    public void RegisterInstance(string accountId, IDeviceSession session)
        => _module.RegisterInstance(accountId, session);

    public void UnregisterInstance(string accountId)
        => _module.UnregisterInstance(accountId);

    public void UpdatePosition(string accountId, Point3D position)
        => _module.UpdatePosition(accountId, position);

    public async Task<bool> UsePotionAsync(string accountId, string potionName, CancellationToken ct = default)
    {
        // Implementation would trigger potion use
        return true;
    }

    public async Task<bool> ApplyBuffAsync(string accountId, string buffName, CancellationToken ct = default)
    {
        // Implementation would trigger buff
        return true;
    }

    public async Task<bool> TriggerRefillAsync(string accountId, CancellationToken ct = default)
    {
        // Trigger refill logic
        return true;
    }

    public async Task<bool> TriggerRepairAsync(string accountId, CancellationToken ct = default)
    {
        // Trigger repair
        return true;
    }

    public async Task<bool> TriggerInventoryManagementAsync(string accountId, CancellationToken ct = default)
    {
        // Trigger inventory management
        return true;
    }

    public async Task<bool> ReturnToGrindSpotAsync(string accountId, CancellationToken ct = default)
    {
        // Trigger return to grind spot
        return true;
    }

    public LogisticsState? GetInstanceState(string accountId)
    {
        // Would need to expose state from module
        return null;
    }
}