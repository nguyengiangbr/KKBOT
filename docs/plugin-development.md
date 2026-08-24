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
    <TargetFramework>net10.0
    <Nullable>enable
    <ImplicitUsings>enable
    <GenerateDocumentationFile>true
    <AssemblyName>KKBOT.Plugin.MyGame
    <RootNamespace>KKBOT.Plugin.MyGame
    <CopyLocalLockFileAssemblies>true
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="....srcKKBOT.Plugins.AbstractionsKKBOT.Plugins.Abstractions.csproj" />
    <ProjectReference Include="....srcKKBOT.Perception.AbstractionsKKBOT.Perception.Abstractions.csproj" />
    <ProjectReference Include="....srcKKBOT.Device.AbstractionsKKBOT.Device.Abstractions.csproj" />
    <ProjectReference Include="....srcKKBOT.AI.AbstractionsKKBOT.AI.Abstractions.csproj" />
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
            ConfigSchema = "{ "type": "object" }",
            DefaultConfig = "{}",
            EntryAssembly = "KKBOT.Plugin.MyGame.dll",
            Assets = ["assets/my_template.png", "assets/health_bar.png"]
        };
    }

    public IReadOnlyCollection<IGameDetector> GetDetectors()
    {
        return [new HealthBarDetector(), new ManaBarDetector()];
    }

    public IReadOnlyCollection<IGameTaskDefinition> GetTasks()
    {
        return [new AutoAttackTask(), new AutoHealTask()];
    }

    public IReadOnlyCollection<IStateDefinition> GetStates()
    {
        var idle = new SimpleStateDefinition("idle", "Idle", "idle", 
            ctx => true, null, ["combat"], TimeSpan.FromSeconds(30), new(), new());
        var combat = new SimpleStateDefinition("combat", "Combat", "action",
            ctx => true, new AttackAction(), ["idle"], TimeSpan.FromMinutes(5), new(), new());
        return [idle, combat];
    }

    public async Task<ModuleValidationResult> ValidateAsync(ModuleContext context, CancellationToken ct)
    {
        // Validate templates exist, config is valid, etc.
        return new ModuleValidationResult(true, []);
    }
}
```

---

## Creating Detectors

Detectors identify game states from screen captures.

### Simple Template Detector

```csharp
public sealed class HealthBarDetector : IGameDetector
{
    public string Id => "health_bar";
    public string Name => "Health Bar";

    public async Task<DetectionResult> DetectAsync(PerceptionContext context, CancellationToken ct)
    {
        // Use OpenCV template matching
        var result = await TemplateMatcher.MatchAsync(
            context.Frame, 
            "assets/health_bar.png",
            threshold: 0.85);
        
        return new DetectionResult
        {
            DetectorId = Id,
            IsMatch = result.IsMatch,
            Confidence = result.Confidence,
            Location = result.Location
        };
    }
}
```

### Advanced Detector with OCR

```csharp
public sealed class PlayerNameDetector : IGameDetector
{
    public string Id => "player_name";
    public string Name => "Player Name OCR";

    public async Task<DetectionResult> DetectAsync(PerceptionContext context, CancellationToken ct)
    {
        var ocrResult = await OcrEngine.RecognizeAsync(
            context.Frame.Data,
            region: new Rectangle(100, 50, 300, 50),
            language: "eng");
        
        return new DetectionResult
        {
            DetectorId = Id,
            IsMatch = !string.IsNullOrEmpty(ocrResult.Text),
            Confidence = ocrResult.Confidence,
            DetectedValue = ocrResult.Text
        };
    }
}
```

---

## Creating Tasks

Tasks define executable game actions.

### Simple Task

```csharp
public sealed class AutoHealTask : IGameTaskDefinition
{
    public string Id => "auto_heal";
    public string Name => "Auto Heal";
    public string? Description => "Automatically heal when HP is low";
    public TaskType Type => TaskType.Repeating;
    public TimeSpan DefaultTimeout => TimeSpan.FromSeconds(30);
    public RetryPolicy DefaultRetryPolicy => new(3, 5000);
    public bool RequiresApproval => false;

    public async Task<IStateDefinition> ExecuteAsync(TaskContext context, CancellationToken ct)
    {
        var device = context.GetDeviceSession();
        var perception = context.GetPerceptionPipeline();
        
        // Check HP
        var frame = await device.CaptureAsync(new CaptureOptions(), ct);
        var hpResult = await _hpDetector.DetectAsync(new PerceptionContext(frame), ct);
        
        if (hpResult.IsMatch && hpResult.Confidence < 0.3) // Low HP
        {
            // Use health potion (key '1')
            await device.SendInputAsync(new InputCommand
            {
                Type = InputType.Key,
                KeyCode = 0x31 // '1' key
            }, ct);
            
            await Task.Delay(1000, ct);
        }
        
        return new IdleState();
    }
}
```

---

## Creating States & FSM

States define the finite state machine for your bot.

### Simple State

```csharp
public sealed class CombatState : IStateDefinition
{
    public string Id => "combat";
    public string Name => "Combat";
    public string Category => "combat";
    
    public Func<PerceptionContext?, bool> EntryCondition => ctx => 
        ctx?.Detections.Any(d => d.DetectorName.Contains("enemy") && d.IsMatch) ?? false;
    
    public IStateAction? Action => new CombatAction();
    
