# KKBOT Framework - SYSTEM MANIFEST

**Generated:** 2025-01-24  
**Solution:** `KKBOT.sln` (68 Projects)  
**Target Framework:** .NET 10.0  
**Architecture:** Hexagonal / Ports & Adapters with Plugin System

---

## 1. PROJECT TREE (CẤY THƯ MỤC THỰC TẾ)

### Solution Structure
```
KKBOT.sln (68 Projects)
├── src/ (53 Projects)
│   ├── Core Platform (14)
│   │   ├── KKBOT.Domain
│   │   ├── KKBOT.Contracts
│   │   ├── KKBOT.Application
│   │   ├── KKBOT.Workflow
│   │   ├── KKBOT.Orchestration
│   │   ├── KKBOT.Device.Abstractions
│   │   ├── KKBOT.Device.Adb
│   │   ├── KKBOT.Device.Simulator
│   │   ├── KKBOT.Perception.Abstractions
│   │   ├── KKBOT.Perception.OpenCv
│   │   ├── KKBOT.Perception.Ocr
│   │   ├── KKBOT.AI.Abstractions
│   │   ├── KKBOT.AI.Ollama
│   │   ├── KKBOT.Plugins.Abstractions
│   │   ├── KKBOT.Plugins.Loader
│   │   ├── KKBOT.Persistence
│   │   ├── KKBOT.Persistence.EfCore
│   │   ├── KKBOT.Observability
│   │   ├── KKBOT.Security
│   │   ├── KKBOT.Identity
│   │   ├── KKBOT.Resilience
│   │   ├── KKBOT.PolicyEngine
│   │   └── KKBOT.Performance (5 sub-projects)
│   ├── Agent & Coordination (4)
│   │   ├── KKBOT.Agent.Abstractions
│   │   ├── KKBOT.Agent
│   │   ├── KKBOT.Agent.Coordination.Abstractions
│   │   ├── KKBOT.Agent.Coordination
│   │   └── KKBOT.Agent.Coordination.Grpc
│   ├── Game Plugins (7)
│   │   ├── KKBOT.Plugin.Sample
│   │   ├── KKBOT.Plugin.AccountLogin (Module 1)
│   │   ├── KKBOT.Plugin.AntiDetection (Module 7)
│   │   ├── KKBOT.Plugin.ResourceOptimizer (Module 3)
│   │   ├── KKBOT.Plugin.NetworkProxy (Module 2)
│   │   ├── KKBOT.Plugin.LogisticsSurvival (Module 4)
│   │   ├── KKBOT.Plugin.CombatQuest (Module 5)
│   │   └── KKBOT.Plugin.EconomyTrading (Module 6)
│   ├── Advanced Platform (6)
│   │   ├── KKBOT.AI.Pipeline
│   │   ├── KKBOT.AI.Optimization (6 sub-projects)
│   │   ├── KKBOT.MLPlatform (8 sub-projects)
│   │   ├── KKBOT.Sim2Real (7 sub-projects)
│   │   ├── KKBOT.Marketplace
│   │   ├── KKBOT.PluginMarketplace
│   │   ├── KKBOT.VisualScripting
│   │   └── KKBOT.Collaboration
│   ├── Enterprise & Hosting (5)
│   │   ├── KKBOT.Host
│   │   ├── KKBOT.CLI
│   │   ├── KKBOT.Dashboard
│   │   ├── KKBOT.ControlCenter.Wpf
│   │   └── KKBOT.Enterprise
│   └── License & Security (3)
│       ├── KKBOT.LicenseServer
│       ├── KKBOT.Licensing.Client
│       └── KKBOT.Licensing.Models
└── tests/ (15 Projects)
    ├── KKBOT.Domain.Tests
    ├── KKBOT.Application.Tests
    ├── KKBOT.Workflow.Tests
    ├── KKBOT.Orchestration.Tests
    ├── KKBOT.Perception.Tests
    ├── KKBOT.Persistence.Tests
    ├── KKBOT.IntegrationTests
    ├── KKBOT.StressTests
    ├── KKBOT.Plugin.NetworkProxy.Tests
    ├── KKBOT.Plugin.LogisticsSurvival.Tests
    ├── KKBOT.Identity.Tests
    ├── KKBOT.Marketplace.Server.Tests
    ├── KKBOT.VisualScripting.Tests
    ├── KKBOT.Collaboration.Tests
    ├── KKBOT.SystemHealthCheck
    └── KKBOT.AI.Tests
```

### Key Source Files by Category

#### Solution & Configuration
- `KKBOT.sln` - Main solution file (68 projects)
- `global.json` - .NET SDK version (10.0.100-preview.1)
- `Directory.Build.props` - Central build configuration
- `Directory.Packages.props` - Central package version management
- `NuGet.Config` - Package source configuration

#### Source Files (.cs) - Total: ~300+ files

**Core Domain:**
```
src/KKBOT.Domain/
├── Common/Entity.cs, Error.cs, Result.cs
├── Enums/DeviceType.cs, InstanceState.cs, OperatorRole.cs, Permission.cs, RecoveryLevel.cs, TaskState.cs
├── Models/Device.cs, Entity.cs, Instance.cs, Job.cs, Module.cs, ModuleVersion.cs, Operator.cs, Profile.cs, ProfileVersion.cs, RecoveryAttempt.cs, Schedule.cs, StateTransition.cs, TaskRun.cs
└── Extensions/InstanceExtensions.cs
```

**Device Abstractions:**
```
src/KKBOT.Device.Abstractions/
├── DispatchResult.cs, IDeviceDiscovery.cs, IDeviceSession.cs, IInputDispatcher.cs, Models.cs
```

**Device ADB Implementation:**
```
src/KKBOT.Device.Adb/
├── AdbClientFactory.cs, AdbCommandRunner.cs, AdbDeviceDiscovery.cs, AdbDeviceSession.cs, AdbHealthCheck.cs, AdbInputDispatcher.cs, AdbScreenshotProvider.cs
```

**Device Simulator:**
```
src/KKBOT.Device.Simulator/
├── FailureInjection.cs, FakeInputDispatcher.cs, ReplayFrameProvider.cs, ScenarioLoader.cs, ScenarioModels.cs, ScenarioParser.cs, ScenarioRunner.cs, SimulatedDeviceSession.cs, SimulatedDeviceSessionFactory.cs, SimulationScenario.cs, TestFixtures.cs
```

**Perception (OpenCV + OCR):**
```
src/KKBOT.Perception.Abstractions/
├── IGameDetector.cs, IOcrEngine.cs, IPerceptionPipeline.cs, PerceptionPipeline.cs

src/KKBOT.Perception.OpenCv/
├── ImageMatcher.cs, ImagePreprocessor.cs, OpenCvDetector.cs, OpenCvGameDetector.cs, PixelConverter.cs, PlaceholderDetector.cs, TemplateAssetLoader.cs

src/KKBOT.Perception.Ocr/
├── OcrPreprocessor.cs, OcrResultParser.cs, PlaceholderOcrEngine.cs, SimpleOcrEngine.cs, TesseractOcrEngine.cs

src/KKBOT.Perception/
├── FrameDropPolicy.cs, PerceptionPipeline.cs, TemporalValidator.cs
```

