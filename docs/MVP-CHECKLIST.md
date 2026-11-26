# Checklist phát triển DevAgent MVP

**Mốc bàn giao** — Người dùng mô tả lỗi trên Discord và nhận mã task. Với lỗi thông thường trên repo đã đăng ký, DevAgent tự lập kế hoạch, sửa trên nhánh riêng trong sandbox, chạy build/test và review diff. Khi các kiểm tra đạt, bot mở pull request (PR) và gửi link về Discord cùng tóm tắt bản sửa và kết quả test. Con người quyết định merge trên GitHub.

**Chính sách cho pilot** — Chủ repo cấu hình trước phạm vi được sửa và các vùng nhạy cảm. Bản sửa nằm trong phạm vi tự động được đi thẳng đến PR. Thay đổi liên quan đến xác thực, phân quyền, migration, credential, CI/CD hoặc thao tác phá hủy cần người có quyền duyệt trước khi thực hiện và duyệt diff trước khi xuất bản. Khi chưa phân loại được, hệ thống dừng để người phụ trách xem xét. Triển khai production và cấp thêm quyền cho agent nằm ngoài phạm vi pilot.

**Cách dùng** — Bắt đầu ở Phase 0. Mỗi ô chỉ được đánh dấu khi có bằng chứng tương ứng, chẳng hạn tài liệu được duyệt, kết quả test, bản ghi audit hoặc liên kết PR. Các mục Phase 1 và 2 là backlog triển khai sau khi chủ dự án duyệt thiết kế.

## Phase 0 · Chốt thiết kế trước khi viết tính năng

- [ ] **0.1 Chốt bài toán pilot.** Viết yêu cầu sản phẩm ngắn, nêu người giao việc, người duyệt, repo thử nghiệm, ba kênh nhận việc (Discord, GitHub Issue, dashboard) và quyền của từng người. Chọn một lỗi có thể tái hiện bằng test, chẳng hạn bộ lọc trả về task sai trạng thái. Ghi trước hành vi mong đợi và cách kiểm tra bản sửa.
- [ ] **0.2 Vẽ kiến trúc và ranh giới dữ liệu.** Có sơ đồ ngữ cảnh và sơ đồ thành phần cho API, Worker, dashboard, PostgreSQL, sandbox, nhà cung cấp AI, GitHub và Discord. Trong modular monolith, chỉ rõ module nào sở hữu task, lần thực thi, quyền truy cập repo, quyết định duyệt và PR. Ghi đường đi của một yêu cầu qua các thành phần.
- [ ] **0.3 Duyệt mô hình domain và ERD.** Thể hiện Project–Repository, Task–Execution, Execution–ToolExecution, Task–Approval và liên kết đến PR. Phân biệt cấu hình agent (`AgentProfile`), agent đang hoạt động (`AgentInstance`) và một lần chạy (`AgentExecution`). Ghi khóa duy nhất chống trùng, quan hệ, chỉ mục cho hàng đợi/timeline, thời gian UTC và cách xử lý cập nhật đồng thời.
- [ ] **0.4 Chốt vòng đời task và phê duyệt.** Vẽ sơ đồ từ `PENDING` đến `COMPLETED`, gồm nhánh tự động và nhánh `WAITING_FOR_APPROVAL` cho kế hoạch hoặc diff nhạy cảm. Với mỗi chuyển trạng thái, ghi tác nhân, điều kiện và dữ liệu cần lưu. Quy định cách xử lý từ chối, thiếu thông tin, lỗi, chặn, thử lại, hết giờ và hủy.
- [ ] **0.5 Thiết kế luồng agent.** Vẽ cách Orchestrator điều phối lần lượt Planner, Coding, Testing và Reviewer, gồm cả nhánh build/test lỗi. Đề xuất một luồng tuần tự cho pilot. Ghi đầu vào, đầu ra, quyền dùng công cụ, điểm lưu tiến độ và điều kiện dừng của từng bước. Định nghĩa hợp đồng gọi model trong Application để có thể đổi nhà cung cấp qua cấu hình và adapter.
- [ ] **0.6 Duyệt hợp đồng API và giao diện.** Phác thảo `/api/v1/` cho đăng ký repo, tạo/xem/hủy task, xem lần chạy và duyệt/từ chối. Chốt DTO, lỗi, phân trang, khóa chống gửi trùng, sự kiện SignalR. Vẽ ba màn hình chính gồm danh sách task, chi tiết lần chạy và màn hình phê duyệt.
- [ ] **0.7 Duyệt mô hình đe dọa và hạn mức.** Xác định ranh giới tin cậy giữa người dùng, nội dung repo/issue, model, sandbox và dịch vụ giữ credentials. Chốt cách ly, chính sách mạng, quyền công cụ và thao tác cần duyệt. Điền con số cho ngân sách mỗi task, timeout, số lần thử và số task đồng thời để nhóm có thể kiểm thử.
- [ ] **0.8 Duyệt quyết định kỹ thuật và roadmap.** Ghi phiên bản tương thích cần dùng cho .NET 10, EF Core/Npgsql, PostgreSQL, React 19 và các SDK tích hợp. Đề xuất PostgreSQL giữ trạng thái và hàng đợi bền vững cho pilot. Ghi rõ nhu cầu nào sẽ kích hoạt việc dùng Redis. Chủ dự án duyệt phạm vi, sơ đồ, ERD, chính sách và các kịch bản nghiệm thu cuối tài liệu trước khi mở Phase 1.

