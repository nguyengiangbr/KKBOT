# Configuration Reference

Complete configuration options for KKBOT.

## Configuration Sources (Priority Order)

1. **appsettings.json** - Base configuration
2. **appsettings.{Environment}.json** - Environment-specific overrides
3. **Environment Variables** - `KKBOT__Section__Key` format
4. **Command Line Arguments** - `--Section:Key=value`
5. **User Secrets** (Development) - `dotnet user-secrets`
6. **Azure Key Vault / HashiCorp Vault** (Production) - Via secret manager

---

## Application Settings

### Logging

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Information",
      "KKBOT": "Debug"
    }
  }
}
```

### Kestrel Server

```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://0.0.0.0:8080"
      },
      "Https": {
        "Url": "https://0.0.0.0:8081",
        "Certificate": {
          "Path": "/app/certs/aspnetcore.pfx",
          "Password": "${CERT_PASSWORD}"
        }
      }
    }
  }
}
```

---

## Database Configuration

### SQLite (Development/Default)

```json
{
  "ConnectionStrings": {
    "KkbotDb": "DataSource=kkbot.db;Cache=Shared;Foreign Keys=True"
  }
}
```

### PostgreSQL (Production)

```json
{
  "ConnectionStrings": {
    "KkbotDb": "Host=postgres;Port=5432;Database=kkbot;Username=kkbot;Password=${POSTGRES_PASSWORD};Pooling=true;Minimum Pool Size=2;Maximum Pool Size=20"
  }
}
```

### SQL Server

```json
{
  "ConnectionStrings": {
    "KkbotDb": "Server=sqlserver;Database=kkbot;User Id=sa;Password=${SQL_PASSWORD};TrustServerCertificate=true"
  }
}
```

---

## Security Configuration

```json
{
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
  }
}
```

### Environment Variables for Secrets

```bash
# Required in production
export SECURITY__ENCRYPTIONPASSPHRASE="your-32-char-passphrase"
export SECURITY__ENCRYPTIONSALT="your-16-char-salt"

