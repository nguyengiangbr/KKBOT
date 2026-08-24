# KKBOT Framework — AUDIT_CHECKLIST.md

> **Mục đích:** Checklist kiểm toán toàn diện cho một framework tự động hóa ứng dụng/game trong môi trường được phép kiểm thử hoặc tự động hóa.
>
> **Phạm vi an toàn:** Không bao gồm bypass anti-cheat, né ban, giả mạo fingerprint/IMEI/MAC, vượt CAPTCHA, injection, đọc/ghi bộ nhớ trái phép, packet manipulation hoặc tự động hóa trái điều khoản dịch vụ.
>
> **Cách sử dụng:** Đánh dấu `[x]` khi đã hoàn thành và có bằng chứng. Để `[ ]` nếu chưa làm. Mỗi mục nên ghi `Evidence`, `Owner`, `Date`, `Risk` hoặc liên kết tới commit/PR/test/report.
>
> **Quy ước trạng thái:**
> - `[ ]` Chưa làm
> - `[~]` Đang làm / một phần
> - `[x]` Đã làm và có bằng chứng
> - `[N/A]` Không áp dụng, phải ghi lý do
>
> **Định nghĩa hoàn thành:** Không đánh dấu `[x]` chỉ vì code đã tồn tại. Một mục chỉ đạt khi có code, test/kiểm tra phù hợp, tài liệu, log hoặc artifact chứng minh.

---

## 0. Thông tin audit

- [ ] Tên dự án và mã phiên bản được ghi rõ.
- [ ] Commit/tag/branch được audit được ghi rõ.
- [ ] Ngày audit được ghi rõ.
- [ ] Người thực hiện audit được ghi rõ.
- [ ] Mục tiêu release được ghi rõ.
- [ ] Môi trường audit được ghi rõ: OS, CPU, RAM, GPU, .NET SDK.
- [ ] Phạm vi audit được ghi rõ.
- [ ] Những phần loại khỏi phạm vi được ghi rõ.
- [ ] Danh sách rủi ro đã biết được ghi rõ.
- [ ] Link tới build artifact, test report và benchmark được ghi rõ.
- [ ] Mọi mục `N/A` đều có lý do.

**Evidence:**

- Commit/tag:
- Environment:
- Auditor:
- Date:
- Notes:

---

## 1. Pháp lý, đạo đức và phạm vi được phép

- [ ] Ứng dụng/game mục tiêu thuộc quyền sở hữu hoặc có giấy phép kiểm thử.
- [ ] Điều khoản sử dụng đã được đọc và lưu bản tham chiếu.
- [ ] Các hành động tự động hóa được phép đã được liệt kê.
- [ ] Các hành động bị cấm đã được liệt kê và bị chặn trong policy engine.
- [ ] Không có tính năng bypass anti-cheat.
- [ ] Không có tính năng né ban hoặc tránh phát hiện.
- [ ] Không thay đổi IMEI, Android ID, MAC hoặc device fingerprint.
- [ ] Không vượt CAPTCHA hoặc cơ chế xác minh người dùng.
- [ ] Không injection DLL, hook trái phép hoặc sửa process.
- [ ] Không đọc/ghi bộ nhớ ứng dụng trái phép.
- [ ] Không can thiệp packet hoặc giao thức ngoài quyền cho phép.
- [ ] Không tự động hóa giao dịch/tài sản nếu điều khoản cấm.
- [ ] Có cơ chế `SafeStop` khi policy không rõ ràng.
- [ ] Có quy trình tiếp nhận khiếu nại và yêu cầu dừng sử dụng.
- [ ] Có người chịu trách nhiệm compliance.
- [ ] Có đánh giá dữ liệu cá nhân và quyền riêng tư.

**Evidence:**

- Terms/policy reference:
- Approved action scope:
- Prohibited action tests:
- Compliance owner:

---

## 2. Mục tiêu sản phẩm và yêu cầu

### 2.1. Product requirements

- [ ] Có Product Requirements Document.
- [ ] Có user personas.
- [ ] Có user journeys.
- [ ] Có danh sách use case.
- [ ] Có non-functional requirements.
- [ ] Có giới hạn sản phẩm rõ ràng.
- [ ] Có tiêu chí thành công đo được.
- [ ] Có ma trận tính năng theo module.
- [ ] Có phân loại MVP, post-MVP và enterprise.
- [ ] Có yêu cầu tương thích Windows.
- [ ] Có yêu cầu offline/local.
- [ ] Có yêu cầu triển khai nhiều máy nếu cần.
- [ ] Có yêu cầu backup/restore.
- [ ] Có yêu cầu audit và compliance.

### 2.2. Acceptance criteria

- [ ] Mỗi use case có acceptance criteria.
- [ ] Acceptance criteria có điều kiện thành công.
- [ ] Acceptance criteria có điều kiện thất bại.
- [ ] Acceptance criteria có timeout.
- [ ] Acceptance criteria có hành vi khi mất kết nối.
- [ ] Acceptance criteria có hành vi khi perception không chắc chắn.
- [ ] Acceptance criteria có test tương ứng.

**Evidence:**

- PRD:
- Requirements matrix:
- Acceptance test report:

---

## 3. Kiến trúc tổng thể

- [ ] Có tài liệu kiến trúc cấp hệ thống.
- [ ] Có sơ đồ context.
- [ ] Có sơ đồ container/component.
- [ ] Có sơ đồ runtime.
- [ ] Có sơ đồ dữ liệu.
- [ ] Có sơ đồ deployment.
- [ ] Có sơ đồ luồng command/query/event.
- [ ] Có ghi rõ boundary giữa UI, API, application, domain và infrastructure.
- [ ] Có ghi rõ dependency direction.
- [ ] Có ADR cho các quyết định quan trọng.
- [ ] Có threat model.
- [ ] Có capacity model.
- [ ] Có kế hoạch modular monolith trước khi tách service.
- [ ] Có tiêu chí khi nào được tách service.
- [ ] Không có dependency vòng.
- [ ] Không có UI gọi trực tiếp device adapter.
- [ ] Không có plugin gọi trực tiếp database.
- [ ] Không có domain phụ thuộc framework UI.
- [ ] Không có AI model tự thực thi command tự do.

