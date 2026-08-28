# Changelog

All notable changes to KKBOT Framework will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [24.1.0] - 2025-01-15

### Added
- Complete game automation platform with 7 modules
- Module 1: Account & Auto-Login (auto login, crash recovery, 2FA)
- Module 2: Network & Proxy (proxy binding, Linux namespaces, WebRTC leak prevention)
- Module 3: Resource Optimizer (FPS limiter, render stripping, RAM cleaner, window grid)
- Module 4: Logistics & Survival (auto heal/buff, refill/repair, smart inventory, stuck detection)
- Module 5: Combat & Quest (skill combo, targeting, questing, party, anti-KS)
- Module 6: Economy & Trading (auto vending, master accumulator, market monitor, trading)
- Module 7: Anti-Detection (humanizer, Telegram bot, remote control)
- ML Platform (8 projects): Model Server, Distillation, Feature Store, A/B Testing, Online Learning
- Sim2Real Pipeline (7 projects): Domain Randomization, Adaptation, Reward Modeling, Deployment, Evaluation
- Multi-Agent Coordination: Redis Registry, MessageBus, Raft Consensus, Leader Election, Redlock
- Plugin System: WASM Sandbox (Wasmtime), AssemblyLoadContext, 20 capabilities, Rust SDK
- Visual Scripting: 18 node types, drag-drop editor, live execution
- GameAgent Framework: LLM Planner, Tool Executor, Memory Systems, Behavioral Cloning
- Security: JWT/RBAC, Capability Sandbox, Ed25519 Plugin Signing, AES-256 Encryption
- Observability: OpenTelemetry, Prometheus (50+ metrics), Grafana, Jaeger
- Developer Tools: CLI (kkbot), VS Code Extension, Mobile Dashboard (React Native), PWA Dashboard
- Local Dev Environment: Bash/PS scripts with Docker Compose
- Time-Travel Debugger: State snapshots, replay system, export/import
- License Server: Activation, validation, offline grace, seat management
- Admin Dashboard: React/TypeScript, instance/plugin/scenario management

### Security
- Threat Model (STRIDE methodology)
- SBOM Generation (CycloneDX)
- Rate Limiting (API + Ollama AI endpoints)
- DPAPI/Key Vault Secret Management
- Audit Log with PII Redaction
- Circuit Breaker for external calls
- Recovery Ladder (7 levels)
- Code Signing pipeline
- InnoSetup Installer with silent mode

### Documentation
- 19 comprehensive markdown documents
- Architecture guides, deployment, monitoring, security
- Plugin development guide
- Multi-agent coordination framework spec
- GameAgent framework specification

### Infrastructure
- Docker/Kubernetes ready (Helm charts)
- CI/CD pipelines (GitHub Actions)
- Local dev environment (Docker Compose)
- PostgreSQL + Redis + RabbitMQ

## [23.12.0] - 2024-12-01

### Added
- Core platform architecture (hexagonal, CQRS, Event Sourcing)
- Plugin architecture (AssemblyLoadContext + WASM)
- Device abstraction (ADB + Simulator)
- Perception pipeline (OpenCV + Tesseract)
- AI integration (Ollama text/vision)
- Orchestration engine (DAG, FSM, Saga, EventStore)
- Visual scenario editor (Blazor Canvas)

## [23.6.0] - 2024-06-15

### Added
- Initial project structure
- Domain models and contracts
- Basic plugin system
- Device abstraction layer

---

## Release Notes Template

### [VERSION] - DATE

#### Added
- Feature description

#### Changed
- Change description

#### Deprecated
- Deprecation description

#### Removed
- Removal description

#### Fixed
- Bug fix description

#### Security
- Security fix description

---

## Migration Guides

### 23.x to 24.1.0
See [MIGRATION_GUIDE_24_1_0.md](docs/MIGRATION_GUIDE_24_1_0.md)

### 23.x to 23.12.0
See [MIGRATION_GUIDE_23_12_0.md](docs/MIGRATION_GUIDE_23_12_0.md)
