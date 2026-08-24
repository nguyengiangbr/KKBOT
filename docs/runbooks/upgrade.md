# Runbook: KKBOT Upgrade

## Overview
This runbook covers upgrading KKBOT Framework to a newer version using blue-green deployment strategy.

## Prerequisites
- Current KKBOT installation running
- Valid license for new version
- Backup of current installation
- Maintenance window scheduled
- Rollback plan reviewed

## Pre-Upgrade Checklist
- [ ] Review release notes for breaking changes
- [ ] Verify license compatibility with new version
- [ ] Backup current installation (C:\\Program Files\\KKBOT Framework)
- [ ] Backup database (C:\\Program Files\\KKBOT Framework\\Data\\kkbot.db)
- [ ] Backup configuration (C:\\Program Files\\KKBOT Framework\\config\\appsettings.json)
- [ ] Notify stakeholders of maintenance window
- [ ] Prepare rollback plan

## Upgrade Procedure

### 1. Blue-Green Deployment (Zero Downtime)

#### Step 1: Prepare Green Environment
```powershell
# Create parallel installation directory
$greenPath = "C:\\Program Files\\KKBOT Framework-Green"
New-Item -ItemType Directory -Force -Path $greenPath

# Extract new version to green directory
.\KKBOT-Setup-24.2.0.exe /VERYSILENT /SUPPRESSMSGBOXES /DIR="$greenPath"

# Copy configuration from blue to green
Copy-Item "C:\\Program Files\\KKBOT Framework\\config\\appsettings.json" "$greenPath\\config\\appsettings.json"
Copy-Item "C:\\Program Files\\KKBOT Framework\\Data\\kkbot.db" "$greenPath\\Data\\kkbot.db" -Force
```

#### Step 2: Validate Green Environment
```powershell
# Test green installation
& "$greenPath\\KKBOT.Host.exe" --version
# Should output new version

# Run health checks
& "$greenPath\\KKBOT.CLI.exe" health check

# Validate license
& "$greenPath\\KKBOT.CLI.exe" license validate --key YOUR_LICENSE_KEY
```

#### Step 3: Switch Traffic (Cutover)
```powershell
# Stop blue instance gracefully
Stop-Process -Name "KKBOT.Host" -Force

# Wait for graceful shutdown (max 30s)
Start-Sleep -Seconds 30

# Start green instance
Start-Process -FilePath "$greenPath\\KKBOT.Host.exe" -WorkingDirectory "$greenPath"

# Verify green is healthy
& "$greenPath\\KKBOT.CLI.exe" health check

# Rename directories (atomic swap)
Rename-Item "C:\\Program Files\\KKBOT Framework" "C:\\Program Files\\KKBOT Framework-Blue"
Rename-Item "$greenPath" "C:\\Program Files\\KKBOT Framework"
```

#### Step 4: Post-Cutover Validation
```powershell
# Verify new version
& "C:\\Program Files\\KKBOT Framework\\KKBOT.Host.exe" --version

# Run smoke tests
& "C:\\Program Files\\KKBOT Framework\\KKBOT.CLI.exe" test smoke

# Monitor logs for errors
Get-Content "C:\\Program Files\\KKBOT Framework\\logs\\*.log" -Wait -Tail 50
```

### 2. In-Place Upgrade (With Downtime)
Use only if blue-green not possible:

```powershell
# Stop current instance
Stop-Process -Name "KKBOT.Host" -Force

# Backup current installation
Compress-Archive -Path "C:\\Program Files\\KKBOT Framework" -DestinationPath "C:\\Backups\\KKBOT-Backup-$(Get-Date -Format 'yyyyMMdd-HHmmss').zip"

# Run installer in upgrade mode
.\KKBOT-Setup-24.2.0.exe /VERYSILENT /SUPPRESSMSGBOXES

# Verify upgrade
& "C:\\Program Files\\KKBOT Framework\\KKBOT.Host.exe" --version
```

## Post-Upgrade Checklist
- [ ] Application launches successfully
- [ ] License activates without errors
- [ ] Dashboard accessible
- [ ] All 7 modules load correctly
- [ ] Database migrations applied
- [ ] Configuration validated
- [ ] Logs writing correctly
- [ ] Performance metrics normal
- [ ] Smoke tests pass
- [ ] Stakeholders notified of completion

## Rollback Procedure
If upgrade fails validation:
```powershell
# Quick rollback (blue-green)
Rename-Item "C:\\Program Files\\KKBOT Framework" "C:\\Program Files\\KKBOT Framework-Green-Failed"
Rename-Item "C:\\Program Files\\KKBOT Framework-Blue" "C:\\Program Files\\KKBOT Framework"
Start-Process -FilePath "C:\\Program Files\\KKBOT Framework\\KKBOT.Host.exe" -WorkingDirectory "C:\\Program Files\\KKBOT Framework"
```

## Version Compatibility
From 24.0.x to 24.1.0: Config only, ~30s downtime
From 24.1.0 to 24.2.0: DB migration, ~2min downtime
From 23.x.x to 24.x.x: Full migration, ~10min downtime

## Support Contacts
- Technical Support: support@kkbot.io
- Emergency Rollback: +1-XXX-XXX-XXXX