### 3.1. Ngăn xếp công nghệ

- [ ] C# là ngôn ngữ runtime chính.
- [ ] .NET SDK/runtime đã được pin.
- [ ] Chính sách hỗ trợ phiên bản .NET đã được xác minh.
- [ ] WPF hoặc WinUI 3 đã được chọn và có lý do.
- [ ] ASP.NET Core đã được chọn cho API/dashboard.
- [ ] SignalR đã được chọn cho realtime.
- [ ] OpenCvSharp chỉ xuất hiện sau abstraction.
- [ ] OCR engine được đóng gói sau abstraction.
- [ ] Ollama adapter chỉ dùng qua interface.
- [ ] SQLite được dùng cho giai đoạn local.
- [ ] PostgreSQL migration plan tồn tại.
- [ ] C++/Rust chỉ được dùng sau benchmark.
- [ ] Python bị giới hạn trong `research/` và tooling.

**Evidence:**

- Architecture document:
- ADR index:
- Dependency graph:
- Threat model:

---

## 4. Repository và quản lý mã nguồn

- [ ] Repository có README dễ bắt đầu.
- [ ] Có CONTRIBUTING.md.
- [ ] Có LICENSE phù hợp.
- [ ] Có CODE_OF_CONDUCT nếu cần.
- [ ] Có SECURITY.md.
- [ ] Có CHANGELOG.md.
- [ ] Có issue template.
- [ ] Có pull request template.
- [ ] Có branch strategy.
- [ ] Có commit convention.
- [ ] Có review policy.
- [ ] Có CODEOWNERS.
- [ ] Có `.editorconfig`.
- [ ] Có `.gitignore` đầy đủ.
- [ ] Không commit `bin/`, `obj/`, `.vs/`, secrets hoặc artifact lớn.
- [ ] Không commit password, token, certificate private key.
- [ ] Đã quét lịch sử Git tìm secret.
- [ ] Dependency lock/version policy tồn tại.
- [ ] Tag release được ký nếu cần.
- [ ] Mọi commit release có trace tới issue/requirement.

**Evidence:**

- Repository URL:
- Branch/tag:
- Secret scan report:
- Review policy:

---

## 5. Cấu trúc solution và project

- [ ] `KKBOT.sln` hoặc solution tương đương tồn tại.
- [ ] `global.json` tồn tại và hợp lệ.
- [ ] `Directory.Build.props` tồn tại.
- [ ] `Directory.Packages.props` tồn tại nếu dùng central package management.
- [ ] Target framework nhất quán.
- [ ] Nullable reference types bật.
- [ ] Implicit usings được kiểm soát.
- [ ] Warnings-as-errors được xem xét.
- [ ] Analyzer được bật.
- [ ] Domain project không phụ thuộc UI.
- [ ] Application project không phụ thuộc WPF.
- [ ] Contracts không chứa implementation.
- [ ] Infrastructure không bị tham chiếu ngược bởi Domain.
- [ ] Test project tách rõ unit/integration/stress.
- [ ] Research không được import vào runtime.
- [ ] Project naming thống nhất.
- [ ] Folder naming thống nhất.
- [ ] Namespace khớp project.
- [ ] Không có file “God class”.
- [ ] Không có thư mục `Misc` chứa code không phân loại.

**Evidence:**

- Solution tree:
- Project dependency graph:
- Build configuration:

---

## 6. Domain layer

### 6.1. Entity và value object

- [ ] `Instance` được định nghĩa rõ.
- [ ] `Device` được định nghĩa rõ.
- [ ] `Module` được định nghĩa rõ.
- [ ] `Profile` được định nghĩa rõ.
- [ ] `Job` được định nghĩa rõ.
- [ ] `TaskRun` được định nghĩa rõ.
- [ ] `StateTransition` được định nghĩa rõ.
- [ ] `PerceptionSnapshot` được định nghĩa rõ.
- [ ] `HealthSnapshot` được định nghĩa rõ.
- [ ] `RecoveryAttempt` được định nghĩa rõ.
- [ ] `AuditEvent` được định nghĩa rõ.
- [ ] `Operator` và quyền được định nghĩa rõ.
- [ ] ID có kiểu riêng hoặc quy ước nhất quán.
- [ ] Invariant được bảo vệ trong domain.
- [ ] Enum không dùng string tùy tiện cho trạng thái quan trọng.
- [ ] Thời gian dùng UTC hoặc quy ước rõ ràng.
- [ ] Không dùng mutable public collection.
- [ ] Không cho phép trạng thái không hợp lệ.

### 6.2. Trạng thái

- [ ] Instance states đầy đủ.
- [ ] Job states đầy đủ.
- [ ] Task states đầy đủ.
- [ ] Có `Unknown`.
- [ ] Có `SafeStop`.
- [ ] Có `ManualReview`.
- [ ] Có `RequiresApproval`.
- [ ] Có transition table.
- [ ] Transition bất hợp lệ bị từ chối.
- [ ] Transition được audit.

### 6.3. Error model

- [ ] Có catalog error code.
- [ ] Error có severity.
- [ ] Error có retryability.
- [ ] Error có public message và technical detail.
- [ ] Error có correlation ID.
- [ ] Secret không xuất hiện trong error.
- [ ] Có mapping lỗi domain/application/infrastructure.