## Phase 1 · Nền tảng chạy được từ máy sạch

- [ ] **1.1 Khởi tạo backend và frontend.** Có ASP.NET Core API, Worker dùng BackgroundService, đăng ký module tường minh và migration đầu tiên. Domain độc lập với EF, Discord, GitHub và SDK AI. Frontend dùng React, Vite, TypeScript strict, cấu trúc theo tính năng và API client chung.
- [ ] **1.2 Dựng môi trường phát triển và CI.** Docker Compose khởi động được các dịch vụ cần cho pilot. Có health check, cấu hình mẫu và nơi cấp secret riêng. GitHub Actions build backend/frontend, chạy test, kiểm tra TypeScript và áp dụng migration lên PostgreSQL mới.
- [ ] **1.3 Xác thực và phân quyền.** API kiểm tra quyền theo project/repo và hành động. Liên kết danh tính Discord/GitHub với quyền trong DevAgent. Kiểm thử người có quyền giao việc, người có quyền duyệt và người ngoài project. Áp dụng cùng chính sách cho dữ liệu đọc và sự kiện SignalR.
- [ ] **1.4 Lưu task và chạy job bền vững.** Lưu task cùng công việc cần xử lý trong một giao dịch. Worker nhận việc có thời hạn, ghi checkpoint, thử lại có giới hạn và lưu việc thất bại để người vận hành xử lý. Thử dừng Worker giữa chừng và chạy hai Worker cùng nhận việc.
- [ ] **1.5 Truy vết và kiểm thử tự động.** Log có cấu trúc bằng Serilog, trace/metric qua OpenTelemetry, liên kết theo task ID và execution ID. Che dữ liệu nhạy cảm trước khi ghi. Có test domain, API, phân quyền và tích hợp PostgreSQL. Dùng nhà cung cấp AI giả lập có kết quả cố định trong CI.

**Nghiệm thu Phase 1** — Từ checkout sạch, chạy được hệ thống và CI. Gửi lặp cùng yêu cầu chỉ sinh một task. Khởi động lại Worker vẫn xử lý được task đó và giữ lịch sử. Người ngoài project bị từ chối khi đọc task hoặc đăng ký nhận sự kiện của nó.

## Phase 2 · Triển khai theo thứ tự phụ thuộc

### 2A · Một task đi được từ API tới repo thử nghiệm