**AI Integration (Ollama):**
```
src/KKBOT.AI.Abstractions/
├── IAiService.cs, IAiVisionService.cs

src/KKBOT.AI.Ollama/
├── Models/OllamaRequest.cs, OllamaResponse.cs, OllamaVisionRequest.cs
├── OllamaOptions.cs, OllamaService.cs, OllamaVisionService.cs
├── RateLimiting/OllamaRateLimiter.cs
```

**Plugins Abstractions:**
```
src/KKBOT.Plugins.Abstractions/
├── BaseGamePlugin.cs, GameEventArgs.cs, IGameModule.cs, IGameTaskDefinition.cs, IStateDefinition.cs
```

**Plugin Loader:**
```
src/KKBOT.Plugins.Loader/
├── PluginLoader.cs
```

**Game Plugins (7 Modules):**
```
src/KKBOT.Plugin.AccountLogin/AccountLoginModule.cs          // Module 1: Auto-Login
src/KKBOT.Plugin.AntiDetection/AntiDetectionModule.cs        // Module 7: Humanizer + Telegram
src/KKBOT.Plugin.ResourceOptimizer/ResourceOptimizerModule.cs // Module 3: FPS Limiter
src/KKBOT.Plugin.NetworkProxy/NetworkProxyModule.cs          // Module 2: Proxy Binding
src/KKBOT.Plugin.LogisticsSurvival/LogisticsSurvivalModule.cs // Module 4: Auto Heal/Buff
src/KKBOT.Plugin.CombatQuest/CombatQuestModule.cs            // Module 5: Skill Combo
src/KKBOT.Plugin.EconomyTrading/EconomyTradingModule.cs      // Module 6: Auto Vending
```

**Orchestration:**
```
src/KKBOT.Orchestration/
├── AdvancedInstanceOrchestrator.cs, DagScheduler.cs, DynamicDag/IDynamicDagBuilder.cs
├── ErrorCodes.cs, EventSourcing/EventStore.cs, EventSourcing/IEventStore.cs
├── EventStore.cs, EventUpcaster.cs, IInstanceManager.cs
├── InstanceOrchestrator.cs, JobLease.cs, MultiTenancy/ITenantProvider.cs
├── OrchestrationExtensions.cs, Projections/Projections.cs
├── RecoveryCoordinator.cs, Saga/ISaga.cs, Saga/Saga.cs
├── Scheduler.cs
```

**Persistence (EF Core):**
```
src/KKBOT.Persistence/
├── DesignTimeDbContextFactory.cs, KkbBotDbContext.cs
├── Repositories/IInstanceRepository.cs, InstanceRepository.cs, IRepository.cs, Repository.cs
├── Abstractions/IInstanceRepository.cs, IJobRepository.cs
├── EfCore/KkbotDbContext.cs, PersistenceExtensions.cs, Repositories.cs
```

**Observability:**
```
src/KKBOT.Observability/
├── Alerts/AlertManager.cs
├── Logging/Bootstrapper.cs, LogEnricher.cs, StructuredLoggingExtensions.cs
├── Metrics/IMetricsRegistry.cs, MetricNames.cs, MetricsRegistry.cs
├── PrometheusEndpointExtensions.cs, Telemetry.cs
├── Tracing/OpenTelemetryExtensions.cs
```

**Security:**
```
src/KKBOT.Security/
├── AesEncryptionService.cs, Audit/AuditLogMiddleware.cs, Audit/PiiRedactionMiddleware.cs
├── AuditLogger.cs, FileSecretManager.cs, LicenseValidator.cs, PermissionEvaluator.cs
├── PluginSigningValidator.cs, Secrets/DpapiSecretManager.cs, SecretStore.cs, SecurityExtensions.cs
```

**Visual Scripting:**
```
src/KKBOT.VisualScripting/
├── Engine/VisualScriptingEngine.cs
├── Models/VisualScriptingModels.cs
├── VisualScriptingExtensions.cs
```

**Workflows:**
```
src/KKBOT.Workflow/StateMachine/
├── IStateMachine.cs, StateMachine.cs
```

---

## 2. CLASS INVENTORY (DANH SÁCH CLASS & INTERFACE)

### KKBOT.Domain (Core Domain Models)

| Class/Interface | Type | Description | Key Methods/Properties |
|-----------------|------|-------------|------------------------|
| `Entity` | Class | Base entity with ID, CreatedAt, UpdatedAt | `Id`, `CreatedAt`, `UpdatedAt`, `Touch()` |
| `Instance` | Class | Game automation instance aggregate | `Id`, `Key`, `State`, `DeviceId`, `ModuleId`, `ProfileId`, `StateTransitions`, `RecordHeartbeat()`, `RecordFrameCapture()`, `IsHealthy()`, `SetState()`, `IncrementRecoveryAttempt()` |
| `Device` | Class | Physical/virtual device representation | `Id`, `Name`, `Type`, `ConnectionString`, `Capabilities`, `IsOnline` |
| `Module` | Class | Game module/plugin metadata | `Id`, `GameId`, `Version`, `Manifest`, `ConfigSchema`, `IsEnabled` |
| `Profile` | Class | Game configuration profile | `Id`, `Name`, `ModuleId`, `ConfigJson`, `Version`, `IsDefault` |
| `Job` | Class | Scheduled work unit | `Id`, `InstanceId`, `ModuleId`, `Parameters`, `State`, `Priority`, `Schedule`, `RetryPolicy` |
| `TaskRun` | Class | Execution record of a task | `Id`, `JobId`, `InstanceId`, `StartTime`, `EndTime`, `State`, `Result`, `ErrorMessage` |
| `StateTransition` | Class | Instance state change log | `Id`, `InstanceId`, `FromState`, `ToState`, `Reason`, `TriggeredBy`, `Timestamp` |
| `RecoveryAttempt` | Class | Recovery action record | `Id`, `InstanceId`, `Level`, `ErrorCode`, `Success`, `Timestamp` |
| `Schedule` | Class | Cron-like scheduling definition | `Id`, `Expression`, `TimeZone`, `Enabled`, `NextRun` |
| `ProfileVersion` | Class | Versioned profile configuration | `Id`, `ProfileId`, `ConfigJson`, `Version`, `CreatedAt` |
| `ModuleVersion` | Class | Versioned module assembly | `Id`, `ModuleId`, `AssemblyPath`, `Version`, `Checksum` |