### 6.4. Test domain

- [ ] Unit test cho invariant.
- [ ] Unit test cho state transition.
- [ ] Unit test cho error mapping.
- [ ] Unit test cho time boundary.
- [ ] Unit test cho cancellation semantics nếu domain liên quan.

**Evidence:**

- Domain project:
- Transition matrix:
- Test report:

---

## 7. Application layer

- [ ] Có command/query separation rõ ràng.
- [ ] Có use case start instance.
- [ ] Có use case stop instance.
- [ ] Có use case pause/resume.
- [ ] Có use case run job.
- [ ] Có use case cancel job.
- [ ] Có use case load/save profile.
- [ ] Có use case validate module.
- [ ] Có use case recover instance.
- [ ] Có use case query status.
- [ ] Có use case query history.
- [ ] Có use case replay scenario.
- [ ] Có authorization trước hành động nhạy cảm.
- [ ] Có idempotency cho command cần thiết.
- [ ] Có transaction boundary rõ.
- [ ] Có timeout boundary rõ.
- [ ] Có cancellation propagation.
- [ ] Không trả domain entity trực tiếp ra API/UI.
- [ ] DTO được version hóa khi cần.
- [ ] Có validation đầu vào.
- [ ] Có audit cho command quan trọng.
- [ ] Có test cho success/failure/cancellation/authorization.

**Evidence:**

- Application service map:
- Command/query list:
- Test report:

---

## 8. Contracts và API model

- [ ] API contracts nằm trong project riêng.
- [ ] DTO không chứa implementation detail.
- [ ] Contract có version strategy.
- [ ] Enum serialization ổn định.
- [ ] Date/time serialization thống nhất.
- [ ] Error response có schema.
- [ ] Pagination được chuẩn hóa.
- [ ] Filtering/sorting được giới hạn.
- [ ] Correlation ID được truyền qua request.
- [ ] Idempotency key được hỗ trợ nơi cần.
- [ ] API không trả secret.
- [ ] OpenAPI/Swagger được cập nhật.
- [ ] Breaking change có migration note.
- [ ] Contract test tồn tại.

---

## 9. Device abstraction và adapter

### 9.1. Interface

- [ ] Có `IDeviceSession`.
- [ ] Có device discovery abstraction.
- [ ] Có application lifecycle abstraction.
- [ ] Có screenshot/capture abstraction.
- [ ] Có input dispatch abstraction cho môi trường được phép.
- [ ] Có health check abstraction.
- [ ] Có capability model.
- [ ] Có device identity ổn định.
- [ ] Có timeout cho mọi I/O.
- [ ] Có cancellation cho mọi I/O.
- [ ] Có typed result/error.
- [ ] Có disposal rõ ràng.

### 9.2. ADB/desktop adapter

- [ ] Command runner không nối chuỗi lệnh thiếu kiểm soát.
- [ ] Argument được validate.
- [ ] Process tree được quản lý.
- [ ] Stdout/stderr được xử lý.
- [ ] Exit code được kiểm tra.
- [ ] Timeout process được xử lý.
- [ ] Device disconnect được phát hiện.
- [ ] Reconnect có giới hạn.
- [ ] Không retry vô hạn.
- [ ] Có graceful shutdown.
- [ ] Có test thiết bị không tồn tại.
- [ ] Có test lệnh timeout.
- [ ] Có test process crash.

### 9.3. Simulator

- [ ] Có fake device session.
- [ ] Có replay frame provider.
- [ ] Có fake input dispatcher.
- [ ] Có failure injection.
- [ ] Có simulated disconnect.
- [ ] Có simulated timeout.
- [ ] Có simulated app crash.
- [ ] Có deterministic clock.
- [ ] Có scenario runner.
- [ ] Test không cần thiết bị thật.

---

## 10. Capture và frame pipeline

- [ ] Có `Frame` model.
- [ ] Frame có ID.
- [ ] Frame có timestamp.
- [ ] Frame có resolution.
- [ ] Frame có source/device ID.
- [ ] Có validation format.
- [ ] Có giới hạn kích thước frame.
- [ ] Có pooling nếu benchmark cần.
- [ ] Không ghi frame tạm xuống disk trong hot path nếu không cần.
- [ ] Có backpressure.
- [ ] Có frame drop policy.
- [ ] Có bounded queue.
- [ ] Có shutdown khi pipeline dừng.
- [ ] Có metrics capture latency.
- [ ] Có metrics dropped frames.
- [ ] Có test frame hỏng.
- [ ] Có test queue đầy.
- [ ] Có test cancellation.

---

## 11. Perception và computer vision

### 11.1. Abstraction

- [ ] Có `IPerceptionProvider`.
- [ ] Có detector interface.
- [ ] Có OCR interface.
- [ ] Có template repository interface.
- [ ] Có ROI definition.
- [ ] Có coordinate mapping.
- [ ] Có model/version metadata.
- [ ] Có confidence score.
- [ ] Có evidence frame reference.
- [ ] Có detector timeout.
- [ ] Có cancellation.

### 11.2. OpenCV adapter

- [ ] OpenCvSharp chỉ nằm trong adapter.
- [ ] Native Mat được dispose đúng.
- [ ] Không rò unmanaged memory.
- [ ] Template được validate.
- [ ] Kích thước template được kiểm tra.
- [ ] Color space được thống nhất.
- [ ] Scale/DPI được xử lý.
- [ ] ROI không vượt biên.
- [ ] Threshold có cấu hình.
- [ ] Matching có benchmark.
- [ ] Có temporal validation.
- [ ] Có debounce.
- [ ] Có fallback khi template không tồn tại.
- [ ] Có test positive/negative.
- [ ] Có test resolution khác nhau.
- [ ] Có test DPI khác nhau.

