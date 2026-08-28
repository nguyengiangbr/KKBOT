# KKBOT Feature Backlog - Complete Feature Checklist

**Last Updated**: 2025-01-15
**Version**: 24.1.0
**Status**: Living document - updated each sprint

---

## LEGEND

| Status | Meaning |
|--------|---------|
| DONE | Fully implemented, tested, documented |
| IN PROGRESS | Currently being worked on |
| PLANNED | Designed, ready for implementation |
| PROPOSED | Idea stage, needs design |
| BLOCKED | Waiting on dependency |
| REFACTOR | Needs improvement |

---

## CORE INFRASTRUCTURE - COMPLETE (100%)

| Feature | Status | Location | Notes |
|---------|--------|----------|-------|
| Plugin Architecture (AssemblyLoadContext) | DONE | KKBOT.Plugins.Loader | Hot-reload, isolation |
| WASM Plugin Sandbox (Wasmtime) | DONE | KKBOT.Plugins.Wasm | 20 capabilities, Rust SDK |
| Device Abstraction (ADB) | DONE | KKBOT.Device.Adb | Screenshot, input, shell |
| Device Abstraction (Simulator) | DONE | KKBOT.Device.Simulator | DSL v2, frame replay |
| Perception Pipeline (OpenCV) | DONE | KKBOT.Perception.OpenCv | Template matching, OCR |
| AI Integration (Ollama) | DONE | KKBOT.AI.Ollama | Text + Vision, Streaming |
| Orchestration (DAG, FSM, Saga) | DONE | KKBOT.Orchestration | Full workflow engine |
| EventStore (Snapshots, Projections) | DONE | KKBOT.Orchestration | v1 to v2 upcasting |
| Visual Scripting (18 nodes) | DONE | KKBOT.VisualScripting | Canvas editor |
| Multi-Agent Coordination | DONE | KKBOT.Agent.Coordination | Registry, MessageBus, Consensus |
| Observability (OTel, Serilog, Prometheus) | DONE | KKBOT.Observability | Full stack |
| Security (JWT, RBAC, WASM sandbox) | DONE | KKBOT.Security | Capability-based |
| Visual Scenario Editor | DONE | KKBOT.UI.Dashboard | Blazor canvas |
| Performance (L2 Cache, Connection Pool) | DONE | KKBOT.Performance | L1/L2, pooling |
| WASM Plugin Sandbox | DONE | KKBOT.Plugins.Wasm | Wasmtime, Rust SDK |

---

## GAME-SPECIFIC MODULES - 3/7 DONE

### MODULE 1: Account & Auto-Login Engine (KKBOT.Plugin.AccountLogin)

| Feature | Status | Implementation Details |
|---------|--------|------------------------|
| Auto Login/Relogin | DONE | Credential injection, form filling |
| Server/Channel/Character Selection | DONE | UI automation via perception |
| Security Screen Detection | DONE | PIN, OTP, Captcha via Perception + AI Vision |
| Crash Recovery | DONE | Process monitoring, PID tracking, auto-restart |
| Heartbeat Monitoring | DONE | Periodic health checks |
| Auto-Relogin | DONE | Random delay, full relogin flow |
| Multi-Account Management | DONE | Concurrent account handling |

Implementation: src/KKBOT.Plugin.AccountLogin/AccountLoginModule.cs

---

### MODULE 3: Resource Optimizer (KKBOT.Plugin.ResourceOptimizer)

| Feature | Status | Implementation Details |
|---------|--------|------------------------|
| FPS Limiter | DONE | 5-10 background, 30 combat, 5 idle |
| Render Stripping | DONE | 3D, particles, shadows, UI (configurable) |
| RAM Cleaner | DONE | Periodic, threshold-based, aggressive mode |
| Window Grid Layout | DONE | Tile, minimize 800x600/480x320, System Tray |
| Resource Monitoring | DONE | CPU/GPU/RAM/FPS, auto FPS adjustment |
| Auto FPS Adjustment | DONE | Based on CPU usage |

Implementation: src/KKBOT.Plugin.ResourceOptimizer/ResourceOptimizerModule.cs

---

### MODULE 7: Anti-Detection & Alerts (KKBOT.Plugin.AntiDetection)

| Feature | Status | Implementation Details |
|---------|--------|------------------------|
| Humanizer Engine | DONE | 100-350ms delays, +/-2-5px jitter, Bezier curves |
| Telegram Bot Alerts | DONE | Death, VIP drop, Disconnect, Low HP, Level up |
| Remote Telegram Control | DONE | /status, /pause_all, /restart_game, /screenshot |
| Human-like Mouse Movement | DONE | Bezier curves with variance |
| Random Action Intervals | DONE | 500-2000ms, pause probability 15% |

