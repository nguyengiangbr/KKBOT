# Troubleshooting Guide

Common issues and solutions for KKBOT.

## Table of Contents

1. [Quick Diagnostics](#quick-diagnostics)
2. [Startup Issues](#startup-issues)
3. [Plugin Problems](#plugin-problems)
4. [Device Connection](#device-connection)
4. [Perception & AI](#perception--ai)
5. [Database Issues](#database-issues)
6. [Performance Problems](#performance-problems)
7. [Dashboard Issues](#dashboard-issues)
8. [Deployment Issues](#deployment-issues)
9. [Log Analysis](#log-analysis)
10. [Emergency Procedures](#emergency-procedures)

---

## Quick Diagnostics

### Health Check Endpoints

```bash
# Overall health
curl http://localhost:8080/health

# Detailed health (if enabled)
curl http://localhost:8080/health/details

# Metrics
curl http://localhost:8080/metrics
```

### Key Commands

```bash
# Check running processes
ps aux | grep dotnet

# Check ports
netstat -tlnp | grep -E '8080|8081|5432|6379|11434'

# Check disk space
df -h

# Check memory
free -h

# Check GPU (if applicable)
nvidia-smi

# Docker logs
docker-compose logs -f kkbot --tail=100

# Kubernetes logs
kubectl logs -n kkbot deployment/kkbot -f --tail=100
```

### Status Dashboard

Access: `http://localhost:8080` → Overview page shows:
- Instance status
- Job queue
- Plugin count
- System resources

---

## Startup Issues

### Application Won't Start

**Symptoms**: Process exits immediately, no logs, or error on startup.

**Diagnosis**:
```bash
# Run directly to see output
dotnet run --project src/KKBOT.UI.Dashboard

# Check for missing dependencies
dotnet restore
dotnet build
```

**Common Causes & Fixes**:

| Error | Cause | Fix |
|-------|-------|-----|
| `Host terminated unexpectedly` | Missing config, port conflict | Check `appsettings.json`, port 8080 free |
| `Could not load file or assembly` | Missing NuGet package, version mismatch | `dotnet restore`, check `.csproj` versions |
| `Permission denied` | File permissions, Docker user | `chmod +x`, check Dockerfile USER |
| `Database connection failed` | Wrong connection string, DB down | Verify `ConnectionStrings__KkbotDb`, DB running |

### Configuration Errors

**Symptoms**: "Configuration section not found", validation errors.

**Fix**:
```bash
# Validate configuration
dotnet run --project src/KKBOT.API -- --validate-config

# Show effective config (secrets masked)
dotnet run --project src/KKBOT.API -- --show-config

# Check environment variables
env | grep -E 'KKBOT|ASPNETCORE|CONNECTION'
```

### Port Conflicts

**Symptoms**: "Address already in use", "Failed to bind to port".

**Fix**:
```bash
# Find process using port
lsof -i :8080
netstat -tlnp | grep 8080

# Kill process
kill -9 <PID>

# Or change port in appsettings.json
"Kestrel": {
  "Endpoints": {
    "Http": { "Url": "http://0.0.0.0:8080" }
  }
}
```

---

## Plugin Problems

### Plugin Not Loading

**Symptoms**: Plugin not shown in dashboard, not in `/api/v1/plugins`.

**Diagnosis**:
```bash
# Check plugin directory
ls -la plugins/

# Check plugin DLL
file plugins/MyPlugin.dll

# Test load manually
dotnet run --project src/KKBOT.Plugin.Loader.Tests -- --path plugins/
```

**Common Causes & Fixes**:

| Issue | Cause | Fix |
|-------|-------|-----|
| `FileNotFoundException` | Missing dependencies | Ensure all deps copied to plugins/ |
| `TypeLoadException` | Version mismatch | Rebuild plugin against current KKBOT.Abstractions |
| `MissingMethodException` | Interface changed | Update plugin to match latest `IGameModule` |
| `AssemblyLoadContext` error | Native dependency | Copy native DLLs (OpenCV) to plugins/ |
| Plugin validates false | `ValidateAsync` returns false | Check plugin logs, fix validation logic |

### Plugin Reload Not Working

**Symptoms**: Changes not reflected after rebuild.

**Fix**:
```bash
# Manual reload via API
curl -X POST http://localhost:8080/api/v1/plugins/reload

# Or restart app (dev mode)
docker-compose restart kkbot

# Check file watcher
# Ensure plugins/ is watched (docker-compose.override.yml mounts source)
```

### Plugin Crashes Application

**Symptoms**: App exits when plugin loads, unhandled exception.

**Diagnosis**:
```bash
# Check logs for plugin error
grep -i "plugin" logs/kkbot-*.log | tail -20

# Run with debugger
dotnet run --project src/KKBOT.UI.Dashboard -- --debug-plugins
```

**Fix**:
- Wrap plugin loading in try/catch (already in `PluginLoader`)
- Check `ValidateAsync` implementation
- Ensure plugin targets .NET 10

---

## Device Connection

### ADB Not Connecting

**Symptoms**: "No devices found", "Connection refused", timeout.

**Diagnosis**:
```bash
# Check ADB server
adb devices

# Check device authorization
adb devices -l

# Test connection manually
adb -s <serial> shell echo test

# Check ADB path
which adb
adb version
```

**Common Causes & Fixes**:

| Issue | Cause | Fix |
|-------|-------|-----|
| `daemon not running` | ADB server down | `adb kill-server && adb start-server` |
| `unauthorized` | Device not authorized | Accept dialog on device, `adb devices` |
| `connection refused` | Wrong IP/port | Check device IP, `adb connect <ip>:5555` |
| `device offline` | USB cable, driver | Reconnect cable, update drivers |
| `multiple devices` | Ambiguous target | Specify `-s <serial>` in config |

### ADB Command Failures

**Symptoms**: Commands timeout, "closed", "broken pipe".

**Fix**:
```json
// Increase timeouts in appsettings.json
"Device": {
  "Adb": {
    "CommandTimeoutSeconds": 30,
    "ConnectTimeoutSeconds": 60,
    "RetryCount": 5,
    "RetryDelayMs": 2000
  }
}
```

### Simulator Issues

**Symptoms**: Simulator not starting, frames not capturing.

**Fix**:
- Check `Device:Simulator:Enabled: true` in config
- Verify `FrameRate` and `DefaultResolution` settings
- Check simulator logs: `grep -i simulator logs/kkbot-*.log`

---

## Perception & AI

### OpenCV Template Matching Fails

**Symptoms**: Low confidence, no matches, crashes.

**Diagnosis**:
```bash
# Check OpenCV version
ldd plugins/libopencv_core.so | head -5

# Test template manually
python3 -c "
import cv2
img = cv2.imread('frame.png')
tpl = cv2.imread('template.png')
res = cv2.matchTemplate(img, tpl, cv2.TM_CCOEFF_NORMED)
print(cv2.minMaxLoc(res))
"
```

**Common Causes & Fixes**:

| Issue | Cause | Fix |
|-------|-------|-----|
| `DllNotFoundException` | Missing OpenCV native libs | Install `libopencv-dev` or copy DLLs |
| Low confidence | Template mismatch | Retake template at same resolution |
| Crash on `Cv2.ImDecode` | Invalid frame data | Check frame format, size > 0 |
| Memory leak | Not disposing Mats | Use `using var mat = ...` |

### OCR Not Working

**Symptoms**: Empty results, garbage text, low confidence.

**Fix**:
```json
// Tune OCR settings
"Perception": {
  "Ocr": {
    "Engine": "Tesseract",
    "Language": "eng",
    "CharacterWhitelist": "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ",
    "Dpi": 300,
    "PageSegMode": 7
  }
}
```

- Preprocess: grayscale, threshold, resize 2x
- Train custom font if game uses custom font
- Use `CharacterWhitelist` to limit vocabulary

### AI (Ollama) Issues

**Symptoms**: Timeout, "connection refused", empty response, OOM.

**Diagnosis**:
```bash
# Test Ollama directly
curl http://localhost:11434/api/tags
curl -X POST http://localhost:11434/api/generate -d '{"model":"llama3.1:8b","prompt":"test"}'

# Check Ollama logs
docker logs ollama
kubectl logs -n kkbot deployment/ollama
```

**Common Causes & Fixes**:

| Issue | Cause | Fix |
|-------|-------|-----|
| `connection refused` | Ollama not running | Start Ollama, check port 11434 |
| Timeout | Model loading, large prompt | Increase `TimeoutSeconds`, use smaller model |
| OOM | Model too large for GPU | Use quantized model (q4_k_m), offload to CPU |
| "model not found" | Model not pulled | `ollama pull llama3.1:8b` |
| Slow response | No GPU, CPU inference | Enable GPU, use smaller model |

### AI Configuration

```json
// Optimize for speed
"AI": {
  "Ollama": {
    "Model": "llama3.1:8b-instruct-q4_k_m",
    "DefaultParams": {
      "Temperature": 0.3,
      "MaxTokens": 512,
      "NumCtx": 2048,
      "NumGpu": 999
    },
    "TimeoutSeconds": 60,
    "KeepAliveMinutes": 30
  }
}
```

---

## Database Issues

### SQLite Locked

**Symptoms**: "database is locked", "SQLITE_BUSY".

**Fix**:
```bash
# Check for multiple processes
lsof kkbot.db

# Enable WAL mode (in DbContext)
protected override void OnConfiguring(DbContextOptionsBuilder options)
{
    options.UseSqlite(connectionString, b => b.UseWAL());
}
```

### Migration Failures

**Symptoms**: "pending migrations", "schema mismatch".

**Fix**:
```bash
# Apply migrations
dotnet ef database update --project src/KKBOT.Persistence.EfCore

# Or in container
dotnet KKBOT.Persistence.EfCore.dll migrate

# Reset (dev only)
rm kkbot.db && dotnet ef database update
```

### Connection Pool Exhausted

**Symptoms**: "timeout waiting for connection", high latency.

**Fix**:
```json
// Increase pool size
"ConnectionStrings": {
  "KkbotDb": "Host=postgres;Database=kkbot;Username=kkbot;Password=xxx;Maximum Pool Size=50;Minimum Pool Size=5"
}
```

### Slow Queries

**Diagnosis**:
```sql
-- Enable query logging (EF Core)
"Logging": {
  "LogLevel": {
    "Microsoft.EntityFrameworkCore.Database.Command": "Information"
  }
}
```

**Fix**:
- Add missing indexes
- Use `AsNoTracking()` for reads
- Avoid N+1 queries (use `Include()`)
- Consider read replicas

---

## Performance Problems

### High CPU Usage

**Diagnosis**:
```bash
# Check process CPU
top -p $(pgrep -f "KKBOT")

# Profile with dotnet-counters
dotnet-counters monitor --process-id <PID> System.Runtime

# Profile with dotnet-trace
dotnet-trace collect -p <PID> --format Speedscope
```

**Common Causes**:
- Tight loops without `await Task.Delay`
- Perception processing every frame (add `FrameSkip`)
- No caching for AI responses
- GC pressure (large object allocations)

**Fix**:
```csharp
// Add frame skip
"Perception": {
  "Pipeline": { "FrameSkip": 2 } // Process every 3rd frame
}

// Cache AI responses
services.AddMemoryCache();
```

### High Memory Usage

**Diagnosis**:
```bash
# Check memory
dotnet-counters monitor -p <PID> System.Runtime --counters "gen-0-gc-count,gen-1-gc-count,gen-2-gc-count,alloc-rate"

# Heap dump
dotnet-dump collect -p <PID>
dotnet-dump analyze <dump-file>
```

**Common Causes**:
- Not disposing `Mat` objects (OpenCV)
- Large frame buffers not released
- Event store growing unbounded
- EF Core change tracker holding entities

**Fix**:
```csharp
// Dispose Mats properly
using var mat = Cv2.ImDecode(data, ImreadModes.Grayscale);
// ... use mat
// auto-disposed

// Limit event store
services.Configure<EventStoreOptions>(o => o.MaxEventsPerInstance = 1000);

// Use AsNoTracking for reads
var instances = await _context.Instances.AsNoTracking().ToListAsync();
```

### Slow Dashboard

**Diagnosis**:
- Check SignalR connection
- Browser dev tools → Network → WS
- Check Blazor circuit latency

**Fix**:
- Reduce `RefreshIntervalSeconds`
- Enable `EnableRealTimeUpdates: false` for polling
- Optimize database queries for dashboard

---

## Dashboard Issues

### Blank Page / Not Loading

**Symptoms**: White screen, loading spinner forever.

**Diagnosis**:
```bash
# Check browser console (F12)
# Check network tab for failed requests

# Check server logs
grep -i "blazor\|circuit" logs/kkbot-*.log
```

**Common Causes & Fixes**:

| Issue | Cause | Fix |
|-------|-------|-----|
| `Failed to load resource` | Static files missing | Check `wwwroot/` published, `UseStaticFiles()` |
| `Connection to server lost` | SignalR disconnect | Check proxy websocket support, increase timeout |
| `Circuit terminated` | Unhandled exception | Check server logs for exception details |
| CSS/JS not loading | MIME type, path | Check `_Host.cshtml` base href, static file middleware |

### Real-time Updates Not Working

**Symptoms**: Data stale, manual refresh needed.

**Fix**:
```json
// Enable SignalR
"Dashboard": {
  "EnableRealTimeUpdates": true,
  "SignalR": { "HubUrl": "/dashboardHub" }
}

// Check proxy passes websockets
# Nginx
proxy_http_version 1.1;
proxy_set_header Upgrade $http_upgrade;
proxy_set_header Connection "upgrade";
```

### MudBlazor Components Broken

**Symptoms**: Layout broken, missing styles, JS errors.

**Fix**:
- Check `_Imports.razor` has `@using MudBlazor`
- Check `_Host.cshtml` has MudBlazor CSS/JS links
- Clear browser cache
- Check version compatibility

---

## Deployment Issues

### Docker Build Fails

**Symptoms**: "COPY failed", "no such file", "permission denied".

**Fix**:
```bash
# Check .dockerignore
cat .dockerignore

# Build with verbose
docker build --progress=plain -t kkbot .

# Check build context size
du -sh .
```

### Docker Compose Issues

| Issue | Fix |
|-------|-----|
| `volume not found` | `docker-compose up -d` creates volumes |
| `port already allocated` | Change host port in `docker-compose.yml` |
| `depends_on` not waiting | Add healthcheck, use `condition: service_healthy` |
| `env_file` not loading | Check file exists, syntax `KEY=VALUE` |

### Kubernetes Deployment Issues

| Issue | Diagnosis | Fix |
|-------|-----------|-----|
| `ImagePullBackOff` | `kubectl describe pod` | Check image name, registry auth, `imagePullSecrets` |
| `CrashLoopBackOff` | `kubectl logs pod --previous` | Fix app crash, check resources |
| `Pending` | `kubectl describe pod` | Check PVC, node resources, scheduler |
| `OOMKilled` | `kubectl describe pod` | Increase memory limit, fix memory leak |
| `Readiness probe failed` | `kubectl get events` | Fix `/health` endpoint, increase `initialDelaySeconds` |

### Helm Issues

```bash
# Debug template rendering
helm template test-release ./helm/kkbot -f values-prod.yaml --debug

# Dry run install
helm install test-release ./helm/kkbot -f values-prod.yaml --dry-run --debug

# Check release status
helm status kkbot -n kkbot

# Rollback
helm rollback kkbot 1 -n kkbot
```

---

## Log Analysis

### Finding Errors

```bash
# Recent errors
grep -i "error\|exception\|fail" logs/kkbot-$(date +%Y%m%d).log | tail -20

# Specific component
grep "PluginLoader\|Orchestrator\|Perception" logs/kkbot-*.log | tail -30

# Time range
sed -n '/2024-01-15 10:00:00/,/2024-01-15 11:00:00/p' logs/kkbot-20240115.log
```

### Structured Log Queries (Seq/Loki)

```logql
# All exceptions
{app="kkbot"} | json | level="Error" | __error__!=""

# Slow operations (>1s)
{app="kkbot"} | json | duration_ms > 1000

# Plugin errors
{app="kkbot"} | json | plugin_id!="" | level="Error"

# Device errors
{app="kkbot"} | json | device_id!="" | level="Warning"

# AI timeouts
{app="kkbot"} | json | ai_model!="" | error="timeout"
```

### Correlation Tracking

```bash
# Follow a request across services
grep "CorrelationId: 00-1234567890abcdef-1234567890abcdef-00" logs/*.log
```

---

## Emergency Procedures

### Complete Restart

```bash
# Docker Compose
docker-compose down
docker-compose up -d

# Kubernetes
kubectl rollout restart deployment/kkbot -n kkbot
kubectl rollout status deployment/kkbot -n kkbot
```

### Database Recovery

```bash
# SQLite backup
cp /app/data/kkbot.db /app/data/kkbot.db.backup.$(date +%Y%m%d)

# PostgreSQL backup
kubectl exec -n kkbot postgresql-0 -- pg_dump -U kkbot kkbot > backup.sql

# Restore
sqlite3 /app/data/kkbot.db < backup.sql  # SQLite
kubectl exec -i -n kkbot postgresql-0 -- psql -U kkbot kkbot < backup.sql  # PostgreSQL
```

### Secret Rotation (Emergency)

```bash
# Generate new keys
NEW_PASSPHRASE=$(openssl rand -base64 32)
NEW_SALT=$(openssl rand -base64 16)

# Update in Vault
vault kv put secret/kkbot/production encryptionPassphrase=$NEW_PASSPHRASE encryptionSalt=$NEW_SALT

# Restart to pick up new secrets
kubectl rollout restart deployment/kkbot -n kkbot
```

### Scale Down to Zero (Maintenance)

```bash
# Kubernetes
kubectl scale deployment/kkbot -n kkbot --replicas=0

# Docker Compose
docker-compose stop kkbot
```

### Rollback Deployment

```bash
# Helm
helm rollback kkbot -n kkbot

# Docker Compose (tag previous image)
docker tag kkbot:latest kkbot:broken
docker tag kkbot:v1.0.0 kkbot:latest
docker-compose up -d

# Kubernetes (image tag)
kubectl set image deployment/kkbot kkbot=ghcr.io/your-org/kkbot:v1.0.0 -n kkbot
```

---

## Getting Help

### Information to Collect

When reporting issues, include:

1. **Environment**: OS, .NET version, Docker/K8s version
2. **Logs**: Last 100 lines from `logs/kkbot-*.log`
3. **Config**: Sanitized `appsettings.json` (remove secrets)
4. **Steps to Reproduce**: Minimal steps
5. **Expected vs Actual**: What should happen vs what happens
6. **Screenshots**: For UI issues

### Support Channels

| Channel | Purpose |
|---------|---------|
| GitHub Issues | Bug reports, feature requests |
| GitHub Discussions | Questions, ideas, community help |
| Discord/Slack | Real-time chat (if available) |
| Email | Security issues: security@kkbot.example.com |

### Debug Mode

```bash
# Enable debug logging
export Logging__LogLevel__Default=Debug
export Logging__LogLevel__KKBOT=Trace

# Or in appsettings.Development.json
{
  "Logging": { "LogLevel": { "Default": "Debug", "KKBOT": "Trace" } }
}
```

---

## Common Error Codes

| Code | Meaning | Action |
|------|---------|--------|
| `KKBOT-001` | Plugin validation failed | Check plugin manifest, assets |
| `KKBOT-002` | Device connection timeout | Check ADB, device authorization |
| `KKBOT-003` | Perception pipeline error | Check OpenCV, template files |
| `KKBOT-004` | AI service unavailable | Check Ollama, model loaded |
| `KKBOT-005` | Database connection failed | Check connection string, DB health |
| `KKBOT-006` | Secret not found | Check Vault, secret path |
| `KKBOT-007` | Encryption/decryption failed | Check passphrase, salt, key version |
| `KKBOT-008` | Job execution timeout | Increase timeout, check deadlock |
| `KKBOT-009` | Instance health check failed | Check device, plugin, resources |
| `KKBOT-010` | Configuration validation failed | Check appsettings, env vars |

---

*Last updated: 2024-01-15 | Version: 1.0.0*