### 11.3. OCR

- [ ] OCR có interface thay thế.
- [ ] Có preprocessing theo vùng.
- [ ] Có whitelist ký tự khi phù hợp.
- [ ] Có normalization.
- [ ] Có confidence.
- [ ] Có validation số/range.
- [ ] Có retry có giới hạn.
- [ ] Không tin một kết quả OCR đơn lẻ cho hành động nguy hiểm.
- [ ] Có test chữ mờ.
- [ ] Có test font/scale.
- [ ] Có benchmark.

### 11.4. VLM/Ollama

- [ ] Ollama client có timeout.
- [ ] Ollama client có cancellation.
- [ ] Model name được cấu hình.
- [ ] Prompt/schema được version hóa.
- [ ] Response được validate bằng schema.
- [ ] JSON lỗi không làm crash worker.
- [ ] VLM không có quyền chạy command trực tiếp.
- [ ] VLM chỉ được gọi theo trigger.
- [ ] Có rate limit.
- [ ] Có circuit breaker.
- [ ] Có privacy policy cho frame.
- [ ] Có fallback `Unknown/SafeStop`.
- [ ] Có replay test cho response lỗi.

---

## 12. Workflow, FSM và task runtime

- [ ] Có state definition độc lập.
- [ ] Có transition table.
- [ ] Có entry/exit behavior.
- [ ] Có precondition.
- [ ] Có success condition.
- [ ] Có failure condition.
- [ ] Có timeout từng state.
- [ ] Có timeout từng task.
- [ ] Có retry policy.
- [ ] Có backoff.
- [ ] Có circuit breaker.
- [ ] Có cancellation.
- [ ] Có pause/resume semantics.
- [ ] Có checkpoint nếu task dài.
- [ ] Có idempotency.
- [ ] Có evidence khi fail.
- [ ] Có action verification sau thao tác.
- [ ] Không có loop vô hạn không guard.
- [ ] Không dùng `Thread.Sleep`.
- [ ] Có deterministic tests.
- [ ] Có test unknown state.
- [ ] Có test repeated failure.
- [ ] Có test cancellation giữa action.
- [ ] Có test shutdown trong mọi state.

### 12.1. Safe action policy

- [ ] Mọi action được whitelist.
- [ ] Action có precondition.
- [ ] Action có timeout.
- [ ] Action có postcondition.
- [ ] Action có audit khi cần.
- [ ] Action bị chặn nếu perception confidence thấp.
- [ ] Action bị chặn khi policy deny.
- [ ] Có emergency stop.
- [ ] Có operator approval cho hành động nhạy cảm.

---

## 13. Scheduling và job system

- [ ] Có job model.
- [ ] Có queue bounded.
- [ ] Có priority.
- [ ] Có concurrency limit.
- [ ] Có resource requirement.
- [ ] Có schedule one-shot.
- [ ] Có schedule định kỳ.
- [ ] Có time window.
- [ ] Có maintenance window.
- [ ] Có cancellation.
- [ ] Có retry policy.
- [ ] Có dead-letter hoặc failed queue.
- [ ] Có idempotency.
- [ ] Có job lease/ownership nếu nhiều worker.
- [ ] Có duplicate prevention.
- [ ] Có graceful shutdown.
- [ ] Có recovery khi worker chết.
- [ ] Có lịch sử job.
- [ ] Có test queue đầy.
- [ ] Có test duplicate.
- [ ] Có test worker crash.
- [ ] Có test clock/time zone.

---

## 14. Instance orchestration và resource management

- [ ] Có instance registry.
- [ ] Có lifecycle manager.
- [ ] Có heartbeat.
- [ ] Có lease/ownership nếu cần.
- [ ] Có device assignment.
- [ ] Có profile assignment.
- [ ] Có module assignment.
- [ ] Có resource budget.
- [ ] Có CPU limit policy.
- [ ] Có memory limit policy.
- [ ] Có VRAM monitoring nếu cần.
- [ ] Có per-instance queue.
- [ ] Có isolation giữa instance.
- [ ] Có graceful stop.
- [ ] Có forced stop với cảnh báo.
- [ ] Có orphan process cleanup.
- [ ] Có startup recovery.
- [ ] Có shutdown recovery.
- [ ] Có instance-level correlation ID.
- [ ] Không dùng global mutable state.
- [ ] Có test nhiều instance.
- [ ] Có test instance crash.
- [ ] Có test resource exhaustion.
- [ ] Có test shutdown toàn hệ thống.

### 14.1. Capacity planning

- [ ] Có benchmark 1 instance.
- [ ] Có benchmark 5 instances.
- [ ] Có benchmark 10 instances.
- [ ] Có benchmark tải mục tiêu.
- [ ] Có benchmark soak test.
- [ ] Có p50/p95/p99 latency.
- [ ] Có memory baseline.
- [ ] Có memory growth analysis.
- [ ] Có frame drop threshold.
- [ ] Có CPU/GPU threshold.
- [ ] Có kết luận capacity thực tế.
- [ ] Không tuyên bố số instance vượt benchmark.

---

## 15. Recovery và self-correction

### 15.1. Health monitoring

- [ ] Có process health.
- [ ] Có device health.
- [ ] Có application health.
- [ ] Có capture health.
- [ ] Có perception health.
- [ ] Có worker heartbeat.
- [ ] Có queue health.
- [ ] Có database health.
- [ ] Có AI service health.
- [ ] Có health snapshot.

### 15.2. Recovery ladder