Implementation: src/KKBOT.Plugin.AntiDetection/AntiDetectionModule.cs

---

## MODULE 2: Network & Proxy Binding (P0 - NEXT)

| Feature | Status | Priority | Dependencies | Effort |
|---------|--------|----------|--------------|--------|
| Proxy Binding per Instance | PLANNED | P0 | Device Abstraction | 3 days |
| HTTP/SOCKS5 Proxy Support | PLANNED | P0 | Network lib | 2 days |
| Proxy Authentication (User/Pass) | PLANNED | P0 | - | 1 day |
| Network Isolation Layer | PLANNED | P0 | OS-level networking | 5 days |
| WebRTC Leak Prevention | PLANNED | P0 | Browser automation | 3 days |
| Direct Socket Leak Prevention | PLANNED | P0 | Socket hooks | 2 days |
| Auto Rotate Proxy | PLANNED | P0 | Proxy API integration | 3 days |
| Dead Proxy Detection | PLANNED | P0 | Health checks | 2 days |
| Auto Failover to Backup Proxy | PLANNED | P0 | Proxy pool | 2 days |
| IP Checker / Validation | PLANNED | P0 | External API | 1 day |
| Per-Instance Proxy Config UI | PROPOSED | P1 | Dashboard | 3 days |

Technical Design Needed:
- Proxy configuration schema (per-instance)
- Network namespace isolation strategy
- Proxy pool management (rotation, health, failover)
- Integration with IDeviceSessionFactory
- Proxy health check protocol

---

## MODULE 4: Logistics & Survival (P1)

| Feature | Status | Priority | Dependencies | Effort |
|---------|--------|----------|--------------|--------|
| Auto Heal/Buff | PLANNED | P1 | Module 1, Perception | 5 days |
| HP/MP % Threshold Healing | PLANNED | P1 | Perception (HP/MP bars) | 2 days |
| Multiple Potion Types | PLANNED | P1 | Inventory detection | 2 days |
| Skill Buff Cooldown Management | PLANNED | P1 | Buff detection | 2 days |
| Auto Refill & Repair | PLANNED | P1 | Module 1, Navigation | 8 days |
| Town Portal / Recall | PLANNED | P1 | Navigation, Perception | 3 days |
| NPC Interaction (Merchant/Blacksmith) | PLANNED | P1 | Dialog detection, OCR | 3 days |
| Auto Buy Potions/Items | PLANNED | P1 | Shop UI detection | 2 days |
| Auto Repair Equipment | PLANNED | P1 | Durability detection | 2 days |
| Return to Grind Spot | PLANNED | P1 | Navigation, Coordinates | 2 days |
| Smart Inventory & Filter | PLANNED | P1 | OCR, Perception | 6 days |
| Attribute/Value/Class Filter | PLANNED | P1 | OCR on item tooltips | 3 days |
| Auto-Equip Better Gear | PLANNED | P1 | Stat comparison | 2 days |
| Auto-Sell Trash/Junk | PLANNED | P1 | Vendor detection, OCR | 2 days |
| Auto-Destroy Worthless Items | PROPOSED | P2 | Configurable rules | 2 days |

Technical Design Needed:
- Inventory grid detection (OCR + template)
- Item tooltip parsing (OCR + regex)
- NPC dialog state machine
- Pathfinding back to grind spot
- Configurable item filter rules engine

---

## MODULE 5: Combat & Quest Engine (P1)

| Feature | Status | Priority | Dependencies | Effort |
|---------|--------|----------|--------------|--------|
| Skill Combo Engine | PLANNED | P1 | Module 1, Perception, AI | 10 days |
| Skill Definition & Config | PLANNED | P1 | Config schema | 2 days |
| Combo Order & Chaining | PLANNED | P1 | Skill sequencer | 3 days |
| Cooldown Management | PLANNED | P1 | Skill tracker | 2 days |
| Range / AoE Detection | PLANNED | P1 | Perception (range circles) | 2 days |
| Priority Targeting (Boss > Elite > Normal) | PLANNED | P1 | Enemy classification | 2 days |
| Hit & Run (Ranged Classes) | PLANNED | P1 | Movement + combat | 3 days |
| Anti-KS / Change Channel | PLANNED | P1 | Module 2 (channel), Perception | 4 days |
| Player Detection (KS risk) | PLANNED | P1 | Player detection | 2 days |
| Auto Channel Hop | PLANNED | P1 | Module 2 | 2 days |
| Safe Zone Coordinates | PLANNED | P1 | Config | 1 day |
| Auto Daily Quest | PLANNED | P1 | Module 1, Navigation | 10 days |
| Main Quest 1 to N Automation | PLANNED | P1 | Quest tracker, OCR | 5 days |
| Daily/Weekly Quest Routines | PLANNED | P1 | Quest detector | 3 days |
| Dungeon/Guild Quest Support | PROPOSED | P2 | Party, Instance detection | 5 days |
| Party Controller (Master-Slave) | PLANNED | P1 | Multi-Agent Coordination | 8 days |
| Follow Master (Pathfinding) | PLANNED | P1 | Pathfinding, Perception | 3 days |
| Accept Party Invite | PLANNED | P1 | Dialog detection | 1 day |
| Assist/Buff Master | PLANNED | P1 | Target tracking, Skills | 2 days |
| Formation Keeping | PROPOSED | P2 | Formation logic | 3 days |

