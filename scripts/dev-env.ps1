# KKBOT Local Development Environment Setup (PowerShell)
# This script sets up a complete local development environment for KKBOT on Windows

param(
    [switch]$Stop,
    [switch]$Restart,
    [switch]$Build,
    [switch]$Test,
    [switch]$Logs,
    [switch]$Status
)

$ErrorActionPreference = "Stop"

Write-Host "=== KKBOT Local Development Environment ===" -ForegroundColor Cyan
Write-Host ""

# Configuration
$KKBOT_VERSION = "24.1.0"
$DOTNET_VERSION = "10.0"
$POSTGRES_VERSION = "16"
$REDIS_VERSION = "7"
$RABBITMQ_VERSION = "3.12"

# Colors
$Red = [ConsoleColor]::Red
$Green = [ConsoleColor]::Green
$Yellow = [ConsoleColor]::Yellow
$Cyan = [ConsoleColor]::Cyan

function Write-Color($message, $color) {
    Write-Host $message -ForegroundColor $color
}

function Check-Command($command) {
    return (Get-Command $command -ErrorAction SilentlyContinue) -ne $null
}

function Install-Prerequisites {
    Write-Color "Installing prerequisites..." $Yellow
    
    if (-not (Check-Command "docker")) {
        Write-Color "Please install Docker Desktop manually" $Red
        Write-Color "Visit: https://www.docker.com/products/docker-desktop/" $Cyan
    }
    
    if (-not (Check-Command "git")) {
        Write-Color "Please install Git manually" $Red
        Write-Color "Visit: https://git-scm.com/download/win" $Cyan
    }
    
    if (Check-Command "choco") {
        choco install -y postgresql redis rabbitmq
    }
}

