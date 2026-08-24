# Runbook: KKBOT Rollback

## Overview
Emergency rollback procedure for KKBOT Framework. Target: < 5 minutes.

## When to Rollback
- Critical bug in production
- Performance regression
- License validation failures
- Security vulnerability discovered
- Upgrade validation failed

## Rollback Decision
SEV-1 (Service down): Immediate, On-call Engineer
SEV-2 (Major feature): Within 15 min, Team Lead
SEV-3 (Minor issue): Next maintenance, Product Owner

## Rollback Procedures

### 1. Blue-Green Rollback (Preferred - < 2 min)
```powershell
# Verify blue environment exists
Test-Path "C:\\Program Files\\KKBOT Framework-Blue"

# Stop current (green) instance
Stop-Process -Name "KKBOT.Host" -Force

# Wait for shutdown
Start-Sleep -Seconds 10

# Swap directories
Rename-Item "C:\\Program Files\\KKBOT Framework" "C:\\Program Files\\KKBOT Framework-Green-Failed"
Rename-Item "C:\\Program Files\\KKBOT Framework-Blue" "C:\\Program Files\\KKBOT Framework"

# Start blue instance
Start-Process -FilePath "C:\\Program Files\\KKBOT Framework\\KKBOT.Host.exe" -WorkingDirectory "C:\\Program Files\\KKBOT Framework"

# Verify
& "C:\\Program Files\\KKBOT Framework\\KKBOT.CLI.exe" health check
```

### 2. In-Place Rollback (Backup Restore - < 5 min)
```powershell
# Stop current instance
Stop-Process -Name "KKBOT.Host" -Force

# Find latest backup
$backup = Get-ChildItem "C:\\Backups\\KKBOT-Backup-*.zip" | Sort-Object LastWriteTime -Descending | Select-Object -First 1

# Restore from backup
Expand-Archive -Path $backup.FullName -DestinationPath "C:\\Program Files" -Force

# Verify
& "C:\\Program Files\\KKBOT Framework\\KKBOT.CLI.exe" health check
```

### 3. Database Rollback (If schema changed)
```powershell
# Restore database from backup
$backupDb = Get-ChildItem "C:\\Backups\\KKBOT-DB-*.db" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Copy-Item $backupDb.FullName "C:\\Program Files\\KKBOT Framework\\Data\\kkbot.db" -Force
```

### 4. Configuration Rollback
```powershell
# Restore previous config
$backupConfig = Get-ChildItem "C:\\Backups\\KKBOT-Config-*.json" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Copy-Item $backupConfig.FullName "C:\\Program Files\\KKBOT Framework\\config\\appsettings.json" -Force
```

## Post-Rollback Verification
```powershell
# 1. Health check
& "C:\\Program Files\\KKBOT Framework\\KKBOT.CLI.exe" health check

# 2. Version check
& "C:\\Program Files\\KKBOT Framework\\KKBOT.Host.exe" --version

# 3. License validation
& "C:\\Program Files\\KKBOT Framework\\KKBOT.CLI.exe" license status

# 4. Module check
& "C:\\Program Files\\KKBOT Framework\\KKBOT.CLI.exe" module list

# 5. Smoke test
& "C:\\Program Files\\KKBOT Framework\\KKBOT.CLI.exe" test smoke
```

## Rollback Checklist
- [ ] Rollback triggered and documented
- [ ] Blue environment verified healthy
- [ ] Application starts successfully
- [ ] License validates
- [ ] All modules load
- [ ] Dashboard accessible
- [ ] Logs show no critical errors
- [ ] Stakeholders notified
- [ ] Incident ticket updated
- [ ] Root cause analysis initiated

## Communication Template
Subject: [ROLLBACK] KKBOT Framework - v{failed_version} -> v{rolled_back_version}

Body:
- Time of rollback: {timestamp}
- Reason: {reason}
- Impact: {user_impact}
- Current status: {status}
- Next steps: {next_steps}
- RCA timeline: {timeline}

## Prevention
After rollback:
1. Create hotfix branch
2. Write failing test for regression
3. Fix root cause
4. Run full test suite
5. Re-deploy with proper validation

## Support Contacts
- On-call Engineer: +1-XXX-XXX-XXXX
- Team Lead: +1-XXX-XXX-XXXX
- Emergency: +1-XXX-XXX-XXXX (24/7)