- [ ] Cài GitHub App với quyền tối thiểu, đăng ký repo theo installation và project. Lưu credential có mã hóa, cấp token ngắn hạn qua thành phần tích hợp và giữ token ngoài ngữ cảnh model.
- [ ] Cho phép người có quyền tạo task qua API và nhận task ID. Lưu nguồn yêu cầu, repo đích, base commit và khóa chống trùng. Planner đọc cấu trúc repo, xuất kế hoạch, tiêu chí kiểm tra và rủi ro. Task thiếu yêu cầu chuyển sang `NEEDS_CLARIFICATION`.
- [ ] Bộ điều phối áp dụng chính sách của repo lên kế hoạch và kiểm tra lại trước mỗi thao tác ghi. Ghi lý do cho phép đi tiếp hoặc yêu cầu duyệt. Nếu đang sửa mà cần chạm vùng nhạy cảm, dừng trước thao tác đó và gửi yêu cầu qua Discord. Gắn quyết định duyệt với người duyệt, thời điểm, phạm vi và phiên bản kế hoạch.

### 2B · Sửa code và kiểm chứng trong sandbox

- [ ] Tạo workspace riêng cho mỗi lần chạy. Container chạy non-root, giới hạn CPU/RAM/thời gian, hạn chế mạng, không gắn thư mục host hoặc Docker socket và không nhận secret production. Kiểm thử việc chặn đọc ngoài workspace, chặn kết nối ngoài chính sách và thu hồi container khi thành công, lỗi, timeout hoặc hủy task.
- [ ] Cấp các công cụ đọc/tìm/tạo/sửa file, xem diff và chạy build/test theo cấu hình repo. Mỗi công cụ kiểm tra đầu vào, quyền, đường dẫn và giới hạn, trả kết quả có cấu trúc và ghi audit. Việc xóa file cần quyền riêng và phê duyệt theo chính sách. Lệnh thực thi chỉ chạy trong sandbox.
- [ ] Coding thực hiện kế hoạch trên nhánh task. Testing lưu lệnh, mã thoát và kết quả build/test. Reviewer đọc diff cùng kết quả test, ghi phát hiện và mức độ. Build/test lỗi hoặc còn phát hiện chặn thì dừng trước bước xuất bản. Mỗi vòng sửa tiêu hao hạn mức thử lại và ngân sách.
- [ ] Ghi token, chi phí ước tính, thời lượng và công cụ đã dùng theo execution. Kiểm tra ngân sách trước lần gọi tiếp theo, giới hạn đầu ra của model và ghi lý do khi chạm trần. Hủy task phải dừng được công việc đang chạy và ngăn các bước tiếp theo.

### 2C · Từ bản thay đổi đã kiểm tra đến một PR

- [ ] Đánh giá lại chính sách trên diff thực tế trước khi push. Với thay đổi cần duyệt, hiển thị diff, base commit, kết quả build/test, phát hiện review và chi phí. Gắn quyết định duyệt với đúng commit. Nếu bản sửa thay đổi, kiểm tra lại và xin duyệt lại theo chính sách.
- [ ] Khi build/test đạt, review hết lỗi chặn và chính sách cho phép, thành phần GitHub đẩy đúng commit đã kiểm tra lên nhánh task và mở PR có liên kết về task, tóm tắt thay đổi và bằng chứng test. Chỉ chuyển task sang `COMPLETED` khi đã lưu được liên kết PR. Người có thẩm quyền thực hiện merge trên GitHub.
- [ ] Khi GitHub đã tạo PR nhưng Worker mất kết nối trước khi lưu kết quả, lần chạy lại tìm PR theo repo/nhánh task và liên kết lại. Áp dụng cùng nguyên tắc chống trùng cho webhook và thông báo.

### 2D · Hoàn thiện ba kênh và dashboard vận hành

