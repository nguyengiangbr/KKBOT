# Runbook: KKBOT Installation

## Overview
This runbook covers the installation of KKBOT Framework on a target machine.

## Prerequisites
- Windows 10/11 or Windows Server 2019/2022 (x64)
- .NET 10 Runtime (installer will verify)
- Administrator privileges
- 4 GB RAM minimum, 8 GB recommended
- 2 GB free disk space
- Internet access for license validation (first run)

## Pre-Installation Checklist
- [ ] Verify target OS version
- [ ] Confirm .NET 10 Runtime is installed or will be installed
- [ ] Verify network connectivity to license server
- [ ] Backup any existing KKBOT installation
- [ ] Obtain valid license key

## Installation Steps

### 1. Download Installer
```powershell
# Download from official release
Invoke-WebRequest -Uri "https://releases.kkbot.io/KKBOT-Setup-24.1.0.exe" -OutFile "KKBOT-Setup-24.1.0.exe"

# Verify checksum
Get-FileHash -Algorithm SHA256 KKBOT-Setup-24.1.0.exe
# Compare with published checksum
```

### 2. Silent Installation (Recommended)
```powershell
# Basic silent install
.\KKBOT-Setup-24.1.0.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART

# Custom installation directory
.\KKBOT-Setup-24.1.0.exe /VERYSILENT /SUPPRESSMSGBOXES /DIR="D:\\KKBOT"

# With desktop shortcut
.\KKBOT-Setup-24.1.0.exe /VERYSILENT /SUPPRESSMSGBOXES /TASKS=desktopicon

# With auto-start
.\KKBOT-Setup-24.1.0.exe /VERYSILENT /SUPPRESSMSGBOXES /TASKS=startup
```

### 3. Interactive Installation
```powershell
# Run with GUI
.\KKBOT-Setup-24.1.0.exe
# Follow wizard prompts
```

### 4. Post-Installation Verification
```powershell
# Verify installation
& "C:\\Program Files\\KKBOT Framework\\KKBOT.Host.exe" --version
# Should output: KKBOT Framework 24.1.0

# Check service status
Get-Service -Name "KKBOT*" | Format-Table -AutoSize

# Verify license activation
& "C:\\Program Files\\KKBOT Framework\\KKBOT.CLI.exe" license validate --key YOUR_LICENSE_KEY
```

## Configuration
### License Activation
```powershell
# Activate license
KKBOT.CLI.exe license activate --key YOUR_LICENSE_KEY --email your@email.com

# Verify activation
KKBOT.CLI.exe license status
```

### Initial Configuration
```yaml
# config/appsettings.json
{
  "LicenseServer": {
    "BaseUrl": "https://license.kkbot.io",
    "TimeoutSeconds": 30
  },
  "Database": {
    "ConnectionString": "Data Source=Data/kkbot.db"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  }
}
```

## Rollback Procedure
If installation fails:
1. Run uninstaller: `C:\\Program Files\\KKBOT Framework\\unins000.exe /VERYSILENT`
2. Remove installation directory: `Remove-Item -Recurse -Force "C:\\Program Files\\KKBOT Framework"`
3. Restore from backup if needed

## Troubleshooting
| Issue | Resolution |
|-------|------------|
| .NET 10 not found | Install from https://dotnet.microsoft.com/download/dotnet/10.0 |
| License validation fails | Check network connectivity to license.kkbot.io |
| Installation fails with access denied | Run as Administrator |
| Antivirus blocks installer | Add exception for installer, verify signature |

## Post-Installation Checklist
- [ ] Application launches successfully
- [ ] License activates without errors
- [ ] Dashboard accessible at configured port
- [ ] Logs writing to `{app}\logs`
- [ ] Configuration file present at `{app}\config\appsettings.json`
- [ ] Desktop shortcut created (if selected)
- [ ] Auto-start configured (if selected)

## Support Contacts
- Technical Support: support@kkbot.io
- License Issues: licensing@kkbot.io
- Emergency: +1-XXX-XXX-XXXX (24/7 for Enterprise)