**Enums:**
| Enum | Values |
|------|--------|
| `InstanceState` | `Created`, `Connecting`, `Ready`, `Running`, `Paused`, `Waiting`, `Recovering`, `Stopped`, `Failed`, `ManualReview`, `Disposed` |
| `DeviceType` | `Adb`, `Simulator`, `Win32`, `Remote` |
| `RecoveryLevel` | `RetryOperation`, `RecaptureAndReevaluate`, `RestartTask`, `RestartApplication`, `ReconnectDevice`, `ManualReview`, `StopInstance` |
| `TaskState` | `Pending`, `Running`, `Succeeded`, `Failed`, `Cancelled`, `Retrying` |
| `Permission` | `InstanceRead`, `InstanceWrite`, `InstanceDelete`, `DeviceRead`, `DeviceWrite`, `PluginRead`, `PluginWrite`, `PluginManage`, `JobRead`, `JobWrite`, `ProfileRead`, `ProfileWrite`, `ModuleRead`, `ModuleWrite`, `SettingsRead`, `SettingsWrite`, `Admin` |
| `OperatorRole` | `SuperAdmin`, `TenantAdmin`, `Operator`, `Developer`, `Viewer` |

**Common Types:**
| Type | Description |
|------|-------------|
| `Result<T>` | Railway-oriented programming result (Success/Failure) |
| `Error` | Structured error with code, message, severity |
| `ErrorCode` | Static class with error code constants |

---

### KKBOT.Device.Abstractions (Device Contracts)

| Interface/Class | Type | Description | Key Members |
|-----------------|------|-------------|-------------|
| `IDeviceSession` | Interface | Device session contract | `DeviceId`, `GetInfoAsync()`, `IsConnectedAsync()`, `CaptureAsync()`, `SendInputAsync()`, `LaunchApplicationAsync()`, `StopApplicationAsync()`, `HealthCheckAsync()` |
| `IDeviceDiscovery` | Interface | Device enumeration | `DiscoverAsync()`, `GetDeviceAsync(id)`, `Watch()` |
| `IDeviceSessionFactory` | Interface | Session factory | `CreateAsync(deviceId)` |
| `IInputDispatcher` | Interface | Input dispatch abstraction | `DispatchAsync(InputCommand)` |
| `InputCommand` | Record | Input action | `Type` (Tap/Swipe/Text/Key), `X`, `Y`, `EndX`, `EndY`, `Text`, `Duration`, `KeyCode` |
| `InputResult` | Record | Input execution result | `IsSuccess`, `ErrorMessage`, `Duration` |
| `DispatchResult` | Record | Low-level dispatch result | `Success`, `Details` |
| `Frame` | Record | Captured screen frame | `FrameId`, `Timestamp`, `Width`, `Height`, `Format`, `Data`, `CaptureLatencyMs`, `Metadata` |
| `CaptureOptions` | Record | Capture configuration | `RegionOfInterest`, `Format`, `Quality`, `Timeout` |
| `DeviceInfo` | Record | Device metadata | `DeviceId`, `Model`, `Manufacturer`, `AndroidVersion`, `ScreenWidth/Height`, `BatteryLevel`, `IsCharging` |
| `ApplicationIdentity` | Record | App to launch/stop | `PackageName`, `ActivityName`, `Arguments` |
| `HealthCheckResult` | Record | Health status | `IsHealthy`, `IsConnected`, `CpuUsage`, `MemoryUsage`, `LatencyMs` |

---

### KKBOT.Device.Adb (ADB Implementation)

| Class | Description | Key Methods |
|-------|-------------|-------------|
| `AdbClientFactory` | Creates ADB sessions | `CreateAsync(deviceId)` |
| `AdbDeviceSession` | ADB device session | Implements `IDeviceSession` using `adb` commands |
| `AdbCommandRunner` | Executes ADB commands | `RunCommandAsync()`, `RunCommandWithOutputAsync()`, circuit breaker, timeout per command type |
| `AdbDeviceDiscovery` | Discovers ADB devices | `DiscoverAsync()` - lists `adb devices` |
| `AdbInputDispatcher` | ADB input dispatch | `DispatchAsync()` via `adb shell input` |
| `AdbScreenshotProvider` | Screen capture via ADB | `CaptureAsync()` using `adb exec-out screencap` |
| `AdbHealthCheck` | ADB health monitoring | Checks daemon, device connectivity, screen state |

---

### KKBOT.Device.Simulator (Testing Simulator)

| Class | Description |
|-------|-------------|
| `SimulatedDeviceSessionFactory` | Creates simulated sessions |
| `SimulatedDeviceSession` | Replays recorded scenarios |
| `ReplayFrameProvider` | Provides frames from scenario files |
| `ScenarioRunner` | Executes DSL v2 scenarios |
| `ScenarioParser` | Parses scenario YAML/JSON |
| `ScenarioModels` | Step types: Capture, Input, Delay, Assert, If, Loop, Parallel, Call, SetVariable, WaitFor |
| `FakeInputDispatcher` | Logs inputs without executing |
| `FailureInjection` | Simulates timeout, corrupt frame, app crash |
| `TestFixtures` | Test helpers |

---

### KKBOT.Perception (Computer Vision + OCR)

| Interface/Class | Description | Key Methods |
|-----------------|-------------|-------------|
| `IPerceptionPipeline` | Main perception orchestrator | `ProcessAsync(frame)`, `AddDetector()`, `RemoveDetector()` |
| `IGameDetector` | Detection contract | `Id`, `Name`, `DetectAsync(context)` → `DetectionResult` |
| `IOcrEngine` | OCR abstraction | `RecognizeAsync(image, region, language)` → `OcrResult` |
| `PerceptionPipeline` | Pipeline implementation | Manages detectors, runs in parallel, handles frame drop |
| `OpenCvDetector` | Template matching detector | `DetectAsync()` using `cv::matchTemplate` |
| `OpenCvGameDetector` | YOLOv8 ONNX detector | Object detection via ONNX Runtime |
| `ImageMatcher` | Template matching core | `MatchAsync(source, template, threshold)` |
| `ImagePreprocessor` | Pre-processing | Grayscale, resize, normalize, threshold |
| `TemplateAssetLoader` | Loads template images | From embedded resources or disk |
| `PixelConverter` | Format conversion | BGRA↔RGB, byte[]↔Mat |
| `TesseractOcrEngine` | Tesseract wrapper | `RecognizeAsync()` with language data |
| `SimpleOcrEngine` | Fallback OCR | Basic pattern matching |
| `OcrPreprocessor` | OCR image prep | Binarize, deskew, resize |
| `OcrResultParser` | Parses OCR output | Extracts numbers, coordinates, text |
| `FrameDropPolicy` | Backpressure handling | Drop oldest, drop newest, drop random |
| `TemporalValidator` | Detection validation | Debounce, confidence smoothing, hysteresis |

---

### KKBOT.AI (Ollama Integration)

