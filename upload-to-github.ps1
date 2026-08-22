#!/usr/bin/env pwsh
# KKBOT GitHub Upload Script (PowerShell)
# Repository: https://github.com/nguyengiangbr/KKBOT
# Usage: .\upload-to-github.ps1 [-CommitMessage "your message"]

param(
    [string]$CommitMessage = "chore: upload KKBOT platform v24.0.0 - Complete platform with 24 steps"
)

$RepoUrl = "https://github.com/nguyengiangbr/KKBOT.git"
$RepoName = "KKBOT"

Write-Host "🚀 KKBOT GitHub Upload Script (PowerShell)" -ForegroundColor Green
Write-Host "Repository: https://github.com/nguyengiangbr/KKBOT" -ForegroundColor Cyan
Write-Host "Commit message: $CommitMessage" -ForegroundColor Cyan
Write-Host ""

# Check if we're in a git repository
if (-not (Test-Path ".git")) {
    Write-Host "📦 Initializing git repository..." -ForegroundColor Yellow
    git init
    git branch -M main
}

# Check if remote origin exists
try {
    $remoteUrl = git remote get-url origin
    Write-Host "🔗 Remote origin already exists: $remoteUrl" -ForegroundColor Green
    git remote set-url origin "https://github.com/nguyengiangbr/KKBOT.git"
}
catch {
    Write-Host "🔗 Adding remote origin..." -ForegroundColor Yellow
    git remote add origin "https://github.com/nguyengiangbr/KKBOT.git"
}

# Show current status
Write-Host ""
Write-Host "📊 Current git status:" -ForegroundColor Cyan
git status --short

# Add all files
Write-Host ""
Write-Host "📦 Adding all files to git..." -ForegroundColor Yellow
git add .

# Show what will be committed
Write-Host ""
Write-Host "📝 Files to be committed:" -ForegroundColor Cyan
git diff --cached --stat

# Commit
Write-Host ""
Write-Host "💾 Committing changes..." -ForegroundColor Yellow
git commit -m "chore: upload KKBOT platform v24.0.0 - Complete platform with 24 steps"

# Push to GitHub
Write-Host ""
Write-Host "🚀 Pushing to GitHub..." -ForegroundColor Green
git push -u origin main --force-with-lease

Write-Host ""
Write-Host "✅ Upload complete!" -ForegroundColor Green
Write-Host "🌐 Repository: https://github.com/nguyengiangbr/KKBOT" -ForegroundColor Cyan
Write-Host "🔗 GitHub Actions: https://github.com/nguyengiangbr/KKBOT/actions" -ForegroundColor Cyan
Write-Host "📦 Releases: https://github.com/nguyengiangbr/KKBOT/releases" -ForegroundColor Cyan