Technical Design Needed:
- Skill configuration DSL (JSON/YAML)
- Combat state machine (engage, retreat, kite)
- Quest state tracking (accept, progress, complete)
- Party communication protocol (via MessageBus)
- Formation/positioning algorithms

---

## MODULE 6: Economy & Trading (P2)

| Feature | Status | Priority | Dependencies | Effort |
|---------|--------|----------|--------------|--------|
| Auto Vending / Shop | PLANNED | P2 | Module 4, Navigation | 8 days |
| Market Navigation | PLANNED | P2 | Navigation | 2 days |
| Shop Setup (Open stall) | PLANNED | P2 | UI automation | 2 days |
| Item Pricing (item_prices.json) | PLANNED | P2 | Config + Market API | 2 days |
| 24/7 Vending Mode | PLANNED | P2 | Background service | 2 days |
| Master Accumulator | PLANNED | P2 | Module 1, Module 2 | 6 days |
| Threshold-based Transfer | PLANNED | P2 | Config thresholds | 2 days |
| Trade Window Automation | PLANNED | P2 | Trade UI detection | 2 days |
| Mail/Postal Transfer | PLANNED | P2 | Mail UI detection | 2 days |
| Return to Grind After Transfer | PLANNED | P2 | Navigation | 1 day |

Technical Design Needed:
- Market price fetching (API or OCR)
- Trade window state machine
- Secure transfer verification
- Economic policy engine (thresholds, limits)

---

## TECHNICAL DEBT & INFRASTRUCTURE IMPROVEMENTS

### Performance Optimization

| Task | Status | Priority | Effort |
|------|--------|----------|--------|
| EF Core Compiled Queries | PLANNED | P1 | 3 days |
| Memory Pooling / Object Reuse | PLANNED | P1 | 3 days |
| Async Optimization / Cancellation Propagation | PLANNED | P1 | 2 days |
| Profiling & Bottleneck Detection | PROPOSED | P2 | 3 days |
| Redis Pipeline / Batching | PROPOSED | P2 | 2 days |
| Object Pooling (Frames, Detections) | PROPOSED | P2 | 2 days |

### Developer Experience

| Task | Status | Priority | Effort |
|------|--------|----------|--------|
| CLI Tool (kkbot CLI) | PROPOSED | P1 | 5 days |
| VS Code Extension | PROPOSED | P2 | 10 days |
| Local Dev Environment (Mock Services) | PROPOSED | P1 | 5 days |
| Time-Travel Debugging | PROPOSED | P2 | 10 days |
| Plugin Template Generator | PROPOSED | P1 | 3 days |

### Advanced AI Pipeline (Post-Modules)

| Task | Status | Priority | Effort |
|------|--------|----------|--------|
| Model Distillation (Teacher-Student) | PROPOSED | P2 | 6 weeks |
| A/B Testing Framework | PROPOSED | P2 | 4 weeks |
| Feature Store for ML | PROPOSED | P2 | 6 weeks |
| Automated Retraining on Drift | PROPOSED | P2 | 6 weeks |
| Sim2Real Training Pipeline | PROPOSED | P1 | 8 weeks |
| Federated Learning | PROPOSED | P2 | 8 weeks |

---

## TESTING GAPS

| Area | Current Coverage | Target | Gap |
|------|------------------|--------|-----|
| Unit Tests | 70% | 85% | +15% |
| Integration Tests | 60% | 80% | +20% |
| Contract Tests | 40% | 70% | +30% |
| Chaos Tests | 10% | 50% | +40% |
| Performance Tests | 20% | 60% | +40% |
| Visual Regression | 0% | 50% | +50% |