| Class | Description | Key Methods |
|-------|-------------|-------------|
| `IAiService` | Text generation abstraction | `GenerateAsync(prompt, options)`, `GenerateStreamAsync()` |
| `IAiVisionService` | Vision abstraction | `AnalyzeImageAsync(image, prompt)` |
| `OllamaService` | Ollama client | HTTP client with streaming, retry, rate limiting |
| `OllamaVisionService` | Vision via Ollama | Base64 image encoding, multi-modal prompts |
| `OllamaRateLimiter` | Token bucket rate limiter | Per-model, per-minute limits |
| `OllamaOptions` | Configuration | `BaseUrl`, `DefaultModel`, `Timeout`, `MaxTokens` |

---

### KKBOT.Plugins.Abstractions (Plugin Contract)

| Interface/Class | Description | Key Members |
|-----------------|-------------|-------------|
| `IGameModule` | Plugin entry point | `Id`, `Version`, `GetManifest()`, `GetDetectors()`, `GetTasks()`, `GetStates()`, `ValidateAsync()` |
| `BaseGamePlugin` | Abstract base class | Template methods: `InitializeAsync()`, `StartAsync()`, `StopAsync()`, `GetHealthAsync()`, 35+ game event handlers |
| `IGameTaskDefinition` | Task definition | `Id`, `Name`, `Type`, `DefaultTimeout`, `RetryPolicy`, `ExecuteAsync(context)` |
| `IStateDefinition` | FSM state | `Id`, `Name`, `Category`, `EntryCondition`, `Action`, `NextStateIds`, `Timeout`, `RetryPolicy`, `EvidencePolicy` |
| `IStateAction` | State behavior | `ExecuteAsync(ct)` |
| `GameContext` | Service accessor | `GetService<T>()`, `Configuration`, `EventBus`, `Device`, `Perception`, `AI`, `Input`, `Scheduler` |

**Event Arguments (35+ types):**
- Login: `LoginEventArgs`, `LoginSuccessEventArgs`, `LoginFailedEventArgs`
- Character: `CharacterEnterWorldEventArgs`, `LevelUpEventArgs`, `DeadEventArgs`, `ReviveEventArgs`
- Combat: `CombatStartEventArgs`, `CombatEndEventArgs`, `SkillUsedEventArgs`, `DamageTakenEventArgs`
- Inventory: `InventoryFullEventArgs`, `ItemDropEventArgs`, `StaminaLowEventArgs`
- State: `StuckEventArgs`, `BossSpawnEventArgs`, `PlayerNearbyEventArgs`
- Social: `ChatMessageEventArgs`, `PartyInviteEventArgs`, `TradeRequestEventArgs`
- System: `WindowFocusLostEventArgs`, `WindowFocusGainedEventArgs`, `DisconnectedEventArgs`, `ReconnectedEventArgs`, `TickEventArgs`

**Support Types:**
| Type | Description |
|------|-------------|
| `ModuleManifest` | Plugin metadata (Id, Name, Version, Author, Permissions, Assets) |
| `ModuleContext` | Runtime context (InstanceId, DeviceId, ModuleId) |
| `ModuleValidationResult` | Validation result (IsValid, Errors[]) |
| `ModuleHealth` | Health status (IsHealthy, Status, Issues[]) |
| `HealthStatus` | Enum: `Healthy`, `Degraded`, `Unhealthy` |
| `RetryPolicy` | MaxRetries, RetryDelayMs, ExponentialBackoff |
| `EvidencePolicy` | RequireScreenshot, RequireOcrResult |

---

### KKBOT.Plugins.Loader

| Class | Description |
|-------|-------------|
| `PluginLoader` | Loads plugins from `plugins/` directory using `AssemblyLoadContext` isolation, supports hot-reload, signature validation |
| `PluginLoadContext` | Collectible `AssemblyLoadContext` for plugin isolation |
| `PluginLoaderOptions` | `PluginDirectory`, `RequireSignature`, `EnableHotReload`, `LoadTimeout` |

---

### KKBOT.Orchestration (Core Engine)

| Class | Description | Key Methods |
|-------|-------------|-------------|
| `InstanceOrchestrator` | Main instance controller | `StartInstanceAsync()`, `StopInstanceAsync()`, `PauseAsync()`, `ResumeAsync()`, `GetInstanceState()` |
| `AdvancedInstanceOrchestrator` | Extended with ML/AI | ML-based decision making, predictive scaling |
| `DagScheduler` | DAG job scheduler | `ScheduleJobRunAsync()`, `RunAsync()` with topological sort |
| `Scheduler` | Cron-like scheduler | `ScheduleRecurring()`, `ScheduleOneShot()`, `Cancel()` |
| `RecoveryCoordinator` | Self-healing engine | `DetermineNextLevelAsync()`, `ExecuteRecoveryAsync()`, 7-level ladder |
| `InstanceManager` | Instance registry | `RegisterAsync()`, `GetInstance()`, `StartAsync()`, `StopAsync()`, `GetCounts()`, `GetResourceBudget()` |
| `EventStore` | Event sourcing | `AppendAsync()`, `GetEventsAsync()`, snapshots, projections, upcasting |
| `Saga` | Distributed transaction | Compensation-based, `ExecuteAsync()` with rollback |
| `Projections` | Read models | `InstanceStateProjection`, `JobExecutionProjection`, `RecoveryProjection`, `MetricsProjection`, `HealthProjection` |
| `EventUpcaster` | Event versioning | v1→v2 migration for 3 event types |

---

### KKBOT.Application (CQRS)

| Class | Description |
|-------|-------------|
| `StartInstanceCommand` | Start instance command |
| `StartInstanceHandler` | Handles start logic |
| `Queries` | Read models for API |

---

### KKBOT.Plugin.* (7 Game Modules)

#### Module 1: AccountLogin (`KKBOT.Plugin.AccountLogin`)
| Class | Description |
|-------|-------------|
| `AccountLoginModule` | Implements `BaseGamePlugin` for auto-login |
| `LoginPhase` | Enum: `NotStarted`, `Launching`, `LoginScreen`, `EnteringCredentials`, `SelectingServer`, `SelectingChannel`, `SelectingCharacter`, `InGame`, `Failed` |
| `AccountConfig` | Credentials, server, character, PIN, 2FA settings |
| `LoginState` | Tracks login progress per account |

**Key Features:**
- Auto credential injection (bypass paste protection)
- Server/Channel/Character selection automation
- Security screen detection (PIN, OTP, Captcha via Perception + AI Vision)
- Crash recovery (process monitoring, PID tracking, auto-restart with random delay, relogin)
- Heartbeat monitoring, crash detection, auto-relogin

#### Module 2: NetworkProxy (`KKBOT.Plugin.NetworkProxy`)
| Class | Description |
|-------|-------------|
| `NetworkProxyModule` | Proxy binding per instance |
| `ProxyConfig` | ProxyId, Host, Port, Type (HTTP/SOCKS5), Auth |
| `ProxyPool` | Pool management, health checks |
| `NetworkProxyService` | `BindProxyAsync()`, `AutoBindProxyAsync()`, `RotateInstanceProxyAsync()`, `ApplyNetworkIsolationAsync()` |

