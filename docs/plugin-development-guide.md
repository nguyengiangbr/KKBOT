# KKBOT Plugin Development Guide

## Overview

KKBOT Framework follows a **Core Engine + Plugin Architecture** where:
- **Core Framework**: Provides infrastructure (Device, Perception, AI, Orchestration, Security)
- **Game Plugins**: Contain game-specific logic (State Machines, Coordinates, Skills, Quests)

Each game (Võ Lâm, MU, SILKROAD, etc.) is implemented as an independent plugin in `/plugins/` directory.

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Plugin Architecture](#plugin-architecture)
3. [Creating Your First Plugin](#creating-your-first-plugin)
4. [BaseGamePlugin Deep Dive](#basegameplugin-deep-dive)
5. [Game Events & Event Handling](#game-events--event-handling)
6. [Game Context & Core Services](#game-context--core-services)
7. [State Machines & Tasks](#state-machines--tasks)
6. [Configuration & Deployment](#configuration--deployment)
7. [Testing & Debugging](#testing--debugging)
7. [Best Practices](#best-practices)

---

## Prerequisites

### Required Tools
- **.NET 10 SDK** or later
- **Visual Studio 2022+** or **VS Code** with C# Dev Kit
- **Git** for version control

### Required Knowledge
- **C# 12+** (records, pattern matching, nullable reference types)
- **Dependency Injection** (Microsoft.Extensions.DependencyInjection)
- **Async/Await** patterns
- **Basic Game Automation concepts**

---

## Plugin Architecture

### Core vs Plugin Responsibilities

| Core Framework (Provided) | Game Plugin (You Implement) |
|---------------------------|----------------------------|
| Device Management (ADB, Win32, Hardware Input) | Game-specific logic (Login, Combat, Quests) |
| Perception Pipeline (Screen Capture, OCR, YOLO) | Coordinates, NPC IDs, Skill IDs, Map Data |
| AI Integration (Ollama, YOLOv8, EasyOCR) | Skill Combos, Rotation Logic, Priority System |
| Multi-instance Orchestration | State Machines, Quest Flow, Farm Routes |
| Plugin Loader & Security | Game-specific Config, UI, Coordinates |
| License Management | Anti-detection, Humanizer Settings |
| Dashboard & Monitoring | Custom UI, Custom Reports |

### Plugin Structure

```
plugins/
└── volam/
    ├── volam.dll                 # Compiled plugin
    ├── volam.pdb                 # Debug symbols
    ├── volam.deps.json           # Dependencies
    ├── volam.runtimeconfig.json  # Runtime config
    ├── volam.manifest.json       # Plugin manifest
    └── config/
        ├── volam.json            # Main config
        ├── coordinates.json      # Map coordinates
        ├── skills.json           # Skill definitions
        ├── npcs.json             # NPC data
        └── quests.json           # Quest definitions
```

---

## Creating Your First Plugin

### Step 1: Create Project

```bash
# Create plugin project
dotnet new classlib -n KKBOT.Plugin.Volam -o src/KKBOT.Plugin.Volam

# Add reference to abstractions
dotnet add src/KKBOT.Plugin.Volam reference src/KKBOT.Plugins.Abstractions

# Add required NuGet packages
dotnet add src/KKBOT.Plugin.Volam package Microsoft.Extensions.Logging
dotnet add src/KKBOT.Plugin.Volam package Microsoft.Extensions.Configuration
dotnet add src/KKBOT.Plugin.Volam package Microsoft.Extensions.DependencyInjection
```

### Step 2: Configure Project (volam.csproj)

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AssemblyName>KKBOT.Plugin.Volam</AssemblyName>
    <RootNamespace>KKBOT.Plugin.Volam</RootNamespace>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <EnableDynamicLoading>true</EnableDynamicLoading>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\KKBOT.Plugins.Abstractions\KKBOT.Plugins.Abstractions.csproj" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="8.0.0" />
  </PropertyGroup>

  <!-- Copy config files to output -->
  <ItemGroup>
    <None Update="config\*.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>

</Project>
```

### Step 2: Create Plugin Manifest (volam.manifest.json)

```json
{
  "name": "volam",
  "displayName": "Võ Lâm Truyền Kỳ",
  "version": "1.0.0",
  "description": "Plugin for Võ Lâm Truyền Kỳ game automation",
  "author": "Your Name",
  "gameId": "volam",
  "gameName": "Võ Lâm Truyền Kỳ",
  "supportedRegions": ["VN"],
  "supportedVersions": ["1.0.0", "latest"],
  "minimumCoreVersion": "1.0.0",
  "requiredPermissions": [
    "device.input",
    "device.screen",
    "device.process",
    "network.http"
  ],
  "configSchema": "config/volam.schema.json",
  "entryPoint": "KKBOT.Plugin.Volam.VolamGameModule",
  "dependencies": [],
  "tags": ["mmorpg", "vietnam", "farming", "pvp"]
}
```

### Step 3: Implement Game Module

Create `VolamGameModule.cs`:

```csharp
using KKBOT.Plugins.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace KKBOT.Plugin.Volam;

/// <summary>
/// Võ Lâm Truyền Kỳ game plugin implementation.
/// </summary>
public sealed class VolamGameModule : BaseGamePlugin
{
    public override string GameId => "volam";
    public override string GameName => "Võ Lâm Truyền Kỳ";
    public override string Version => "1.0.0";

    public VolamGameModule(ILogger<VolamGameModule> logger, IServiceProvider serviceProvider) 
        : base(logger, serviceProvider)
    {
    }

    // ========================================
    // TEMPLATE METHODS - Core Lifecycle
    // ========================================

    protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("Initializing Võ Lâm plugin...");
        
        // Load game-specific configuration
        var config = GetConfig<VolamConfig>();
        
        // Initialize game-specific services
        // e.g., Load coordinates, skill data, NPC database
        
        await Task.CompletedTask;
    }

    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("Starting Võ Lâm bot...");
        
        // Start background tasks
        // e.g., Start perception pipeline, AI inference loop
        
        await Task.CompletedTask;
    }

    protected override async Task OnStopAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("Stopping Võ Lâm bot...");
        
        // Cleanup resources
        // e.g., Stop perception, release device handles
        
        await Task.CompletedTask;
    }

    protected override async Task<ModuleHealth> OnHealthCheckAsync(CancellationToken cancellationToken)
    {
        var health = new ModuleHealth { IsHealthy = true, Status = HealthStatus.Healthy };
        
        // Check game process is running
        var device = GameContext.GetDeviceSession();
        if (device == null || !device.IsConnected)
        {
            health.IsHealthy = false;
            health.Status = HealthStatus.Unhealthy;
            health.Issues.Add("Device not connected");
        }
        
        // Check game process
        var process = GameContext.GetDeviceSession()?.GetProcess();
        if (process == null || process.HasExited)
        {
            health.IsHealthy = false;
            health.Status = HealthStatus.Unhealthy;
            health.Issues.Add("Game process not found");
        }

        return health;
    }

    // ========================================
    // GAME EVENT HANDLERS
    // ========================================

    protected override async Task OnLoginAsync(LoginEventArgs args, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Login started for {Username} on {Server}", args.Username, args.Server);
        
        // Implement auto-login logic:
        // 1. Find login window
        // 2. Enter credentials
        // 3. Handle captcha/2FA
        // 4. Select character
        // 5. Enter game world
    }

    protected override async Task OnLoginSuccessAsync(LoginSuccessEventArgs args, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Login successful for {Character} (Level {Level})", 
            args.CharacterName, args.CharacterLevel);
        
        // Post-login setup:
        // - Apply buffs
        // - Check inventory
        // - Start farming route
    }

    protected override async Task OnLoginFailedAsync(LoginFailedEventArgs args, CancellationToken cancellationToken)
    {
        Logger.LogWarning("Login failed: {Error} (Retry: {Retryable})", 
            args.ErrorMessage, args.IsRetryable);
        
        // Handle retry logic
        if (args.IsRetryable && args.RetryCount < 3)
        {
            await Task.Delay(args.SuggestedRetryDelay);
            // Trigger retry
        }
    }

    protected override async Task OnCharacterEnterWorldAsync(CharacterEnterWorldEventArgs args, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Character {Character} entered world at {Map} ({X},{Y})", 
            args.CharacterName, args.MapName, args.Position.X, args.Position.Y);
        
        // Post-enter-world setup:
        // - Apply auto-buffs
        // - Check inventory/equipment
        // - Start farming/questing routine
    }

    protected override async Task OnCombatStartAsync(CombatStartEventArgs args, CancellationToken cancellationToken)
    {
        Logger.LogDebug("Combat started with {Target} (Distance: {Distance})", 
            args.TargetName, args.Distance);
        
        // Combat initialization:
        // - Select target
        // - Start skill rotation
        // - Enable auto-pot
    }

    protected override async Task OnCombatEndAsync(CombatEndEventArgs args, CancellationToken cancellationToken)
    {
        Logger.LogDebug("Combat ended. Victory: {Victory}, Duration: {Duration}, EXP: {Exp}", 
            args.IsVictory, args.Duration, args.ExperienceGained);
        
        // Post-combat:
        // - Loot items
        // - Heal/mana recovery
        // - Check inventory
    }

    protected override async Task OnSkillUsedAsync(SkillUsedEventArgs args, CancellationToken cancellationToken)
    {
        Logger.LogTrace("Skill used: {Skill} (Level {Level}) on {Target}", 
            args.SkillName, args.SkillLevel, args.TargetId);
        
        // Track skill usage for rotation optimization
    }

    protected override async Task OnDamageTakenAsync(DamageTakenEventArgs args, CancellationToken cancellationToken)
    {
        Logger.LogDebug("Took {Damage} {Type} damage from {Attacker} (HP: {HP})", 
            args.Damage, args.DamageType, args.AttackerName, args.RemainingHp);
        
        // Auto-pot logic
        if (args.RemainingHp < 30 && args.ShouldAutoPot)
        {
            // Use healing potion/skill
        }
    }

    protected override async Task OnDeadAsync(DeadEventArgs args, CancellationToken cancellationToken)
    {
        Logger.LogWarning("Character died at {Position}. Cause: {Cause}. Lost {Exp} EXP", 
            args.Position, args.DeathCause, args.ExperienceLost);
        
        // Death handling:
        // - Revive at town/spot
        // - Re-buff
        // - Return to farming spot
    }

    protected override async Task OnStuckAsync(StuckEventArgs args, CancellationToken cancellationToken)
    {
        Logger.LogWarning("Character stuck at ({X},{Y}) for {Duration}ms (Count: {Count})", 
            args.Position.X, args.Position.Y, args.StuckDurationMs, args.StuckCount);
        
        // Unstuck logic:
        // - Random movement
        // - Jump
        // - Teleport skill
        // - Relogin if severe
    }

    protected override async Task OnStaminaLowAsync(StaminaLowEventArgs args, CancellationToken cancellationToken)
    {
        Logger.LogDebug("Stamina low: {Current}/{Max} ({Percentage:P1})", 
            args.CurrentStamina, args.MaxStamina, args.Percentage);
        
        // Auto-pot stamina
        if (args.ShouldAutoPot)
        {
            // Use stamina potion
        }
    }

    protected override async Task OnInventoryFullAsync(InventoryFullEventArgs args, CancellationToken cancellationToken)
    {
        Logger.LogWarning("Inventory full! Free slots: {Free}/{Max}", args.FreeSlots, args.MaxCapacity);
        
        // Inventory management:
        // - Sell junk items
        // - Store in warehouse
        // - Destroy trash items
    }

    protected override async Task OnLevelUpAsync(LevelUpEventArgs args, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Level up! {Previous} -> {New} (+{Stat} stats, +{Skill} skills)", 
            args.PreviousLevel, args.NewLevel, args.StatPointsGained, args.SkillPointsGained);
        
        // Auto-assign stats/skills based on build
    }

    protected override async Task OnQuestCompleteAsync(QuestCompleteEventArgs args, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Quest completed: {Quest} (EXP: {Exp}, Gold: {Gold})", 
            args.QuestName, args.ExperienceReward, args.GoldReward);
        
        // Auto-accept next quest
    }

    protected override async Task OnDisconnectedAsync(DisconnectedEventArgs args, CancellationToken cancellationToken)
    {
        Logger.LogError("Disconnected: {Reason} (Code: {Code})", args.Reason, args.ErrorCode);
        
        if (args.ShouldReconnect)
        {
            // Trigger reconnection logic
        }
    }

    protected override async Task OnTickAsync(TickEventArgs args, CancellationToken cancellationToken)
    {
        // Called every tick (e.g., 100ms)
        // Use for periodic checks:
        // - Buff monitoring
        // - Cooldown tracking
        // - Position validation
    }

    // ========================================
    // CONFIGURATION
    // ========================================

    protected override async Task LoadConfigurationAsync(CancellationToken cancellationToken)
    {
        // Load main config
        var config = GetConfig<VolamConfig>();
        
        // Load additional configs
        // Coordinates, Skills, NPCs, Quests loaded on demand
        
        Logger.LogInformation("Configuration loaded for {GameName}", GameName);
    }

    protected override async Task ValidateCoreVersionAsync(CancellationToken cancellationToken)
    {
        // Custom version validation if needed
        await base.ValidateCoreVersionAsync(cancellationToken);
    }
}
```

### Step 4: Configuration Classes

Create `VolamConfig.cs`:

```csharp
namespace KKBOT.Plugin.Volam.Configuration;

public sealed class VolamConfig
{
    public LoginConfig Login { get; init; } = new();
    public CombatConfig Combat { get; init; } = new();
    public FarmingConfig Farming { get; init; } = new();
    public AntiDetectionConfig AntiDetection { get; init; } = new();
    public NotificationConfig Notifications { get; init; } = new();
}

public sealed class LoginConfig
{
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty; // Encrypted
    public string Server { get; init; } = "Server1";
    public string Character { get; init; } = string.Empty;
    public bool AutoLogin { get; init; } = true;
    public int RetryAttempts { get; init; } = 3;
    public int RetryDelaySeconds { get; init; } = 30;
}

public sealed class CombatConfig
{
    public SkillRotationConfig SkillRotation { get; init; } = new();
    public TargetingConfig Targeting { get; init; } = new();
    public AutoPotConfig AutoPot { get; init; } = new();
    public BuffConfig Buffs { get; init; } = new();
    public int MaxCombatDistance { get; init; } = 20;
    public bool EnableAutoAttack { get; init; } = true;
    public bool EnableKsProtection { get; init; } = true;
}

public sealed class SkillRotationConfig
{
    public List<SkillConfig> Skills { get; init; } = new();
    public bool EnableCombo { get; init; } = true;
    public int ComboDelayMs { get; init; } = 100;
}

public sealed class SkillConfig
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int Priority { get; init; } = 0;
    public int CooldownMs { get; init; } = 0;
    public int ManaCost { get; init; } = 0;
    public SkillCondition[] Conditions { get; init; } = Array.Empty<SkillCondition>();
}

public sealed class SkillCondition
{
    public string Type { get; init; } = string.Empty; // hp_below, mp_below, target_hp_below, buff_missing, etc.
    public string Value { get; init; } = string.Empty;
    public string Operator { get; init; } = "lt"; // lt, gt, eq, ne
}

/// <summary>
/// Example coordinate data structure
/// </summary>
public sealed class CoordinateData
{
    public string MapName { get; init; } = string.Empty;
    public List<Waypoint> Waypoints { get; init; } = new();
    public List<NpcData> Npcs { get; init; } = new();
    public List<MobData> Mobs { get; init; } = new();
}

public sealed class Waypoint
{
    public string Name { get; init; } = string.Empty;
    public int X { get; init; }
    public int Y { get; init; }
    public int Z { get; init; }
    public WaypointType Type { get; init; } = WaypointType.Walk;
    public int Radius { get; init; } = 5;
}

public enum WaypointType
{
    Walk,
    Teleport,
    NpcInteract,
    Gather,
    Wait
}
```

### Step 5: Build and Test

```bash
# Build the plugin
dotnet build src/KKBOT.Plugin.Volam -c Release

# Run tests
dotnet test tests/KKBOT.Plugin.Volam.Tests

# Package for deployment
dotnet publish src/KKBOT.Plugin.Volam -c Release -o ./artifacts/plugins/volam
```

---

## BaseGamePlugin Deep Dive

### Core Properties

```csharp
public abstract class BaseGamePlugin : IGameModule
{
    // REQUIRED - Must implement
    public abstract string GameId { get; }      // "volam", "mu", "silkroad"
    public abstract string GameName { get; }    // "Võ Lâm Truyền Kỳ"
    public abstract string Version { get; }     // "1.0.0"
    
    // OPTIONAL - Override as needed
    public virtual string MinimumCoreVersion => "1.0.0";
    public virtual IReadOnlyList<string> SupportedRegions => new[] { "VN" };
    public virtual IReadOnlyList<string> SupportedGameVersions => new[] { "latest" };
    public virtual IReadOnlyList<string> RequiredPermissions => Array.Empty<string>();
    public virtual string ConfigSchema => "{}";
```

### Core Services Access

```csharp
protected GameContext GameContext => _gameContext;

// Quick access to core services
protected IDeviceSession? Device => GameContext.GetDeviceSession();
protected IPerceptionPipeline? Perception => GameContext.GetPerceptionPipeline();
protected IAiService? Ai => GameContext.GetAiService();
protected IAiVisionService? Vision => GameContext.GetAiVisionService();
protected IOcrEngine? Ocr => GameContext.GetOcrEngine();
protected IInputDispatcher? Input => GameContext.GetInputDispatcher();
protected IDeviceDiscovery? Discovery => GameContext.GetDeviceDiscovery();
protected IInstanceManager? Instances => GameContext.GetInstanceManager();
protected Scheduler? Scheduler => GameContext.GetScheduler();
protected RecoveryCoordinator? Recovery => GameContext.GetRecoveryCoordinator();
```

### Configuration Access

```csharp
// Get typed config
var config = GetConfig<VolamConfig>();

// Get config section
var section = GameContext.GetSection("Volam:Combat");

// Get raw configuration
var config = GameContext.Configuration;
```

### Logging

```csharp
// Use injected logger (recommended)
Logger.LogInformation("Starting {GameName}", GameName);

// Or create typed logger
var logger = GameContext.CreateLogger<VolamGameModule>();
logger.LogDebug("Debug message");
```

### Event Bus

```csharp
// Publish custom events to other plugins or Core
await GameContext.GetEventBus().PublishAsync(new CustomEventArgs 
{ 
    GameId = GameId, 
    CustomData = "data" 
});

// Subscribe to Core events
var bus = GameContext.GetEventBus();
bus.Subscribe<SomeCoreEvent>(OnCoreEventAsync);
```

---

## Game Events & Event Handling

### Subscribing to Events

The `BaseGamePlugin` automatically subscribes to all game events. Override the handlers you need:

```csharp
// Lifecycle Events
protected virtual Task OnLoginAsync(LoginEventArgs args, CancellationToken ct)
protected virtual Task OnLoginSuccessAsync(LoginSuccessEventArgs args, CancellationToken ct)
protected virtual Task OnLoginFailedAsync(LoginFailedEventArgs args, CancellationToken ct)
protected virtual Task OnCharacterEnterWorldAsync(CharacterEnterWorldEventArgs args, CancellationToken ct)

// Combat Events
protected virtual Task OnCombatStartAsync(CombatStartEventArgs args, CancellationToken ct)
protected virtual Task OnCombatEndAsync(CombatEndEventArgs args, CancellationToken ct)
protected virtual Task OnSkillUsedAsync(SkillUsedEventArgs args, CancellationToken ct)
protected virtual Task OnDamageTakenAsync(DamageTakenEventArgs args, CancellationToken ct)

// Life Events
protected virtual Task OnDeadAsync(DeadEventArgs args, CancellationToken ct)
protected virtual Task OnReviveAsync(ReviveEventArgs args, CancellationToken ct)
protected virtual Task OnLevelUpAsync(LevelUpEventArgs args, CancellationToken ct)

// System Events
protected virtual Task OnStuckAsync(StuckEventArgs args, CancellationToken ct)
protected virtual Task OnStaminaLowAsync(StaminaLowEventArgs args, CancellationToken ct)
protected virtual Task OnInventoryFullAsync(InventoryFullEventArgs args, CancellationToken ct)
protected virtual Task OnLevelUpAsync(LevelUpEventArgs args, CancellationToken ct)
protected virtual Task OnQuestCompleteAsync(QuestCompleteEventArgs args, CancellationToken ct)
protected virtual Task OnDisconnectedAsync(DisconnectedEventArgs args, CancellationToken ct)
protected virtual Task OnReconnectedAsync(ReconnectedEventArgs args, CancellationToken ct)
protected virtual Task OnTickAsync(TickEventArgs args, CancellationToken ct)
// ... and 20+ more events
```

### Publishing Custom Events

```csharp
// Publish custom events to other plugins or Core
await GameContext.GetEventBus().PublishAsync(new CustomEventArgs 
{ 
    GameId = GameId, 
    CustomData = "data" 
});
```

---

## Game Context & Core Services

### Available Services

```csharp
// Device & Input
var device = GameContext.GetDeviceSession();
var input = GameContext.GetInputDispatcher();
var discovery = GameContext.GetDeviceDiscovery();

// Perception & Vision
var perception = GameContext.GetPerceptionPipeline();
var vision = GameContext.GetAiVisionService();
var ocr = GameContext.GetOcrEngine();

// AI Services
var ai = GameContext.GetAiService();
var vision = GameContext.GetAiVisionService();

// Orchestration
var instances = GameContext.GetInstanceManager();
var scheduler = GameContext.GetScheduler();
var recovery = GameContext.GetRecoveryCoordinator();

// Configuration
var config = GameContext.GetConfig<VolamConfig>();
var section = GameContext.GetSection("Volam:Combat");
var rawConfig = GameContext.Configuration;
```

### Device Control Example

```csharp
protected async Task MoveToAsync(int x, int y, int z, CancellationToken ct)
{
    var device = GameContext.GetDeviceSession();
    if (device == null) return;

    // Move using hardware input (Win32/Hardware level)
    await device.MoveMouseAsync(x, y, ct);
    await device.ClickAsync(MouseButton.Left, ct);
    
    // Or use game-specific movement
    await device.SendKeyAsync(Key.W, 2000, ct); // Walk forward 2 seconds
}
```

### Perception Example

```csharp
protected async Task<bool> FindAndClickNpcAsync(string npcName, CancellationToken ct)
{
    var perception = GameContext.GetPerceptionPipeline();
    if (perception == null) return false;

    // Capture screen
    var frame = await perception.CaptureAsync(ct);
    
    // Detect NPC using YOLO
    var detections = await perception.DetectAsync(frame, "npc", ct);
    
    var npc = detections.FirstOrDefault(d => d.Label.Contains(npcName));
    if (npc == null) return false;

    // Click on NPC
    var input = GameContext.GetInputDispatcher();
    await input.ClickAsync(npc.CenterX, npc.CenterY, MouseButton.Left, ct);
    
    // Wait for interaction
    await Task.Delay(1000, ct);
    return true;
}
```

---

## State Machines & Tasks

### Defining States

```csharp
// In your plugin, define states for the FSM
public class VolamStates
{
    public static readonly StateDefinition Idle = new("Idle", "Waiting for commands");
    public static readonly StateDefinition Login = new("Login", "Logging into game");
    public static readonly StateDefinition EnterWorld = new("EnterWorld", "Entering game world");
    public static readonly StateDefinition Farming = new("Farming", "Auto-farming monsters");
    public static readonly StateDefinition Combat = new("Combat", "In combat");
    public static readonly StateDefinition Dead = new("Dead", "Character dead");
    public static readonly StateDefinition Stuck = new("Stuck", "Character stuck");
    public static readonly StateDefinition InventoryFull = new("InventoryFull", "Inventory management");
    public static readonly StateDefinition Disconnected = new("Disconnected", "Network disconnected");
    public static readonly StateDefinition Relogin = new("Relogin", "Reconnecting to game");
}
```

### Defining Tasks

```csharp
public class VolamTasks
{
    public static readonly GameTaskDefinition AutoLogin = new("AutoLogin", "Auto login sequence")
    {
        Steps = new[]
        {
            new TaskStep("FindLoginWindow", "Find game login window"),
            new TaskStep("EnterCredentials", "Enter username/password"),
            new TaskStep("HandleCaptcha", "Handle captcha if present"),
            new TaskStep("SelectServer", "Select game server"),
            new TaskStep("SelectCharacter", "Select character"),
            new TaskStep("EnterWorld", "Enter game world")
        }
    };

    public static readonly GameTaskDefinition FarmRoute = new("FarmRoute", "Farming route")
    {
        Steps = new[]
        {
            new TaskStep("MoveToSpot", "Move to farming spot"),
            new TaskStep("FindTarget", "Find valid target"),
            new TaskStep("Combat", "Engage combat"),
            new TaskStep("Loot", "Collect drops"),
            new TaskStep("CheckBuffs", "Reapply buffs"),
            new TaskStep("CheckInventory", "Manage inventory"),
            new TaskStep("CheckHealth", "Heal if needed")
        }
    };
}
```

### Using State Machine

```csharp
protected override async Task OnStartAsync(CancellationToken ct)
{
    var fsm = GameContext.GetService<IStateMachine<VolamStates>>();
    
    // Configure transitions
    fsm.Configure(VolamStates.Idle)
        .Permit(Trigger.Login, VolamStates.Login)
        .Permit(Trigger.StartFarming, VolamStates.Farming);

    fsm.Configure(VolamStates.Login)
        .Permit(Trigger.LoginSuccess, VolamStates.EnterWorld)
        .Permit(Trigger.LoginFailed, VolamStates.Idle);

    // Start state machine
    await fsm.StartAsync(VolamStates.Idle, ct);
}
```

---

## Configuration & Deployment

### Configuration Files

```
volam/
├── volam.json              # Main configuration
├── coordinates.json        # Map coordinates, waypoints
├── skills.json             # Skill definitions & rotations
├── npcs.json               # NPC database
├── mobs.json               # Monster database
├── quests.json             # Quest definitions
├── items.json              # Item database
└── volam.schema.json       # JSON Schema for validation
```

### volam.json Example

```json
{
  "Login": {
    "Username": "your_username",
    "Password": "encrypted_password_here",
    "Server": "Server1",
    "Character": "YourCharacterName",
    "AutoLogin": true,
    "RetryAttempts": 3,
    "RetryDelaySeconds": 30
  },
  "Combat": {
    "SkillRotation": {
      "Skills": [
        {
          "Id": "skill_1",
          "Name": "Thiên Long Bát Bộ",
          "Priority": 100,
          "CooldownMs": 3000,
          "ManaCost": 50,
          "Conditions": [
            { "Type": "target_hp_below", "Operator": "lt", "Value": "80" }
          ]
        }
      ],
      "EnableCombo": true,
      "ComboDelayMs": 100
    },
    "Targeting": {
      "Mode": "Nearest",
      "MaxDistance": 20,
      "IgnoreList": ["NPC_Vendor", "NPC_Quest"]
    },
    "AutoPot": {
      "HpThresholdPercent": 40,
      "MpThresholdPercent": 30,
      "StaminaThresholdPercent": 20
    }
  },
  "Farming": {
    "Route": "Route_DaoDuong",
    "MaxFarmTimeHours": 4,
    "AutoSell": true,
    "AutoRepair": true,
    "AutoStore": true
  },
  "AntiDetection": {
    "HumanizerEnabled": true,
    "RandomDelayMinMs": 50,
    "RandomDelayMaxMs": 200,
    "MouseJitterEnabled": true,
    "KeyPressVariation": true
  },
  "Notifications": {
    "TelegramEnabled": true,
    "TelegramBotToken": "bot_token_here",
    "TelegramChatId": "chat_id_here",
    "NotifyOnLevelUp": true,
    "NotifyOnDisconnect": true,
    "NotifyOnBossSpawn": true,
    "NotifyOnRareDrop": true
  }
}
```

### Deployment

```bash
# 1. Build in Release mode
dotnet build src/KKBOT.Plugin.Volam -c Release

# 2. Publish as self-contained (optional)
dotnet publish src/KKBOT.Plugin.Volam -c Release -r win-x64 --self-contained false -o ./artifacts/volam

# 3. Sign the plugin (requires signing certificate)
# The build pipeline will sign automatically

# 4. Deploy to plugins directory
cp -r ./artifacts/volam/* /path/to/kkbot/plugins/volam/

# 5. Verify signature
dotnet tool run signtool verify /pa ./artifacts/volam/VolamGameModule.dll
```

### Hot Reload (Development)

```bash
# Enable hot reload in config
{
  "PluginLoader": {
    "EnableHotReload": true,
    "PluginDirectory": "./plugins"
  }
}

# When you rebuild the plugin, it will auto-reload
dotnet build src/KKBOT.Plugin.Volam --no-restore
# Plugin automatically reloads within 1-2 seconds
```

---

## Testing & Debugging

### Unit Tests

```csharp
// tests/KKBOT.Plugin.Volam.Tests/VolamGameModuleTests.cs

public class VolamGameModuleTests
{
    private readonly Mock<ILogger<VolamGameModule>> _logger;
    private readonly Mock<IServiceProvider> _services;
    private readonly VolamGameModule _plugin;

    public VolamGameModuleTests()
    {
        _logger = new Mock<ILogger<VolamGameModule>>();
        _services = new Mock<IServiceProvider>();
        _plugin = new VolamGameModule(_logger.Object, _services.Object);
    }

    [Fact]
    public async Task InitializeAsync_ShouldInitializeSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();

        // Act
        await _plugin.InitializeAsync(services);

        // Assert
        Assert.True(_plugin.GetType().GetField("_initialized", 
            BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(_plugin) as bool? == true);
    }

    [Fact]
    public void OnStuckAsync_ShouldExecuteUnstuckLogic()
    {
        // Arrange
        var args = new StuckEventArgs 
        { 
            Position = (100, 200, 0), 
            StuckDurationMs = 10000 
        };

        // Act
        _plugin.OnStuckAsync(args, CancellationToken.None).Wait();

        // Assert - verify unstuck logic was called
        // Verify device.MoveMouseAsync was called, etc.
    }
}
```

### Integration Tests

```csharp
// tests/KKBOT.Plugin.Volam.Tests/IntegrationTests.cs

public class VolamIntegrationTests : IClassFixture<VolamTestFixture>
{
    private readonly VolamTestFixture _fixture;

    public VolamIntegrationTests(VolamTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Plugin_Loads_And_Initializes()
    {
        // Load plugin via PluginLoader
        var loader = _fixture.ServiceProvider.GetRequiredService<PluginLoader>();
        var plugin = await loader.LoadPluginAsync("./artifacts/volam/VolamGameModule.dll");

        Assert.NotNull(plugin);
        Assert.Equal("volam", plugin.GameId);
        Assert.Equal("Võ Lâm Truyền Kỳ", plugin.GameName);

        // Initialize
        await plugin.InitializeAsync(_fixture.ServiceProvider);
        
        // Start
        await plugin.StartAsync();

        // Health check
        var health = await plugin.GetHealthAsync();
        Assert.True(health.IsHealthy);

        // Cleanup
        await plugin.StopAsync();
    }
}
```

### Debugging Tips

```csharp
// Enable detailed logging
// appsettings.Development.json
{
  "Logging": {
    "LogLevel": {
      "KKBOT.Plugin.Volam": "Debug",
      "KKBOT.Plugins.Loader": "Debug"
    }
  }
}

// Use conditional breakpoints
// Attach to process: dotnet.exe (KKBOT.Host)

// Live reload during development
// 1. Run KKBOT.Host
// 2. Make changes to plugin
// 3. dotnet build src/KKBOT.Plugin.Volam
// 3. Watch console: "Plugin volam reloaded"
```

---

## Best Practices

### 1. **Never Hardcode Game Data in Core**
```csharp
// ❌ BAD - Hardcoded in plugin logic
if (npcName == "NPC_Vendor_Volam") { ... }

// ✅ GOOD - Data-driven
var npc = _npcDatabase.GetByName("NPC_Vendor");
if (npc?.Type == NpcType.Vendor) { ... }
```

### 2. **Use Configuration for All Tunable Values**
```csharp
// ❌ BAD
 await Task.Delay(1000); // Hardcoded delay

// ✅ GOOD
 await Task.Delay(_config.Combat.ComboDelayMs, ct);
```

### 3. **Handle Cancellation Properly**
```csharp
protected override async Task OnCombatStartAsync(CombatStartEventArgs args, CancellationToken ct)
{
    while (!ct.IsCancellationRequested)
    {
        await PerformCombatActionAsync(ct);
        await Task.Delay(100, ct); // Respect cancellation
    }
}
```

### 4. **Handle Errors Gracefully**
```csharp
protected override async Task OnCombatStartAsync(CombatStartEventArgs args, CancellationToken ct)
{
    try
    {
        await ExecuteCombatRotationAsync(args, ct);
    }
    catch (OperationCanceledException)
    {
        throw; // Re-throw cancellation
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Combat error for {Target}", args.TargetName);
        // Recovery: return to safe state
        await RecoveryCoordinator.RecoverAsync("combat_error", ct);
    }
}
```

### 5. **Use Structured Logging**
```csharp
Logger.LogInformation("Combat started with {Target} at distance {Distance} using {Skill}", 
    args.TargetName, args.Distance, initiatingSkill);
// Output: Combat started with "Quái Lv 50" at distance 12.5 using "Thiên Long Bát Bộ"
```

### 6. **Configuration Validation**
```csharp
protected override async Task LoadConfigurationAsync(CancellationToken ct)
{
    var config = GetConfig<VolamConfig>();
    
    // Validate required fields
    if (string.IsNullOrEmpty(config.Login.Username))
        throw new InvalidOperationException("Username not configured");
    
    if (config.Combat.SkillRotation.Skills.Count == 0)
        Logger.LogWarning("No skills configured for combat rotation");
}
```

### 7. **Resource Cleanup**
```csharp
public override void Dispose()
{
    // Unsubscribe from events
    UnregisterEventHandlers();
    
    // Dispose game-specific resources
    _skillRotation?.Dispose();
    _targetingSystem?.Dispose();
    _pathFinder?.Dispose();
    
    base.Dispose();
}
```

---

## Plugin Template Checklist

When creating a new game plugin, verify:

- [ ] Project references `KKBOT.Plugins.Abstractions`
- [ ] Implements `BaseGamePlugin` 
- [ ] Implements all required abstract properties (`GameId`, `GameName`, `Version`)
- [ ] Overrides `OnInitializeAsync`, `OnStartAsync`, `OnStopAsync`
- [ ] Implements `OnHealthCheckAsync` for monitoring
- [ ] Handles all relevant game events (`OnLoginAsync`, `OnCombatStartAsync`, etc.)
- [ ] Loads configuration via `GetConfig<TConfig>()`
- [ ] Uses `GameContext` for core services
- [ ] Has `volam.manifest.json` with correct metadata
- [ ] Includes JSON Schema for config validation
- [ ] Has unit tests for critical logic
- [ ] Builds successfully in Release mode
- [ ] Plugin manifest includes required permissions
- [ ] Config schema (`volam.schema.json`) validates config

## Example: Minimal Plugin Structure

```
src/KKBOT.Plugin.NewGame/
├── KKBOT.Plugin.NewGame.csproj
├── NewGameModule.cs              # Main plugin class
├── NewGameConfig.cs              # Configuration classes
├── NewGameStates.cs              # State definitions
├── NewGameTasks.cs               # Task definitions
├── NewGameCoordinates.cs         # Coordinate data
├── NewGameManifest.json          # Plugin manifest
└── config/
    ├── newgame.json              # Main config
    ├── coordinates.json          # Coordinates
    ├── skills.json               # Skills
    └── newgame.schema.json       # JSON Schema
```

---

## Support & Resources

- **API Reference**: [docs/api-reference.md](docs/api-reference.md)
- **Core Services Guide**: [docs/core-services.md](docs/core-services.md)
- **Perception Pipeline**: [docs/perception.md](docs/perception.md)
- **AI Integration**: [docs/ai-integration.md](docs/ai-integration.md)
- **Plugin Marketplace**: [marketplace.kkbot.io](https://marketplace.kkbot.io)
- **Discord Community**: [discord.gg/kkbot](https://discord.gg/kkbot)
- **GitHub Issues**: [github.com/kkbot/framework/issues](https://github.com/kkbot/framework/issues)

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2025-01-15 | Initial release |

---

**Happy Plugin Development! 🎮🤖**

*Built with ❤️ by the KKBOT Team*