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
10. [Game-Specific Modules](#game-specific-modules)

---

## System Context

KKBOT is a **modular, extensible game automation platform** built with .NET 10. It provides a complete infrastructure for building game automation bots with enterprise-grade features.

### Key Characteristics

| Aspect | Decision |
|--------|----------|
| Platform | .NET 10 (LTS) |
| Architecture | Hexagonal (Ports & Adapters) |
| Plugin Model | AssemblyLoadContext + WASM (Wasmtime) |
| Communication | gRPC + REST + SignalR |
| Data | EF Core (SQLite/PostgreSQL) + EventStore |
| Messaging | Redis Pub/Sub + In-memory |
| Observability | OpenTelemetry + Serilog + Prometheus |
| Security | JWT + RBAC + Capability-based sandbox |

---

## Architectural Style

### Hexagonal Architecture (Ports & Adapters)

KKBOT follows the **Hexagonal Architecture** pattern to ensure separation of concerns and testability.

```
+-------------------------------------------------------------+
|                        KKBOT Platform                       |
+-------------------------------------------------------------+
|  +----------+  +----------+  +----------+  +----------+     |
|  |  API     |  | Dashboard|  |  CLI     |  |  Plugins |  <- Adapters (Driving)
|  |  Layer   |  | (Blazor) |  |  Tool    |  |  Loader  |
|  +----+-----+  +----+-----+  +----+-----+  +----+-----+     |
|       |             |             |             |             |
|       +-------------+-------------+-------------+             |
|                     v                                         |
|  +----------------------------------------------------------+  |
|  |                    Application Layer                      |  |  <- Application Core
|  |  Commands | Queries | Validators | Mediator | Handlers   |  |
|  +----------------------------------------------------------+  |
|                     |                                         |
|  +-------------------+------------------+-------------------+  |
|  v                  v                  v                   v  |
| +--------+    +------------+   +------------+         +------+ |
| |Device  |    |Perception  |   |AI          |         |Orches| |  <- Ports (Driven)
| |Abstraction    |Pipeline    |   |Services    |         |tration| |
| +--------+    +------------+   +------------+         +------+ |
|       |             |                  |                    |  |
|  +----+-----+  +----+-----+     +-------+-------+          |  |
|  | ADB     |  |OpenCV     |     |Ollama       |             |  |  <- Adapters (Driven)
|  | Simulator| |Template   |     |Vision       |             |  |
|  |         |  |Matcher    |     |             |             |  |
|  +---------+  +-----------+     +-------------+             |  |
|       |             |                  |                    |  |
|       +-------------+------------------+--------------------+  |
|                     v                                         |
|  +----------------------------------------------------------+  |
|  |              Persistence (EF Core) + Security             |  |
|  +----------------------------------------------------------+  |
```

### Core Principles

1. **Domain-Centric** - Business logic in Domain layer, no external dependencies
2. **Dependency Inversion** - Core depends on abstractions (Ports), not concretions
3. **Plugin Isolation** - Each plugin runs in isolated AssemblyLoadContext or WASM
4. **Event-Driven** - EventStore for audit trail, projections for read models
5. **Capability-Based Security** - Deny-by-default with explicit capabilities

---

## Core Components

### 1. Domain Layer (KKBOT.Domain)

Pure domain models with no external dependencies.

| Entity | Description |
|--------|-------------|
| Instance | Game automation instance with state machine |
| Device | Physical/virtual device abstraction |
| Module | Game plugin metadata |
| Job | Scheduled automation work |
| TaskRun | Execution tracking |
| Profile | Game configuration |
| Operator | User/operator management |
| Schedule | Recurring job definitions |

### 2. Application Layer (KKBOT.Application)

Use cases and business logic orchestration.

| Pattern | Implementation |
|---------|----------------|
| CQRS | Commands (mutations) + Queries (reads) |
| MediatR | In-process message bus |
| Validators | FluentValidation pipeline behaviors |
| Behaviors | Logging, Validation, Transaction, Retry |

### 3. Orchestration Layer (KKBOT.Orchestration)

Core automation orchestration engine.

| Component | Responsibility |
|-----------|----------------|
| InstanceManager | Instance lifecycle (Create, Start, Stop, Pause, Resume) |
| Scheduler | Job scheduling, queuing, concurrency control |
| DagScheduler | DAG-based task execution with dependencies |
| InstanceOrchestrator | FSM execution from plugin states |
| RecoveryCoordinator | Escalation ladder, auto-recovery |
| EventStore | Event sourcing with snapshots/projections |
| SagaOrchestrator | Compensation-based transactions |

### 4. Plugin System (KKBOT.Plugins.*)

Extensible plugin architecture.

| Component | Purpose |
|-----------|---------|
| AssemblyLoadContext | .NET plugin isolation |
| Wasmtime | WASM sandbox (20 capabilities) |
| PluginLoader | Discovery, validation, hot-reload |
| Manifest | Plugin metadata, permissions, config schema |

### 4. Device Layer (KKBOT.Device.*)

Hardware abstraction layer.

| Adapter | Capabilities |
|---------|--------------|
| ADB | Android devices (screenshot, input, shell, apps) |
| Simulator | Headless testing, scenario DSL v2, frame replay |
| Scrcpy | Low-latency streaming (planned) |

### 5. Perception Pipeline (KKBOT.Perception.*)

Computer vision capabilities.

| Component | Technology |
|-----------|------------|
| Template Matching | OpenCV (TM_CCOEFF_NORMED) |
| OCR | Tesseract / PaddleOCR |
| Pipeline | Parallel detector execution |
| Preprocessing | Grayscale, resize, denoise |

### 6. AI Services (KKBOT.AI.*)

LLM and vision integration.

| Service | Provider | Features |
|---------|----------|----------|
| IAiService | Ollama | Text gen, streaming, function calling |
| IAiVisionService | Ollama | Vision, image analysis |
| IAiPipeline | Local/Cloud | Training, inference, feature store |

### 8. Multi-Agent Coordination (KKBOT.Agent.Coordination)

Distributed agent coordination.

| Component | Technology |
|-----------|------------|
| Agent Registry | Redis (TTL, capability indexing) |
| Message Bus | Redis Pub/Sub + Streams |
| Consensus | Raft-inspired (2/3 majority) |
| Leader Election | Priority-based |
| Distributed Lock | Redlock (Lua scripts) |

---

## Data Flow

### Game Automation Loop

Device -> Perceive -> Decide -> Execute -> Feedback Loop

### Event Sourcing Flow

Command -> Handler -> Events -> EventStore -> Projections -> Read Models
                    |
                    +-> Snapshots (every N events)
                    +-> Upcasters (v1 to v2 migration)

### Multi-Agent Coordination

Agent A -> MessageBus <- Agent B
   |                         |
   +-------> Registry <------+
   |                         |
   +-------> Coordination <--+
          |      |      |
          v      v      v
       Consensus  Lock  Sync

---

## Deployment Architecture

### Local Development

API (Port 5000) + Dashboard (Port 5001) + Plugins (Folder) -> SQLite + Redis

### Production (Kubernetes)

Ingress (NGINX) -> API Pod (x3) / Dashboard Pod / Workers (xN) -> PostgreSQL + Redis Cluster + MinIO

---

## Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Runtime | .NET | 10.0 LTS |
| Web Framework | ASP.NET Core | 10.0 |
| UI | Blazor Server + MudBlazor | 8.x |
| ORM | EF Core | 8.0 |
| Database | SQLite / PostgreSQL | 16 |
| Cache/Message | Redis | 7.0 |
| Serialization | System.Text.Json | 8.0 |
| DI Container | Microsoft.Extensions.DI | 8.0 |
| Logging | Serilog | 2.12 |
| Tracing | OpenTelemetry | 1.7 |
| Metrics | Prometheus | 2.45 |
| Templating | Razor | 10.0 |
| gRPC | Grpc.AspNetCore | 2.57 |
| WASM | Wasmtime | 18.0 |
| Computer Vision | OpenCV | 4.8 |
| AI | Ollama / Semantic Kernel | Latest |

---

## Design Decisions

### ADR-001: Use .NET 10 LTS
**Status**: Accepted
**Rationale**: Long-term support, latest language features, cross-platform

### ADR-002: Hexagonal Architecture
**Status**: Accepted
**Rationale**: Separation of concerns, testability, plugin extensibility

### ADR-003: FSM for Workflow
**Status**: Accepted
**Rationale**: Deterministic state transitions, clear recovery paths

### ADR-004: DI via Microsoft.Extensions.Hosting
**Status**: Accepted
**Rationale**: Standard, works with all project types

### ADR-005: AssemblyLoadContext for Plugins
**Status**: Accepted
**Rationale**: Isolation without process overhead

### ADR-006: WASM for Untrusted Plugins
**Status**: Accepted
**Rationale**: True memory isolation, capability-based security

### ADR-007: Event Sourcing for Audit
**Status**: Accepted
**Rationale**: Complete audit trail, temporal queries, replay capability

### ADR-008: Redis for Coordination
**Status**: Accepted
**Rationale**: Pub/Sub, distributed locks, distributed cache

---

## Scalability & Performance

### Horizontal Scaling

| Component | Strategy |
|-----------|----------|
| API | Stateless, HPA (CPU 70%) |
| Workers | Queue-based, N consumers |
| Dashboard | Sticky sessions or stateless |
| Redis | Cluster mode |
| PostgreSQL | Read replicas + PgBouncer |

### Performance Optimizations

| Optimization | Implementation |
|--------------|----------------|
| L2 Cache | L1 (Memory) + L2 (Redis) |
| Connection Pooling | HTTP, Redis, DB, gRPC |
| Compiled Queries | EF Core compiled queries |
| Object Pooling | Memory-efficient allocations |
| Async/Await | Full async pipeline |
| Streaming | IAsyncEnumerable for AI responses |

### Resource Limits (Per Instance)

| Resource | Default Limit |
|----------|---------------|
| CPU | 2 cores |
| Memory | 2 GB |
| GPU | Optional |
| FPS | Configurable (5-60) |

---

## Security Architecture

### Authentication & Authorization

| Layer | Mechanism |
|-------|-----------|
| Authentication | JWT (RS256), Refresh Tokens |
| Authorization | RBAC + Resource-based (PermissionAuthorizationHandler) |
| API Security | Rate Limiting, CORS, Security Headers |
| Secrets | AES-256, Key Rotation, Vault-ready |

### Plugin Security

| Layer | Protection |
|-------|------------|
| Sandbox | WASM (Wasmtime) with fuel metering |
| Capabilities | 20 granular permissions |
| Signing | Ed25519 signatures |
| Isolation | AssemblyLoadContext / WASM memory isolation |

### Data Protection

| Data | Protection |
|------|------------|
| Secrets | AES-256-GCM, Key Rotation |
| PII | Encryption at rest, Minimization |
| Audit | Immutable, Append-only |
| Transport | TLS 1.3, mTLS for gRPC |

---

## Game-Specific Modules

### Module 1: Account & Auto-Login
- Auto login/relogin with credential injection
- Server/Channel/Character selection
- Security screens (PIN/OTP/Captcha)
- Crash recovery with auto-relogin

### Module 3: Resource Optimizer
- FPS Limiter (5-10 background)
- Render Stripping (3D, particles, shadows)
- RAM Cleaner (periodic, threshold-based)
- Window Grid Layout (tile, minimize, tray)

### Module 7: Anti-Detection
- Humanizer (delays, jitter, Bezier curves)
- Telegram Alerts (Death, VIP, Disconnect)
- Remote Control (Telegram Bot)

### Planned Modules
- Module 2: Network & Proxy Binding
- Module 4: Logistics & Survival
- Module 5: Combat & Quest Engine
- Module 6: Economy & Trading

---

## Related Documents

- [Deployment Guide](DEPLOYMENT.md)
- [Configuration Reference](configuration.md)
- [Plugin Development](plugin-development.md)
- [GameAgent Framework](GAME_AGENT_FRAMEWORK_SPEC.md)
- [Multi-Agent Coordination](MULTI_AGENT_COORDINATION_FRAMEWORK.md)
- [API Reference](api-reference.md)

---

*Last Updated: 2025-01-15*
*Version: 24.1.0*