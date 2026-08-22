# Plugin Development Guide

This guide walks you through creating a complete game plugin for KKBOT.

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Project Setup](#project-setup)
3. [Implementing IGameModule](#implementing-igamemodule)
4. [Creating Detectors](#creating-detectors)
5. [Creating Tasks](#creating-tasks)
6. [Creating States & FSM](#creating-states--fsm)
7. [Configuration & Assets](#configuration--assets)
8. [Testing Your Plugin](#testing-your-plugin)
9. [Packaging & Distribution](#packaging--distribution)
10. [Best Practices](#best-practices)

---

## Prerequisites

- .NET 10 SDK
- Basic knowledge of C# and async/await
- Understanding of game automation concepts
- Optional: OpenCV knowledge for custom detectors

---

## Project Setup

### 1. Create Class Library

```bash
mkdir MyGamePlugin
cd MyGamePlugin
dotnet new classlib -f net10.0
```

### 2. Add References

```bash
dotnet add reference ../src/KKBOT.Plugins.Abstractions/KKBOT.Plugins.Abstractions.csproj
dotnet add reference ../src/KKBOT.Perception.Abstractions/KKBOT.Perception.Abstractions.csproj
dotnet add reference ../src/KKBOT.Device.Abstractions/KKBOT.Device.Abstractions.csproj
dotnet add reference ../src/KKBOT.AI.Abstractions/KKBOT.AI.Abstractions.csproj
```

### 3. Configure Project File

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <AssemblyName>KKBOT.Plugin.MyGame</AssemblyName>
    <RootNamespace>KKBOT.Plugin.MyGame</RootNamespace>
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\KKBOT.Plugins.Abstractions\KKBOT.Plugins.Abstractions.csproj" />
    <ProjectReference Include="..\..\src\KKBOT.Perception.Abstractions\KKBOT.Perception.Abstractions.csproj" />
    <ProjectReference Include="..\..\src\KKBOT.Device.Abstractions\KKBOT.Device.Abstractions.csproj" />
    <ProjectReference Include="..\..\src\KKBOT.AI.Abstractions\KKBOT.AI.Abstractions.csproj" />
  </ItemGroup>

  <!-- Auto-copy to plugins folder on build -->
  <Target Name="CopyToPluginsFolder" AfterTargets="Build">
    <Copy SourceFiles="$(TargetPath)" DestinationFolder="$(SolutionDir)plugins" />
    <Copy SourceFiles="$(TargetDir)$(TargetName).pdb" DestinationFolder="$(SolutionDir)plugins" Condition="Exists('$(TargetDir)$(TargetName).pdb')" />
    <Copy SourceFiles="@(ReferenceCopyLocalPaths)" DestinationFolder="$(SolutionDir)plugins" Condition="'%(ReferenceCopyLocalPaths.Extension)' == '.dll' And '%(ReferenceCopyLocalPaths.FileName)' != 'KKBOT.Plugins.Abstractions' And '%(ReferenceCopyLocalPaths.FileName)' != 'KKBOT.Perception.Abstractions' And '%(ReferenceCopyLocalPaths.FileName)' != 'KKBOT.Device.Abstractions' And '%(ReferenceCopyLocalPaths.FileName)' != 'KKBOT.AI.Abstractions'" />
  </Target>
</Project>
```

---

## Implementing IGameModule

The core of every plugin is the `IGameModule` interface:

```csharp
public interface IGameModule
{
    string Id { get; }
    string Version { get; }
    ModuleManifest GetManifest();
    IReadOnlyCollection<IGameDetector> GetDetectors();
    IReadOnlyCollection<IGameTaskDefinition> GetTasks();
    IReadOnlyCollection<IStateDefinition> GetStates();
    Task<ModuleValidationResult> ValidateAsync(ModuleContext context, CancellationToken cancellationToken);
}
```

### Basic Implementation

```csharp
public sealed class MyGameModule : IGameModule
{
    public string Id => "my-game";
    public string Version => "1.0.0";

    public ModuleManifest GetManifest()
    {
        return new ModuleManifest
        {
            Id = Id,
            Name = "My Game Bot",
            Version = Version,
            Author = "Your Name",
            Description = "Automation for My Game",
            SupportedResolutions = [new Resolution(1920, 1080)],
            RequiredPermissions = ["DEVICE_READ", "INPUT_WRITE", "SCREENSHOT_CAPTURE"],
            ConfigSchema = """{ "type": "object", "properties": { "autoAttack": { "type": "boolean", "default": true } } }""",
            DefaultConfig = """{ "autoAttack": true }""",
            EntryAssembly = "KKBOT.Plugin.MyGame.dll",
            Assets = ["templates/health.png", "templates/mana.png"]
        };
    }

    public IReadOnlyCollection<IGameDetector> GetDetectors() => [/* detectors */];
    public IReadOnlyCollection<IGameTaskDefinition> GetTasks() => [/* tasks */];
    public IReadOnlyCollection<IStateDefinition> GetStates() => [/* states */];

    public async Task<ModuleValidationResult> ValidateAsync(ModuleContext context, CancellationToken ct)
    {
        var errors = new List<ModuleValidationError>();
        // Validate assets, config, etc.
        return new ModuleValidationResult(errors.Count == 0, errors);
    }
}
```

---

## Creating Detectors

Detectors analyze game frames and return structured results.

### Interface

```csharp
public interface IGameDetector
{
    string Id { get; }
    string Name { get; }
    DetectorType Type { get; }
    double RequiredConfidence { get; }
    Task<DetectorResult> DetectAsync(Frame frame, CancellationToken cancellationToken);
}
```

### Template Matching Detector (OpenCV)

```csharp
public sealed class HealthBarDetector : IGameDetector
{
    public string Id => "health_bar";
    public string Name => "Health Bar Detector";
    public DetectorType Type => DetectorType.TemplateMatch;
    public double RequiredConfidence => 0.85;

    public async Task<DetectorResult> DetectAsync(Frame frame, CancellationToken ct)
    {
        // Use OpenCV to match template
        // This is simplified - real implementation uses OpenCvSharp
        
        var result = new DetectorResult
        {
            DetectorId = Id,
            DetectorName = Name,
            Confidence = 0.92,
            IsMatch = true,
            DetectedValue = "HP: 78%",
            BoundingBox = new Rectangle(50, 50, 200, 30),
            Timestamp = DateTime.UtcNow,
            RawScore = 0.92
        };
        return result;
    }
}
```

### Color Analysis Detector

```csharp
public sealed class ManaBarDetector : IGameDetector
{
    public string Id => "mana_bar";
    public string Name => "Mana Bar Detector";
    public DetectorType Type => DetectorType.ColorAnalysis;
    public double RequiredConfidence => 0.80;

    public async Task<DetectorResult> DetectAsync(Frame frame, CancellationToken ct)
    {
        // Analyze pixel colors in mana bar region
        var manaPercent = AnalyzeManaColor(frame);
        
        return new DetectorResult
        {
            DetectorId = Id,
            DetectorName = Name,
            Confidence = 0.88,
            IsMatch = manaPercent > 0,
            DetectedValue = $"MP: {manaPercent}%",
            BoundingBox = new Rectangle(50, 90, 200, 20),
            Timestamp = DateTime.UtcNow
        };
    }

    private int AnalyzeManaColor(Frame frame)
    {
        // Implementation: decode frame, sample pixels, calculate blue ratio
        return 65; // Example
    }
}
```

### OCR Detector

```csharp
public sealed class GoldDetector : IGameDetector
{
    public string Id => "gold_amount";
    public string Name => "Gold Amount OCR";
    public DetectorType Type => DetectorType.Ocr;
    public double RequiredConfidence => 0.70;

    private readonly IOcrEngine _ocrEngine;

    public GoldDetector(IOcrEngine ocrEngine)
    {
        _ocrEngine = ocrEngine;
    }

    public async Task<DetectorResult> DetectAsync(Frame frame, CancellationToken ct)
    {
        // Crop gold region
        var goldRegion = CropRegion(frame, new Rectangle(1700, 20, 150, 40));
        
        var ocrResult = await _ocrEngine.RecognizeAsync(goldRegion, ct);
        var goldAmount = ParseGold(ocrResult.Text);
        
        return new DetectorResult
        {
            DetectorId = Id,
            DetectorName = Name,
            Confidence = ocrResult.Confidence,
            IsMatch = ocrResult.Confidence >= RequiredConfidence,
            DetectedValue = $"Gold: {goldAmount:N0}",
            BoundingBox = new Rectangle(1700, 20, 150, 40),
            Timestamp = DateTime.UtcNow
        };
    }

    private long ParseGold(string text) => long.TryParse(text.Replace(",", ""), out var g) ? g : 0;
    
    private byte[] CropRegion(Frame frame, Rectangle rect) => frame.Data; // Simplified
}
```

---

## Creating Tasks

Tasks are discrete units of work that can be scheduled and executed.

### Interface

```csharp
public interface IGameTaskDefinition
{
    string Id { get; }
    string Name { get; }
    string? Description { get; }
    TaskType Type { get; }           // OneShot, Repeating, Manual
    TimeSpan DefaultTimeout { get; }
    RetryPolicy DefaultRetryPolicy { get; }
    bool RequiresApproval { get; }
    Task<IStateDefinition> ExecuteAsync(TaskContext context, CancellationToken cancellationToken);
}
```

### One-Shot Task

```csharp
public sealed class UseHealthPotionTask : IGameTaskDefinition
{
    public string Id => "use_health_potion";
    public string Name => "Use Health Potion";
    public string? Description => "Consumes a health potion from inventory";
    public TaskType Type => TaskType.OneShot;
    public TimeSpan DefaultTimeout => TimeSpan.FromSeconds(5);
    public RetryPolicy DefaultRetryPolicy => new(1, 200, false);
    public bool RequiresApproval => false;

    public async Task<IStateDefinition> ExecuteAsync(TaskContext context, CancellationToken ct)
    {
        // 1. Get device session from context
        // 2. Send input to use potion (key press, click, etc.)
        // 3. Wait for animation
        // 4. Return next state
        
        await Task.Delay(500, ct); // Simulate potion use delay
        
        // Return the state to transition to
        return new CombatState(); // Stay in combat after healing
    }
}
```

### Repeating Task

```csharp
public sealed class AutoAttackTask : IGameTaskDefinition
{
    public string Id => "auto_attack";
    public string Name => "Auto Attack";
    public string? Description => "Continuously uses basic attack skill";
    public TaskType Type => TaskType.Repeating;
    public TimeSpan DefaultTimeout => TimeSpan.FromMinutes(10);
    public RetryPolicy DefaultRetryPolicy => new(3, 1000, true);
    public bool RequiresApproval => false;

    public async Task<IStateDefinition> ExecuteAsync(TaskContext context, CancellationToken ct)
    {
        // Check if in combat, use skill if off cooldown
        await Task.Delay(200, ct); // Attack interval
        
        return new CombatState(); // Continue combat
    }
}
```

### Manual Task (Requires Approval)

```csharp
public sealed class PurchaseItemTask : IGameTaskDefinition
{
    public string Id => "purchase_item";
    public string Name => "Purchase Item from Shop";
    public string? Description => "Buys specified item from NPC shop (requires user approval)";
    public TaskType Type => TaskType.Manual;
    public TimeSpan DefaultTimeout => TimeSpan.FromMinutes(2);
    public RetryPolicy DefaultRetryPolicy => new(2, 2000, true);
    public bool RequiresApproval => true; // User must approve spending

    public async Task<IStateDefinition> ExecuteAsync(TaskContext context, CancellationToken ct)
    {
        var itemId = context.Parameters.GetValueOrDefault("itemId")?.ToString() ?? "";
        var quantity = int.Parse(context.Parameters.GetValueOrDefault("quantity")?.ToString() ?? "1");
        
        // Navigate to shop, buy item
        await Task.Delay(1000, ct);
        
        return new IdleState();
    }
}
```

---

## Creating States & FSM

States define the finite state machine for your game automation.

### Interface

```csharp
public interface IStateDefinition
{
    string Id { get; }
    string Name { get; }
    string Category { get; }
    Func<PerceptionContext?, bool> EntryCondition { get; }
    IStateAction? Action { get; }
    IReadOnlyList<string> NextStateIds { get; }
    TimeSpan Timeout { get; }
    RetryPolicy RetryPolicy { get; }
    EvidencePolicy EvidencePolicy { get; }
}
```

### State Implementation

```csharp
public sealed class IdleState : IStateDefinition
{
    public string Id => "idle";
    public string Name => "Idle";
    public string Category => "navigation";
    
    // Enter this state when no other conditions match
    public Func<PerceptionContext?, bool> EntryCondition => ctx => true;
    
    public IStateAction? Action => null; // No continuous action
    
    public IReadOnlyList<string> NextStateIds => 
        ["combat", "healing", "mana_restoring", "questing", "selling"];
    
    public TimeSpan Timeout => TimeSpan.FromMinutes(5);
    public RetryPolicy RetryPolicy => new();
    public EvidencePolicy EvidencePolicy => new(false, false);
}

public sealed class CombatState : IStateDefinition
{
    public string Id => "combat";
    public string Name => "In Combat";
    public string Category => "combat";
    
    // Enter when battle detector finds enemy
    public Func<PerceptionContext?, bool> EntryCondition => ctx =>
        ctx?.DetectorResults.Any(d => d.DetectorId == "enemy_detected" && d.IsMatch) == true;
    
    public IStateAction? Action => new CombatAction();
    
    public IReadOnlyList<string> NextStateIds => 
        ["idle", "healing", "mana_restoring", "looting", "fleeing", "dead"];
    
    public TimeSpan Timeout => TimeSpan.FromMinutes(3);
    public RetryPolicy RetryPolicy => new(3, 1000, true);
    public EvidencePolicy EvidencePolicy => new(true, false);
}
```

### State Actions

```csharp
public sealed class CombatAction : IStateAction
{
    private readonly IInputDispatcher _input;
    private readonly ILogger<CombatAction> _logger;

    public CombatAction(IInputDispatcher input, ILogger<CombatAction> logger)
    {
        _input = input;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct)
    {
        // Continuous combat logic
        // 1. Check skill cooldowns
        // 2. Use available skills
        // 3. Move if needed
        // 4. Check for emergency heal
        
        await Task.Delay(200, ct); // Combat loop interval
    }
}

public sealed class HealingAction : IStateAction
{
    public async Task ExecuteAsync(CancellationToken ct)
    {
        // Use potion, cast heal spell, etc.
        await Task.Delay(800, ct);
    }
}
```

---

## Configuration & Assets

### Config Schema (JSON Schema)

```json
{
  "type": "object",
  "properties": {
    "autoAttack": { "type": "boolean", "default": true, "description": "Enable auto attack" },
    "healThreshold": { "type": "integer", "minimum": 10, "maximum": 90, "default": 30, "description": "HP % to heal at" },
    "manaThreshold": { "type": "integer", "minimum": 10, "maximum": 90, "default": 20, "description": "MP % to restore at" },
    "lootFilter": { 
      "type": "array", 
      "items": { "type": "string" }, 
      "default": ["rare", "epic", "legendary"],
      "description": "Item qualities to loot"
    },
    "skillRotation": {
      "type": "array",
      "items": { "type": "string" },
      "default": ["skill_1", "skill_2", "skill_3"],
      "description": "Skill priority order"
    }
  },
  "required": ["autoAttack", "healThreshold", "manaThreshold", "lootFilter", "skillRotation"]
}
```

### Accessing Config in Plugin

```csharp
public sealed class MyGameModule : IGameModule
{
    // Config is passed via Instance.Configuration (JSON string)
    // Deserialize in your tasks/states:
    
    private MyGameConfig GetConfig(Instance instance)
    {
        return JsonSerializer.Deserialize<MyGameConfig>(instance.Configuration) 
               ?? new MyGameConfig();
    }
}

public record MyGameConfig
{
    public bool AutoAttack { get; init; } = true;
    public int HealThreshold { get; init; } = 30;
    public int ManaThreshold { get; init; } = 20;
    public string[] LootFilter { get; init; } = ["rare", "epic", "legendary"];
    public string[] SkillRotation { get; init; } = ["skill_1", "skill_2", "skill_3"];
}
```

### Assets

Place assets in your project under `assets/` folder:

```
MyGamePlugin/
├── assets/
│   ├── templates/
│   │   ├── health_bar.png
│   │   ├── mana_bar.png
│   │   └── enemy_indicator.png
│   ├── ocr/
│   │   └── game_font.png
│   └── sounds/
│       └── alert.wav
```

Assets are copied to output and referenced in `Manifest.Assets`.

---

## Testing Your Plugin

### 1. Unit Tests

```csharp
[Fact]
public async Task HealthBarDetector_ReturnsValidResult()
{
    var detector = new HealthBarDetector();
    var frame = CreateTestFrame(); // Helper to create test frame
    
    var result = await detector.DetectAsync(frame, CancellationToken.None);
    
    Assert.True(result.IsMatch);
    Assert.InRange(result.Confidence, 0.8, 1.0);
    Assert.NotNull(result.DetectedValue);
}
```

### 2. Integration Test with Simulator

```csharp
[Fact]
public async Task AutoAttackTask_ExecutesInCombatState()
{
    // Arrange
    var simulator = new SimulatedDeviceSession(...);
    var plugin = new MyGameModule();
    var task = plugin.GetTasks().First(t => t.Id == "auto_attack");
    
    // Act
    var context = new TaskContext(Guid.NewGuid(), Guid.NewGuid(), new Dictionary<string, object?>());
    var nextState = await task.ExecuteAsync(context, CancellationToken.None);
    
    // Assert
    Assert.IsType<CombatState>(nextState);
}
```

### 3. Manual Testing

1. Build plugin: `dotnet build -c Release`
2. Copy DLL to `plugins/` folder
3. Start KKBOT Dashboard
4. Navigate to Plugins page → Reload
5. Create instance with your plugin
6. Start instance and monitor logs

---

## Packaging & Distribution

### 1. Build Release

```bash
dotnet build -c Release
```

### 2. Create NuGet Package (Optional)

```xml
<!-- In .csproj -->
<PropertyGroup>
  <PackageId>KKBOT.Plugin.MyGame</PackageId>
  <PackageVersion>1.0.0</PackageVersion>
  <Authors>Your Name</Authors>
  <Description>KKBOT plugin for My Game</Description>
  <PackageTags>kkbot;plugin;game-automation</PackageTags>
  <RepositoryUrl>https://github.com/your-repo/MyGamePlugin</RepositoryUrl>
</PropertyGroup>
```

```bash
dotnet pack -c Release -o ./nupkg
```

### 3. Distribution Checklist

- [ ] DLL and PDB in output
- [ ] All asset files copied (templates, OCR fonts, etc.)
- [ ] Dependencies included (or documented as required)
- [ ] `Manifest.Assets` lists all required files
- [ ] `Manifest.ConfigSchema` validates user config
- [ ] README with setup instructions
- [ ] Version follows SemVer

---

## Best Practices

### 1. Error Handling

```csharp
public async Task<DetectorResult> DetectAsync(Frame frame, CancellationToken ct)
{
    try
    {
        // Detection logic
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Detector {Id} failed", Id);
        return new DetectorResult
        {
            DetectorId = Id,
            DetectorName = Name,
            Confidence = 0,
            IsMatch = false,
            DetectedValue = $"ERROR: {ex.Message}",
            Timestamp = DateTime.UtcNow
        };
    }
}
```

### 2. Cancellation Token Support

```csharp
public async Task<IStateDefinition> ExecuteAsync(TaskContext context, CancellationToken ct)
{
    while (!ct.IsCancellationRequested)
    {
        await DoWorkAsync(ct);
        await Task.Delay(100, ct); // Respect cancellation
    }
    return new IdleState();
}
```

### 3. Logging

```csharp
private readonly ILogger<MyDetector> _logger;

public MyDetector(ILogger<MyDetector> logger) => _logger = logger;

public async Task<DetectorResult> DetectAsync(Frame frame, CancellationToken ct)
{
    _logger.LogDebug("Running detector {Id}", Id);
    // ...
    _logger.LogInformation("Detector {Id} found match with confidence {Confidence}", Id, result.Confidence);
}
```

### 4. Dependency Injection

Register services in your module's DI container:

```csharp
// In your plugin, you can use constructor injection for detectors/tasks
public sealed class MyDetector : IGameDetector
{
    private readonly IOcrEngine _ocr;
    private readonly ILogger<MyDetector> _logger;

    public MyDetector(IOcrEngine ocr, ILogger<MyDetector> logger)
    {
        _ocr = ocr;
        _logger = logger;
    }
    // ...
}
```

### 5. Performance

- Cache template images in memory
- Use `RequiredConfidence` to filter early
- Limit frame processing rate
- Dispose OpenCV Mats properly

### 6. Security

- Never hardcode credentials
- Use `ISecretManager` for API keys
- Validate all user inputs
- Request minimal permissions in manifest

---

## Example: Complete Plugin Structure

```
MyGamePlugin/
├── MyGamePlugin.csproj
├── MyGameModule.cs           # Main module implementation
├── Detectors/
│   ├── HealthBarDetector.cs
│   ├── ManaBarDetector.cs
│   └── EnemyDetector.cs
├── Tasks/
│   ├── AutoAttackTask.cs
│   ├── HealTask.cs
│   └── LootTask.cs
├── States/
│   ├── IdleState.cs
│   ├── CombatState.cs
│   ├── HealingState.cs
│   └── LootingState.cs
├── Actions/
│   ├── CombatAction.cs
│   └── HealAction.cs
├── Config/
│   └── MyGameConfig.cs
├── assets/
│   └── templates/
└── README.md
```

---

## Next Steps

1. Study the [Sample RPG Plugin](../samples/KKBOT.Plugin.SampleGame/SampleGameModule.cs)
2. Check [API Reference](api-reference.md) for available services
3. Join [Discussions](https://github.com/your-org/kkbot/discussions) for help
4. Submit your plugin to the [Plugin Marketplace](https://github.com/your-org/kkbot-plugins) (coming soon)

---

*Happy coding! 🎮🤖*