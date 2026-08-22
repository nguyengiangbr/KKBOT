#!/bin/bash
# KKBOT First-time GitHub Setup Script
# Repository: https://github.com/nguyengiangbr/KKBOT
# Run this script ONCE for initial setup

set -e

REPO_URL="https://github.com/nguyengiangbr/KKBOT.git"
REPO_NAME="KKBOT"

echo "🚀 KKBOT First-time GitHub Setup"
echo "Repository: https://github.com/nguyengiangbr/KKBOT"
echo ""

# Check if we're in the right directory
if [ ! -f "README.md" ] || [ ! -d "src" ]; then
    echo "❌ Error: Please run this script from the KKBOT root directory"
    exit 1
fi

# Check if GitHub CLI is installed
if ! command -v gh &> /dev/null; then
    echo "⚠️  GitHub CLI (gh) not found. Installing..."
    if command -v apt-get &> /dev/null; then
        sudo apt-get update && sudo apt-get install -y gh
    elif command -v brew &> /dev/null; then
        brew install gh
    elif command -v winget &> /dev/null; then
        winget install GitHub.cli
    else
        echo "❌ Please install GitHub CLI manually: https://cli.github.com/"
        exit 1
    fi
fi

# Check if authenticated with GitHub
if ! gh auth status > /dev/null 2>&1; then
    echo "🔐 Please authenticate with GitHub first:"
    gh auth login
fi

# Check if repository exists on GitHub
if ! gh repo view nguyengiangbr/KKBOT > /dev/null 2>&1; then
    echo "📦 Creating GitHub repository..."
    gh repo create nguyengiangbr/KKBOT --public --description "KKBOT - Game Automation Platform with .NET 10, Plugin Architecture, AI/ML Pipeline, Real-time Collaboration, and Enterprise Features" --homepage "https://github.com/nguyengiangbr/KKBOT" --enable-issues --enable-wiki --enable-projects
else
    echo "✅ Repository already exists on GitHub"
fi

# Initialize git if not already
if [ ! -d ".git" ]; then
    echo "📦 Initializing git repository..."
    git init
    git branch -M main
fi

# Configure git user if not set
if ! git config user.name > /dev/null 2>&1; then
    read -p "Enter your git user name: " git_user
    git config user.name "$git_user"
fi

if ! git config user.email > /dev/null 2>&1; then
    read -p "Enter your git email: " git_email
    git config user.email "$git_email"
fi

# Add remote if not exists
if ! git remote get-url origin > /dev/null 2>&1; then
    echo "🔗 Adding remote origin..."
    git remote add origin https://github.com/nguyengiangbr/KKBOT.git
else
    echo "🔗 Remote origin already exists"
    git remote set-url origin https://github.com/nguyengiangbr/KKBOT.git
fi

# Create .gitignore if not exists
if [ ! -f ".gitignore" ]; then
    echo "📝 Creating .gitignore..."
    cat > .gitignore << 'EOF'
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
EOF
    echo "✅ Created .gitignore"
fi

# Add all files
echo "📦 Adding all files..."
git add .

# Show status
echo ""
echo "📊 Git status:"
git status --short

# Commit
echo ""
echo "💾 Creating initial commit..."
git commit -m "chore: initial commit - KKBOT platform v24.0.0

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

Total: 24 steps completed"

# Push to GitHub
echo ""
echo "🚀 Pushing to GitHub..."
git push -u origin main --force-with-lease

echo ""
echo "✅ Setup complete!"
echo "🌐 Repository: https://github.com/nguyengiangbr/KKBOT"
echo "🔗 GitHub Actions: https://github.com/nguyengiangbr/KKBOT/actions"
echo "📦 Releases: https://github.com/nguyengiangbr/KKBOT/releases"
echo ""
echo "🎉 KKBOT is now on GitHub!"
echo ""
echo "Next steps:"
echo "  1. Enable GitHub Actions in repository settings"
echo "  2. Configure branch protection rules for main branch"
echo "  3. Add required secrets (ENCRYPTION_PASSPHRASE, ENCRYPTION_SALT, etc.)"
echo "  4. Enable GitHub Pages for documentation (optional)"
echo "  4. Create first release: gh release create v24.0.0 --title 'v24.0.0' --notes 'Complete platform release'"