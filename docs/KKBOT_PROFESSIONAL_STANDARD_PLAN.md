# KKBOT Framework - Professional Standard Implementation Plan

> **Phuong an:** KKBOT Professional Standard (khong Comet 100%, lay phan technical excellence, bo compliance conflict)
> **Muc tieu:** Professional Tool cho Studio Farm - du chong crack, chay 24/7 stable, uy tin dinh gia cao
> **License Management:** Lam ky o Phase 2 (Tier 1)

---

## PHASE 1: CORE FOUNDATION (Tuan 1-4) - CRITICAL
*Cot loi bat buoc - Chay on dinh 24/7, chong crash, tu phuc hoi*

### 1.1 License Server Foundation (Core - lam nen)
- [ ] **1.1.1** Thiet ke License Schema: LicenseKey, SeatLimit, ExpiresAt, Features[], HardwareID[], OfflineGraceDays(7), Signature(Ed25519)
- [ ] **1.1.2** License Validation Library (Client-side): Verify signature, check expiry, check seat, offline grace cache (encrypted file)
- [ ] **1.1.3** License Server API (Minimal): POST /validate, GET /features, POST /activate, POST /deactivate, GET /status
- [ ] **1.1.4** Offline Grace: Cache license local (AES-256-GCM), allow 7 days offline, auto-sync khi online
- [ ] **1.1.5** Hardware Fingerprint: Stable ID (Motherboard + MAC + Disk ID hash), khong dung IMEI/Android ID

### 1.2 Secrets & Config Management
- [ ] **1.2.1** DPAPI/Windows Credential Manager integration cho: Game credentials, Proxy auth, API keys
- [ ] **1.2.2** Atomic Config Save: Write temp -> flush -> rename (atomic), file locking (FileShare.None)
- [ ] **1.2.3** Config Schema Validation: JSON Schema Draft 2020, range/cross-field validation
- [ ] **1.2.4** Config Versioning + Migration: Auto-migrate old profile, backup truoc migrate

### 1.3 Recovery & Reliability Core
- [ ] **1.3.1** Recovery Ladder 5-Level: Retry(3) -> Recapture Perception -> Restart Task -> Restart Instance -> Manual Review
- [ ] **1.3.2** Circuit Breaker: Ollama (50% error/30s), Database (3 failures/10s), External API
- [ ] **1.3.3** Health Monitor: Process heartbeat (3s), Device connection, Perception pipeline, AI service
- [ ] **1.3.4** Orphan Process Cleanup: Kill child processes on parent exit, PID file lock
- [ ] **1.3.5** Graceful Shutdown: Signal handling (SIGTERM/SIGINT), drain jobs, save state, cleanup

### 1.4 Rate Limiting & Protection
- [ ] **1.4.1** API Rate Limiting: Token bucket (100 req/s burst, 20 req/s sustained per IP)
- [ ] **1.4.2** Ollama Rate Limit: Token bucket per model (configurable)
- [ ] **1.4.3** Per-Instance Action Throttle: Max actions/min configurable per module
- [ ] **1.4.4** Idempotency Keys: Support Idempotency-Key header cho mutations

### 1.5 Capture & Perception Hardening
- [ ] **1.5.1** Frame Drop Policy: Drop oldest when queue > 2 frames, backpressure signal
- [ ] **1.5.2** Temporal Validation: Debounce perception (3 frames confirm), debounce OCR (2 reads)
- [ ] **1.5.3** Perception Confidence Gate: Action blocked neu confidence < threshold (configurable)

---

## PHASE 2: PROFESSIONAL POLISH (Tuan 5-8) - HIGH
*Chuyen nghiep - Installer, Auto-update, Monitoring, License Management KY*

### 2.1 License Management KY (Tier 1 - Lam ky tai day)
- [ ] **2.1.1** License Server Full: PostgreSQL backend, REST API, Admin Dashboard
- [ ] **2.1.2** License Models: 
  - Trial (7 ngay, 1 seat, core features)
  - Basic (30 ngay, 5 seats, core + combat)
  - Pro (365 ngay, 20 seats, all modules)
  - Enterprise (Custom, unlimited, priority support)
- [ ] **2.1.3** License Features Matrix: Module-level gating (Combat, Quest, Economy, AntiDetection)
- [ ] **2.1.4** Seat Management: Concurrent seat tracking, auto-release on disconnect (5 min timeout)
- [ ] **2.1.5** Offline Grace Implementation Ky:
  - Encrypted cache (AES-256-GCM + Argon2id key derivation)
  - Grace period countdown UI
  - Auto-sync khi online (exponential backoff)
  - Grace expiry -> Graceful shutdown (save state, stop instances)
- [ ] **2.1.6** License Revocation: Real-time revocation push (SignalR), local cache invalidation
- [ ] **2.1.7** License Analytics: Activation heatmap, usage per feature, churn prediction
- [ ] **2.1.8** Admin Dashboard: Issue/revoke/extend licenses, view activations, export CSV