- [ ] Level 1 retry operation.
- [ ] Level 2 recapture/re-evaluate.
- [ ] Level 3 restart task.
- [ ] Level 4 restart application.
- [ ] Level 5 reconnect device session.
- [ ] Level 6 manual review.
- [ ] Level 7 stop instance.
- [ ] Mỗi level có giới hạn.
- [ ] Có exponential/backoff nếu phù hợp.
- [ ] Có cooldown.
- [ ] Có evidence.
- [ ] Có audit.
- [ ] Có circuit breaker.
- [ ] Recovery không tạo vòng lặp vô hạn.
- [ ] Recovery không thay đổi fingerprint để né kiểm soát.
- [ ] Recovery không vượt xác minh người dùng.

### 15.3. Self-correction

- [ ] Tách lỗi cấu hình khỏi lỗi code.
- [ ] Tách lỗi perception khỏi lỗi device.
- [ ] Tách lỗi transient khỏi lỗi permanent.
- [ ] AI chỉ đề xuất, policy engine quyết định.
- [ ] Tự sửa config có version.
- [ ] Tự sửa config có diff.
- [ ] Tự sửa config có rollback.
- [ ] Giới hạn số lần tự sửa.
- [ ] Có operator approval khi cần.
- [ ] Có test self-correction thất bại.

---

## 16. Plugin SDK và mở rộng đa ứng dụng

- [ ] Có `IGameModule` hoặc contract tương đương.
- [ ] Có module manifest.
- [ ] Có module ID.
- [ ] Có module version.
- [ ] Có minimum engine version.
- [ ] Có supported platform.
- [ ] Có capabilities.
- [ ] Có assets manifest.
- [ ] Có configuration schema.
- [ ] Có detector registration.
- [ ] Có task registration.
- [ ] Có state registration.
- [ ] Có validation hook.
- [ ] Có compatibility check.
- [ ] Có module enable/disable.
- [ ] Có module rollback.
- [ ] Có checksum/signature.
- [ ] Có plugin load failure handling.
- [ ] Plugin không được tùy ý chạy process hệ thống.
- [ ] Plugin không được truy cập secret ngoài scope.
- [ ] Plugin không được sửa database schema trực tiếp.
- [ ] Có sample plugin.
- [ ] Có plugin template generator.
- [ ] Có plugin documentation.
- [ ] Có replay fixture cho sample plugin.
- [ ] Có test load/unload/version mismatch.

### 16.1. Quy trình thêm ứng dụng mới

- [ ] Có intake checklist.
- [ ] Có scope được phép.
- [ ] Có UI inventory.
- [ ] Có screen/asset inventory.
- [ ] Có coordinate mapping.
- [ ] Có detector plan.
- [ ] Có state diagram.
- [ ] Có task matrix.
- [ ] Có recovery matrix.
- [ ] Có replay data.
- [ ] Có acceptance tests.
- [ ] Có performance baseline.
- [ ] Có release note.

---

## 17. Profile, configuration và secrets

### 17.1. Configuration

- [ ] Có schema cho cấu hình.
- [ ] Có version cấu hình.
- [ ] Có default profile.
- [ ] Có validation.
- [ ] Có range validation.
- [ ] Có cross-field validation.
- [ ] Có environment override.
- [ ] Có profile import/export.
- [ ] Có profile diff.
- [ ] Có profile rollback.
- [ ] Có atomic save.
- [ ] Có file locking hoặc concurrency control.
- [ ] Có migration giữa version.
- [ ] Cấu hình lỗi không làm crash toàn hệ thống.
- [ ] Cấu hình không chứa secret plaintext.

### 17.2. Secrets

- [ ] Secret dùng Windows Credential Manager/DPAPI/secret store.
- [ ] Không log secret.
- [ ] Không trả secret qua API.
- [ ] Có rotation procedure.
- [ ] Có revoke procedure.
- [ ] Có access audit.
- [ ] Có redaction middleware.
- [ ] Có secret scan trong CI.
- [ ] Secret production khác development.
- [ ] Không hard-code license/private key.

---

## 18. Persistence và database

### 18.1. Data model

- [ ] Có ERD.
- [ ] Có migration strategy.
- [ ] Có primary key strategy.
- [ ] Có foreign key.
- [ ] Có unique constraint.
- [ ] Có index cho query chính.
- [ ] Có created/updated timestamps.
- [ ] Có optimistic concurrency nếu cần.
- [ ] Có soft delete policy nếu cần.
- [ ] Có retention policy.
- [ ] Có audit retention policy.
- [ ] Có dữ liệu nhạy cảm classification.

### 18.2. SQLite

- [ ] SQLite migration chạy được từ database trống.
- [ ] Có backup trước migration.
- [ ] Có transaction.
- [ ] Có busy timeout.
- [ ] Có xử lý database lock.
- [ ] Có integrity check.
- [ ] Có test restart.
- [ ] Có test corruption handling.

### 18.3. PostgreSQL readiness

- [ ] Có abstraction không phụ thuộc SQLite.
- [ ] Có test provider khác nếu cần.
- [ ] Không dùng SQL đặc thù SQLite trong domain.
- [ ] Có PostgreSQL schema plan.
- [ ] Có backup/restore test.
- [ ] Có connection pool settings.
- [ ] Có migration rollback strategy.
- [ ] Có retention/partition plan nếu dữ liệu lớn.

### 18.4. Repository

- [ ] Query không kéo toàn bộ bảng không cần thiết.
- [ ] Có pagination.
- [ ] Có cancellation.
- [ ] Có timeout.
- [ ] Có transaction boundary.
- [ ] Có retry chỉ cho lỗi transient.
- [ ] Không retry lỗi constraint.
- [ ] Có integration tests.

---

## 19. ASP.NET Core API và SignalR

### 19.1. API