# Optional
export SECURITY__SECRETSPATH="/app/data/secrets.json"
export SECURITY__TOKENEXPIRATIONMINUTES="120"
```

---

## Plugin Configuration

```json
{
  "Plugins": {
    "Path": "plugins",
    "AutoReload": true,
    "ReloadIntervalMinutes": 5,
    "AllowedAssemblies": [],  // Empty = allow all
    "BlockedAssemblies": [],
    "SandboxEnabled": false,   // Requires .NET 8+ with WASM
    "LoadTimeoutSeconds": 30
  }
}
```

### Environment Variables

```bash
export PLUGINS__PATH="/app/plugins"
export PLUGINS__AUTORELOAD="true"
export PLUGINS__RELOADINTERVALMINUTES="10"
```

---

## Device Configuration

### ADB Settings

```json
{
  "Device": {
    "Adb": {
      "Path": "adb",           // or full path: "/usr/bin/adb"
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
  }
}
```

---

## Perception Configuration

```json
{
  "Perception": {
    "OpenCv": {
      "TemplateThreshold": 0.85,
      "MatchMethod": "CcoeffNormed",  // CcoeffNormed, SqdiffNormed, CcorrNormed
      "MaxTemplates": 100,
      "Preprocessing": {
        "Enabled": true,
        "Grayscale": true,
        "Resize": { "Width": 0, "Height": 0 },  // 0 = no resize
        "Denoise": false
      }
    },
    "Ocr": {
      "Engine": "Tesseract",  // Tesseract, EasyOcr, Custom
      "Language": "eng",
      "CharacterWhitelist": "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz",
      "Dpi": 300,
      "PageSegMode": 7  // Single line
    },
    "Pipeline": {
      "ParallelDetectors": true,
      "MaxConcurrency": 4,
      "FrameSkip": 0,  // Process every Nth frame
      "CacheTemplates": true
    }
  }
}
```

---

## AI Configuration (Ollama)

```json
{
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
      "ApiKey": "${OPENAI_API_KEY}",
      "Model": "gpt-4-turbo"
    }
  }
}
```

---

## Orchestration Configuration

```json
{
  "Orchestration": {
    "Scheduler": {
      "MaxConcurrentJobs": 10,
      "DefaultJobTimeoutMinutes": 30,
      "CleanupIntervalMinutes": 60,
      "MaxJobHistory": 10000
    },
    "InstanceManager": {
      "MaxInstances": 50,
      "HealthCheckIntervalSeconds": 30,
      "AutoRestartFailed": true,
      "MaxRestartAttempts": 3
    },
    "Recovery": {
      "Enabled": true,
      "CheckpointIntervalMinutes": 5,
      "MaxRecoveryAttempts": 3,
      "RecoveryTimeoutMinutes": 10
    },
    "EventStore": {
      "Enabled": true,
      "MaxEventsPerInstance": 10000,
      "RetentionDays": 30
    }
  }
}
```

---

## Observability Configuration

```json
{
  "Observability": {
    "Serilog": {
      "MinimumLevel": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning",
        "Microsoft.EntityFrameworkCore": "Information"
      },
      "WriteTo": [
        { "Name": "Console" },
        {
          "Name": "File",
          "Args": {
            "path": "logs/kkbot-.log",
            "rollingInterval": "Day",
            "retainedFileCountLimit": 30,
            "fileSizeLimitBytes": 104857600,
            "rollOnFileSizeLimit": true,
            "shared": true,
            "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}"
          }
        },
        {
          "Name": "Seq",
          "Args": {
            "serverUrl": "http://seq:5341",
            "apiKey": "${SEQ_API_KEY}"
          }
        }
      ],
      "Enrich": [
        "FromLogContext",
        "WithMachineName",
        "WithProcessId",
        "WithThreadId",
        "WithEnvironmentName",
        "WithAssemblyName",
        "WithAssemblyVersion"
      ]
    },
    "Metrics": {
      "Enabled": true,
      "Endpoint": "/metrics",
      "Port": 9090,
      "IncludeRuntimeMetrics": true,
      "IncludeProcessMetrics": true
    },
    "HealthChecks": {
      "Enabled": true,
      "Endpoint": "/health",
      "DetailedErrors": false,
      "AllowedEndpoints": ["127.0.0.1", "::1"]
    },
    "Tracing": {
      "Enabled": false,
      "Exporter": "Zipkin",  // Zipkin, Jaeger, OTLP
      "Endpoint": "http://jaeger:14268/api/traces",
      "SampleRate": 0.1
    }
  }
}
```

---

## Dashboard Configuration

```json
{
  "Dashboard": {
    "Title": "KKBOT Dashboard",
    "Theme": "light",  // light, dark, auto
    "Language": "en",
    "RefreshIntervalSeconds": 30,
    "MaxLogEntries": 1000,
    "DefaultPageSize": 25,
    "EnableRealTimeUpdates": true,
    "SignalR": {
      "Enabled": true,
      "HubUrl": "/dashboardHub"
    }
  }
}
```

---

## Environment-Specific Configs

### Development (appsettings.Development.json)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information",
      "KKBOT": "Trace"
    }
  },
  "Security": {
    "EncryptionPassphrase": "dev-passphrase-change-me",
    "EncryptionSalt": "dev-salt-change-me"
  },
  "Device": {
    "Simulator": { "Enabled": true }
  },
  "Observability": {
    "Serilog": {
      "MinimumLevel": "Debug",
      "WriteTo": [{ "Name": "Console" }]
    }
  }
}
```

### Production (appsettings.Production.json)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Security": {
    "EncryptionPassphrase": "${ENCRYPTION_PASSPHRASE}",
    "EncryptionSalt": "${ENCRYPTION_SALT}"
  },
  "Device": {
    "Adb": { "Path": "/usr/bin/adb" },
    "Simulator": { "Enabled": false }
  },
  "Observability": {
    "Serilog": {
      "WriteTo": [
        { "Name": "Console" },
        {
          "Name": "File",
          "Args": {
            "path": "/app/logs/kkbot-.log",
            "rollingInterval": "Day",
            "retainedFileCountLimit": 90
          }
        },
        {
          "Name": "Seq",
          "Args": { "serverUrl": "http://seq:5341" }
        }
      ]
    },
    "Metrics": { "Enabled": true },
    "Tracing": { "Enabled": true, "Exporter": "OTLP", "Endpoint": "http://jaeger:4317" }
  }
}
```

---

## Docker Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `ASPNETCORE_ENVIRONMENT` | Runtime environment | `Production` |
| `ASPNETCORE_URLS` | Server URLs | `http://+:8080` |
| `ConnectionStrings__KkbotDb` | Database connection | `DataSource=/app/data/kkbot.db` |
| `Security__SecretsPath` | Secrets file path | `/app/data/secrets.json` |
| `Security__EncryptionPassphrase` | **Required** AES passphrase | - |
| `Security__EncryptionSalt` | **Required** AES salt | - |
| `Plugins__Path` | Plugin directory | `/app/plugins` |
| `PLUGINS__AUTORELOAD` | Auto-reload plugins | `true` |