**Key Features:**
- Per-instance proxy binding (HTTP/SOCKS5 with auth)
- Network isolation layer (Linux namespaces, WebRTC/Direct Socket leak prevention)
- Proxy health checks & auto-rotation (dead proxy detection, fallback pool)
- IP leak prevention (bind verification)

#### Module 3: ResourceOptimizer (`KKBOT.Plugin.ResourceOptimizer`)
| Class | Description |
|-------|-------------|
| `ResourceOptimizerModule` | Resource management |
| `FpsLimiter` | 5-10 FPS background, 30 combat, 5 idle |
| `RenderStripper` | 3D, particles, shadows, UI stripping |
| `RamCleaner` | Periodic, threshold-based, aggressive mode |
| `WindowGridLayout` | Tile, minimize 800×600/480×320, System Tray |

**Key Features:**
- Dynamic FPS limiting based on instance state
- Render stripping (configurable: 3D, particles, shadows, UI)
- RAM auto-cleaner (timer + threshold + aggressive mode)
- Window grid layout for multi-instance management

#### Module 4: LogisticsSurvival (`KKBOT.Plugin.LogisticsSurvival`)
| Class | Description |
|-------|-------------|
| `LogisticsSurvivalModule` | Auto sustain |
| `SmartInventoryManager` | Filter: Attribute/Value/Class, auto-equip, auto-sell, auto-destroy |

**Key Features:**
- Auto Heal/Buff (% HP/MP, multiple potions, skill cooldowns)
- Auto Refill & Repair (Town portal → NPC → Buy/Repair → Return)
- Smart Inventory (filter by attribute/value/class, auto-equip better gear, auto-sell junk)

#### Module 5: CombatQuest (`KKBOT.Plugin.CombatQuest`)
| Class | Description |
|-------|-------------|
| `CombatQuestModule` | Combat & quest automation |
| `SkillComboEngine` | Combo order, cooldown, range, priority, hit&run |
| `TargetingSystem` | Priority: normal/boss, anti-KS, channel hop |
| `QuestEngine` | Main quest 1→N, daily/dungeon/guild |
| `PartyController` | Master-Slave, Follow, Accept invite, Assist/Buff |

**Key Features:**
- Skill combo engine with priority & cooldown management
- Anti-KS / Change Channel (channel hop, safe zone coordinates)
- Auto daily quest (main, daily, dungeon, guild)
- Party controller (master-slave, follow, assist, buff)

#### Module 6: EconomyTrading (`KKBOT.Plugin.EconomyTrading`)
| Class | Description |
|-------|-------------|
| `EconomyTradingModule` | Economic automation |
| `AutoVending` | Market navigation, item_prices.json, 24/7 vending |
| `MasterAccumulator` | Threshold transfer, Trade/Mail, Return to grind |

**Key Features:**
- Auto vending/shop (market nav, dynamic pricing, 24/7)
- Master accumulator (gold/item threshold → trade/mail → return)

#### Module 7: AntiDetection (`KKBOT.Plugin.AntiDetection`)
| Class | Description |
|-------|-------------|
| `AntiDetectionModule` | Human-like behavior |
| `HumanizerEngine` | 100-350ms delays, ±2-5px jitter, Bezier curves |
| `TelegramBot` | Alerts + remote control |

**Key Features:**
- Humanizer: Random delays (100-350ms), coordinate jitter (±2-5px), Bezier curve mouse movement
- Telegram Bot: Death/VIP drop/Disconnect/Low HP/Level up alerts
- Remote commands: `/status`, `/pause_all`, `/restart_game`, `/screenshot`

---

### KKBOT.AI.Pipeline (AI Pipeline)

| Class | Description |
|-------|-------------|
| `IPipelineServices` | Pipeline abstraction |
| `PipelineServices` | Training/inference orchestration |
| `PipelineDefinition` | Steps, dependencies, resources |
| `PipelineRun` | Execution tracking |
| `AdvancedServices` | Model management, A/B testing |

---

### KKBOT.MLPlatform (ML Platform - 8 Projects)

| Project | Description |
|---------|-------------|
| `KKBOT.MLPlatform.Abstractions` | Core interfaces |
| `KKBOT.MLPlatform.ModelServer` | ONNX serving with batching, GPU, auto-scaling |
| `KKBOT.MLPlatform.Distillation` | Knowledge distillation (Temp scaling, INT8/INT4, EWC) |
| `KKBOT.MLPlatform.FeatureStore` | Hybrid Redis+PostgreSQL, point-in-time queries |
| `KKBOT.MLPlatform.ABTesting` | Bayesian inference, sequential testing, bandits |
| `KKBOT.MLPlatform.OnlineLearning` | Experience replay, EWC, concept drift detection |
| `KKBOT.MLPlatform` | Meta-package |

---

### KKBOT.Sim2Real (Sim2Real Pipeline - 7 Projects)

| Project | Description |
|---------|-------------|
| `Abstractions` | Core interfaces (PipelineJob, DRConfig, RewardConfig, DeploymentConfig) |
| `DomainRandomization` | Visual/physics/lighting/camera/texture randomization + curriculum |
| `DomainAdaptation` | CycleGAN, DR-CycleGAN, SimOpt, ADAT, DACs (FID/LPIPS/SSIM) |
| `RewardModeling` | GAIL, SQIL, CQL, IQL, Offline RL |
| `PolicyDeployment` | ONNX/TensorRT/TorchScript/WASM + quantization (INT8/INT4/FP16) |
| `Evaluation` | Sim2Real gap analysis, statistical tests, video recording |
| `Sim2Real` | Orchestrator with checkpointing, retry, stage management |

---

### KKBOT.Resilience

| Class | Description |
|-------|-------------|
| `CircuitBreaker` | Polly-based: failure threshold, timeout, half-open testing |
| `RecoveryLadder` | 7-level: Retry → Recapture → RestartTask → RestartApp → ReconnectDevice → ManualReview → Stop |

---

### KKBOT.PolicyEngine

| Class | Description |
|-------|-------------|
| `PolicyEngine` | Rule evaluation engine with CEL expressions |

---

### KKBOT.Security

| Class | Description |
|-------|-------------|
| `AesEncryptionService` | AES-256-GCM encryption |
| `DpapiSecretManager` | Windows DPAPI secret storage |
| `FileSecretManager` | Encrypted file secrets |
| `AuditLogger` | Structured audit logging |
| `AuditLogMiddleware` | HTTP audit trail |
| `PiiRedactionMiddleware` | PII redaction in logs |
| `LicenseValidator` | Ed25519 signature verification |
| `PluginSigningValidator` | Plugin signature validation |
| `PermissionEvaluator` | RBAC permission checks |

---

### KKBOT.Identity

