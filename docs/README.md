# KKBOT - Game Automation Platform

![KKBOT Logo](docs/assets/logo.png)

> **KKBOT** is a modular, extensible game automation platform built with .NET 10, featuring a plugin architecture, computer vision perception, AI integration, and a modern Blazor dashboard.

[![Build Status](https://github.com/your-org/kkbot/workflows/CI/CD/badge.svg)](https://github.com/your-org/kkbot/actions)
[![NuGet Version](https://img.shields.io/nuget/v/KKBOT.Core.svg)](https://www.nuget.org/packages/KKBOT.Core)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)

## 🎯 Features

### Core Platform
- **🔌 Plugin Architecture** - Hot-loadable game modules with isolated AssemblyLoadContext + WASM (Wasmtime) sandbox
- **👁️ Computer Vision** - OpenCV-based template matching, OCR pipeline, perception pipeline
- **🤖 AI Integration** - Ollama LLM client with vision support, streaming responses, AI Pipeline (training/inference)
- **🎮 Device Abstraction** - ADB for Android, Simulator with Scenario DSL v2 for testing
- **📊 Orchestration** - DAG Scheduler, FSM from plugin states, Event Sourcing, Saga pattern, Recovery Coordinator
- **💾 Persistence** - EF Core with SQLite/PostgreSQL, Repository pattern, EventStore with snapshots/projections
- **🔐 Security** - JWT/RBAC, AES-256 encryption, Audit logging, Capability-based WASM sandbox
- **📈 Observability** - OpenTelemetry (Jaeger/OTLP), Serilog, Prometheus metrics, AlertManager, Health checks
- **🖥️ Modern Dashboard** - Blazor Server + MudBlazor, Real-time monitoring, Visual Scripting Editor
- **🤝 Collaboration** - Multi-user instance control, Presence, Recordings, Screen sharing
- **🤖 Multi-Agent Coordination** - Agent Registry, MessageBus, Consensus, Leader Election, Distributed Locks
- **🧠 GameAgent Framework** - LLM-powered agents with planning, tool use, memory, learning

### Game-Specific Modules
- **🔑 Module 1: Account & Auto-Login** - Auto login/relogin, Server/Channel/Character selection, Security screens (PIN/OTP/Captcha), Crash recovery
- **🛡️ Module 7: Anti-Detection & Alerts** - Humanizer (delays, jitter, Bezier curves), Telegram Bot alerts, Remote control
- **⚡ Module 3: Resource Optimizer** - FPS Limiter (5-10 background), Render Stripping, RAM Cleaner, Window Grid Layout
- **🌐 Module 2: Network & Proxy** (planned) - Per-instance proxy binding, Isolation, Auto-rotation
- **🏥 Module 4: Logistics & Survival** (planned) - Auto Heal/Buff, Auto Refill/Repair, Smart Inventory
- **⚔️ Module 5: Combat & Quest** (planned) - Skill Combo Engine, Anti-KS, Auto Quest, Party Controller
- **💰 Module 6: Economy & Trading** (planned) - Auto Vending, Master Accumulator

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        KKBOT Platform                           │
├─────────────────────────────────────────────────────────────────┤
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐        │
│  │  API     │  │ Dashboard│  │  CLI     │  │  Plugins │        │
│  │  Layer   │  │ (Blazor) │  │  Tool    │  │  Loader  │        │
│  └────┬─────┘  └────┬─────┘  └────┬─────┘  └────┬─────┘        │
│       │             │             │             │               │
│       └─────────────┼─────────────┼─────────────┘               │
│                     ▼                                           │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │                    Application Layer                      │  │
│  │  Commands │ Queries │ Validators │ Mediator │ Handlers   │  │
│  └──────────────────────────────────────────────────────────┘  │
│                     │                                           │
│  ┌──────────────────┼──────────────────┬────────────────────┐  │
│  ▼                  ▼                  ▼                      ▼  │
│ ┌────────┐    ┌────────────┐   ┌────────────┐         ┌──────┐ │
│ │Device  │    │Perception  │   │AI          │         │Orches│ │
│ │Abstraction    │Pipeline    │   │Services    │         │tration│ │
│ └────────┘    └────────────┘   └────────────┘         └──────┘ │
│       │             │                  │                      │  │
│  ┌────┴────┐  ┌─────┴─────┐     ┌──────┴──────┐              │  │
│  │ ADB     │  │OpenCV     │     │Ollama       │              │  │
│  │ Simulator│ │Template   │     │Vision       │              │  │
│  │         │  │Matcher    │     │             │              │  │
│  └─────────┘  └───────────┘     └─────────────┘              │  │
│       │             │                  │                      │  │
│       └─────────────┴──────────────────┘                      │  │
│                     ▼                                           │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │              Persistence (EF Core) + Security             │  │
│  └──────────────────────────────────────────────────────────┘  │
```

## 🚀 Quick Start

### Prerequisites
- .NET 10 SDK
- Docker 24+ (for containerized deployment)
- Redis 7+
- Ollama (for AI features)

### Local Development
```bash
# Clone
git clone https://github.com/your-org/kkbot.git
cd kkbot

# Restore & Build
dotnet restore
dotnet build

# Run API
dotnet run --project src/KKBOT.API

# Run Dashboard (separate terminal)
dotnet run --project src/KKBOT.UI.Dashboard
```

### Docker Deployment
```bash
# Development
docker-compose up -d

# Production with monitoring
docker-compose --profile postgres --profile redis --profile monitoring up -d
```

### Kubernetes (Helm)
```bash
helm install kkbot ./helm/kkbot -n kkbot --create-namespace -f values-prod.yaml
```

## 📁 Project Structure

```
src/
├── KKBOT.API/                    # REST API + SignalR hubs
├── KKBOT.UI.Dashboard/           # Blazor Server Dashboard
├── KKBOT.Domain/                 # Domain models & entities
├── KKBOT.Application/            # Commands, Queries, Handlers
├── KKBOT.Orchestration/          # Scheduler, FSM, Saga, EventStore
├── KKBOT.Device.*/               # ADB, Simulator, DSL v2
├── KKBOT.Perception.*/           # OpenCV, OCR, Pipeline
├── KKBOT.AI.*/                   # Ollama, AI Pipeline
├── KKBOT.Plugins.*/              # Plugin system, WASM sandbox
├── KKBOT.Agent.*/                # GameAgent, Coordination
├── KKBOT.Plugin.AccountLogin/    # Module 1: Auto-Login
├── KKBOT.Plugin.AntiDetection/   # Module 7: Anti-Detection
├── KKBOT.Plugin.ResourceOptimizer/ # Module 3: Resource Optimizer
└── tests/                        # Integration & Unit tests
```

## 📖 Documentation

| Document | Description |
|----------|-------------|
| [Architecture](docs/ARCHITECTURE.md) | System architecture & design decisions |
| [Deployment](docs/DEPLOYMENT.md) | Docker, Kubernetes, Local deployment |
| [Configuration](docs/configuration.md) | Configuration reference |
| [Plugin Development](docs/plugin-development.md) | Creating game plugins |
| [GameAgent Framework](docs/GAME_AGENT_FRAMEWORK_SPEC.md) | AI Agent specification |
| [Multi-Agent Coordination](docs/MULTI_AGENT_COORDINATION_FRAMEWORK.md) | Agent coordination system |
| [GameAgent Spec](docs/GAME_AGENT_FRAMEWORK_SPEC.md) | AI Agent technical spec |
| [API Reference](docs/api-reference.md) | REST API documentation |

## 🔧 Configuration

Key settings in `appsettings.json`:
```json
{
  "ConnectionStrings": { "KkbotDb": "DataSource=kkbot.db" },
  "Security": { "EncryptionPassphrase": "change-me", "EncryptionSalt": "change-me" },
  "OpenTelemetry": { "EnableJaeger": true, "EnablePrometheus": true },
  "AI": { "Ollama": { "BaseUrl": "http://localhost:11434", "Model": "llama3.1:8b" } },
  "Plugins": { "Path": "plugins", "AutoReload": true }
}
```

## 🤝 Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## 📄 License

MIT License - see [LICENSE](LICENSE) for details.

## 🙏 Acknowledgments

- OpenCV, Wasmtime, Semantic Kernel, MudBlazor, and all open-source dependencies.

---

**Status**: Production-ready infrastructure with 3/7 game-specific modules implemented. See [CURRENT_STATE.md](CURRENT_STATE.md) for details.