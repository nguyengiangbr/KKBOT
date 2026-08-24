# Configuration Reference

Complete configuration reference for KKBOT.

## AppSettings Structure

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Information",
      "KKBOT": "Debug"
    }
  },
  "Serilog": {
    "MinimumLevel": "Information",
    "LevelOverrides": {
      "Microsoft": "Warning",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning",
      "System.Net.Http": "Warning"
    },
    "EnableConsole": true,
    "EnableFile": true,
    "FilePath": "logs/kkbot-.log",
    "RollingInterval": "Day",
    "RetainedFileCountLimit": 31,
    "EnableOpenTelemetry": false,
    "OtlpEndpoint": "http://localhost:4317",
    "UseJsonFormatter": true
  },
  "OpenTelemetry": {
    "ServiceName": "KKBOT",
    "ServiceVersion": "1.0.0",
    "Environment": "development",
    "EnableJaeger": true,
    "JaegerHost": "localhost",
    "JaegerPort": 6831,
    "EnableOtlp": false,
    "OtlpEndpoint": "http://localhost:4317",
    "EnablePrometheus": true,
    "PrometheusPort": 9464,
    "EnableConsoleExporter": false,
    "SamplingRatio": 1.0,
    "ResourceAttributes": {}
  },
  "ConnectionStrings": {
    "KkbotDb": "DataSource=kkbot.db;Cache=Shared;Foreign Keys=True"
  },
  "Security": {
    "SecretsPath": "secrets.json",
    "EncryptionPassphrase": "CHANGE_ME_IN_PRODUCTION",
    "EncryptionSalt": "CHANGE_ME_IN_PRODUCTION",
    "TokenExpirationMinutes": 60,
    "RefreshTokenExpirationDays": 7,
    "PasswordHashIterations": 100000,
    "ApiKeys": {
      "Enabled": false,
      "HeaderName": "X-API-Key"
    }
  },
  "Plugins": {
    "Path": "plugins",
    "AutoReload": true,
    "ReloadIntervalMinutes": 5,
    "AllowedAssemblies": [],
    "BlockedAssemblies": [],
    "SandboxEnabled": false,
    "LoadTimeoutSeconds": 30
  },
  "Device": {
    "Adb": {
      "Path": "adb",
      "ServerPort": 5037,
      "ConnectTimeoutSeconds": 30,
      "CommandTimeoutSeconds": 10,
      "RetryCount": 3,
      "RetryDelayMs": 1000
    },
    "Simulator": {
      "Enabled": true,
      "FrameRate": 30,
      "DefaultResolution": { "Width": 1920, "Height": 1080 }
    }
  },
  "Perception": {
    "OpenCv": {
      "TemplateThreshold": 0.85,
      "MatchMethod": "CcoeffNormed",
      "MaxTemplates": 100,
      "Preprocessing": {
        "Enabled": true,
        "Grayscale": true,
        "Resize": { "Width": 0, "Height": 0 },
        "Denoise": false
      }
    },
    "Ocr": {
      "Engine": "Tesseract",
      "Language": "eng",
      "CharacterWhitelist": "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz",
      "Dpi": 300,
      "PageSegMode": 7
    },
    "Pipeline": {
      "ParallelDetectors": true,
      "MaxConcurrency": 4,
      "FrameSkip": 0,
      "CacheTemplates": true
    }
  },
  "AI": {
    "Ollama": {
      "BaseUrl": "http://localhost:11434",
      "Model": "llama3.1:8b",
      "VisionModel": "llava:7b",
      "DefaultParams": {
        "Temperature": 0.7,
        "MaxTokens": 2048,
        "TopP": 0.9,
        "TopK": 40,
        "RepeatPenalty": 1.1,
        "Seed": -1,
        "Stream": false
      },
      "TimeoutSeconds": 120,
      "KeepAliveMinutes": 10,
      "EnableCaching": true,
      "CacheTtlMinutes": 30
    },
    "Fallback": {
      "Enabled": false,
      "Provider": "OpenAI",
      "ApiKey": "OPENAI_API_KEY_PLACEHOLDER",
      "Model": "gpt-4-turbo"
    }
  },
  "Orchestration": {
    "Scheduler": {
      "MaxConcurrentJobs": 10,
      "DefaultJobTimeoutMinutes": 30,
      "CleanupIntervalMinutes": 60,
      "MaxJobHistory": 10000
    },
    "InstanceManager": {
      "MaxInstances": 20,
      "ResourceCheckIntervalSeconds": 30
    },
    "Recovery": {
      "MaxRetriesPerLevel": 3,
      "EscalationDelaySeconds": 30
    }
  },
  "Coordination": {
    "Registry": {
      "AgentTtlMinutes": 10,
      "HeartbeatTtlMinutes": 2
    },
    "MessageBus": {
      "EnableTracing": true,
      "DefaultTimeoutSeconds": 30
    },
    "Lock": {
      "DefaultTtlSeconds": 30,
      "MaxRetryAttempts": 3
    },
    "Service": {
      "DefaultTaskTimeout": "00:05:00",
      "MaxConcurrentCoordination": 10
    },
    "Strategy": {
      "DeadlineWarningThreshold": "00:05:00",
      "OverloadThreshold": 0.8
    }
  },
  "GameAgent": {
    "DefaultPlanner": "llm",
    "DefaultExecutor": "standard",
    "DefaultMemory": "efcore",
    "DefaultLearner": "bc",
    "Planner": {
      "Model": "llama3.1:8b",
      "Temperature": 0.3,
      "MaxTokens": 4096,
      "SystemPromptTemplate": "prompts/planner.md",
      "MaxPlanSteps": 20,
      "ReplanThreshold": 3
    },
    "Executor": {
      "MaxConcurrentTools": 3,
      "DefaultTimeout": "00:05:00",
      "RetryPolicy": {
        "MaxRetries": 2,
        "BaseDelayMs": 1000
      }
    },
    "Memory": {
      "EpisodicRetentionDays": 30,
      "SemanticRetentionDays": 365,
      "WorkingMemoryTtlMinutes": 60,
      "VectorDimensions": 768,
      "SimilarityThreshold": 0.75
    },
    "Safety": {
      "DefaultLevel": "Normal",
      "RequireApprovalFor": ["purchase", "trade", "delete"],
      "MaxActionsPerMinute": 60,
      "HumanApprovalTimeout": "00:02:00"
    },
    "Learning": {
      "Enabled": true,
      "MinTrajectoriesForSkill": 10,
      "SkillConfidenceThreshold": 0.8,
      "OnlineAdaptationRate": 0.01
    }
  },
  "Performance": {
    "L2Cache": {
      "L1SlidingExpiration": "00:05:00",
      "L1AbsoluteExpiration": "00:30:00",
      "L1SizeLimitBytes": 104857600,
      "EnableL1Cache": true,
      "EnableL2Cache": true,
      "EnableInvalidationEvents": true
    },
    "ConnectionPool": {
      "HttpPooledConnectionLifetime": "00:05:00",
      "HttpPooledConnectionIdleTimeout": "00:02:00",
      "HttpMaxConnectionsPerServer": 100,
      "HttpDefaultTimeout": "00:00:30",
      "HttpRetryCount": 3,
      "HttpCircuitBreakerThreshold": 5,
      "HttpCircuitBreakerDuration": "00:00:30",
      "GrpcPooledConnectionLifetime": "00:10:00",
      "GrpcPooledConnectionIdleTimeout": "00:05:00"
    }
  },
  "AntiDetection": {
    "MinClickDelayMs": 100,
    "MaxClickDelayMs": 350,
    "CoordinateJitterMinPx": 2,
    "CoordinateJitterMaxPx": 5,
    "EnableBezierMouseMovement": true,
    "BezierControlPoints": 3,
    "BezierVariance": 0.3,
    "MinActionIntervalMs": 500,
    "MaxActionIntervalMs": 2000,
    "PauseProbability": 0.15,
    "MinPauseDurationMs": 1000,
    "MaxPauseDurationMs": 5000,
    "EnableTelegramBot": false,
    "TelegramBotToken": "",
    "AllowedChatIds": [],
    "TelegramApiUrl": "https://api.telegram.org/bot",
    "AlertOnDeath": true,
    "AlertOnVipDrop": true,
    "AlertOnDisconnect": true,
    "AlertOnLowHp": false,
    "LowHpThresholdPercent": 20,
    "EnableRemoteControl": false,
    "CommandPrefix": "/"
  },
  "ResourceOptimizer": {
    "EnableFpsLimiter": true,
    "BackgroundFpsLimit": 10,
    "CombatFpsLimit": 30,
    "IdleFpsLimit": 5,
    "EnableRenderStripping": false,
    "Strip3DRendering": true,
    "StripParticleEffects": true,
    "StripUIRendering": false,
    "StripShadowEffects": true,
    "EnableRamCleaner": true,
    "RamCleanIntervalMinutes": 10,
    "MemoryThresholdMb": 500,
    "AggressiveCleanup": false,
    "EnableWindowGridLayout": true,
    "MinWindowWidth": 800,
    "MinWindowHeight": 600,
    "GridColumns": 4,
    "GridRows": 3,
    "WindowMargin": 5,
    "HideToSystemTray": true,
    "MinimizeToTray": true,
    "MonitoringIntervalSeconds": 30,
    "CpuThresholdPercent": 80,
    "GpuThresholdPercent": 90
  }
}
```

## Environment Variables

All settings can be overridden via environment variables using double underscore notation:

| Environment Variable | AppSettings Path |
|---------------------|------------------|
| `CONNECTIONSTRINGS__KKBOTDB` | `ConnectionStrings.KkbotDb` |
| `SECURITY__ENCRYPTIONPASSPHRASE` | `Security.EncryptionPassphrase` |
| `JWT__KEY` | `Jwt.Key` |
| `AI__OLLAMA__BASEURL` | `AI.Ollama.BaseUrl` |
| `REDIS__CONNECTIONSTRING` | `Redis.ConnectionString` |
| `TELEGRAM__BOTTOKEN` | `AntiDetection.TelegramBotToken` |

## Configuration Files

| File | Purpose |
|------|---------|
| `appsettings.json` | Base configuration |
| `appsettings.Development.json` | Development overrides |
| `appsettings.Production.json` | Production overrides |
| `appsettings.Local.json` | Local machine overrides (gitignored) |

## Configuration Validation

Run configuration validation on startup:

```bash
dotnet run --project src/KKBOT.API -- --validate-config
```

## Secrets Management

### Production

Store secrets in:
- Kubernetes Secrets
- HashiCorp Vault
- Azure Key Vault
- AWS Secrets Manager

### Development

Use `appsettings.Local.json` (gitignored) or user secrets:

```bash
dotnet user-secrets set "Security:EncryptionPassphrase" "your-passphrase"
dotnet user-secrets set "Security:EncryptionSalt" "your-salt"
```

## Configuration Hot Reload

All configuration sections support hot reload via `IOptionsMonitor`. Changes to `appsettings.json` are detected automatically.

## Configuration Validation

Settings classes implement `IValidatableObject` for validation. Invalid configuration throws on startup.

---

*Last Updated: 2025-01-15*
*Version: 24.1.0*