| Class | Description |
|-------|-------------|
| `KKBOTPermissions` | 47 permissions across 12 categories |
| `PermissionAuthorizationHandler` | Resource-based auth |
| `RequirePermissionAttribute` | Controller/action authorization |
| `IdentityService` | User management, JWT tokens |

---

### KKBOT.CLI

| Class | Description |
|-------|-------------|
| `Program` | `kkbot` CLI: instance, plugin, device, scenario, profile commands |

---

### KKBOT.Host / KKBOT.Dashboard / KKBOT.ControlCenter.Wpf

| Project | Description |
|---------|-------------|
| `KKBOT.Host` | Generic host for Windows Service / Docker |
| `KKBOT.Dashboard` | Blazor Server + MudBlazor |
| `KKBOT.ControlCenter.Wpf` | WPF control center (MVVM) |

---

### KKBOT.LicenseServer / KKBOT.Licensing.Client

| Component | Description |
|-----------|-------------|
| `LicenseServer` | ASP.NET Core API: activation, heartbeat, offline grace, revocation |
| `LicenseValidator` | Client-side validation, Ed25519, hardware fingerprint |
| `HardwareFingerprintGenerator` | Stable machine ID |

---

## 3. FEATURE & LOGIC LIST (TÍNH NĂNG VÀ LUỒNG XỬ LÝ)

### Core Platform Features

| Feature | Implementation | Status |
|---------|----------------|--------|
| **Plugin Architecture** | AssemblyLoadContext + WASM (Wasmtime) isolation, hot-reload, capability-based security | ✅ Complete |
| **Device Abstraction** | ADB (Android), Simulator (DSL v2), Win32 (planned) | ✅ Complete |
| **Perception Pipeline** | OpenCV template matching, YOLOv8 ONNX, Tesseract/EasyOCR, pipeline with frame drop policy | ✅ Complete |
| **AI Integration** | Ollama text + vision, streaming IAsyncEnumerable, rate limiting, circuit breaker | ✅ Complete |
| **Orchestration Engine** | DAG Scheduler, FSM from plugin states, Event Sourcing, Saga pattern, Recovery Coordinator | ✅ Complete |
| **Visual Scripting** | 18 node types, Canvas editor, live execution | ✅ Complete |
| **Multi-Agent Coordination** | Redis Registry, MessageBus, Raft Consensus, Leader Election, Redlock | ✅ Complete |
| **Observability** | OpenTelemetry (Jaeger/OTLP), Serilog, 50+ Prometheus metrics, AlertManager, Health checks | ✅ Complete |
| **Security** | JWT/RBAC (47 perms, 5 roles), Capability sandbox, Ed25519 plugin signing, DPAPI secrets, Audit log | ✅ Complete |
| **Visual Scenario Editor** | Blazor Canvas drag-drop, 10 node types, execution engine | ✅ Complete |
| **EventStore Maturation** | Snapshots, 5 Projections, Event Upcasting v1→v2 | ✅ Complete |
| **WASM Plugin Sandbox** | Wasmtime, 20 capabilities, Rust SDK, fuel metering | ✅ Complete |
| **Performance** | L2 Cache, Connection Pooling, Object Pooling, Async optimization, EF Core compiled queries | ✅ Complete |

---

### Game-Specific Modules (7/7 Complete)

| Module | Name | Key Features | Implementation |
|--------|------|--------------|----------------|
| **1** | Account & Auto-Login | Auto login/relogin, credential injection (bypass paste), Server/Channel/Character selection, PIN/OTP/Captcha via Perception+AI, Crash recovery (PID tracking, auto-restart, random delay, relogin), Heartbeat monitoring | `AccountLoginModule.cs` |
| **2** | Network & Proxy | Per-instance proxy binding (HTTP/SOCKS5 + auth), Proxy pools with health checks, Auto-rotation on dead proxy, Network isolation (Linux namespaces, WebRTC/Direct Socket leak prevention), IP leak verification | `NetworkProxyModule.cs`, `DirectSocketLeakPrevention.cs`, `LinuxNetworkNamespaceIsolation.cs`, `WebRTCLeakPrevention.cs` |
| **3** | Resource Optimizer | FPS Limiter (5-10 background, 30 combat, 5 idle), Render Stripping (3D/particles/shadows/UI), RAM Cleaner (periodic/threshold/aggressive), Window Grid Layout (Tile/Minimize 800×600/480×320/System Tray), CPU/GPU/RAM/FPS monitoring with auto FPS adjustment | `ResourceOptimizerModule.cs` |
| **4** | Logistics & Survival | Auto Heal/Buff (% HP/MP, multiple potions, skill cooldowns), Auto Refill & Repair (Town portal → NPC → Buy/Repair → Return), Smart Inventory (Attribute/Value/Class filter, auto-equip, auto-sell, auto-destroy), Stuck detection | `LogisticsSurvivalModule.cs`, `SmartInventoryManager.cs` |
| **5** | Combat & Quest | Skill Combo Engine (order, cooldown, range, priority, hit&run), Targeting (nearest/boss, anti-KS, channel hop), Auto Quest (Main 1→N, daily/dungeon/guild), Party Controller (Master-Slave, Follow, Accept invite, Assist/Buff) | `CombatQuestModule.cs` |
| **6** | Economy & Trading | Auto Vending (Market nav, item_prices.json, 24/7), Master Accumulator (Threshold → Trade/Mail → Return to grind), Market monitor | `EconomyTradingModule.cs` |
| **7** | Anti-Detection & Alerts | Humanizer (100-350ms delay, ±2-5px jitter, Bezier curves), Telegram Bot (Death/VIP drop/Disconnect/Low HP/Level up alerts), Remote control (`/status`, `/pause_all`, `/restart_game`, `/screenshot`) | `AntiDetectionModule.cs` |

---

### Advanced Features

| Feature | Description |
|---------|-------------|
| **AI Pipeline** | Training/inference orchestration, model versioning, A/B testing, feature store integration |
| **ML Platform** | ONNX Model Server (batching, GPU), Knowledge Distillation (INT8/INT4), Hybrid Feature Store (Redis+PG), A/B Testing (Bayesian, bandits), Online Learning (EWC, drift detection) |
| **Sim2Real Pipeline** | 6-stage: Domain Randomization → Domain Adaptation → Reward Modeling → Policy Training → Deployment → Evaluation |
| **Visual Scripting** | 18 nodes: Device(Capture/Input), Perception(Detect/OCR), AI(Vision/Chat), Logic(If/Loop/Parallel), Game(Skill/Move/Wait) |
| **Multi-Agent** | Redis registry, gRPC MessageBus, Raft consensus, Leader election, Redlock distributed locks |
| **License System** | License Server (activation, heartbeat, offline grace, revocation), Client validator (Ed25519, HW fingerprint) |

---

### Data Flow & Processing Logic