### 2.2 Installer & Distribution
- [ ] **2.2.1** InnoSetup/NSIS Installer: Silent install, custom path, start menu, uninstaller
- [ ] **2.2.2** Code Signing: EV Certificate + RFC 3161 Timestamp, Dual sign (SHA1 + SHA256)
- [ ] **2.2.3** Auto-Updater: Delta updates (bsdiff), signature verification, rollback on failure
- [ ] **2.2.3** Prerequisite Checker: .NET 10 Runtime, VC++ Redist, ADB drivers, Virtualization check
- [ ] **2.2.4** First-Run Wizard: License activation, ADB setup, Game path detection, Proxy test

### 2.3 Monitoring & Observability Pro
- [ ] **2.3.1** Structured Logging: Serilog + Seq/ELK, Correlation ID, PII redaction (credentials, IPs)
- [ ] **2.3.2** Metrics Dashboard (Grafana): Instance health, CPU/RAM/VRAM, FPS, Actions/min, Gold/h, Error rate
- [ ] **2.3.3** Alerting Rules: Instance crash > 3/h, CPU > 90%, Memory leak detection, Proxy dead > 5min
- [ ] **2.3.4** Crash Reporter: Minidump + context (instance state, last action, perception snapshot) -> auto-upload
- [ ] **2.3.5** Audit Log: Separate append-only log (License events, Config changes, Admin actions, Security events)

### 2.4 Crash Reporting & Debugging
- [ ] **2.4.1** Minidump Collection: Windows Error Reporting (WER) integration
- [ ] **2.4.2** Context Capture: Instance state, last 10 actions, last perception snapshot, config hash
- [ ] **2.4.3** Symbol Server: SourceLink + PDB upload to symbol server
- [ ] **2.4.4** Support Bundle Generator: Logs + Config (redacted) + Crash dumps + System info -> zip

---

## PHASE 3: ENTERPRISE ADD-ON (Tuan 9-12) - MEDIUM
*Cho Studio lon, Multi-tenant, SLA*

### 3.1 Multi-Tenant License Server
- [ ] **3.1.1** Organization/Team hierarchy: Root org -> Teams -> Users
- [ ] **3.1.2** RBAC: Admin / Operator / Viewer / Auditor
- [ ] **3.1.3** License Pool: Shared seat pool across team, borrowing/returning
- [ ] **3.1.4** License Transfer: Transfer seat between orgs (approval workflow)

### 3.2 Advanced License Features
- [ ] **3.2.1** Floating Licenses: Checkout/checkin model (concurrent usage)
- [ ] **3.2.2** License Borrowing: Offline checkout (max 30 days)
- [ ] **3.2.2** Feature Flags: Gradual rollout, A/B testing license features
- [ ] **3.2.4** Webhook Integration: License events -> Discord/Slack/HTTP webhook

### 3.3 Enterprise Operations
- [ ] **3.3.1** SIEM Integration: Syslog/CEF format, Splunk/ELK/Datadog
- [ ] **3.3.2** Multi-Region Deployment: License server geo-replication
- [ ] **3.3.3** SLA Dashboard: Uptime, latency, support ticket integration
- [ ] **3.3.4** Audit Trail: Immutable log (append-only, tamper-evident)

---

## PHASE 4: DEPLOYMENT & COMMERCIAL (Tuan 13-16)
*Commercial Ready*

### 4.1 Packaging & Installation
- [ ] **4.1.1** InnoSetup/NSIS Installer: Custom wizard, license agreement, component selection
- [ ] **4.1.2** Silent Install/Uninstall: CLI flags cho automation deployment
- [ ] **4.1.3** Code Signing: EV Cert + Timestamp (RFC 3161), Dual sign (SHA1 + SHA256)
- [ ] **4.1.4** SmartScreen Reputation: Build reputation qua Microsoft Partner Center

### 4.2 Runbooks & Documentation
- [ ] **4.1.1** Install/Upgrade/Rollback Runbooks (Markdown + Checklist)
- [ ] **4.1.2** Incident Runbook: Crash, License fail, Proxy dead, Game update
- [ ] **4.1.3** Backup/Restore Runbook: Config, License, Database
- [ ] **4.1.4** Emergency Stop Procedure: Kill switch toan he thong
- [ ] **4.1.5** Operator Guide: Dashboard usage, Module config, Troubleshooting
- [ ] **4.1.6** Plugin SDK Guide: API reference, Sample plugins, Best practices

### 4.2 Commercial Packaging
- [ ] **4.2.1** License Key Generator: CLI tool cho admin (kkbot license generate --tier Pro --seats 20 --days 365)
- [ ] **4.2.2** License Key Format: KKBOT-XXXX-XXXX-XXXX-XXXX (Base32, checksum)
- [ ] **4.2.2** Offline Activation: Challenge-Response (RSA-2048) cho air-gapped

---

## PHASE 5: TESTING & QUALITY GATES (Song song toan bo)

### 5.1 Test Infrastructure
- [ ] **5.1.1** Deterministic Clock/Random: TimeProvider, RandomProvider abstraction
- [ ] **5.1.2** Fake Device/Perception/AI: In-memory implementations for unit tests
- [ ] **5.1.3** Test Data Builders: Fluent builders cho Instance, Profile, License