    public IReadOnlyList<string> NextStateIds => ["idle", "healing", "fleeing"];
    public TimeSpan Timeout => TimeSpan.FromMinutes(5);
    public RetryPolicy RetryPolicy => new(3, 5000);
    public EvidencePolicy EvidencePolicy => new(true, true);
}
```

### State Action

```csharp
public sealed class CombatAction : IStateAction
{
    public async Task ExecuteAsync(CancellationToken ct)
    {
        // Combat logic: attack, use skills, manage cooldowns
        await Task.Delay(100, ct); // Placeholder
    }
}
```

---

## Configuration & Assets

### Module Configuration Schema

Define your module's configuration as JSON Schema:

```json
{
  "type": "object",
  "properties": {
    "healThreshold": { "type": "number", "minimum": 0, "maximum": 1, "default": 0.4 },
    "manaThreshold": { "type": "number", "minimum": 0, "maximum": 1, "default": 0.3 },
    "attackRange": { "type": "number", "minimum": 1, "maximum": 500, "default": 200 },
    "enableAutoBuff": { "type": "boolean", "default": true },
    "buffCooldowns": {
      "type": "object",
      "additionalProperties": { "type": "integer", "minimum": 0 }
    }
  }
}
```

### Default Configuration

```json
{
  "healThreshold": 0.4,
  "manaThreshold": 0.3,
  "attackRange": 200,
  "enableAutoBuff": true,
  "buffCooldowns": {
    "buff1": 300,
    "buff2": 600
  }
}
```

### Asset Organization

```
MyGamePlugin/
├── assets/
│   ├── templates/
│   │   ├── health_bar.png
│   │   ├── mana_bar.png
│   │   └── enemy_icon.png
│   ├── ocr/
│   │   └── font_config.json
│   └── config/
│       └── default_settings.json
├── KKBOT.Plugin.MyGame.csproj
└── MyGameModule.cs
```

---

## Testing Your Plugin

### Unit Tests

```csharp
[Fact]
public async Task AutoHealTask_ShouldHeal_WhenHpLow()
{
    // Arrange
    var mockDevice = new Mock<IDeviceSession>();
    var mockPerception = new Mock<IPerceptionPipeline>();
    var task = new AutoHealTask();
    
    // Mock low HP detection
    mockPerception.Setup(p => p.ProcessAsync(It.IsAny<Frame>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new[] { new DetectionResult { DetectorId = "health_bar", IsMatch = true, Confidence = 0.2 } });
    
    // Act
    var result = await task.ExecuteAsync(new TaskContext(...), CancellationToken.None);
    
    // Assert
    mockDevice.Verify(d => d.SendInputAsync(It.Is<InputCommand>(i => i.KeyCode == 0x31), It.IsAny<CancellationToken>()), Times.Once);
}
```

### Integration Tests

```bash
# Run plugin tests
dotnet test tests/KKBOT.Plugin.MyGame.Tests

# Run integration tests
dotnet test tests/KKBOT.Orchestration.Integration.Tests --filter "FullyQualifiedName~MyGame"
```

---

## Packaging & Distribution

### Build for Release

```bash
dotnet build -c Release
dotnet pack -c Release -o ./artifacts
```

### Plugin Package Structure

```
mygame-plugin-v1.0.0/
├── KKBOT.Plugin.MyGame.dll
├── KKBOT.Plugin.MyGame.pdb
├── manifest.json
├── assets/
│   └── templates/
│       ├── health_bar.png
│       └── enemy_icon.png
└── README.md
```

### Manifest.json

```json
{
  "id": "my-game",
  "name": "My Game Bot",
  "version": "1.0.0",
  "author": "Your Name",
  "description": "Automation for My Game",
  "supportedResolutions": [[1920, 1080]],
  "requiredPermissions": ["DEVICE_READ", "INPUT_WRITE"],
  "entryAssembly": "KKBOT.Plugin.MyGame.dll",
  "assets": ["assets/templates/health_bar.png", "assets/templates/enemy_icon.png"]
}
```

### Publishing to Marketplace

1. Build release package
2. Sign with Ed25519 key
3. Upload to marketplace via API or web UI
3. Fill metadata (screenshots, changelog, compatibility)
4. Submit for review

---

## Best Practices

### Performance
- **Cache templates** - Load once, reuse across detections
- **Parallel detectors** - Run independent detectors in parallel
- **Frame skip** - Process every Nth frame for non-critical detections
- **Memory management** - Dispose frames, use object pooling

### Reliability
- **Graceful degradation** - Fallback when detection fails
- **Timeouts** - Set reasonable timeouts on all async operations
- **Retry logic** - Exponential backoff for transient failures
- **Health checks** - Monitor device connection, plugin health

### Security
- **Least privilege** - Request minimum required permissions
- **Input validation** - Validate all user/config inputs
- **No hardcoded secrets** - Use configuration/secret store
- **Input sanitization** - Prevent injection in AI prompts

### Maintainability
- **Single responsibility** - One detector/task per class
- **Dependency injection** - Use DI for testability
- **Logging** - Structured logging with correlation IDs
- **Documentation** - XML docs for public APIs

### Testing
- **Unit test coverage** > 80%
- **Integration tests** for critical paths
- **Scenario tests** using Simulator DSL v2
- **Chaos testing** - Random failures, network partitions

---

## Example: Complete Plugin

See `src/KKBOT.Plugin.Sample/SampleGameModule.cs` for a minimal working example.

---

## Support

- **Documentation**: https://docs.kkbot.example.com/plugins
- **API Reference**: https://docs.kkbot.example.com/api
- **Community**: https://github.com/your-org/kkbot/discussions
- **Issues**: https://github.com/your-org/kkbot/issues

*Happy coding! 🎮*