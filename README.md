# KKBOT - Game Automation Platform

![KKBOT Logo](docs/assets/logo.png)

> **KKBOT** is a modular, extensible game automation platform built with .NET 10, featuring a plugin architecture, computer vision perception, AI integration, and a modern Blazor dashboard.

[![Build Status](https://github.com/your-org/kkbot/workflows/CI/CD/badge.svg)](https://github.com/your-org/kkbot/actions)
[![NuGet Version](https://img.shields.io/nuget/v/KKBOT.Core.svg)](https://www.nuget.org/packages/KKBOT.Core)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)

## 🎯 Features

- **🔌 Plugin Architecture** - Hot-loadable game modules with isolated AssemblyLoadContext
- **👁️ Computer Vision** - OpenCV-based template matching, OCR pipeline
- **🤖 AI Integration** - Ollama LLM client with vision support, streaming responses
- **🎮 Device Abstraction** - ADB for Android, simulator for testing
- **📊 Orchestration** - DAG scheduler, dynamic FSM from plugin states, event sourcing
- **💾 Persistence** - EF Core with SQLite/PostgreSQL, repository pattern
- **🔐 Security** - Secret management (Vault-ready), AES encryption, audit logging
- **📈 Observability** - Serilog, health checks, Prometheus metrics, Grafana dashboards
- **🖥️ Modern Dashboard** - Blazor Server + MudBlazor, real-time monitoring
- **🤝 Real-time Collaboration** - Multi-user instance control, presence, recordings
- **🤖 AI/ML Pipeline** - Model registry, training, inference, feature store, monitoring
- **⚡ GPU/Edge Optimization** - TensorRT, ONNX quantization, edge deployment
- **🔐 Enterprise Security** - SSO, audit, GDPR/SOC2, advanced RBAC, data governance
- **☸️ Cloud Native** - Docker, Helm chart, GitHub Actions CI/CD, Kubernetes ready

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        KKBOT Platform                           │
├─────────────────────────────────────────────────────────────────┤
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐        │
│  │  API     │  │ Dashboard│  │  CLI     │  │  Plugins │        │
│  │  Layer   │  │  (Blazor)│  │  Tool    │  │  Loader  │        │
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
└─────────────────────────────────────────────────────────────────┘
```

## 🚀 Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker](https://www.docker.com/) (optional, for containerized deployment)
- [Ollama](https://ollama.ai/) (optional, for AI features)

### Development Setup

```bash
# Clone repository
git clone https://github.com/your-org/kkbot.git
cd kkbot

# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Run tests
dotnet test

# Start dashboard (Blazor Server)
dotnet run --project src/KKBOT.UI.Dashboard

# Start API
dotnet run --project src/KKBOT.API
```

### Docker Development

```bash
# Start with hot reload
docker-compose up -d

# View logs
docker-compose logs -f kkbot
```

### Production Deployment

```bash
# Set required secrets
export ENCRYPTION_PASSPHRASE="your-secure-passphrase"
export ENCRYPTION_SALT="your-secure-salt"
export POSTGRES_PASSWORD="secure-db-password"

# Deploy with Docker Compose
docker-compose --profile postgres --profile redis up -d

# Or deploy to Kubernetes
helm install kkbot ./helm/kkbot -f values-prod.yaml -n kkbot --create-namespace
```

## 📚 Documentation

| Document | Description |
|----------|-------------|
| [Architecture Overview](docs/architecture.md) | System design and component interactions |
| [API Reference](docs/api-reference.md) | Complete REST API documentation |
| [Plugin Development](docs/plugin-development.md) | Guide to creating game modules |
| [Deployment Guide](DEPLOYMENT.md) | Docker, Kubernetes, CI/CD instructions |
| [Configuration](docs/configuration.md) | All configuration options |
| [Security](docs/security.md) | Secret management, encryption, audit |
| [Monitoring](docs/monitoring.md) | Metrics, logging, alerting setup |
| [Troubleshooting](docs/troubleshooting.md) | Common issues and solutions |

## 🔌 Creating a Plugin

### 1. Create a Class Library

```bash
dotnet new classlib -n MyGamePlugin
cd MyGamePlugin
dotnet add reference ../src/KKBOT.Plugins.Abstractions/KKBOT.Plugins.Abstractions.csproj
dotnet add reference ../src/KKBOT.Perception.Abstractions/KKBOT.Perception.Abstractions.csproj
```

### 2. Implement IGameModule

```csharp
public sealed class MyGameModule : IGameModule
{
    public string Id => "my-game";
    public string Version => "1.0.0";

    public ModuleManifest GetManifest() => new()
    {
        Id = Id,
        Name = "My Game Bot",
        Version = Version,
        Author = "Your Name",
        Description = "Automation for My Game",
        SupportedResolutions = [new Resolution(1920, 1080)],
        RequiredPermissions = ["DEVICE_READ", "INPUT_WRITE"],
        Assets = ["templates/health_bar.png"]
    };

    public IReadOnlyCollection<IGameDetector> GetDetectors() => 
        [new HealthBarDetector(), new ManaBarDetector()];

    public IReadOnlyCollection<IGameTaskDefinition> GetTasks() => 
        [new AutoAttackTask(), new HealTask()];

    public IReadOnlyCollection<IStateDefinition> GetStates() => 
        [new IdleState(), new CombatState(), new HealingState()];

    public async Task<ModuleValidationResult> ValidateAsync(ModuleContext ctx, CancellationToken ct)
    {
        // Validate assets, config, permissions
        return new ModuleValidationResult(true, []);
    }
}
```

### 3. Build and Deploy

```bash
dotnet build -c Release
# Copy output DLL to plugins/ directory
```

See [Plugin Development Guide](docs/plugin-development.md) for complete details.

## 📦 Project Structure

```
KKBOT/
├── src/
│   ├── KKBOT.Domain              # Domain models, enums, events
│   ├── KKBOT.Application         # MediatR commands/queries, validators
│   ├── KKBOT.Contracts           # Shared DTOs, interfaces
│   ├── KKBOT.Device.Abstractions # Device interfaces
│   ├── KKBOT.Device.Adb          # ADB implementation
│   ├── KKBOT.Device.Simulator    # Simulator for testing
│   ├── KKBOT.Perception.Abstractions # Perception interfaces
│   ├── KKBOT.Perception.OpenCv   # OpenCV implementation
│   ├── KKBOT.Perception.Ocr      # OCR engine
│   ├── KKBOT.AI.Abstractions     # AI service interfaces
│   ├── KKBOT.AI.Ollama           # Ollama implementation
│   ├── KKBOT.AI.Pipeline         # AI/ML Pipeline
│   ├── KKBOT.Plugins.Abstractions # Plugin contracts
│   ├── KKBOT.Plugins.Loader      # Plugin loading (AssemblyLoadContext)
│   ├── KKBOT.Plugin.Sample       # Sample plugin
│   ├── KKBOT.Plugin.SampleGame   # Complete sample game plugin
│   ├── KKBOT.Plugins.Loader      # Plugin loading (AssemblyLoadContext)
│   ├── KKBOT.Plugins.Marketplace # Plugin marketplace backend
│   ├── KKBOT.Orchestration       # Scheduler, FSM, EventStore
│   ├── KKBOT.Persistence.Abstractions # Repository interfaces
│   ├── KKBOT.Persistence.EfCore  # EF Core implementation
│   ├── KKBOT.Security.Abstractions # Security interfaces
│   ├── KKBOT.Security            # Security implementations
│   ├── KKBOT.Observability       # Telemetry, health checks
│   ├── KKBOT.API                 # REST API (Swagger/OpenAPI)
│   ├── KKBOT.UI.Dashboard        # Blazor Server dashboard
│   ├── KKBOT.AI.Pipeline         # AI/ML Pipeline
│   ├── KKBOT.AI.Optimization     # GPU/Edge Optimization
│   ├── KKBOT.Analytics           # Advanced Analytics & BI
│   ├── KKBOT.Collaboration       # Real-time Collaboration
│   ├── KKBOT.Enterprise          # Enterprise Features
│   ├── KKBOT.AI.Optimization     # GPU/Edge Optimization
│   ├── KKBOT.Collaboration       # Real-time Collaboration
│   ├── KKBOT.AI.Pipeline         # AI/ML Pipeline
│   ├── KKBOT.Observability       # Telemetry, health checks
│   ├── KKBOT.Security.Abstractions # Security interfaces
│   ├── KKBOT.Security            # Security implementations
│   ├── KKBOT.Plugins.Marketplace # Plugin marketplace backend
│   ├── KKBOT.Plugin.Sample       # Sample plugin
│   ├── KKBOT.Plugin.SampleGame   # Complete sample game plugin
│   ├── KKBOT.Orchestration       # Scheduler, FSM, EventStore
│   ├── KKBOT.Persistence.Abstractions # Repository interfaces
│   ├── KKBOT.Persistence.EfCore  # EF Core implementation
│   ├── KKBOT.Security.Abstractions # Security interfaces
│   ├── KKBOT.Security            # Security implementations
│   ├── KKBOT.Observability       # Telemetry, health checks
│   ├── KKBOT.API                 # REST API (Swagger/OpenAPI)
│   └── KKBOT.UI.Dashboard        # Blazor Server dashboard
├── samples/
│   └── KKBOT.Plugin.SampleGame   # Complete sample game plugin
├── tests/                        # Unit & integration tests
├── helm/kkbot/                   # Helm chart for Kubernetes
├── monitoring/                   # Prometheus/Grafana configs
├── docs/                         # Documentation
├── mobile/                       # React Native mobile app
├── docs/                         # Documentation
├── Dockerfile
├── docker-compose.yml
├── docker-compose.override.yml
├── .github/workflows/ci.yml      # CI/CD pipeline
└── README.md
```

## 🧪 Testing

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test project
dotnet test tests/KKBOT.Perception.Tests
dotnet test tests/KKBOT.Plugin.Loader.Tests
dotnet test tests/KKBOT.Orchestration.Tests
dotnet test tests/KKBOT.Persistence.Tests
dotnet test tests/KKBOT.Security.Tests
dotnet test tests/KKBOT.AI.Pipeline.Tests
dotnet test tests/KKBOT.Plugin.Marketplace.Tests
dotnet test tests/KKBOT.Orchestration.Tests
dotnet test tests/KKBOT.Orchestration.Advanced.Tests
dotnet test tests/KKBOT.Persistence.Tests
dotnet test tests/KKBOT.Security.Tests
dotnet test tests/KKBOT.Plugin.Marketplace.Tests
dotnet test tests/KKBOT.Identity.Tests
dotnet test tests/KKBOT.AI.Pipeline.Tests
dotnet test tests/KKBOT.Collaboration.Tests
dotnet test tests/KKBOT.Marketplace.Server.Tests
dotnet test tests/KKBOT.VisualScripting.Tests
dotnet test tests/KKBOT.Plugin.Marketplace.Tests
dotnet test tests/KKBOT.Identity.Tests
dotnet test tests/KKBOT.AI.Pipeline.Tests
dotnet test tests/KKBOT.Collaboration.Tests
dotnet test tests/KKBOT.Marketplace.Server.Tests
dotnet test tests/KKBOT.AI.Optimization.Tests
```

## 📊 Monitoring

### Health Checks
- `GET /health` - Liveness/readiness probe

### Metrics (Prometheus)
- `GET /metrics` - Application metrics

### Key Metrics
| Metric | Description |
|--------|-------------|
| `kkbot_instances_total` | Total instances |
| `kkbot_instances_status` | Instances by status |
| `kkbot_jobs_total` | Total jobs |
| `kkbot_jobs_status` | Jobs by status |
| `kkbot_job_duration_seconds` | Job execution time |
| `kkbot_plugin_count` | Loaded plugins |

### Grafana Dashboards
Import dashboards from `monitoring/grafana/dashboards/`

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit changes (`git commit -m 'Add amazing feature'`)
4. Push to branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Development Guidelines
- Follow [.NET coding conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Write unit tests for new features
- Update documentation for API changes
- Ensure all tests pass before submitting PR

## 📄 License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments
- [OpenCvSharp4](https://github.com/shimat/OpenCvSharp) - OpenCV bindings for .NET
- [MudBlazor](https://mudblazor.com/) - Blazor component library
- [Serilog](https://serilog.net/) - Structured logging
- [MediatR](https://github.com/jbogard/MediatR) - Mediator pattern
- [FluentValidation](https://fluentvalidation.net/) - Validation library
- [Polly](https://github.com/App-vNext/Polly) - Resilience patterns

## 📞 Support
- **Issues**: [GitHub Issues](https://github.com/your-org/kkbot/issues)
- **Discussions**: [GitHub Discussions](https://github.com/your-org/kkbot/discussions)
- **Email**: support@kkbot.example.com

---

<div align="center">
  <strong>Made with ❤️ by the KKBOT Team</strong>
</div>