### 5.2 Test Layers
- [ ] **5.2.1** Unit Tests: Domain invariants, State transitions, License validation, Config migration
- [ ] **5.2.2** Integration Tests: Device adapter, Capture pipeline, License validation, Plugin loader
- [ ] **5.3.1** Replay Tests: Frame fixtures -> Expected perception -> Expected action (Deterministic)
- [ ] **5.3.2** Replay Negative Cases: Low confidence, Unknown popup, Reconnect, App crash
- [ ] **5.4.1** E2E Tests: Start -> Load Profile -> Run Job -> Pause -> Resume -> Cancel -> Recovery -> Stop
- [ ] **5.4.2** Soak Test: 48h continuous, 10 instances, memory leak check
- [ ] **5.5.1** Chaos Tests: Disk full, Network partition, Process crash, AI timeout, DB lock
- [ ] **5.5.2** Stress Test: 10->50->100 instances ramp, resource exhaustion

### 5.3 Quality Gates (CI/CD)
- [ ] **5.4.1** Build + Unit + Integration pass
- [ ] **5.4.2** Static Analysis: SonarQube/CodeQL, No critical/high issues
- [ ] **5.4.3** Coverage Threshold: >80% Domain, >70% Application
- [ ] **5.4.4** Benchmark Regression: <5% performance regression
- [ ] **5.4.4** Security Scan: Dependency check, SAST, Secret scan, Container scan

---

## PRIORITY MATRIX (Executive Summary)

| Priority | Items | Effort | Blocking |
|----------|-------|--------|----------|
| **P0 - Block Release** | License Server Core, Code Signing, Installer, Recovery Ladder, Circuit Breaker, DPAPI Secrets | 4-6 tuan | Yes |
| **P1 - Professional** | License Server Full + Admin UI, Installer/Updater, Crash Reporter, Monitoring/Alerting | 4-6 tuan | No (Post-launch OK) |
| **P2 - Enterprise** | Multi-tenant, Floating License, SIEM, Multi-region | 4-6 tuan | No (Post-launch) |
| **P3 - Polish** | Runbooks, Operator Guide, Plugin SDK Guide, Telemetry | 2-3 tuan | No |

---

## LICENSE MANAGEMENT - CHI TIET KY (KHI LAM SE LAM KY)

### LICENSE MANAGEMENT ARCHITECTURE

CLIENT (KKBOT App)              |  LICENSE SERVER
License Validator Lib            |  License API
- Verify Ed25519 sig             |  - POST /validate
- Check expiry/seat              |  - POST /activate
- Offline grace cache            |  - GET /features
- Hardware fingerprint           |  - POST /deactivate
- Auto-sync (backoff)            |  - GET /status

PostgreSQL
- Licenses
- Activations
- Audit Log
- Organizations

Admin Dashboard
- Issue/Revoke
- Analytics
- Seat Heatmap

KEY TECHNICAL DECISIONS:
- Ed25519 Signature (fast, small, no padding oracle)
- AES-256-GCM for offline cache (authenticated encryption)
- Argon2id for cache key derivation (memory-hard)
- Hardware Fingerprint: SHA256(MotherboardSerial + MAC + DiskSerial)
- Offline Grace: 7 days default, configurable per tier
- Seat Release: 5 min after disconnect heartbeat loss
- Revocation: SignalR push + local cache TTL 5min
- Ed25519 Private Key: HSM/Key Vault only, never in code

KEY PRINCIPLES (KKBOT WAY)

1. License First Architecture: License validation o layer thap nhat, moi module check feature flag truoc khi chay
2. Offline-First: Cache everything, sync khi online, graceful degradation
3. Hardware-Bound: License gan hardware, khong dung account/password de share
4. Graceful Degradation: License het -> Graceful shutdown (save state) khong crash
5. Audit Everything License: Moi activation/deactivation/validation deu log audit
6. No Phone Home for Core Logic: Core automation chay offline, chi license check can online

---

Total Estimated: 16-20 weeks cho Full Professional Standard
License Management: ~6 tuan (Phase 1 core + Phase 2 full) - LAM KY TAI PHASE 2

EXECUTION ORDER (Recommended)

Week 1-2:  1.1 License Core + 1.2 Secrets + 1.3 Recovery + 1.4 Rate Limit
Week 3-4:  1.5 Perception Hardening + 5.x Test Infra + CI/CD Pipeline
Week 5-6:  2.1 License Server Full + Admin UI + 2.2 Installer + Code Signing
Week 7-8:  2.3 Monitoring/Alerting + 2.4 Crash Reporter + 2.1.7 License Analytics
Week 9-10: 3.x Enterprise Features (neu co khach Studio lon)
Week 11-12: 4.x Deployment/Runbooks + 5.x Quality Gates
Week 13-16: Soak Test 48h + Chaos Tests + Load Test + Release Gate

---

*Total Estimated: 16-20 weeks cho Full Professional Standard*
*License Management: ~6 tuan (Phase 1 core + Phase 2 full) - LAM KY TAI PHASE 2*