- [ ] Có authentication.
- [ ] Có authorization.
- [ ] Có RBAC.
- [ ] Có request validation.
- [ ] Có global exception handler.
- [ ] Có ProblemDetails/error schema.
- [ ] Có rate limiting.
- [ ] Có CORS policy cụ thể.
- [ ] Có HTTPS policy.
- [ ] Có request size limit.
- [ ] Có timeout.
- [ ] Có health endpoints.
- [ ] Có OpenAPI.
- [ ] Có API versioning strategy.
- [ ] Có audit cho mutation.
- [ ] Có integration tests.

### 19.2. SignalR

- [ ] Có hub contract.
- [ ] Có group strategy.
- [ ] Có authorization trên hub.
- [ ] Có reconnect handling.
- [ ] Có event ordering strategy.
- [ ] Có duplicate event handling.
- [ ] Có backpressure strategy.
- [ ] Có payload size limit.
- [ ] Có heartbeat.
- [ ] Có stale client cleanup.
- [ ] Có test disconnect/reconnect.
- [ ] Có test nhiều client.

---

## 20. WPF control center

### 20.1. MVVM

- [ ] View không chứa business logic.
- [ ] ViewModel không gọi ADB trực tiếp.
- [ ] Command có cancellation.
- [ ] Async command không block UI thread.
- [ ] Observable state thread-safe.
- [ ] Binding errors được theo dõi.
- [ ] Navigation state rõ ràng.
- [ ] Dependency injection được dùng có kiểm soát.
- [ ] Có design-time data hoặc mock.

### 20.2. Màn hình

- [ ] Dashboard tổng quan.
- [ ] Instance grid.
- [ ] Instance detail.
- [ ] Device management.
- [ ] Module manager.
- [ ] Profile editor.
- [ ] Job scheduler.
- [ ] Perception viewer.
- [ ] Logs viewer.
- [ ] Error/ManualReview queue.
- [ ] Health/metrics view.
- [ ] Settings.
- [ ] Backup/restore.
- [ ] About/version/license.
- [ ] Emergency stop.

### 20.3. UX quality

- [ ] Loading state.
- [ ] Empty state.
- [ ] Error state.
- [ ] Offline state.
- [ ] Confirmation cho hành động nguy hiểm.
- [ ] Undo/rollback nơi phù hợp.
- [ ] Keyboard accessibility.
- [ ] High DPI support.
- [ ] Dark/light theme nếu cần.
- [ ] Localization strategy.
- [ ] Không freeze UI khi chạy nhiều instance.
- [ ] Có UI automation smoke test nếu cần.

---

## 21. Observability

### 21.1. Logging

- [ ] Structured logging.
- [ ] Correlation ID.
- [ ] Instance ID.
- [ ] Device ID.
- [ ] Job ID.
- [ ] Task ID.
- [ ] Module ID/version.
- [ ] State transition log.
- [ ] Recovery log.
- [ ] Audit log riêng.
- [ ] PII redaction.
- [ ] Secret redaction.
- [ ] Log level policy.
- [ ] Log rotation.
- [ ] Log retention.
- [ ] Không log frame nhạy cảm mặc định.

### 21.2. Metrics

- [ ] Running instances.
- [ ] Failed instances.
- [ ] Job success/failure.
- [ ] Task timeout.
- [ ] Recovery count.
- [ ] Capture latency p50/p95/p99.
- [ ] Perception latency.
- [ ] OCR latency.
- [ ] Queue depth.
- [ ] Frame drops.
- [ ] CPU/RAM/VRAM.
- [ ] Database latency.
- [ ] API latency.
- [ ] SignalR connections.
- [ ] Error count by code.

### 21.3. Tracing

- [ ] Dashboard command trace.
- [ ] Application service trace.
- [ ] Workflow trace.
- [ ] Device operation trace.
- [ ] Perception trace.
- [ ] Database trace.
- [ ] External local AI trace.
- [ ] Trace sampling policy.
- [ ] Sensitive data không nằm trong span.

### 21.4. Alerting

- [ ] Worker mất heartbeat.
- [ ] Recovery vượt ngưỡng.
- [ ] CPU quá cao.
- [ ] RAM tăng bất thường.
- [ ] Queue tăng liên tục.
- [ ] Database lỗi.
- [ ] AI service không phản hồi.
- [ ] Module incompatibility.
- [ ] Audit/policy violation.

---

## 22. Security

- [ ] Threat model được cập nhật.
- [ ] Attack surface inventory.
- [ ] Principle of least privilege.
- [ ] Authentication.
- [ ] Authorization.
- [ ] RBAC.
- [ ] Session expiration.
- [ ] Refresh token policy.
- [ ] Secret management.
- [ ] Input validation.
- [ ] Path traversal protection.
- [ ] Command injection protection.
- [ ] Deserialization an toàn.
- [ ] Plugin validation.
- [ ] File permission policy.
- [ ] Local API binding policy.
- [ ] HTTPS khi có remote access.
- [ ] Rate limiting.
- [ ] Audit trail.
- [ ] Data encryption at rest nếu cần.
- [ ] Backup encryption.
- [ ] Dependency vulnerability scan.
- [ ] Static analysis.
- [ ] SBOM được tạo.
- [ ] Security incident procedure.
- [ ] Disclosure/contact process.

### 22.1. Privacy

- [ ] Data inventory.
- [ ] Data minimization.
- [ ] Frame retention policy.
- [ ] OCR text retention policy.
- [ ] Account credential handling.
- [ ] User consent nếu thu thập dữ liệu.
- [ ] Export/delete procedure.
- [ ] Redaction trong support bundle.

---

## 23. Testing strategy

### 23.1. Test foundation

- [ ] Test naming convention.
- [ ] Test fixture convention.
- [ ] Deterministic clock.
- [ ] Deterministic random source.
- [ ] Fake device.
- [ ] Fake perception.
- [ ] Fake clock.
- [ ] Test data builder.
- [ ] No test phụ thuộc production secret.