---

## Kubernetes ConfigMap Example

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: kkbot-config
data:
  appsettings.json: |
    {
      "Logging": { "LogLevel": { "Default": "Information" } },
      "ConnectionStrings": { "KkbotDb": "DataSource=/app/data/kkbot.db" },
      "Security": { "SecretsPath": "/app/data/secrets.json" },
      "Plugins": { "Path": "/app/plugins" },
      "Observability": {
        "Serilog": {
          "MinimumLevel": "Information",
          "WriteTo": [
            { "Name": "Console" },
            { "Name": "File", "Args": { "path": "/app/logs/kkbot-.log", "rollingInterval": "Day" } }
          ]
        }
      }
    }
```

---

## Kubernetes Secret Example

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: kkbot-secrets
type: Opaque
stringData:
  ENCRYPTION_PASSPHRASE: "your-32-character-passphrase-here"
  ENCRYPTION_SALT: "your-16-char-salt"
  POSTGRES_PASSWORD: "secure-db-password"
  # Optional external services
  OPENAI_API_KEY: "sk-..."
  SEQ_API_KEY: "your-seq-api-key"
```

---

## Validation

Run configuration validation at startup:

```bash
# Check config syntax
dotnet run --project src/KKBOT.API -- --validate-config

# Show effective config (with secrets masked)
dotnet run --project src/KKBOT.API -- --show-config
```

---

## Secrets Management

### Development

```bash
# Store secrets locally
dotnet user-secrets set "Security:EncryptionPassphrase" "dev-passphrase"
dotnet user-secrets set "Security:EncryptionSalt" "dev-salt"
```

### Production (Vault)

```csharp
// In Program.cs
builder.Services.AddSingleton<ISecretManager>(sp => 
    new VaultSecretManager(
        vaultUrl: "https://vault.example.com",
        token: Environment.GetEnvironmentVariable("VAULT_TOKEN"),
        mountPoint: "secret",
        path: "kkbot",
        sp.GetRequiredService<ILogger<VaultSecretManager>>()
    )
);
```

---

## Complete Example (Production)

```json
{
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft": "Warning" } },
  "AllowedHosts": "*",
  "Kestrel": { "Endpoints": { "Http": { "Url": "http://0.0.0.0:8080" } } },
  "ConnectionStrings": { "KkbotDb": "Host=postgres;Database=kkbot;Username=kkbot;Password=${POSTGRES_PASSWORD}" },
  "Security": { "SecretsPath": "/app/data/secrets.json", "EncryptionPassphrase": "${ENCRYPTION_PASSPHRASE}", "EncryptionSalt": "${ENCRYPTION_SALT}" },
  "Plugins": { "Path": "/app/plugins", "AutoReload": true },
  "Device": { "Adb": { "Path": "/usr/bin/adb" } },
  "Perception": { "OpenCv": { "TemplateThreshold": 0.85 }, "Ocr": { "Engine": "Tesseract" } },
  "AI": { "Ollama": { "BaseUrl": "http://ollama:11434", "Model": "llama3.1:8b" } },
  "Orchestration": { "Scheduler": { "MaxConcurrentJobs": 20 } },
  "Observability": {
    "Serilog": { "MinimumLevel": "Information", "WriteTo": [{ "Name": "Console" }, { "Name": "File", "Args": { "path": "/app/logs/kkbot-.log" } }] },
    "Metrics": { "Enabled": true },
    "HealthChecks": { "Enabled": true }
  }
}
```