function Install-DotNet {
    Write-Color "Checking .NET SDK..." $Yellow
    
    if (Check-Command "dotnet") {
        $version = dotnet --version
        $major = [int]($version.Split('.')[0])
        if ($major -ge 10) {
            Write-Color ".NET $DOTNET_VERSION already installed ($version)" $Green
            return
        }
    }
    
    Write-Color "Installing .NET $DOTNET_VERSION SDK..." $Yellow
    
    if (Check-Command "choco") {
        choco install -y dotnet-10-sdk
    } elseif (Check-Command "winget") {
        winget install Microsoft.DotNet.SDK.10
    } else {
        Write-Color "Please install .NET 10 SDK manually" $Red
        Write-Color "Visit: https://dotnet.microsoft.com/download/dotnet/10.0" $Cyan
    }
    
    $env:PATH = [System.Environment]::GetEnvironmentVariable("Path", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path", "User")
}

function Start-Infrastructure {
    Write-Color "Starting infrastructure services..." $Yellow
    
    $dockerCompose = @"
version: '3.8'

services:
  postgres:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: kkbot
      POSTGRES_USER: kkbot
      POSTGRES_PASSWORD: kkbot_dev_password
    ports:      - "5432:5432"
    volumes:      - postgres_data:/var/lib/postgresql/data
    healthcheck:      test: ["CMD-SHELL", "pg_isready -U kkbot -d kkbot"]      interval: 10s      timeout: 5s      retries: 5

  redis:
    image: redis:7-alpine
    command: redis-server --requirepass kkbot_dev_password --maxmemory 256mb --maxmemory-policy allkeys-lru
    ports:      - "6379:6379"
    volumes:      - redis_data:/data
    healthcheck:      test: ["CMD", "redis-cli", "-a", "kkbot_dev_password", "ping"]      interval: 10s      timeout: 5s      retries: 5

  rabbitmq:
    image: rabbitmq:3.12-management-alpine
    environment:      RABBITMQ_DEFAULT_USER: kkbot      RABBITMQ_DEFAULT_PASS: kkbot_dev_password
    ports:      - "5672:5672"      - "15672:15672"
    volumes:      - rabbitmq_data:/var/lib/rabbitmq
    healthcheck:      test: ["CMD", "rabbitmq-diagnostics", "check_running"]      interval: 10s      timeout: 5s      retries: 5

  jaeger:
    image: jaegertracing/all-in-one:1.50
    ports:      - "16686:16686"      - "6831:6831/udp"      - "14268:14268"
    environment:      COLLECTOR_OTLP_ENABLED: true

  prometheus:
    image: prom/prometheus:v2.48.0
    ports:      - "9090:9090"
    volumes:      - ./monitoring/prometheus.yml:/etc/prometheus/prometheus.yml      - prometheus_data:/prometheus
    command:      - '--config.file=/etc/prometheus/prometheus.yml'      - '--storage.tsdb.path=/prometheus'

  grafana:
    image: grafana/grafana:10.2.0
    ports:      - "3000:3000"
    environment:      GF_SECURITY_ADMIN_USER: admin      GF_SECURITY_ADMIN_PASSWORD: admin      GF_USERS_ALLOW_SIGN_UP: "false"
    volumes:      - grafana_data:/var/lib/grafana      - ./monitoring/grafana/dashboards:/etc/grafana/provisioning/dashboards      - ./monitoring/grafana/datasources:/etc/grafana/provisioning/datasources

volumes:
  postgres_data:
  redis_data:
  rabbitmq_data:
  prometheus_data:
  grafana_data:
"@

    $dockerCompose | Out-File -FilePath "docker-compose.dev.yml" -Encoding utf8
    
    Write-Color "Starting Docker services..." $Cyan
    docker-compose -f docker-compose.dev.yml up -d
    
    Write-Color "Waiting for services to be ready..." $Cyan
    Start-Sleep -Seconds 15
    
    docker-compose -f docker-compose.dev.yml ps
}

function Setup-Database {
    Write-Color "Setting up database..." $Yellow
    
    $maxAttempts = 30
    $attempt = 0
    
    while ($attempt -lt $maxAttempts) {
        try {
            docker-compose -f docker-compose.dev.yml exec -T postgres pg_isready -U kkbot -d kkbot
            Write-Color "PostgreSQL is ready!" $Green
            break
        } catch {
            $attempt++
            Write-Host "Waiting for PostgreSQL... ($attempt/$maxAttempts)"
            Start-Sleep -Seconds 2
        }
    }
    
    if ($attempt -ge $maxAttempts) {
        throw "PostgreSQL failed to start in time"
    }
    
    Write-Color "Running database migrations..." $Cyan
    dotnet ef database update --project src/KKBOT.Persistence --startup-project src/KKBOT.API
}

function Build-Solution {
    Write-Color "Building solution..." $Yellow
    
    dotnet restore KKBOT.sln
    dotnet build KKBOT.sln --configuration Debug --no-restore
}

function Run-Tests {
    Write-Color "Running tests..." $Yellow
    
    dotnet test KKBOT.sln --configuration Debug --no-build --verbosity normal
}

function Start-API {
    Write-Color "Starting KKBOT API..." $Yellow
    
    $process = Start-Process -FilePath "dotnet" -ArgumentList "run --configuration Debug" -WorkingDirectory "src/KKBOT.API" -PassThru
    $process.Id | Out-File -FilePath ".api.pid"
    Write-Color "API PID: $($process.Id)" $Cyan
}

function Start-Dashboard {
    Write-Color "Starting KKBOT Dashboard..." $Yellow
    
    $process = Start-Process -FilePath "dotnet" -ArgumentList "run --configuration Debug" -WorkingDirectory "src/KKBOT.UI.Dashboard" -PassThru
    $process.Id | Out-File -FilePath ".dashboard.pid"
    Write-Color "Dashboard PID: $($process.Id)" $Cyan
}

function Show-Status {
    Write-Host ""
    Write-Color "=== KKBOT Development Environment Ready ===" $Green
    Write-Host ""
    Write-Color "Services:" $Cyan
    Write-Host "  PostgreSQL:  localhost:5432 (kkbot/kkbot_dev_password)"
    Write-Host "  Redis:       localhost:6379 (password: kkbot_dev_password)"
    Write-Host "  RabbitMQ:    localhost:5672 (management: localhost:15672)"
    Write-Host "  Jaeger:      localhost:16686"
    Write-Host "  Prometheus:  localhost:9090"
    Write-Host "  Grafana:     localhost:3000 (admin/admin)"
    Write-Host ""
    Write-Color "Applications:" $Cyan
    Write-Host "  KKBOT API:       http://localhost:5000"
    Write-Host "  Swagger UI:      http://localhost:5000/swagger"
    Write-Host "  Dashboard:       http://localhost:5001"
    Write-Host ""
    Write-Color "Useful commands:" $Cyan
    Write-Host "  Stop all:        .\dev-env.ps1 -Stop"
    Write-Host "  View logs:       .\dev-env.ps1 -Logs"
    Write-Host "  Run tests:       .\dev-env.ps1 -Test"
    Write-Host "  Rebuild:         .\dev-env.ps1 -Build"
    Write-Host ""
}

function Stop-Environment {
    Write-Color "Stopping KKBOT development environment..." $Yellow
    
    if (Test-Path ".api.pid") {
        $pid = Get-Content ".api.pid"
        Stop-Process -Id $pid -Force -ErrorAction SilentlyContinue
        Remove-Item ".api.pid"
    }    
    if (Test-Path ".dashboard.pid") {
        $pid = Get-Content ".dashboard.pid"
        Stop-Process -Id $pid -Force -ErrorAction SilentlyContinue
        Remove-Item ".dashboard.pid"
    }
    
    docker-compose -f docker-compose.dev.yml down
    
    Write-Color "Environment stopped" $Green
}

function Show-Logs {
    docker-compose -f docker-compose.dev.yml logs -f --tail=100
}

if ($Stop) {
    Stop-Environment
} elseif ($Restart) {
    Stop-Environment
    Start-Sleep -Seconds 3
    & $MyInvocation.MyCommand.Definition
} elseif ($Build) {
    Build-Solution
} elseif ($Test) {
    Run-Tests
} elseif ($Logs) {
    Show-Logs
} elseif ($Status) {
    Show-Status
} else {
    Install-Prerequisites
    Install-DotNet
    Start-Infrastructure
    Setup-Database
    Build-Solution
    Start-API
    Start-Dashboard
    Show-Status
}