### 23.2. Unit tests

- [ ] Domain invariants.
- [ ] State transitions.
- [ ] Retry policy.
- [ ] Timeout policy.
- [ ] Cancellation.
- [ ] Scheduler priority.
- [ ] Configuration validation.
- [ ] Coordinate mapping.
- [ ] Confidence policy.
- [ ] Error mapping.
- [ ] Authorization policy.
- [ ] Profile migration.

### 23.3. Integration tests

- [ ] Device adapter.
- [ ] Capture pipeline.
- [ ] OpenCV adapter.
- [ ] OCR adapter.
- [ ] Ollama adapter mock.
- [ ] SQLite.
- [ ] ASP.NET API.
- [ ] SignalR.
- [ ] Plugin loader.
- [ ] Recovery coordinator.

### 23.4. Replay tests

- [ ] Có frame fixture hợp lệ.
- [ ] Có expected perception.
- [ ] Có expected state.
- [ ] Có expected action.
- [ ] Có negative cases.
- [ ] Có low-confidence cases.
- [ ] Có unknown popup cases.
- [ ] Có reconnect case.
- [ ] Có app crash case.
- [ ] Replay chạy deterministic.

### 23.5. End-to-end tests

- [ ] Start instance.
- [ ] Load profile.
- [ ] Run job.
- [ ] Pause/resume.
- [ ] Cancel.
- [ ] Recovery.
- [ ] Manual review.
- [ ] Stop all.
- [ ] Restart application.
- [ ] Restore database.

### 23.6. Stress/soak/chaos

- [ ] Stress theo tải tăng dần.
- [ ] Soak test nhiều giờ.
- [ ] Memory leak test.
- [ ] CPU saturation test.
- [ ] Queue overload test.
- [ ] Device disconnect test.
- [ ] Database lock test.
- [ ] AI timeout test.
- [ ] Plugin exception test.
- [ ] Disk full simulation.
- [ ] Process crash simulation.
- [ ] Network interruption test.
- [ ] Graceful shutdown test.

### 23.7. Quality gates

- [ ] Build pass.
- [ ] Unit tests pass.
- [ ] Integration tests pass.
- [ ] No critical static analysis issue.
- [ ] Coverage threshold đạt mục tiêu.
- [ ] No high vulnerability.
- [ ] Benchmark đạt SLO.
- [ ] Manual smoke test pass.
- [ ] Release candidate được sign-off.

---

## 24. Performance và tối ưu

- [ ] Có performance goals.
- [ ] Có benchmark harness.
- [ ] Có baseline trước tối ưu.
- [ ] Có profiling CPU.
- [ ] Có profiling memory.
- [ ] Có profiling native allocation.
- [ ] Có profiling capture.
- [ ] Có profiling perception.
- [ ] Có queue latency measurement.
- [ ] Có GC analysis.
- [ ] Có object pooling chỉ khi cần.
- [ ] Có giới hạn concurrency.
- [ ] Có backpressure.
- [ ] Có batch processing khi phù hợp.
- [ ] Có frame throttling.
- [ ] Có resource budget.
- [ ] Không tối ưu bằng phỏng đoán.
- [ ] C++/Rust chỉ được thêm sau benchmark.
- [ ] Native component có fallback managed.
- [ ] Native component có ABI/version test.
- [ ] Có regression benchmark.

---

## 25. Deployment và vận hành

### 25.1. Environments

- [ ] Development.
- [ ] Test.
- [ ] Staging.
- [ ] Production.
- [ ] Config tách theo environment.
- [ ] Secret tách theo environment.
- [ ] Database tách theo environment.
- [ ] Logging level tách theo environment.

### 25.2. Packaging

- [ ] Self-contained publish.
- [ ] Runtime dependency list.
- [ ] Installer.
- [ ] Uninstaller.
- [ ] Version display.
- [ ] File integrity/checksum.
- [ ] Code signing nếu cần.
- [ ] Firewall/network requirements.
- [ ] Prerequisite checker.
- [ ] First-run setup.
- [ ] Configuration migration.
- [ ] Backup trước upgrade.
- [ ] Rollback.
- [ ] Offline installation nếu cần.

### 25.3. Operations

- [ ] Install runbook.
- [ ] Upgrade runbook.
- [ ] Rollback runbook.
- [ ] Backup runbook.
- [ ] Restore runbook.
- [ ] Incident runbook.
- [ ] Device troubleshooting.
- [ ] Module troubleshooting.
- [ ] Database troubleshooting.
- [ ] Log collection procedure.
- [ ] Support bundle không chứa secret.
- [ ] Emergency stop procedure.
- [ ] Data deletion procedure.

---

## 26. License, entitlement và thương mại hóa

- [ ] Mô hình kinh doanh được xác định.
- [ ] EULA/Terms of Service của sản phẩm được soạn.
- [ ] Privacy Policy được soạn.
- [ ] License server scope được xác định.
- [ ] Offline grace period được xác định.
- [ ] Device activation policy được xác định.
- [ ] Seat/instance quota được xác định.
- [ ] Subscription expiry behavior được xác định.
- [ ] Revocation behavior an toàn.
- [ ] Clock tampering policy được xem xét.
- [ ] License secret không nằm trong client.
- [ ] License server có rate limiting.
- [ ] License event có audit.
- [ ] Có support/admin console.
- [ ] Có refund/cancel procedure.
- [ ] Có telemetry consent.
- [ ] Có dữ liệu billing tối thiểu.
- [ ] License failure không làm mất dữ liệu local.
- [ ] Có test offline/expired/revoked/clock skew.

---

## 27. Documentation