#### Instance Lifecycle
```
1. Profile Loaded → 2. Device Assigned → 3. Plugin Loaded → 4. InitializeAsync()
      ↓
5. StartAsync() → 6. GameContext.EventBus subscribed → 7. OnStartAsync() (plugin logic)
      ↓
8. Scheduler/Orchestrator runs Tasks → 9. Perception Pipeline processes frames
      ↓
10. Detectors → OCR → AI Vision → State Machine evaluates EntryCondition
      ↓
11. State Action executes → Input sent via IInputDispatcher → Game responds
      ↓
12. Heartbeat recorded → Health monitored → Recovery triggered if needed
      ↓
13. StopAsync() → Unregister events → Cleanup
```

#### Perception Pipeline
```
Frame Capture (30 FPS) → FrameDropPolicy (backpressure)
    ↓
Parallel Detectors (Template/YOLO/OCR) → TemporalValidator (debounce/hysteresis)
    ↓
PerceptionContext built → State Machine EntryCondition evaluated
    ↓
Action selected → Executed via IInputDispatcher
```

#### Recovery Ladder (7 Levels)
```
Level 1: RetryOperation (max 3)           → Same operation again
Level 2: RecaptureAndReevaluate (max 2)   → New frame + re-run perception
Level 3: RestartTask (max 2)              → Restart current task
Level 4: RestartApplication (max 1)       → Stop/Start game process
Level 5: ReconnectDevice (max 2)          → Re-establish device session
Level 6: ManualReview                     → Pause, alert operator
Level 7: StopInstance                     → Mark failed, stop permanently
```

---

## 4. PLUGIN API (HÀM API/INTERFACE CHO PLUGIN)

### Core Plugin Contract: `IGameModule`

```csharp
public interface IGameModule
{
    string Id { get; }                    // Unique plugin ID (e.g., "volam")
    string Version { get; }               // Plugin version
    ModuleManifest GetManifest();         // Metadata: name, permissions, resolutions, assets
    IReadOnlyCollection<IGameDetector> GetDetectors();  // Vision detectors
    IReadOnlyCollection<IGameTaskDefinition> GetTasks(); // Automation tasks
    IReadOnlyCollection<IStateDefinition> GetStates();   // FSM states
    Task<ModuleValidationResult> ValidateAsync(ModuleContext context, CancellationToken ct);
}
```

### Base Class: `BaseGamePlugin` (Inherit for full features)

#### Lifecycle Methods (Override)
```csharp
protected virtual Task OnInitializeAsync(CancellationToken ct)     // Called once on init
protected virtual Task OnStartAsync(CancellationToken ct)          // Called on start
protected virtual Task OnStopAsync(CancellationToken ct)           // Called on stop
protected virtual Task<ModuleHealth> OnHealthCheckAsync(CancellationToken ct) // Health check
protected virtual Task LoadConfigurationAsync(CancellationToken ct) // Custom config loading
protected virtual Task ValidateCoreVersionAsync(CancellationToken ct) // Version check
```

#### Game Event Handlers (35+ - All Optional)
```csharp
// Authentication
protected virtual Task OnLoginAsync(LoginEventArgs args, CancellationToken ct)
protected virtual Task OnLoginSuccessAsync(LoginSuccessEventArgs args, CancellationToken ct)
protected virtual Task OnLoginFailedAsync(LoginFailedEventArgs args, CancellationToken ct)

// Character
protected virtual Task OnCharacterEnterWorldAsync(CharacterEnterWorldEventArgs args, CancellationToken ct)
protected virtual Task OnLevelUpAsync(LevelUpEventArgs args, CancellationToken ct)
protected virtual Task OnDeadAsync(DeadEventArgs args, CancellationToken ct)
protected virtual Task OnReviveAsync(ReviveEventArgs args, CancellationToken ct)

// Combat
protected virtual Task OnCombatStartAsync(CombatStartEventArgs args, CancellationToken ct)
protected virtual Task OnCombatEndAsync(CombatEndEventArgs args, CancellationToken ct)
protected virtual Task OnSkillUsedAsync(SkillUsedEventArgs args, CancellationToken ct)
protected virtual Task OnDamageTakenAsync(DamageTakenEventArgs args, CancellationToken ct)

// Inventory & Resources
protected virtual Task OnInventoryFullAsync(InventoryFullEventArgs args, CancellationToken ct)
protected virtual Task OnItemDropAsync(ItemDropEventArgs args, CancellationToken ct)
protected virtual Task OnStaminaLowAsync(StaminaLowEventArgs args, CancellationToken ct)

// State
protected virtual Task OnStuckAsync(StuckEventArgs args, CancellationToken ct)
protected virtual Task OnBossSpawnAsync(BossSpawnEventArgs args, CancellationToken ct)
protected virtual Task OnPlayerNearbyAsync(PlayerNearbyEventArgs args, CancellationToken ct)

// Social
protected virtual Task OnChatMessageAsync(ChatMessageEventArgs args, CancellationToken ct)
protected virtual Task OnPartyInviteAsync(PartyInviteEventArgs args, CancellationToken ct)
protected virtual Task OnTradeRequestAsync(TradeRequestEventArgs args, CancellationToken ct)

// System
protected virtual Task OnWindowFocusLostAsync(WindowFocusLostEventArgs args, CancellationToken ct)
protected virtual Task OnWindowFocusGainedAsync(WindowFocusGainedEventArgs args, CancellationToken ct)
protected virtual Task OnDisconnectedAsync(DisconnectedEventArgs args, CancellationToken ct)
protected virtual Task OnReconnectedAsync(ReconnectedEventArgs args, CancellationToken ct)
protected virtual Task OnTickAsync(TickEventArgs args, CancellationToken ct)
```

#### Service Access via `GameContext`
```csharp
protected GameContext GameContext { get; }

// Core Services
protected TService GetService<TService>() where TService : class
protected TService GetRequiredService<TService>() where TService : class
protected TConfig GetConfig<TConfig>() where TConfig : class, new()

// Available Services:
IDeviceSession          // Device control (capture, input, launch app)
IPerceptionPipeline     // Vision processing (detect, OCR)
IAiService              // LLM text generation (streaming)
IAiVisionService        // LLM vision analysis
IInputDispatcher        // Low-level input (tap, swipe, text, key)
IDeviceDiscovery        // Device enumeration
IScheduler              // Job scheduling
IInstanceManager        // Instance lifecycle
IRecoveryCoordinator    // Self-healing
IGameEventBus           // Event pub/sub
IConfiguration          // Configuration access
```

#### Configuration Access
```csharp
// Strongly-typed config (from appsettings.json → Plugin config section)
var config = GetConfig<MyGameConfig>();

// Or raw configuration
var section = GameContext.Configuration.GetSection("MyPlugin:Combat");
```

#### Event Publishing
```csharp
// Publish custom events to other plugins or Core
await GameContext.GetService<IGameEventBus>().PublishAsync(new CustomEventArgs 
{ 
    GameId = GameId, 
    CustomData = "data" 
});
```

---

### Required Implementations per Plugin

