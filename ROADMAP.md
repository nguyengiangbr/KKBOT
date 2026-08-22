# KKBOT Roadmap

## v1.0 – Core release (target Q4 2024)
- Complete Device adapters (ADB, Simulator) with robust retry & health‑check.
- Finish Perception pipeline (OpenCV + OCR) with confidence‑based fallback.
- Implement full AI service (Ollama streaming, prompt templating, cache).
- Stabilise Orchestration: DAG scheduler, dynamic FSM generation, recovery ladder.
- Security: JWT authentication, RBAC, secret rotation, audit logging.
- Observability: Serilog, OpenTelemetry metrics & tracing, alerting.
- UI: WPF Control Center + Web Dashboard (SignalR real‑time updates, live video stream).
- Test coverage ≥ 80 % for all core layers, integration tests for end‑to‑end flows.
- CI/CD pipeline (GitHub Actions) generating Docker images for host & web dashboard.

## Future enhancements
- Plugin marketplace (publish/consume via NuGet‑style feed).
- Distributed execution (run instances on multiple worker nodes).
- Advanced AI features (function calling, multimodal reasoning).
- Mobile companion app (Flutter/React Native).
- Full‑screen streaming UI with remote control.

---

*Roadmap is a living document and will be updated as the project evolves.*