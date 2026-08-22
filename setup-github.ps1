#!/usr/bin/env pwsh
# KKBOT First-time GitHub Setup Script (PowerShell)
# Repository: https://github.com/nguyengiangbr/KKBOT
# Run this script ONCE for initial setup

$ErrorActionPreference = "Stop"

$RepoUrl = "https://github.com/nguyengiangbr/KKBOT.git"
$RepoName = "KKBOT"

Write-Host "🚀 KKBOT First-time GitHub Setup" -ForegroundColor Green
Write-Host "Repository: https://github.com/nguyengiangbr/KKBOT" -ForegroundColor Cyan
Write-Host ""

# Check if we're in the right directory
if (-not (Test-Path "README.md") -or -not (Test-Path "src")) {
    Write-Host "❌ Error: Please run this script from the KKBOT root directory" -ForegroundColor Red
    exit 1
}

# Check if GitHub CLI is installed
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Host "⚠️  GitHub CLI (gh) not found. Installing..." -ForegroundColor Yellow
    if (Get-Command winget -ErrorAction SilentlyContinue) {
        winget install GitHub.cli
    }
    elseif (Get-Command scoop -ErrorAction SilentlyContinue) {
        scoop install gh
    }
    elseif (Get-Command choco -ErrorAction SilentlyContinue) {
        choco install gh
    }
    else {
        Write-Host "❌ Please install GitHub CLI manually: https://cli.github.com/" -ForegroundColor Red
        exit 1
    }
}

# Check if authenticated with GitHub
if (-not (gh auth status -ErrorAction SilentlyContinue)) {
    Write-Host "🔐 Please authenticate with GitHub first:" -ForegroundColor Yellow
    gh auth login
}

# Check if repository exists on GitHub
if (-not (gh repo view nguyengiangbr/KKBOT -ErrorAction SilentlyContinue)) {
    Write-Host "📦 Creating GitHub repository..." -ForegroundColor Yellow
    gh repo create nguyengiangbr/KKBOT --public --description "KKBOT - Game Automation Platform with .NET 10, Plugin Architecture, AI/ML Pipeline, Real-time Collaboration, and Enterprise Features" --homepage "https://github.com/nguyengiangbr/KKBOT" --enable-issues --enable-wiki --enable-projects
}
else {
    Write-Host "✅ Repository already exists on GitHub" -ForegroundColor Green
}

# Initialize git if not already
if (-not (Test-Path ".git")) {
    Write-Host "📦 Initializing git repository..." -ForegroundColor Yellow
    git init
    git branch -M main
}

# Configure git user if not set
if (-not (git config user.name)) {
    $gitUser = Read-Host "Enter your git user name"
    git config user.name $gitUser
}

if (-not (git config user.email)) {
    $gitEmail = Read-Host "Enter your git email"
    git config user.email $gitEmail
}

# Add remote if not exists
if (-not (git remote get-url origin -ErrorAction SilentlyContinue)) {
    Write-Host "🔗 Adding remote origin..." -ForegroundColor Yellow
    git remote add origin https://github.com/nguyengiangbr/KKBOT.git
}
else {
    Write-Host "🔗 Remote origin already exists" -ForegroundColor Green
    git remote set-url origin https://github.com/nguyengiangbr/KKBOT.git
}

# Create .gitignore if not exists
if (-not (Test-Path ".gitignore")) {
    Write-Host "📝 Creating .gitignore..." -ForegroundColor Yellow
    $gitignoreContent = @"
# Git
.git
.gitignore

# Build outputs
**/bin
**/obj
**/out
**/publish

# IDE
.vs
.vscode
*.swp
*.swo
*~

# OS
.DS_Store
Thumbs.db

# Logs
*.log
logs/

# Test results
TestResults/
**/coverage/
**/coverage.cobertura.xml
**/coveragereport/

# Docker
Dockerfile*
docker-compose*
.dockerignore

# Helm
helm/

# Kubernetes
*.kubeconfig

# Secrets
*.pem
*.key
*.crt
secrets.json
*.env
.env*

# Database
*.db
*.sqlite
*.sqlite3

# Plugins (built plugins only, not source)
plugins/*.dll
plugins/*.pdb

# Documentation
*.md
!README.md

# NuGet
packages/
*.nupkg

# Temporary files
tmp/
temp/
*.tmp
"@

    Set-Content -Path ".gitignore" -Value $gitignoreContent -Encoding UTF8
    Write-Host "✅ Created .gitignore" -ForegroundColor Green
}

# Add all files
Write-Host "📦 Adding all files..." -ForegroundColor Yellow
git add .

# Show status
Write-Host ""
Write-Host "📊 Git status:" -ForegroundColor Cyan
git status --short

# Commit
Write-Host ""
Write-Host "💾 Creating initial commit..." -ForegroundColor Yellow
$commitMessage = @"
chore: initial commit - KKBOT platform v24.0.0

Complete game automation platform with:
- Plugin Architecture (hot-loadable, AssemblyLoadContext isolation)
- Computer Vision (OpenCV template matching, OCR pipeline)
- AI Integration (Ollama LLM, Vision models)
- Device Abstraction (ADB, Simulator)
- Orchestration (DAG scheduler, FSM, Event Sourcing)
- Persistence (EF Core, SQLite/PostgreSQL)
- Security (Vault-ready, AES-256-GCM, Audit logging)
- Observability (Serilog, Health checks, Prometheus, Grafana)
- Blazor Server Dashboard (MudBlazor, real-time SignalR)
- Plugin Marketplace (Registry, Installer, Dependency resolution)
- AI/ML Pipeline (Model registry, Training, Inference, Deployment)
- GPU/Edge Optimization (TensorRT, ONNX, Jetson/RPi)
- Real-time Collaboration (SignalR, Presence, Recordings)
- Enterprise Features (SSO, Audit, GDPR/SOC2, RBAC)
- Visual Scripting (Drag-drop, 18 node types)
- Mobile Companion (React Native/Expo)
- Plugin Marketplace Backend (Signing, Moderation, Webhooks)
- Advanced Analytics & BI (Dashboards, Anomaly detection, Forecasting)
- Advanced Orchestration (Event Sourcing, Saga, Dynamic DAG)
- Advanced Marketplace (Sentiment, Security, License compliance)
- Mobile Companion (React Native/Expo)
- Visual Scripting (Drag-drop, 18 node types)
- Deployment (Docker, Helm, GitHub Actions, K8s)

Total: 24 steps completed
"@

git commit -m $commitMessage

# Push to GitHub
Write-Host ""
Write-Host "🚀 Pushing to GitHub..." -ForegroundColor Green
git push -u origin main --force-with-lease

Write-Host ""
Write-Host "✅ Setup complete!" -ForegroundColor Green
Write-Host "🌐 Repository: https://github.com/nguyengiangbr/KKBOT" -ForegroundColor Cyan
Write-Host "🔗 GitHub Actions: https://github.com/nguyengiangbr/KKBOT/actions" -ForegroundColor Cyan
Write-Host "📦 Releases: https://github.com/nguyengiangbr/KKBOT/releases" -ForegroundColor Cyan
Write-Host ""
Write-Host "🎉 KKBOT is now on GitHub!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Enable GitHub Actions in repository settings"
Write-Host "  2. Configure branch protection rules for main branch"
Write-Host "  3. Add required secrets (ENCRYPTION_PASSPHRASE, ENCRYPTION_SALT, etc.)"
Write-Host "  4. Enable GitHub Pages for documentation (optional)"
Write-Host "  5. Create first release: gh release create v24.0.0 --title 'v24.0.0' --notes 'Complete platform release'"