Missing Test Suites:
- Module 1: AccountLogin integration tests
- Module 3: ResourceOptimizer load tests
- Module 7: AntiDetection behavior tests
- Coordination: Consensus partition tests
- Coordination: Leader failover tests
- Plugin: Hot-reload stress tests
- Scenario DSL: Execution correctness tests

---

## PACKAGING & DISTRIBUTION

| Task | Status | Priority | Effort |
|------|--------|----------|--------|
| NuGet Package Publishing (CI/CD) | PLANNED | P1 | 2 days |
| Plugin Marketplace Backend | PROPOSED | P2 | 4 weeks |
| Plugin Signing/Verification Pipeline | PLANNED | P1 | 1 week |
| WASM Plugin SDK Documentation | PLANNED | P1 | 1 week |
| Rust SDK Crates.io Publishing | PROPOSED | P2 | 1 week |

---

## DOCUMENTATION GAPS

| Document | Status | Priority |
|----------|--------|----------|
| API Reference (OpenAPI/Swagger) | DONE | - |
| Plugin Development Guide | DONE | - |
| GameAgent Framework Spec | DONE | - |
| Multi-Agent Coordination Spec | DONE | - |
| Architecture Decision Records (ADRs) | PLANNED | P1 |
| Troubleshooting Guide | PLANNED | P1 |
| Performance Tuning Guide | PROPOSED | P2 |
| Security Hardening Guide | PROPOSED | P1 |
| Migration Guides (v23 to v24, v24 to v25) | PROPOSED | P2 |

---

## SECURITY HARDENING

| Task | Status | Priority |
|------|--------|----------|
| Dependency Scanning (Dependabot) | DONE | - |
| SAST/DAST in CI/CD | PLANNED | P1 |
| Penetration Testing | PROPOSED | P2 |
| Secret Scanning (GitLeaks/TruffleHog) | PLANNED | P1 |
| SBOM Generation | PLANNED | P1 |
| CVE Scanning in CI | PLANNED | P1 |

---

## IMPLEMENTATION TRACKER

### Sprint Planning (Next 4 Sprints)

| Sprint | Focus | Deliverables |
|--------|-------|--------------|
| Sprint 1 | Module 2: Network & Proxy | Proxy binding, isolation, rotation |
| Sprint 2 | Module 4: Logistics & Survival | Heal, buff, refill, inventory |
| Sprint 3 | Module 5: Combat & Quest | Skill combo, quest, party |
| Sprint 4 | Module 6: Economy & Trading | Vending, accumulator |

### Definition of Done (Per Feature)

- Unit tests >= 85% coverage
- Integration tests passing
- Documentation updated
- CHANGELOG.md updated
- Code review approved
- CI/CD pipeline green
- Performance benchmarks met
- Security scan passed

---

## PRIORITY MATRIX (RICE Scoring)

| Feature | Reach | Impact | Confidence | Effort | RICE Score | Priority |
|---------|-------|--------|------------|--------|------------|----------|
| Module 2: Proxy Binding | 90% | 10 | 80% | 13 | 554 | P0 |
| Module 4: Auto Heal/Buff | 100% | 9 | 85% | 8 | 956 | P1 |
| Module 4: Auto Refill | 90% | 8 | 80% | 8 | 720 | P1 |
| Module 5: Skill Combo | 95% | 10 | 75% | 10 | 712 | P1 |
| Module 5: Auto Quest | 90% | 8 | 75% | 10 | 540 | P1 |
| Module 2: Auto Proxy Rotation | 80% | 9 | 70% | 10 | 504 | P0 |
| Module 4: Smart Inventory | 70% | 7 | 60% | 6 | 294 | P1 |
| Module 5: Party Controller | 60% | 8 | 65% | 8 | 390 | P1 |
| Module 6: Auto Vending | 50% | 7 | 60% | 8 | 262 | P2 |
| Module 6: Master Accumulator | 40% | 8 | 50% | 6 | 133 | P2 |
| AI Distillation | 40% | 9 | 50% | 30 | 60 | P2 |
| Sim2Real Pipeline | 30% | 10 | 40% | 40 | 30 | P2 |

---

## HOW TO USE THIS LIST

1. Sprint Planning: Pick from P0/P1 items based on capacity
2. Daily Standup: Update status (PLANNED to IN PROGRESS to DONE)
3. Sprint Review: Move completed to DONE, add new PROPOSED items
4. Retrospective: Review RICE scores, adjust priorities

---

## UPDATE LOG

| Date | Version | Changes |
|------|---------|---------|
| 2025-01-15 | 1.0 | Initial comprehensive backlog created |

---

This is a living document. Update status during daily standups, sprint planning, and retrospectives.
Last updated: 2025-01-15 | Version: 24.1.0