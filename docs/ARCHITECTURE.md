# Architecture Overview

Comprehensive system architecture documentation for KKBOT.

## Table of Contents

1. [System Context](#system-context)
2. [Architectural Style](#architectural-style)
3. [Core Components](#core-components)
4. [Data Flow](#data-flow)
5. [Deployment Architecture](#deployment-architecture)
6. [Technology Stack](#technology-stack)
7. [Design Decisions](#design-decisions)
8. [Scalability & Performance](#scalability--performance)
9. [Security Architecture](#security-architecture)
10. [Extensibility Points](#extensibility-points)

---

## System Context

### Context Diagram

```
┌─────────────┐     ┌──────────────────┐     ┌─────────────────┐
│   Player    │────▶│      KKBOT       │────▶│   Game Client   │
│  (Human)    │     │   Platform       │     │   (Android/PC)  │
└─────────────┘     └────────┬─────────┘     └────────┬────────┘
                             │                        │
                    ┌────────┴────────┐                │
                    │                 │                │
              ┌─────▼─────┐     ┌─────▼─────┐    ┌─────▼─────┐
              │  Ollama   │     │ PostgreSQL│    │   ADB     │
              │  (AI/LLM) │     │  (Data)   │    │  (Device) │
              └───────────┘     └───────────┘    └───────────┘
                             │
                    ┌────────▼────────┐
                    │   Monitoring    │
                    │ (Prom/Grafana)  │
                    └─────────────────┘
```

### Actors

| Actor | Description | Interactions |
|-------|-------------|--------------|
| **Player** | Human operator | Dashboard UI, CLI, API |
| **Game Client** | Target application | ADB/Input, Screen capture |
| **AI Provider** | Ollama/OpenAI | Text generation, Vision |
| **Database** | PostgreSQL/SQLite | Persistence |
| **Monitoring** | Prometheus/Grafana | Metrics, Logs, Alerts |

---

## Architectural Style

### Hexagonal Architecture (Ports & Adapters)

```
┌─────────────────────────────────────────────────────────────────┐
│                        DOMAIN LAYER                             │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────────┐  │
│  │  Entities   │  │ Value Objs  │  │   Domain Events         │  │
│  │ Instance    │  │ Resolution  │  │ InstanceCreated         │  │
│  │ Job         │  │ Config      │  │ JobCompleted            │  │
│  │ Module      │  │ Coordinates │  │ PluginLoaded            │  │
│  └─────────────┘  └─────────────┘  └─────────────────────────┘  │
│                          │                                      │
│              ┌───────────┴───────────┐                          │
│              │   Domain Services     │                          │
│              │ InstanceManager       │                          │
│              │ JobScheduler          │                          │
│              │ PluginValidator       │                          │
│              └───────────┬───────────┘                          │
└──────────────────────────┼──────────────────────────────────────┘
                           │ Ports (Interfaces)
          ┌────────────────┼────────────────┐
          ▼                ▼                ▼
┌─────────────────┐ ┌─────────────┐ ┌─────────────────┐
│   APPLICATION   │ │  INFRASTRUCTURE│ │    PRESENTATION│
│   (Use Cases)   │ │  (Adapters)    │ │  (Controllers) │
│                 │ │                │ │                │
│ Commands/Queries│ │ EF Core        │ │ REST API       │
│ Validators      │ │ ADB Client     │ │ Blazor Dashboard│
│ Mediator        │ │ Ollama Client  │ │ SignalR Hub    │
│ Event Handlers  │ │ File Secrets   │ │ Swagger UI     │
│                 │ │ OpenCV         │ │                │
└─────────────────┘ └─────────────────┘ └─────────────────┘
```

### Layer Responsibilities

| Layer | Responsibility | Technologies |
|-------|----------------|--------------|
| **Domain** | Business logic, rules, invariants | Pure C#, no dependencies |
| **Application** | Use cases, orchestration, transactions | MediatR, FluentValidation |
| **Infrastructure** | External adapters (DB, APIs, Hardware) | EF Core, HttpClient, OpenCvSharp |
| **Presentation** | API endpoints, UI, CLI | ASP.NET Core, Blazor, Swagger |

### Dependency Rule

```
Presentation → Application → Domain ← Infrastructure
     ↑              ↑              ↑
     └──────────────┴──────────────┘
          (Interfaces defined in Domain/Application)
```

---

## Core Components

### 1. Domain Models

```csharp
// Core aggregates
public record Instance(Guid Id, string Name, InstanceStatus Status, ModuleRef? Module);
public record Job(Guid Id, Guid InstanceId, JobType Type, JobStatus Status, string Payload);
public record Module(Guid Id, string Name, string Version, string EntryAssembly);
public record Device(Guid Id, string Name, DeviceType Type, string ConnectionString);

// Enums
public enum InstanceStatus { Created, Running, Stopped, Error, Paused }
public enum JobStatus { Pending, Running, Completed, Failed, Cancelled }
public enum JobType { OneShot, Repeating, Manual }
public enum DeviceType { ADB, Simulator, Emulator }

// Events
public record InstanceCreatedEvent(Guid InstanceId);
public record JobCompletedEvent(Guid InstanceId, Guid JobId, bool Success);
public record PluginLoadedEvent(string PluginId, string Version);
```

### 2. Plugin System

```csharp
// Plugin Contract (defined in KKBOT.Plugins.Abstractions)
public interface IGameModule
{
    string Id { get; }
    string Version { get; }
    ModuleManifest GetManifest();
    IReadOnlyCollection<IGameDetector> GetDetectors();
    IReadOnlyCollection<IGameTaskDefinition> GetTasks();
    IReadOnlyCollection<IStateDefinition> GetStates();
    Task<ModuleValidationResult> ValidateAsync(ModuleContext context, CancellationToken ct);
}

// Plugin Loader (KKBOT.Plugins.Loader)
public sealed class PluginLoader
{
    public async Task<IReadOnlyCollection<IGameModule>> LoadPluginsAsync(string path, CancellationToken ct);
}

// Isolation via AssemblyLoadContext
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    // Loads plugin + dependencies from plugin folder
}
```

### 3. Perception Pipeline

```csharp
// Detector Interface
public interface IGameDetector
{
    string Id { get; }
    string Name { get; }
    DetectorType Type { get; }  // TemplateMatch, ColorAnalysis, Ocr, Custom
    double RequiredConfidence { get; }
    Task<DetectorResult> DetectAsync(Frame frame, CancellationToken ct);
}

// Pipeline
public interface IPerceptionPipeline
{
    Task<IReadOnlyList<DetectorResult>> ProcessAsync(Frame frame, CancellationToken ct);
}

// Frame Data
public record Frame(Guid Id, byte[] Data, int Width, int Height, PixelFormat Format, DateTime Timestamp);
```

### 4. AI Services

```csharp
// Text Generation
public interface IAiService
{
    Task<AiResponse> GenerateAsync(string prompt, CancellationToken ct);
}

// Vision
public interface IAiVisionService
{
    Task<AiResponse> GenerateAsync(byte[] imageData, string prompt, CancellationToken ct);
}

// Ollama Implementation
public sealed class OllamaService : IAiService
{
    private readonly HttpClient _http;
    private readonly OllamaOptions _options;
    
    public async Task<AiResponse> GenerateAsync(string prompt, CancellationToken ct)
    {
        // POST /api/generate with streaming support
    }
}
```

### 5. Orchestration

```csharp
// DAG Scheduler
public sealed class DagScheduler
{
    public async Task RunAsync(IReadOnlyList<DagJob> jobs, 
        Func<DagJob, CancellationToken, Task<bool>> runJob, CancellationToken ct);
}

// Instance Orchestrator
public sealed class InstanceOrchestrator
{
    public async Task RunInstanceAsync(Instance instance, string pluginsPath, CancellationToken ct);
}

// Event Store
public sealed class EventStore
{
    public void Append(IOrchestrationEvent evt);
    public IReadOnlyCollection<IOrchestrationEvent> GetAll();
}
```

### 5. Device Abstraction

```csharp
// Session Interface
public interface IDeviceSession : IAsyncDisposable
{
    DeviceInfo Info { get; }
    Task<Frame> CaptureAsync(CancellationToken ct);
    Task<DispatchResult> SendInputAsync(InputCommand command, CancellationToken ct);
    Task<HealthCheckResult> CheckHealthAsync(CancellationToken ct);
    Task StartAppAsync(string packageName, CancellationToken ct);
    Task StopAppAsync(string packageName, CancellationToken ct);
}

// ADB Implementation
public sealed class AdbDeviceSession : IDeviceSession
{
    private readonly AdbCommandRunner _runner;
    private readonly string _deviceSerial;
}

// Simulator (for testing)
public sealed class SimulatedDeviceSession : IDeviceSession { }
```

---

## Data Flow

### Instance Lifecycle

```
1. CREATE INSTANCE
   POST /api/v1/instances
   │
   ▼
   InstanceCreated (Domain Event)
   │
   ▼
2. LOAD PLUGIN
   PluginLoader.LoadPluginsAsync("plugins")
   │
   ▼
   PluginLoadedEvent
   │
   ▼
3. START INSTANCE
   InstanceOrchestrator.RunInstanceAsync()
   │
   ├──▶ DagScheduler: Run task DAG
   │       │
   │       ├──▶ Task: Capture Frame
   │       │       │
   │       │       ▼
   │       │   DeviceSession.CaptureAsync()
   │       │       │
   │       │       ▼
   │       │   PerceptionPipeline.ProcessAsync()
   │       │       │
   │       │       ├──▶ Detector 1 (Template Match)
   │       │       ├──▶ Detector 2 (OCR)
   │       │       └──▶ Detector N...
   │       │       │
   │       │       ▼
   │       │   State Machine: Evaluate EntryConditions
   │       │       │
   │       │       ▼
   │       │   StateAction.ExecuteAsync()
   │       │       │
   │       │       ▼
   │       │   DeviceSession.SendInputAsync()
   │       │
   ├──▶ EventStore: Append events
   │
   ▼
4. MONITOR & CONTROL
   Health checks, metrics, dashboard updates
```

### Job Execution Flow

```
Job Created (Pending)
    │
    ▼
Job Scheduler picks up (Priority + ScheduledAt)
    │
    ▼
Job Status → Running
    │
    ▼
Execute Task:
  1. Get Instance Device Session
  2. Run Perception Pipeline
  3. Evaluate State Machine
  4. Execute State Action
  5. Send Input Commands
    │
    ▼
Job Status → Completed / Failed
    │
    ▼
EventStore: JobCompletedEvent
    │
    ▼
Metrics Updated
    │
    ▼
Notification (if configured)
```

### Plugin Hot Reload

```
File Watcher detects .dll change in plugins/
    │
    ▼
PluginLoader.LoadPluginsAsync() (new AssemblyLoadContext)
    │
    ▼
Validate Manifests & Dependencies
    │
    ▼
Swap Plugin References (atomic)
    │
    ▼
EventStore: PluginReloadedEvent
    │
    ▼
Notify Dashboard (SignalR)
```

---

## Deployment Architecture

### Development (Docker Compose)

```
┌─────────────────────────────────────────────────────────────┐
│                     Docker Network                           │
│  ┌────────────┐  ┌──────────┐  ┌────────┐  ┌────────────┐  │
│  │  KKBOT     │  │PostgreSQL│  │ Redis  │  │  Ollama    │  │
│  │  (API+UI)  │  │  (DB)    │  │(Cache) │  │  (AI)      │  │
│  │  :8080     │  │  :5432   │  │ :6379  │  │  :11434    │  │
│  └────────────┘  └──────────┘  └────────┘  └────────────┘  │
│       │               │            │            │            │
│       └───────────────┴────────────┴────────────┘            │
│                           │                                    │
│              ┌────────────▼────────────┐                       │
│              │      Volumes            │                       │
│              │  data, logs, plugins    │                       │
│              └─────────────────────────┘                       │
└─────────────────────────────────────────────────────────────┘
```

### Production (Kubernetes)

```
┌─────────────────────────────────────────────────────────────────┐
│                    Kubernetes Cluster                            │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │                   Namespace: kkbot                       │   │
│  │  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌────────────┐ │   │
│  │  │ KKBOT    │ │ KKBOT    │ │ KKBOT    │ │ Prometheus │ │   │
│  │  │ Deployment (HPA)     │ │ Grafana  │ │ + Alertmgr │ │   │
│  │  └──────────┘ └──────────┘ └──────────┘ └────────────┘ │   │
│  │       │           │           │           │             │   │
│  │  ┌────┴────┐ ┌────┴────┐ ┌────┴────┐ ┌────┴────┐       │   │
│  │  │ Service │ │ Service │ │ Service │ │ Service   │       │   │
│  │  └────┬────┘ └────┬────┘ └────┬────┘ └────┬────┘       │   │
│  │       │           │           │           │             │   │
│  │  ┌────▼───────────▼───────────▼───────────▼────┐        │   │
│  │  │           ConfigMap + Secrets              │        │   │
│  │  └────────────────────────────────────────────┘        │   │
│  │       │           │           │           │             │   │
│  │  ┌────▼────┐ ┌────▼────┐ ┌────▼────┐ ┌────▼────┐       │   │
│  │  │  PVC    │ │  PVC    │ │  PVC    │ │  PVC    │       │   │
│  │  │ (Data)  │ │ (Logs)  │ │(Plugins)│ │(Postgres)│       │   │
│  │  └─────────┘ └─────────┘ └─────────┘ └─────────┘       │   │
│  └─────────────────────────────────────────────────────────┘   │
│                              │                                 │
│  ┌───────────────────────────▼──────────────────────────────┐  │
│  │              External Dependencies                        │  │
│  │  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐  │  │
│  │  │ Ingress  │  │ Vault    │  │ PostgreSQL│  │  Ollama  │  │  │
│  │  │ (NGINX)  │  │ (Secrets)│  │ (Managed) │  │ (GPU)    │  │  │
│  │  └──────────┘  └──────────┘  └──────────┘  └──────────┘  │  │
│  └──────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

### GitOps Flow

```
┌─────────────┐    ┌─────────────┐    ┌─────────────┐    ┌─────────────┐
│   Developer │───▶│   GitHub    │───▶│  GitHub     │───▶│  ArgoCD /   │
│   Pushes    │    │   Repository│    │  Actions    │    │  Flux       │
└─────────────┘    └─────────────┘    └──────┬──────┘    └──────┬──────┘
                                             │                  │
                                    ┌────────┴────────┐         │
                                    ▼                 ▼         ▼
                              ┌──────────┐    ┌──────────┐ ┌──────────┐
                              │ Build &  │    │ Security │ │ Deploy   │
                              │ Test     │    │ Scan     │ │ to K8s   │
                              └──────────┘    └──────────┘ └──────────┘
```

---

## Technology Stack

### Backend

| Category | Technology | Version | Purpose |
|----------|------------|---------|---------|
| Runtime | .NET | 10.0 | Core framework |
| Web Framework | ASP.NET Core | 10.0 | API, Blazor Server |
| ORM | Entity Framework Core | 8.0 | Database access |
| DI | Microsoft.Extensions.DI | 8.0 | Dependency injection |
| Mediation | MediatR | 12.0 | CQRS, decoupling |
| Validation | FluentValidation | 11.0 | Input validation |
| Resilience | Polly | 8.0 | Retry, circuit breaker |
| Logging | Serilog | 2.12 | Structured logging |
| Metrics | Prometheus.AspNetCore | 8.0 | Metrics exposition |
| Tracing | OpenTelemetry | 1.7 | Distributed tracing |

### Computer Vision

| Library | Version | Purpose |
|---------|---------|---------|
| OpenCvSharp4 | 4.8 | OpenCV bindings |
| Tesseract (optional) | 5.x | OCR engine |

### AI

| Service | Model | Purpose |
|---------|-------|---------|
| Ollama | llama3.1:8b | Text generation |
| Ollama | llava:7b | Vision (image + text) |

### Frontend

| Technology | Version | Purpose |
|------------|---------|---------|
| Blazor Server | 10.0 | Interactive UI |
| MudBlazor | 6.19 | Component library |
| SignalR | 10.0 | Real-time updates |

### Infrastructure

| Component | Technology | Version |
|-----------|------------|---------|
| Containerization | Docker | 24+ |
| Orchestration | Kubernetes | 1.28+ |
| Package Manager | Helm | 3.12+ |
| CI/CD | GitHub Actions | - |
| Service Mesh | Istio (optional) | 1.20+ |
| Monitoring | Prometheus + Grafana | 2.48 / 10.2 |
| Logging | Loki + Promtail | 2.9 |
| Tracing | Jaeger / Tempo | 1.50 / 2.3 |
| Secret Management | HashiCorp Vault | 1.15 |

### Database

| Environment | Database | Version |
|-------------|----------|---------|
| Development | SQLite | 3.45 |
| Production | PostgreSQL | 16 |
| Cache | Redis | 7 |

---

## Design Decisions

### 1. Hexagonal Architecture

**Decision**: Use Ports & Adapters (Hexagonal) architecture.

**Rationale**:
- Domain isolation from infrastructure
- Testability without external dependencies
- Easy to swap implementations (SQLite ↔ PostgreSQL, ADB ↔ Simulator)
- Clear separation of concerns

**Trade-offs**:
- More boilerplate (interfaces, adapters)
- Learning curve for team

### 2. Plugin Architecture with AssemblyLoadContext

**Decision**: Isolate plugins in collectible AssemblyLoadContext.

**Rationale**:
- Hot reload without app restart
- Dependency isolation (different plugin versions)
- Security boundary (future: sandbox)
- Unload unused plugins to free memory

**Trade-offs**:
- Complexity in dependency resolution
- Cannot share singleton state across plugins
- Serialization across context boundaries

### 3. MediatR for CQRS

**Decision**: Use MediatR for commands/queries.

**Rationale**:
- Decouples handlers from controllers
- Pipeline behaviors (validation, logging, transactions)
- Easy to add cross-cutting concerns
- Testable handlers

**Trade-offs**:
- Indirection (harder to navigate code)
- Overhead for simple CRUD

### 4. EF Core with SQLite/PostgreSQL

**Decision**: EF Core as ORM, support both SQLite and PostgreSQL.

**Rationale**:
- Code-first migrations
- LINQ queries, type safety
- Easy testing with InMemory/SQLite
- PostgreSQL for production scale

**Trade-offs**:
- Migration complexity for schema changes
- Performance tuning required for high load

### 5. Blazor Server for Dashboard

**Decision**: Blazor Server over Blazor WebAssembly.

**Rationale**:
- Direct access to server services (no API layer needed for UI)
- Smaller download size
- Better for internal tools
- Real-time via SignalR built-in

**Trade-offs**:
- Requires persistent connection
- Server memory per connection
- Latency on high-latency networks

### 6. OpenCV for Computer Vision

**Decision**: OpenCvSharp4 for template matching.

**Rationale**:
- Industry standard, performant
- Multiple match methods
- GPU acceleration support
- Mature .NET bindings

**Trade-offs**:
- Native dependencies (platform-specific)
- Large binary size
- Learning curve

---

## Scalability & Performance

### Horizontal Scaling

| Component | Scaling Strategy |
|-----------|------------------|
| **API/UI** | HPA based on CPU/Memory, max 10 replicas |
| **Job Workers** | Separate deployment, scale by queue depth |
| **Plugin Loader** | Singleton per pod, plugins shared via ReadOnlyMany PVC |
| **Database** | Read replicas, connection pooling (20 max) |
| **Redis** | Cluster mode for cache scaling |
| **Ollama** | GPU node pool, model sharding |

### Performance Targets

| Metric | Target | Measurement |
|--------|--------|-------------|
| API Latency (p95) | < 200ms | `/api/v1/*` |
| Frame Processing | < 50ms | Perception pipeline |
| Job Dispatch | < 100ms | Queue to start |
| Plugin Load | < 5s | 10 plugins |
| Dashboard Update | < 1s | SignalR push |

### Caching Strategy

| Data | Cache | TTL | Invalidation |
|------|-------|-----|--------------|
| Plugin Manifests | Memory | 5 min | File watcher |
| AI Responses | Memory | 30 min | Key-based |
| Instance Config | Memory | 1 min | On update |
| Device Info | Memory | 30 sec | Health check |

### Database Optimization

```sql
-- Key indexes
CREATE INDEX IX_Jobs_InstanceId_Status ON Jobs(InstanceId, Status);
CREATE INDEX IX_Jobs_CreatedAt ON Jobs(CreatedAt DESC);
CREATE INDEX IX_Instances_Status ON Instances(Status);
CREATE INDEX IX_Instances_ModuleId ON Instances(ModuleId);

-- Partitioning (PostgreSQL)
CREATE TABLE jobs_partitioned (LIKE jobs INCLUDING ALL) PARTITION BY RANGE (CreatedAt);
```

---

## Security Architecture

### Defense in Depth

```
┌─────────────────────────────────────────────────────────────┐
│                    Network Perimeter                         │
│  WAF, DDoS Protection, TLS 1.3, Rate Limiting               │
└────────────────────────────┬────────────────────────────────┘
                             │
┌────────────────────────────▼────────────────────────────────┐
│                   Kubernetes Network                          │
│  Network Policies, mTLS (Istio), Egress Control              │
└────────────────────────────┬────────────────────────────────┘
                             │
┌────────────────────────────▼────────────────────────────────┐
│                    Pod Security                               │
│  Non-root, ReadOnlyRootFS, Drop ALL Capabilities             │
│  Seccomp, AppArmor, SELinux                                  │
└────────────────────────────┬────────────────────────────────┘
                             │
┌────────────────────────────▼────────────────────────────────┐
│                  Application Security                         │
│  Input Validation, Output Encoding, CSP, Security Headers    │
│  Secret Management (Vault), Encryption (AES-256-GCM)         │
│  Audit Logging, Rate Limiting, JWT/OAuth2 (planned)          │
└────────────────────────────┬────────────────────────────────┘
                             │
┌────────────────────────────▼────────────────────────────────┐
│                    Data Security                              │
│  Encryption at Rest (DB), Encryption in Transit (TLS)        │
│  Key Rotation, Data Classification, Retention Policies       │
└──────────────────────────────────────────────────────────────┘
```

### Zero Trust Principles

1. **Verify Explicitly** - Every request authenticated & authorized
2. **Least Privilege** - Minimal RBAC, scoped secrets
3. **Assume Breach** - Monitoring, segmentation, encryption

---

## Extensibility Points

### 1. Custom Detectors

```csharp
public interface IGameDetector
{
    Task<DetectorResult> DetectAsync(Frame frame, CancellationToken ct);
}

// Register in plugin
public IReadOnlyCollection<IGameDetector> GetDetectors() 
    => [new MyCustomDetector()];
```

### 2. Custom Tasks

```csharp
public interface IGameTaskDefinition
{
    Task<IStateDefinition> ExecuteAsync(TaskContext context, CancellationToken ct);
}

// Register in plugin
public IReadOnlyCollection<IGameTaskDefinition> GetTasks() 
    => [new MyCustomTask()];
```

### 3. Custom States

```csharp
public interface IStateDefinition
{
    Func<PerceptionContext?, bool> EntryCondition { get; }
    IStateAction? Action { get; }
    IReadOnlyList<string> NextStateIds { get; }
}

// Register in plugin
public IReadOnlyCollection<IStateDefinition> GetStates() 
    => [new MyCustomState()];
```

### 4. Custom AI Providers

```csharp
public interface IAiService
{
    Task<AiResponse> GenerateAsync(string prompt, CancellationToken ct);
}

// Register in DI
services.AddSingleton<IAiService, MyCustomAiProvider>();
```

### 5. Custom Device Drivers

```csharp
public interface IDeviceSession
{
    Task<Frame> CaptureAsync(CancellationToken ct);
    Task<DispatchResult> SendInputAsync(InputCommand command, CancellationToken ct);
}

// Register in DI
services.AddSingleton<IDeviceSessionFactory, MyDeviceFactory>();
```

### 6. Custom Secret Managers

```csharp
public interface ISecretManager
{
    Task<string?> GetSecretAsync(string key, CancellationToken ct);
}

// Register in DI
services.AddSingleton<ISecretManager, AzureKeyVaultSecretManager>();
```

### 7. Pipeline Behaviors (MediatR)

```csharp
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(TRequest request, 
        RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        _logger.LogInformation("Handling {Request}", typeof(TRequest).Name);
        var response = await next();
        _logger.LogInformation("Handled {Request}", typeof(TRequest).Name);
        return response;
    }
}

// Register
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
```

### 8. Health Check Contributions

```csharp
public class PluginHealthCheck : IHealthCheck
{
    private readonly PluginLoader _loader;
    
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct)
    {
        var plugins = await _loader.LoadPluginsAsync("plugins", ct);
        return plugins.Count > 0 
            ? HealthCheckResult.Healthy($"{plugins.Count} plugins loaded")
            : HealthCheckResult.Degraded("No plugins loaded");
    }
}

// Register
builder.Services.AddHealthChecks()
    .AddCheck<PluginHealthCheck>("plugins");
```

---

## Future Evolution

### Planned Enhancements

| Area | Enhancement | Priority |
|------|-------------|----------|
| **Multi-tenancy** | Tenant isolation, per-tenant plugins | High |
| **Event Sourcing** | Full event store, projections, snapshots | Medium |
| **Saga Pattern** | Distributed transactions for complex workflows | Medium |
| **Plugin Marketplace** | Registry, dependency resolution, versions | High |
| **ML Pipeline** | Custom model training, inference pipeline | Low |
| **Mobile App** | React Native companion app | Low |
| **WebAssembly Plugins** | Sandbox plugins in WASM | Medium |

### Technical Debt

- [ ] Replace SQLite with PostgreSQL in all environments
- [ ] Implement proper JWT authentication
- [ ] Add OpenTelemetry auto-instrumentation
- [ ] Migrate to .NET 10 native AOT
- [ ] Implement plugin sandboxing (WASM)
- [ ] Add GraphQL API alongside REST

---

*Document version: 1.0.0 | Last updated: 2024-01-15*