- [ ] Discord nhận lệnh giao việc, trả task ID, hỗ trợ xem trạng thái và hủy. Gửi thông báo cần duyệt kèm liên kết đến màn hình duyệt, báo lỗi hoặc gửi link PR khi hoàn tất.
- [ ] GitHub nhận Issue theo điều kiện kích hoạt đã chốt trong Phase 0. Xác minh chữ ký webhook, kiểm tra installation/repo và quyền người kích hoạt, loại sự kiện lặp. Đồng bộ trạng thái PR và CI về task.
- [ ] Dashboard tạo task qua cùng API, hiển thị task đang chạy, hoàn tất, thất bại và chờ duyệt. Trang chi tiết có kế hoạch, timeline, tool call, lỗi, chi phí và PR. SignalR cập nhật trực tiếp, client đọc bù từ lịch sử đã lưu sau khi mất kết nối.
- [ ] Giao diện có trạng thái tải, trống, thành công và lỗi. Người dùng thao tác bàn phím được với tạo task và phê duyệt. Có test component/form và E2E cho luồng từ tạo task đến xem PR.

## Kịch bản nghiệm thu trước khi bàn giao MVP

| Đạt | Kịch bản cần chạy | Bằng chứng để chấp nhận |
| --- | --- | --- |
| ☐ | Giao qua Discord một lỗi trong phạm vi tự động, có test tái hiện trên repo pilot, chạy qua GitHub App và nhà cung cấp AI thật | Test hồi quy thất bại trên bản gốc và thành công trên bản sửa. Bot tự gửi đúng một link PR về Discord. Task có kết quả build/test, review, chi phí và lý do được phép mở PR tự động. |
| ☐ | Tạo các task độc lập từ Discord, GitHub Issue và dashboard | Mỗi kênh trả hoặc hiển thị task ID, truy được nguồn, theo dõi được tiến trình và dẫn tới đúng kết quả. |
| ☐ | Cho một task đang tự động cần sửa vùng nhạy cảm. Thử từ chối kế hoạch, từ chối diff hoặc đổi bản sửa sau khi duyệt | Worker dừng trước thao tác nhạy cảm. Khi bị từ chối hoặc thiếu phê duyệt cho đúng bản sửa, task ghi lý do và không xuất bản. |
| ☐ | Cho build/test thất bại hoặc Reviewer ghi nhận lỗi chặn | Có bằng chứng lỗi và lịch sử xử lý. PR chỉ được mở sau khi điều kiện kiểm tra và duyệt đều đạt. |
| ☐ | Gửi webhook lặp, chạy hai Worker, ngắt Worker ngay sau khi GitHub tạo PR | Sau phục hồi chỉ có một task cho cùng yêu cầu, một PR cho task và lịch sử nhất quán. |
| ☐ | Người ngoài repo truy cập, agent tự duyệt hoặc nội dung README yêu cầu dùng quyền ngoài chính sách | Quyền hiệu lực vẫn do hệ thống kiểm tra. Tác vụ trái quyền bị từ chối, audit ghi lý do và log không chứa token. |
| ☐ | Hủy giữa chừng, để task hết giờ hoặc chạm trần ngân sách | Task dừng với lý do rõ ràng, sandbox được thu hồi và không phát sinh bước xuất bản mới. |
| ☐ | Mất kết nối dashboard rồi mở lại | Timeline khôi phục đủ sự kiện theo thứ tự. Từ task ID tra được execution, tool call, kết quả áp dụng chính sách, quyết định duyệt nếu có, chi phí và PR. |

**Điều kiện phát hành pilot** — Các kịch bản trên có bằng chứng, CI xanh bằng dữ liệu giả lập xác định, và chủ dự án chấp nhận PR mẫu sau khi kiểm tra diff. Với mỗi hạng mục triển khai, ghi mục tiêu, hiện trạng, thay đổi, file ảnh hưởng, test đã chạy, kết quả và phần còn tồn đọng.

**Backlog sau MVP** — Điều phối nhiều agent độc lập, giao việc agent-to-agent, quản lý phòng ban/team, Workflow Designer, Knowledge Base, ngân sách phân cấp, triển khai production và vận hành đa tổ chức.
