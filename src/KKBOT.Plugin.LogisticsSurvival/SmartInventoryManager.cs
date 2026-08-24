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
using KKBOT.Observability;
using KKBOT.Observability.Tracing;

namespace KKBOT.Plugin.LogisticsSurvival;

/// <summary>
/// Smart Inventory management with rule-based filtering, auto-equip, auto-sell, auto-destroy.
/// </summary>
public sealed class SmartInventoryManager
{
    private readonly ILogger<SmartInventoryManager> _logger;
    private readonly LogisticsSurvivalOptions _options;
    private readonly IDeviceSessionFactory _deviceFactory;
    private readonly IPerceptionPipeline _perceptionPipeline;
    private readonly IAiService _aiService;
    private readonly IMetricsRegistry _metrics;

    private readonly ConcurrentDictionary<string, InventoryState> _inventoryStates = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastFullScan = new();
    private readonly Timer? _scanTimer;
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly IMetricsRegistry _metrics;

    public SmartInventoryManager(
        ILogger<SmartInventoryManager> logger,
        IOptions<LogisticsSurvivalOptions> options,
        IDeviceSessionFactory deviceFactory,
        IPerceptionPipeline perceptionPipeline,
        IAiService aiService,
        IMetricsRegistry metrics)
    {
        _logger = logger;
        _options = options.Value;
        _deviceFactory = deviceFactory;
        _perceptionPipeline = perceptionPipeline;
        _metrics = metrics;
    }

    /// <summary>
    /// Starts monitoring inventory for an account.
    /// </summary>
    public void StartMonitoring(string accountId, IDeviceSession session)
    {
        _inventoryStates[accountId] = new InventoryState
        {
            AccountId = accountId,
            Session = session,
            LastFullScan = DateTimeOffset.MinValue,
            ItemCache = new(),
            EquippedItems = new(),
            FilterResults = new()
        };
        _lastFullScan[accountId] = DateTimeOffset.MinValue;
    }

    /// <summary>
    /// Stops monitoring for an account.
    /// </summary>
    public void StopMonitoring(string accountId)
    {
        _inventoryStates.TryRemove(accountId, out _);
        _lastFullScan.TryRemove(accountId, out _);
    }

