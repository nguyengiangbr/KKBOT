# PROMPT KIỂM THỬ KKBOT — COMPREHENSIVE QUALITY AUDIT

Bạn là Senior/Fellow Software Quality Engineer, Principal Architect và Security Reviewer. Hãy kiểm thử source KKBOT hiện tại một cách có hệ thống, dựa trên source thật, không đoán chức năng chưa tồn tại.

## Nhiệm vụ bắt buộc
1. Đọc AGENTS.md, README.md, docs/ARCHITECTURE.md, docs/CURRENT_STATE.md và file checklist HTML/Markdown nếu có.
2. Liệt kê solution/project/file thực tế.
3. Xác định branch, commit, target framework, SDK và package versions.
4. Chạy build/test/analyzer bằng command thật.
5. Kiểm thử theo từng nhóm dưới đây.
6. Với mỗi mục, đưa ra PASS, FAIL, BLOCKED/REVIEW hoặc N/A.
7. Không đánh dấu PASS nếu chỉ thấy code tồn tại mà chưa có test hoặc bằng chứng.
8. Không sửa code trong vòng audit đầu tiên.
9. Tạo report có đường dẫn file, class/method, line nếu có, command, output và artifact.
10. Sau khi audit xong, lập backlog sửa lỗi theo Critical → High → Medium → Low.

## Quy tắc màu/trạng thái
- PASS: màu xanh. Code/test/bằng chứng đạt.
- FAIL: màu đỏ. Có lỗi hoặc thiếu bắt buộc; cần code/test lại.
- BLOCKED/REVIEW: màu vàng. Không thể kiểm thử, thiếu môi trường, hoặc cần người có quyền quyết định.
- PENDING: màu xám. Chưa có kết quả.
- N/A: màu xám; bắt buộc ghi lý do.

## Lệnh kiểm tra tối thiểu
Tùy cấu trúc source, hãy chạy các lệnh tương ứng và lưu output:
- dotnet --info
- dotnet restore
- dotnet build --configuration Release
- dotnet test --configuration Release
- dotnet format --verify-no-changes
- dotnet list package --vulnerable --include-transitive
- git status --short
- git rev-parse HEAD
- git branch --show-current

Không chạy lệnh phá dữ liệu. Không dùng secret production. Không gửi dữ liệu nhạy cảm ra ngoài.

## Ma trận kiểm thử
### A. Build và repository
- Restore, Release build, unit tests, format, analyzer, vulnerability scan, secret scan, CI reproducibility.

### B. Kiến trúc
- Layer direction, dependency cycles, domain isolation, application boundary, contracts, ADR/docs consistency.

### C. Domain/Application
- Entity invariants, invalid transitions, error model, idempotency, authorization, cancellation propagation.

### D. Workflow/FSM/Scheduler
- Happy path, Unknown/SafeStop, timeout, retry/backoff, circuit breaker, pause/resume/cancel, priority, bounded queue, duplicate jobs, graceful shutdown.

### E. Device/Process/Capture
- Discovery, disconnect, app lifecycle, command timeout, disposal, frame validation, queue backpressure, simulator/replay, action whitelist.

### F. Perception/AI
- ROI/template validation, confidence threshold, temporal validation, OpenCV disposal, OCR validation, Ollama schema/timeout/rate limit/fallback/privacy, latency benchmark.

### G. Orchestration/capacity
- Instance isolation, heartbeat, resource budget, orphan cleanup, load tests, soak tests, emergency stop.

### H. Recovery/manual review
- Health checks, recovery ladder, limits, evidence, manual review queue, restart persistence.

### I. Plugin SDK
- Manifest, compatibility, isolation, asset versioning, load failure, enable/disable/rollback, sample plugin, new-module checklist.

### J. Configuration
- Schema, migration, atomic save, diff/rollback, secret storage, environment separation.

### K. Persistence
- SQLite migration, transactions, locking, indexes/query plan, backup/restore, PostgreSQL readiness, retention/deletion.

### L. WPF
- MVVM boundary, UI responsiveness, dashboard grid, detail view, profile editor, manual review, error/loading/offline states, DPI/accessibility.

### M. API/SignalR
- Authentication, RBAC, validation/errors, rate limits, hub authorization, reconnect/order/duplicate handling, payload limits, integration tests.

### N. Security/privacy
- Threat model, secrets, command/path injection, plugin trust, audit, PII/frame redaction, dependency scan, incident procedure.

### O. Observability
- Structured logs, redaction, metrics, tracing, health endpoints, alerts.

### P. Release/commercial readiness
- Unit/integration/replay/stress/chaos, installer, upgrade/rollback, support bundle, release sign-off.

## Tiêu chí PASS
Một mục chỉ PASS khi:
- Có implementation phù hợp.
- Có test hoặc kiểm tra thực tế phù hợp.
- Kết quả pass được ghi lại.
- Không có lỗi Critical/High liên quan.
- Tài liệu không mâu thuẫn với code.

## Output bắt buộc
Tạo các file:
1. `reports/KKBOT_TEST_REPORT.md`
2. `reports/KKBOT_FINDINGS.md`
3. `reports/KKBOT_TEST_RESULTS.json`
4. `reports/KKBOT_TEST_DASHBOARD.html`

HTML phải có:
- Tổng số mục.
- Số PASS màu xanh.
- Số FAIL màu đỏ.
- Số BLOCKED/REVIEW màu vàng.
- Số PENDING/N/A màu xám.
- Thanh tiến độ.
- Bộ lọc theo trạng thái và severity.
- Tìm kiếm theo ID/file/chức năng.
- Bảng gồm ID, nhóm, chức năng, severity, trạng thái, evidence, file/line, test command, finding, recommendation.
- Dữ liệu trong HTML phải khớp JSON/Markdown.
- Không dùng màu chỉ là tín hiệu duy nhất; luôn ghi chữ PASS/FAIL.
- Có nút in/lưu PDF.

## Schema JSON
Mỗi test item:
{
  "id": "WF-003",
  "category": "Workflow",
  "name": "Task timeout",
  "severity": "Critical",
  "status": "PASS|FAIL|BLOCKED|PENDING|NA",
  "file": "path/to/file.cs",
  "line": 0,
  "test_command": "dotnet test ...",
  "evidence": "command output, test name, commit or artifact",
  "finding": "",
  "recommendation": "",
  "owner": "",
  "next_action": ""
}

## Format finding
- ID:
- Severity:
- Status:
- File/line:
- Evidence:
- Impact:
- Root cause:
- Recommended fix:
- Required test:
- Regression risk:
- Owner:

## Quy tắc kết thúc
- Không tự sửa code trong audit đầu tiên.
- Nếu build fail, tiếp tục phân tích tĩnh phần còn lại nhưng đánh dấu các test phụ thuộc build là BLOCKED.
- Nếu không thể chạy một test do thiếu môi trường, ghi rõ lý do, không đánh dấu PASS.
- Không kết luận “sẵn sàng thương mại hóa” nếu còn Critical/High chưa xử lý.
- Cuối report phải có: executive summary, top 10 findings, 10 việc tiếp theo, release decision GO/GO WITH EXCEPTIONS/NO-GO.