- [ ] README quick start.
- [ ] Architecture guide.
- [ ] Developer setup.
- [ ] Coding standards.
- [ ] Contribution guide.
- [ ] Plugin SDK guide.
- [ ] Profile schema guide.
- [ ] API guide.
- [ ] Deployment guide.
- [ ] Operator guide.
- [ ] Troubleshooting guide.
- [ ] Security guide.
- [ ] Backup/restore guide.
- [ ] Release guide.
- [ ] Changelog.
- [ ] ADR index.
- [ ] Glossary.
- [ ] Diagrams cập nhật.
- [ ] Ví dụ minimal chạy được.
- [ ] Mọi tài liệu khớp code hiện tại.

---

## 28. 24-step verification matrix

> Điền tên step thực tế của dự án vào cột đầu nếu 24 step của bạn có tên khác.

| Step | Nội dung | Code | Test | Docs | Evidence | Status |
|---:|---|---|---|---|---|---|
| 1 | Repository và solution setup | [ ] | [ ] | [ ] | | [ ] |
| 2 | Build configuration và package management | [ ] | [ ] | [ ] | | [ ] |
| 3 | Domain models và invariants | [ ] | [ ] | [ ] | | [ ] |
| 4 | Application commands/queries | [ ] | [ ] | [ ] | | [ ] |
| 5 | Contracts và error model | [ ] | [ ] | [ ] | | [ ] |
| 6 | Workflow/FSM | [ ] | [ ] | [ ] | | [ ] |
| 7 | Task runtime/cancellation/timeout | [ ] | [ ] | [ ] | | [ ] |
| 8 | Scheduler và job queue | [ ] | [ ] | [ ] | | [ ] |
| 9 | Device abstraction | [ ] | [ ] | [ ] | | [ ] |
| 10 | Simulator và failure injection | [ ] | [ ] | [ ] | | [ ] |
| 11 | ADB/device adapter | [ ] | [ ] | [ ] | | [ ] |
| 12 | Capture/frame pipeline | [ ] | [ ] | [ ] | | [ ] |
| 13 | Perception abstraction | [ ] | [ ] | [ ] | | [ ] |
| 14 | OpenCV/template detector | [ ] | [ ] | [ ] | | [ ] |
| 15 | OCR adapter | [ ] | [ ] | [ ] | | [ ] |
| 16 | Ollama/VLM adapter có kiểm soát | [ ] | [ ] | [ ] | | [ ] |
| 17 | Instance orchestration/resource budget | [ ] | [ ] | [ ] | | [ ] |
| 18 | Recovery/health/manual review | [ ] | [ ] | [ ] | | [ ] |
| 19 | Plugin SDK/module loader | [ ] | [ ] | [ ] | | [ ] |
| 20 | Profile/configuration/versioning | [ ] | [ ] | [ ] | | [ ] |
| 21 | Persistence SQLite/PostgreSQL plan | [ ] | [ ] | [ ] | | [ ] |
| 22 | ASP.NET Core/SignalR/WPF | [ ] | [ ] | [ ] | | [ ] |
| 23 | Observability/security/testing | [ ] | [ ] | [ ] | | [ ] |
| 24 | Packaging/commercial readiness/release | [ ] | [ ] | [ ] | | [ ] |

---

## 29. Release gate

### 29.1. Blockers — phải bằng 0

- [ ] Không có lỗi build.
- [ ] Không có test critical thất bại.
- [ ] Không có secret trong repository hoặc artifact.
- [ ] Không có high/critical security vulnerability chưa có exception.
- [ ] Không có data-loss bug đã biết.
- [ ] Không có deadlock/race condition critical đã biết.
- [ ] Có emergency stop.
- [ ] Có backup/restore đã kiểm thử.
- [ ] Có migration đã kiểm thử.
- [ ] Có rollback plan.
- [ ] Có policy ngăn hành động ngoài phạm vi cho phép.

### 29.2. Sign-off

- [ ] Engineering sign-off.
- [ ] QA sign-off.
- [ ] Security sign-off.
- [ ] Product sign-off.
- [ ] Operations sign-off.
- [ ] Compliance sign-off nếu cần.

**Release decision:**

- [ ] GO
- [ ] GO WITH EXCEPTIONS
- [ ] NO-GO

**Exceptions:**

| ID | Mục chưa đạt | Risk | Owner | Deadline | Mitigation | Approved by |
|---|---|---|---|---|---|---|
| | | | | | | |

---

## 30. Findings và backlog

| ID | Category | Severity | Finding | Location | Impact | Recommendation | Owner | Status |
|---|---|---|---|---|---|---|---|---|
| | | | | | | | | |

### Severity definitions

- **Critical:** Có thể gây mất dữ liệu, chiếm quyền, crash toàn hệ thống hoặc vi phạm phạm vi nghiêm trọng.
- **High:** Ảnh hưởng lớn tới ổn định, bảo mật, khả năng mở rộng hoặc release.
- **Medium:** Cần sửa trước production nhưng không chặn toàn bộ development.
- **Low:** Cải tiến chất lượng, UX hoặc maintainability.
- **Informational:** Ghi nhận, chưa cần hành động ngay.

---

## 31. Báo cáo kết luận

### Điểm mạnh

- 
- 
- 

### Khoảng trống quan trọng nhất

- 
- 
- 

### Ba rủi ro lớn nhất

1. 
2. 
3. 

### Mười việc cần làm tiếp theo

1. 
2. 
3. 
4. 
5. 
6. 
7. 
8. 
9. 
10. 

### Kết luận sẵn sàng thương mại hóa

- [ ] Chưa sẵn sàng — prototype.
- [ ] Chưa sẵn sàng — internal alpha.
- [ ] Chưa sẵn sàng — closed beta.
- [ ] Có thể pilot có kiểm soát.
- [ ] Có thể phát hành production.

**Lý do:**