#### 1. Module Manifest
```csharp
public ModuleManifest GetManifest() => new ModuleManifest
{
    Id = "mygame",
    Name = "My Game Bot",
    Version = "1.0.0",
    Author = "Me",
    Description = "Automation for My Game",
    SupportedResolutions = [new Resolution(1920, 1080)],
    RequiredPermissions = ["DEVICE_READ", "INPUT_WRITE", "SCREENSHOT_CAPTURE"],
    ConfigSchema = "{ \"type\": \"object\" }",
    DefaultConfig = "{}",
    EntryAssembly = "KKBOT.Plugin.MyGame.dll",
    Assets = ["assets/template.png"]
};
```

#### 2. Detectors (Vision)
```csharp
public IReadOnlyCollection<IGameDetector> GetDetectors() => 
    [new HealthBarDetector(), new ManaBarDetector(), new EnemyDetector()];

// Detector implementation:
public sealed class HealthBarDetector : IGameDetector
{
    public string Id => "health_bar";
    public string Name => "Health Bar";
    public async Task<DetectionResult> DetectAsync(PerceptionContext ctx, CancellationToken ct)
    {
        var result = await TemplateMatcher.MatchAsync(ctx.Frame, "assets/health_bar.png", 0.85);
        return new DetectionResult { DetectorId = Id, IsMatch = result.IsMatch, Confidence = result.Confidence, Location = result.Location };
    }
}
```

#### 3. Tasks (Automation Units)
```csharp
public IReadOnlyCollection<IGameTaskDefinition> GetTasks() => 
    [new AutoAttackTask(), new AutoHealTask(), new AutoLootTask()];

// Task implementation:
public sealed class AutoHealTask : IGameTaskDefinition
{
    public string Id => "auto_heal";
    public string Name => "Auto Heal";
    public TaskType Type => TaskType.Repeating;
    public TimeSpan DefaultTimeout => TimeSpan.FromSeconds(30);
    public RetryPolicy DefaultRetryPolicy => new(3, 5000);
    public bool RequiresApproval => false;

    public async Task<IStateDefinition> ExecuteAsync(TaskContext context, CancellationToken ct)
    {
        var device = context.GetDeviceSession();
        var frame = await device.CaptureAsync(new CaptureOptions(), ct);
        var hp = await _hpDetector.DetectAsync(new PerceptionContext(frame), ct);
        if (hp.IsMatch && hp.Confidence < 0.3) 
        {
            await device.SendInputAsync(new InputCommand { Type = InputType.Key, KeyCode = 0x31 }, ct); // '1' key
            await Task.Delay(1000, ct);
        }
        return new IdleState();
    }
}
```

#### 4. States (FSM)
```csharp
public IReadOnlyCollection<IStateDefinition> GetStates() => 
    [new IdleState(), new CombatState(), new HealingState(), new DeadState()];

// State implementation:
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

---

### Input System (for plugins)

```csharp
// Input Types
public enum InputType { Tap, Swipe, Text, Key }

// Send Input via Device Session
await deviceSession.SendInputAsync(new InputCommand
{
    Type = InputType.Tap,
    X = 500,
    Y = 300
}, ct);

await deviceSession.SendInputAsync(new InputCommand
{
    Type = InputType.Swipe,
    X = 100, Y = 100,
    EndX = 500, EndY = 500,
    Duration = TimeSpan.FromMilliseconds(500)
}, ct);

await deviceSession.SendInputAsync(new InputCommand
{
    Type = InputType.Text,
    Text = "Hello World"
}, ct);

await deviceSession.SendInputAsync(new InputCommand
{
    Type = InputType.Key,
    KeyCode = 0x31, // '1' key
    Duration = TimeSpan.FromMilliseconds(100)
}, ct);
```

---

### Health & Monitoring

```csharp
public async Task<ModuleHealth> GetHealthAsync(CancellationToken ct)
{
    var health = new ModuleHealth 
    { 
        ModuleId = GameId, 
        ModuleName = GameName, 
        IsHealthy = true, 
        Status = HealthStatus.Healthy 
    };
    
    // Check device connection
    var device = GameContext.GetService<IDeviceSession>();
    if (device == null || !device.IsConnected)
    {
        health.IsHealthy = false;
        health.Issues.Add("Device not connected");
    }
    
    // Check game process
    var process = device?.GetProcess();
    if (process == null || process.HasExited)
    {
        health.IsHealthy = false;
        health.Issues.Add("Game process not found");
    }
    
    return health;
}
```

---

### Plugin Project Template (`.csproj`)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <AssemblyName>KKBOT.Plugin.MyGame</AssemblyName>
    <RootNamespace>KKBOT.Plugin.MyGame</RootNamespace>
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
    <EnableDynamicLoading>true</EnableDynamicLoading>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\KKBOT.Plugins.Abstractions\KKBOT.Plugins.Abstractions.csproj" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
  </ItemGroup>

  <ItemGroup>
    <None Update="config\*.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
</Project>
```

---

### Manifest File (`mygame.manifest.json`)

```json
{
  "name": "mygame",
  "displayName": "My Game Bot",
  "version": "1.0.0",
  "description": "Automation for My Game",
  "author": "Your Name",
  "gameId": "mygame",
  "gameName": "My Game",
  "supportedRegions": ["VN"],
  "supportedVersions": ["1.0.0", "latest"],
  "minimumCoreVersion": "1.0.0",
  "requiredPermissions": [
    "device.input",
    "device.screen",
    "device.process",
    "network.http"
  ],
  "configSchema": "config/mygame.schema.json",
  "entryPoint": "KKBOT.Plugin.MyGame.MyGameModule",
  "dependencies": [],
  "tags": ["mmorpg", "farming", "pvp"]
}
```

---

## SUMMARY STATISTICS

| Metric | Value |
|--------|-------|
| **Total Projects** | 68 (53 src + 15 tests) |
| **Core Platform Projects** | 14 |
| **Game Modules** | 7/7 (100%) |
| **Advanced Platform Projects** | 6 (ML, Sim2Real, Visual Scripting, etc.) |
| **Lines of Code (est.)** | ~150,000+ |
| **Test Projects** | 15 |
| **Documentation Files** | 32 (in `docs/`) |
| **Plugin APIs** | 35+ event handlers, 15+ core services |
| **Architecture** | Hexagonal / Ports & Adapters |
| **Deployment** | Docker, Helm, K8s ready |

---

## NOTABLE IMPLEMENTATION GAPS / KNOWN ISSUES

1. **GameContext** - Referenced in `BaseGamePlugin` but implementation not found in current source (may be in separate assembly or needs implementation)
2. **Win32 Device Adapter** - Only ADB and Simulator implemented; Win32 desktop automation planned
3. **Module 2 NetworkProxy** - Duplicate entry in solution file (listed twice)
4. **MLPlatform** - Duplicate project entries in solution (listed twice)

---

*This manifest was auto-generated from source code analysis. For the latest updates, refer to the actual source files.*