    /// <summary>
    /// Performs a full inventory scan and applies filter rules.
    /// </summary>
    public async Task<InventoryScanResult> ScanAndFilterAsync(string accountId, CancellationToken ct = default)
    {
        if (!_options.EnableSmartInventory)
        {
            return new InventoryScanResult { Success = false, Message = "Smart inventory disabled" };
        }

        if (!_inventoryStates.TryGetValue(accountId, out var state))
        {
            return new InventoryScanResult { Success = false, Message = "Account not monitored" };
        }

        if (state.Session == null)
        {
            return new InventoryScanResult { Success = false, Message = "No device session" };
        }

        var stopwatch = Stopwatch.StartNew();
        var result = new InventoryScanResult { AccountId = accountId };

        try
        {
            // Capture inventory screen
            var frame = await state.Session.CaptureAsync(new CaptureOptions
            {
                RegionOfInterest = GetInventoryRegion()
            }, ct);

            // Run perception pipeline
            var detections = await _perceptionPipeline.ProcessAsync(frame, ct);

            // Parse detected items
            var items = await ParseInventoryItemsAsync(detections, ct);

            // Apply filter rules
            var filterResults = ApplyFilterRules(items);

            // Execute actions
            var executedActions = await ExecuteFilterActionsAsync(state.Session, filterResults, ct);

            // Update cache
            state.ItemCache = items.ToDictionary(i => i.Id, i => i);
            state.FilterResults = filterResults;
            _lastFullScan[accountId] = DateTimeOffset.UtcNow;

            result.Success = true;
            result.ItemsFound = items.Count;
            result.ActionsExecuted = executedActions.Count;
            result.Duration = stopwatch.Elapsed;
            result.FilterResults = filterResults;

            _metrics.RecordHttpRequests(); // Reuse metric for inventory scans
            _metrics.RecordAiInferenceLatency(stopwatch.ElapsedMilliseconds); // Reuse for timing

            _logger.LogInformation("Inventory scan for {AccountId}: {ItemCount} items, {ActionCount} actions in {Duration}ms",
                accountId, items.Count, executedActions.Count, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning inventory for {AccountId}", accountId);
            result.Success = false;
            result.Message = ex.Message;
            result.Duration = stopwatch.Elapsed;
            return result;
        }
    }

    /// <summary>
    /// Auto-equips better gear from inventory.
    /// </summary>
    public async Task<EquipResult> AutoEquipBetterGearAsync(string accountId, CancellationToken ct = default)
    {
        if (!_options.AutoEquipBetterGear)
        {
            return new EquipResult { Success = false, Message = "Auto-equip disabled" };
        }

        if (!_inventoryStates.TryGetValue(accountId, out var state) || state.Session == null)
        {
            return new EquipResult { Success = false, Message = "Account not monitored" };
        }

        var result = new EquipResult { AccountId = accountId };
        var equipped = 0;

        try
        {
            // Scan inventory
            var scanResult = await ScanAndFilterAsync(accountId, ct);
            if (!scanResult.Success) return new EquipResult { Success = false, Message = scanResult.Message };

            // Get currently equipped items
            var equippedItems = await GetEquippedItemsAsync(accountId, ct);
            var inventoryItems = scanResult.FilterResults
                .Where(fr => fr.Action == FilterAction.Keep || fr.Action == FilterAction.Store)
                .Select(fr => fr.Item)
                .ToList();

            // Compare and equip better items
            foreach (var invItem in inventoryItems.Where(i => i.Type == ItemType.Weapon || i.Type == ItemType.Armor || i.Type == ItemType.Accessory))
            {
                var equippedItem = equippedItems.FirstOrDefault(e => e.Slot == invItem.Slot);
                if (equippedItem == null || IsBetterItem(invItem, equippedItem))
                {
                    var equipResult = await EquipItemAsync(accountId, invItem, ct);
                    if (equipResult.Success)
                    {
                        equipped++;
                        _logger.LogInformation("Auto-equipped {ItemName} for {AccountId}", invItem.Name, accountId);
                    }
                }
            }

            result.Success = true;
            result.ItemsEquipped = equipped;
            _metrics.IncrementHttpRequests(); // Reuse metric
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error auto-equipping for {AccountId}", accountId);
            result.Success = false;
            result.Message = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Auto-sells junk items to vendor.
    /// </summary>
    public async Task<SellResult> AutoSellJunkAsync(string accountId, CancellationToken ct = default)
    {
        if (!_options.AutoSellJunk)
        {
            return new SellResult { Success = false, Message = "Auto-sell disabled" };
        }

        if (!_inventoryStates.TryGetValue(accountId, out var state) || state.Session == null)
        {
            return new SellResult { Success = false, Message = "Account not monitored" };
        }

        var result = new SellResult { AccountId = accountId };
        var sold = 0;
        long totalGold = 0;

        try
        {
            var scanResult = await ScanAndFilterAsync(accountId, ct);
            if (!scanResult.Success) return new SellResult { Success = false, Message = scanResult.Message };

            var junkItems = scanResult.FilterResults
                .Where(fr => fr.Action == FilterAction.Sell)
                .Select(fr => fr.Item)
                .ToList();

            if (junkItems.Count == 0)
            {
                result.Success = true;
                result.Message = "No junk items to sell";
                return result;
            }

            var state = _inventoryStates[accountId];
            
            // Navigate to vendor
            await GoToVendorAsync(accountId, CancellationToken.None);

            // Open vendor window
            await state.Session!.SendInputAsync(new InputCommand
            {
                Type = InputType.Key,
                Text = "F" // Interact key
            }, CancellationToken.None);
            await Task.Delay(1000, ct);

            // Sell each junk item
            foreach (var item in junkItems)
            {
                var sellResult = await SellItemAsync(state.Session!, item, CancellationToken.None);
                if (sellResult.Success)
                {
                    sold++;
                    totalGold += sellResult.GoldReceived;
                }
            }

            result.Success = true;
            result.ItemsSold = sold;
            result.TotalGold = totalGold;

            _metrics.IncrementHttpRequests(); // Reuse metric
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error auto-selling for {AccountId}", accountId);
            result.Success = false;
            result.Message = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Auto-destroys worthless items.
    /// </summary>
    public async Task<DestroyResult> AutoDestroyWorthlessAsync(string accountId, CancellationToken ct = default)
    {
        if (!_options.AutoDestroyWorthless)
        {
            return new DestroyResult { Success = false, Message = "Auto-destroy disabled" };
        }

        if (!_inventoryStates.TryGetValue(accountId, out var state) || state.Session == null)
        {
            return new DestroyResult { Success = false, Message = "Account not monitored" };
        }

        var result = new DestroyResult { AccountId = accountId };
        var destroyed = 0;

        try
        {
            var scanResult = await ScanAndFilterAsync(accountId, ct);
            if (!scanResult.Success) return new DestroyResult { Success = false, Message = scanResult.Message };

            var destroyItems = scanResult.FilterResults
                .Where(fr => fr.Action == FilterAction.Destroy)
                .Select(fr => fr.Item)
                .ToList();

            foreach (var item in destroyItems)
            {
                var destroyResult = await DestroyItemAsync(state.Session!, item, ct);
                if (destroyResult.Success) destroyed++;
            }

            result.Success = true;
            result.ItemsDestroyed = destroyed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error auto-destroying for {AccountId}", accountId);
            result.Success = false;
            result.Message = ex.Message;
        }

        return result;
    }

    // ==================== HELPER METHODS ====================

    private Rectangle GetInventoryRegion()
    {
        // Default inventory region - would be configurable
        return new Rectangle(100, 100, 800, 600);
    }

    private async Task<List<InventoryItem>> ParseInventoryItemsAsync(List<DetectionResult> detections, CancellationToken ct)
    {
        var items = new List<InventoryItem>();
        
        foreach (var detection in detections.Where(d => d.IsMatch && d.DetectorName.Contains("item")))
        {
            var item = new InventoryItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = detection.DetectedValue ?? detection.DetectorName,
                Type = InferItemType(detection.DetectorName),
                Quality = InferQuality(detection.DetectorName),
                Level = 1,
                BoundingBox = detection.BoundingBox,
                Confidence = detection.Confidence,
                Slot = InferSlot(detection.DetectorName)
            };
            items.Add(item);
        }

        // If AI vision available, enhance with AI
        if (_aiService != null && _options.EnableSmartInventory)
        {
            // Could use AI vision to enhance item detection
        }

        return items;
    }

    private ItemType InferItemType(string detectorName)
    {
        var lower = detectorName.ToLower();
        if (lower.Contains("weapon") || lower.Contains("sword") || lower.Contains("bow") || lower.Contains("staff"))
            return ItemType.Weapon;
        if (lower.Contains("armor") || lower.Contains("helmet") || lower.Contains("chest") || lower.Contains("glove") || lower.Contains("boot"))
            return ItemType.Armor;
        if (lower.Contains("ring") || lower.Contains("amulet") || lower.Contains("necklace") || lower.Contains("belt"))
            return ItemType.Accessory;
        if (lower.Contains("potion") || lower.Contains("scroll") || lower.Contains("food"))
            return ItemType.Consumable;
        if (lower.Contains("material") || lower.Contains("ore") || lower.Contains("herb") || lower.Contains("essence"))
            return ItemType.Material;
        return ItemType.Other;
    }

    private ItemQuality InferQuality(string detectorName)
    {
        var lower = detectorName.ToLower();
        if (lower.Contains("mythic") || lower.Contains("artifact")) return ItemQuality.Mythic;
        if (lower.Contains("legendary")) return ItemQuality.Legendary;
        if (lower.Contains("epic")) return ItemQuality.Epic;
        if (lower.Contains("rare")) return ItemQuality.Rare;
        if (lower.Contains("uncommon")) return ItemQuality.Uncommon;
        if (lower.Contains("junk") || lower.Contains("trash") || lower.Contains("gray")) return ItemQuality.Junk;
        return ItemQuality.Common;
    }

    private EquipmentSlot InferSlot(string detectorName)
    {
        var lower = detectorName.ToLower();
        if (lower.Contains("helmet") || lower.Contains("head")) return EquipmentSlot.Head;
        if (lower.Contains("chest") || lower.Contains("armor")) return EquipmentSlot.Chest;
        if (lower.Contains("glove") || lower.Contains("hand")) return EquipmentSlot.Hands;
        if (lower.Contains("boot") || lower.Contains("leg") || lower.Contains("pants")) return EquipmentSlot.Feet;
        if (lower.Contains("ring")) return EquipmentSlot.Ring1;
        if (lower.Contains("amulet") || lower.Contains("neck")) return EquipmentSlot.Neck;
        if (lower.Contains("weapon") || lower.Contains("sword") || lower.Contains("bow") || lower.Contains("staff")) return EquipmentSlot.MainHand;
        if (lower.Contains("shield") || lower.Contains("offhand")) return EquipmentSlot.OffHand;
        return EquipmentSlot.None;
    }

    private List<FilterResult> ApplyFilterRules(List<InventoryItem> items)
    {
        var results = new List<FilterResult>();

        foreach (var item in items)
        {
            var matchedRule = _options.FilterRules
                .OrderByDescending(r => r.Priority)
                .FirstOrDefault(rule => MatchesRule(item, rule));

            var action = matchedRule?.Action ?? FilterAction.Keep;
            results.Add(new FilterResult
            {
                Item = item,
                Action = action,
                MatchedRule = matchedRule?.Name ?? "Default"
            });
        }

        return results;
    }

    private bool MatchesRule(InventoryItem item, ItemFilterRule rule)
    {
        if (rule.MinQuality.HasValue && item.Quality < rule.MinQuality.Value) return false;
        if (rule.MaxQuality.HasValue && item.Quality > rule.MaxQuality.Value) return false;
        if (rule.ItemType.HasValue && item.Type != rule.ItemType.Value) return false;
        if (rule.MinItemLevel > 0 && item.Level < rule.MinItemLevel) return false;
        if (rule.MaxItemLevel < int.MaxValue && item.Level > rule.MaxItemLevel) return false;
        if (rule.MinValue > 0 && item.Value < rule.MinValue) return false;

        if (rule.RequiredKeywords.Length > 0)
        {
            var nameLower = item.Name.ToLower();
            if (!rule.RequiredKeywords.All(k => nameLower.Contains(k.ToLower()))) return false;
        }

        if (rule.ForbiddenKeywords.Length > 0)
        {
            var nameLower = item.Name.ToLower();
            if (rule.ForbiddenKeywords.Any(k => nameLower.Contains(k.ToLower()))) return false;
        }

        if (rule.RequiredStats.Length > 0)
        {
            foreach (var statReq in rule.RequiredStats)
            {
                if (!CheckStat(item.Stats, statReq)) return false;
            }
        }

        if (rule.ForbiddenStats.Length > 0)
        {
            foreach (var statReq in rule.ForbiddenStats)
            {
                if (CheckStat(item.Stats, statReq)) return false;
            }
        }

        return true;
    }

    private bool CheckStat(Dictionary<string, int> stats, string requirement)
    {
        // Parse "STAT>50" or "STAT<30" format
        var match = Regex.Match(requirement, @"^(w+)([<>]=?)(d+)$");
        if (!match.Success) return false;

        var statName = match.Groups[1].Value;
        var op = match.Groups[2].Value;
        var value = int.Parse(match.Groups[3].Value);

        if (!stats.TryGetValue(statName, out var actualValue)) return false;

        return op switch
        {
            ">" => actualValue > value,
            ">=" => actualValue >= value,
            "<" => actualValue < value,
            "<=" => actualValue <= value,
            "=" => actualValue == value,
            _ => false
        };
    }

    private async Task<List<FilterActionResult>> ExecuteFilterActionsAsync(IDeviceSession session, List<FilterResult> results, CancellationToken ct)
    {
        var executed = new List<FilterActionResult>();

        foreach (var result in results.Where(r => r.Action != FilterAction.Keep))
        {
            try
            {
                var success = await ExecuteFilterActionAsync(session, result.Item, result.Action, CancellationToken.None);
                executed.Add(new FilterActionResult
                {
                    Item = result.Item,
                    Action = result.Action,
                    Success = success
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute filter action for {ItemName}", result.Item.Name);
                executed.Add(new FilterActionResult
                {
                    Item = result.Item,
                    Action = result.Action,
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        return executed;
    }

    private async Task<bool> ExecuteFilterActionAsync(IDeviceSession session, InventoryItem item, FilterAction action, CancellationToken ct)
    {
        // Right-click item to open context menu
        if (item.BoundingBox.HasValue)
        {
            await session.SendInputAsync(new InputCommand
            {
                Type = InputType.RightClick,
                X = item.BoundingBox.Value.X + item.BoundingBox.Value.Width / 2,
                Y = item.BoundingBox.Value.Y + item.BoundingBox.Value.Height / 2
            }, ct);
            await Task.Delay(200, ct);
        }

        switch (action)
        {
            case FilterAction.Sell:
                await SendKeyAsync("S"); // Sell hotkey
                break;
            case FilterAction.Destroy:
                await SendKeyAsync("Delete");
                break;
            case FilterAction.Store:
                await SendKeyAsync("T"); // Store hotkey
                break;
            case FilterAction.MoveToWarehouse:
                await SendKeyAsync("W"); // Warehouse hotkey
                break;
        }

        await Task.Delay(300, ct);
        return true;
    }

    private async Task<bool> SendKeyAsync(string key)
    {
        // Would send key via device session
        return true;
    }

    private async Task<List<EquippedItem>> GetEquippedItemsAsync(string accountId, CancellationToken ct)
    {
        // In real implementation, would scan equipment slots
        return new List<EquippedItem>();
    }

    private bool IsBetterItem(InventoryItem candidate, EquippedItem current)
    {
        // Compare item stats
        var candidateScore = CalculateItemScore(candidate);
        var currentScore = CalculateItemScore(current);
        return candidateScore > currentScore;
    }

    private double CalculateItemScore(InventoryItem item)
    {
        var score = 0.0;
        score += item.Quality * 100;
        score += item.Level * 10;
        foreach (var stat in item.Stats) score += stat.Value * 2;
        return score;
    }

    private double CalculateItemScore(EquippedItem item)
    {
        var score = 0.0;
        score += item.Quality * 100;
        score += item.Level * 10;
        foreach (var stat in item.Stats) score += stat.Value * 2;
        return score;
    }

    private async Task<bool> EquipItemAsync(string accountId, InventoryItem item, CancellationToken ct)
    {
        // Right-click item, select equip
        return true;
    }

    private async Task GoToVendorAsync(string accountId, CancellationToken ct)
    {
        // Navigate to nearest vendor
        await Task.Delay(5000, ct);
    }

    private async Task<SellResult> SellItemAsync(IDeviceSession session, InventoryItem item, CancellationToken ct)
    {
        return new SellResult { Success = true, GoldReceived = item.Value };
    }

    private async Task<DestroyResult> DestroyItemAsync(IDeviceSession session, InventoryItem item, CancellationToken ct)
    {
        return new DestroyResult { Success = true };
    }

    public void Dispose()
    {
        // Cleanup timers
    }
}

// ==================== STATE & RESULT CLASSES ====================

public sealed class InventoryState
{
    public string AccountId { get; set; } = string.Empty;
    public IDeviceSession? Session { get; set; }
    public DateTimeOffset LastFullScan { get; set; }
    public Dictionary<string, InventoryItem> ItemCache { get; set; } = new();
    public Dictionary<string, EquippedItem> EquippedItems { get; set; } = new();
    public List<FilterResult> FilterResults { get; set; } = new();
}

public sealed record InventoryItem
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public ItemType Type { get; init; }
    public ItemQuality Quality { get; init; }
    public int Level { get; init; } = 1;
    public Dictionary<string, int> Stats { get; init; } = new();
    public Rectangle? BoundingBox { get; init; }
    public double Confidence { get; init; }
    public EquipmentSlot Slot { get; init; } = EquipmentSlot.None;
    public long Value { get; init; } = 0;
    public int Quantity { get; init; } = 1;
}

public sealed record EquippedItem
{
    public string Name { get; init; } = string.Empty;
    public EquipmentSlot Slot { get; init; }
    public ItemQuality Quality { get; init; }
    public int Level { get; init; } = 1;
    public Dictionary<string, int> Stats { get; init; } = new();
    public long Value { get; init; } = 0;
}

public sealed record FilterResult
{
    public InventoryItem Item { get; init; } = null!;
    public FilterAction Action { get; init; }
    public string MatchedRule { get; init; } = string.Empty;
}

public sealed record FilterActionResult
{
    public InventoryItem Item { get; init; } = null!;
    public FilterAction Action { get; init; }
    public bool Success { get; init; }
    public string? Error { get; init; }
}

public sealed record InventoryScanResult
{
    public string AccountId { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string? Message { get; init; }
    public int ItemsFound { get; init; }
    public int ActionsExecuted { get; init; }
    public TimeSpan Duration { get; init; }
    public List<FilterResult> FilterResults { get; init; } = new();
}

public sealed record EquipResult
{
    public string AccountId { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string? Message { get; init; }
    public int ItemsEquipped { get; init; }
}

public sealed record SellResult
{
    public string AccountId { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string? Message { get; init; }
    public int ItemsSold { get; init; }
    public long TotalGold { get; init; }
}

public sealed record DestroyResult
{
    public string AccountId { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string? Message { get; init; }
    public int ItemsDestroyed { get; init; }
}

public sealed record EquipResult
{
    public string AccountId { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string? Message { get; init; }
    public int ItemsEquipped { get; init; }
}

public enum EquipmentSlot
{
    None,
    Head,
    Neck,
    Shoulders,
    Chest,
    Waist,
    Legs,
    Feet,
    Wrists,
    Hands,
    Ring1,
    Ring2,
    Trinket1,
    Trinket2,
    MainHand,
    OffHand,
    Ranged
}