# Giới thiệu code dự án "Đuổi hình bắt chữ"

Tài liệu để **học code** và **thuyết trình**. Số liệu bên dưới đo thật bằng Roslyn
ngày 2026-10-05 (không ước lượng); tên file, tên hàm đều có thật trong repo.

---

## 1. Giới thiệu trong 30 giây

> Game đoán chữ từ hình trên Windows, viết bằng **C# / WPF**. Có **chế độ một người**
> (lưu tiến trình, bảng xếp hạng) và **chế độ hai người qua mạng LAN** với hai kiểu
> chơi: *Thi đấu* (ai ghép chữ nhanh hơn) và *Đố nhau* (luân phiên ra đề). Phần
> nhiều người chơi do **máy chủ ASP.NET Core + SignalR** điều khiển: máy chủ giữ đáp
> án, tự đo thời gian và chấm điểm, nên không ai gian lận được bằng cách sửa app.

Ba câu nên nhớ:

1. **Kiến trúc 3 dự án**: `Core` (luật chơi + dữ liệu, không phụ thuộc giao diện) /
   `Server` (đấu mạng) / app WPF (giao diện).
2. **Máy chủ là trọng tài**: client chỉ gửi đáp án lên, máy chủ chấm và phát kết quả.
3. **Dữ liệu một chỗ**: SQLite + EF Core, kể cả ảnh câu đố nằm trong DB.

---

## 2. Độ phức tạp của dự án (số liệu thật)

| | File | Dòng code | Kiểu (class/record…) | Hàm |
|---|---:|---:|---:|---:|
| App WPF (giao diện, MVVM) | 24 | 2.769 | 33 | 183 |
| Core (luật chơi, dữ liệu) | 22 | 954 | 42 | 58 |
| Server (SignalR) | 4 | 588 | 5 | 50 |
| **Tổng C#** | **50** | **4.311** | **80** | **291** |

Thêm: **2.943 dòng XAML** (5 cửa sổ + `App.xaml` + 3 file theme/style), 52 câu đố, 5 bảng DB,
8 bước migration, 77 commit từ 04/09 đến 05/10/2026. Khoảng **40% dòng .cs là chú
thích và dòng trống** (code giải thích "vì sao", không chỉ "làm gì").

### Tổng số dòng toàn dự án (các file git theo dõi, đo 05/10/2026)

| Loại | File | Dòng thô | Dòng code thực* |
|---|---:|---:|---:|
| **C# viết tay** (App + Core + Server) | 50 | 7.261 | **4.311** |
| **XAML** (giao diện + theme) | 9 | 2.943 | **2.525** |
| **→ Code game viết tay (C# + XAML)** | **59** | **10.204** | **6.836** |
| C# EF Migrations (công cụ tự sinh) | 17 | 2.697 | 2.027 |
| Cấu hình (`csproj`, `slnx`, `appsettings`…) | 8 | 237 | 191 |
| Dữ liệu câu đố (`puzzles.json`) | 1 | 55 | 54 |
| Công cụ sinh ảnh (`tools/*.js`) | 2 | 189 | 147 |
| Tài liệu Markdown (README, Docs…) | 6 | 960 | 733 |
| **Tổng, trừ BMad** | **93** | **14.342** | **9.988** |

\* Dòng code thực = bỏ dòng trống và dòng chú thích. Chưa tính thư mục `_bmad/` (19 file,
công cụ quản lý quy trình, không phải code game) và 52 ảnh câu đố.

**Nói gọn khi pitch:** khoảng **6.800 dòng code viết tay** (C# + XAML), hoặc khoảng
**10.000 dòng** nếu tính cả migration tự sinh, cấu hình, dữ liệu và tài liệu.

**Độ phức tạp từng hàm (cyclomatic)** — 291 hàm:

| Mức | Số hàm |
|---|---:|
| Dễ (≤ 5) | 257 (88%) |
| Vừa (6–10) | 28 |
| Cao (11–20) | 5 |
| Rất cao (> 20) | 1 |

Trung bình **2,9**, trung vị 2, trung bình **10 dòng/hàm**. Hàm khó nhất:

| CC | Hàm | Vì sao phức tạp |
|---:|---|---|
| 21 | `PuzzleSync.Sync` | đối chiếu thư mục ảnh ↔ DB: thêm, sửa, xóa, bỏ qua nếu ảnh không đổi |
| 14 | `GameHub.RunMatch` | vòng lặp cả ván: phát câu, chờ, trừ mạng, kiểm tra điều kiện dừng |
| 13 | `MatchViewModel.EndMatch` | dựng lời kết ván: thắng/thua/hòa/đối thủ rời |
| 12 | `GameHub.PickAndBeginDuelRound` | chọn người ra đề, gửi 6 câu, chờ 15 giây |
| 11 | `LocalServer.EnsureRunningAsync` | tự bật máy chủ, đợi nó sẵn sàng, đủ loại lỗi |

**Cách đọc con số này (quan trọng khi bị hỏi):**

- Dự án **nhỏ đến vừa**, thuật toán đều **đơn giản**. Chỗ khó **không nằm ở thuật toán**
  mà ở **quản lý trạng thái** và **phối hợp nhiều bên chạy cùng lúc**.
- Hai lớp lớn nhất là `MatchViewModel` (856 dòng code, 145 thành viên) và
  `GameViewModel` (659 dòng, 126 thành viên). Chúng nặng vì phải nhớ rất nhiều cờ
  trạng thái (đang khóa, đang tạm dừng, đang hỏi kim cương, đang đếm phạt…).
- Chỗ phức tạp thật sự: **đồng bộ giữa client – máy chủ – đồng hồ** (xem mục 5).

---

## 3. Kiến trúc

```
┌──────────────────────── App WPF (2.769 dòng) ────────────────────────┐
│  Cửa sổ (XAML)  ──binding──▶  ViewModel  ──gọi──▶  Services           │
│  Login/Mode/Menu/Main/Match   Game/Match/Login…    ServerClient (REST)│
│                                                    MatchClient(SignalR)│
│                                                    LocalServer, Audio │
└───────────────┬───────────────────────────────────────────┬──────────┘
                │ dùng chung                                │ HTTP + WebSocket
        ┌───────▼────────┐                          ┌───────▼─────────┐
        │  Core (954)    │◀─────── dùng chung ──────│ Server (588)    │
        │ luật chơi, DB, │                          │ GameHub (SignalR)│
        │ chấm điểm,     │                          │ RoomManager     │
        │ hợp đồng tin   │                          │ TokenService    │
        └────────────────┘                          └─────────────────┘
                 SQLite (Data/game.db): Accounts, PlayerStates, PuzzleResults,
                                        Puzzles (có cả ảnh), PuzzleAnswers
```

**Vì sao tách `Core`?** Cùng một luật (chuẩn hóa đáp án, dựng ô chữ, bộ phím, chấm
điểm) cần chạy ở **cả client lẫn máy chủ**. Để một bản trong `Core` thì không bao giờ
có chuyện sửa một nơi quên nơi kia. `Core` cố ý **không** tham chiếu WPF để máy chủ
dùng được.

**Pattern chính: MVVM.** Cửa sổ chỉ có XAML (hình dạng); mọi logic nằm trong
ViewModel, giao diện liên kết (binding) vào thuộc tính. Ví dụ: `RelayCommand` cho nút
bấm, `ViewModelBase.SetProperty` báo giao diện cập nhật, `ThemedViewModel` cho nút
sáng/tối dùng chung 5 màn.

---

## 4. Một lượt chơi chạy thế nào

### Chế độ một người (`GameViewModel`)

1. `PuzzleRepository.LoadAll` nạp 52 câu (không kèm ảnh, ảnh nạp riêng từng câu).
2. Mỗi ván có một **hạt giống** (`RunSeed`) → `Arrange` xáo thứ tự câu theo hạt giống.
   Lưu **hạt giống** thay vì lưu cả danh sách → thoát ra vào lại dựng lại đúng thứ tự.
3. `PuzzleRound.Create` dựng ô đáp án (bỏ dấu) và bộ phím: chữ thật + chữ nhiễu, xáo.
4. Người chơi ghép chữ, bấm **Trả lời** → `CheckAnswer`. Đúng: điểm nền
   `10 × độ khó` + thưởng tốc độ (tối đa gấp đôi). Sai / hết 60 giây / bỏ qua: mất 1 mạng.
5. Hết mạng hoặc hết bộ câu → `EndRun` chốt sổ, so với kỷ lục, `GameStateService`
   ghi trạng thái **ván sau** (0 điểm, đầy mạng).

### Chế độ đấu (client ↔ `GameHub`)

```
Client A: CreateRoom ──▶ Máy chủ sinh mã phòng 6 ký tự
Client B: JoinRoom(mã, mật khẩu) ──▶ cả phòng nhận RoomChanged
Chủ phòng: StartMatch ──▶ GameHub.RunMatch (chạy nền, vòng lặp từng câu)
   mỗi câu:  RoundStarted (số ô + bộ phím, KHÔNG có đáp án)
             ↓ người chơi gửi SubmitAnswer
             AnswerJudged  (máy chủ chấm, tự đo thời gian)
             ↓ hết giờ hoặc cả hai đã đúng
             RoundEnded    (lúc này mới lộ đáp án) → ai chưa đúng mất 1 mạng
   một người hết mạng ──▶ MatchEnded
```

Giao thức: **8 hàm client gọi** (`CreateRoom`, `JoinRoom`, `SetReady`, `LeaveRoom`,
`SetMode`, `StartMatch`, `SubmitAnswer`, `PickPuzzle`) và **6 sự kiện máy chủ phát**
(`RoomChanged`, `RoundStarted`, `AnswerJudged`, `PickStarted`, `RoundEnded`, `MatchEnded`).

---

## 5. Tám kỹ thuật đáng khoe (và chỗ tìm trong code)

| # | Kỹ thuật | Vì sao hay | Ở đâu |
|---|---|---|---|
| 1 | **Máy chủ là trọng tài** | Đáp án không rời máy chủ trước khi hết câu; thời gian do máy chủ đo; ảnh phát bằng **mã hex ngẫu nhiên** chứ không bằng tên file (tên file chính là đáp án) | `RoomManager.Judge`, `ImageKey` |
| 2 | **Chống dò đáp án bằng máy** | Sai càng nhiều thì phạt chờ càng lâu (1…5 giây), máy chủ chặn chứ không tin client | `RoomManager.Cooldown` |
| 3 | **Xáo theo hạt giống, lưu được** | Một con số dựng lại cả thứ tự ván; thuật toán xáo là một phần của định dạng lưu | `GameViewModel.Arrange` |
| 4 | **Chuẩn hóa tiếng Việt** | Tách dấu bằng Unicode FormD, bỏ dấu kết hợp, đổi `đ→d` → "CÁ HEO" = "ca heo" | `AnswerChecker.Normalize` |
| 5 | **Đồng bộ thư mục ↔ DB** | Thêm ảnh vào thư mục là có câu mới; chỉ đọc lại byte khi kích thước/giờ sửa đổi | `PuzzleSync.Sync` |
| 6 | **Lưu mật khẩu đúng cách** | PBKDF2-SHA256, 100.000 vòng, muối riêng mỗi tài khoản, so sánh chống dò thời gian | `AccountService` |
| 7 | **Đồng thời an toàn** | Danh sách người chơi sửa từ nhiều luồng → khóa `Room.Gate`; ván chạy nền dùng `IHubContext` vì Hub bị hủy sau mỗi lời gọi | `Room`, `GameHub.RunMatch` |
| 8 | **Tự bật máy chủ cho chế độ LAN** | Người tạo phòng chỉ bấm một nút; app bật `DuoiHinhBatChu.Server.exe` nghe `0.0.0.0` và hiện IP LAN để đọc cho bạn bè | `LocalServer` |

**Công thức điểm** (hay bị hỏi):

- Một người: `10 × độ khó` + thưởng tốc độ `≤ điểm nền`; lối "ngẫu nhiên" nhân **×1,5**.
- Thi đấu: `100 × độ khó × max(0,2 ; 1 − giây/20)` — trả lời càng nhanh càng nhiều, sát giờ vẫn giữ 20%.
- Đố nhau: người đoán không ra thì người ra đề được `50 × độ khó`.

---

## 6. Lộ trình học code (một buổi tối, ~2 giờ)

Đọc theo thứ tự này, **từ nhỏ đến lớn**:

| Bước | File | Mục tiêu (≈ phút) |
|---|---|---|
| 1 | `Core/Models/Puzzle.cs`, `PlayerProfile.cs`, `MatchContracts.cs` | Biết dữ liệu gì chạy qua hệ thống (10) |
| 2 | `Core/Services/AnswerChecker.cs`, `SoloScoring.cs`, `MatchScoring.cs` | Luật chơi thuần túy, ngắn, dễ nói (10) |
| 3 | `Core/Models/PuzzleRound.cs` | Dựng ô + bộ phím (10) |
| 4 | `Core/Data/GameDbContext.cs`, `PuzzleSync.cs`, `GameStateService.cs` | Dữ liệu và lưu tiến trình (20) |
| 5 | `Server/RoomManager.cs` (hàm `Judge`) rồi `GameHub.cs` (`RunMatch`) | Trái tim chế độ đấu (30) |
| 6 | `ViewModels/ViewModelBase.cs`, `RelayCommand.cs`, `GameViewModel.cs` | MVVM + màn chơi (25) |
| 7 | `Services/MatchClient.cs`, `ServerClient.cs`, `LocalServer.cs` | Cách client nối máy chủ (15) |
| 8 | `ViewModels/MatchViewModel.cs` (lướt, không đọc hết) | Biết nó làm gì, không cần thuộc (10) |

Mẹo: mở `MainWindow.xaml` cạnh `GameViewModel.cs` và tìm một nút (ví dụ nút "Trả lời")
để thấy **binding nối XAML với ViewModel** — đó là ý chính của MVVM.

---

## 7. Câu hỏi hay gặp và cách trả lời

**Vì sao máy chủ chấm đáp án mà không để client chấm?**
Client là thứ người dùng sửa được. Nếu client tự chấm và tự khai thời gian thì ai
cũng "nhanh nhất". Máy chủ giữ đáp án và đo giờ nên kết quả đáng tin.

**Làm sao chống gian lận bằng cách thử hàng nghìn đáp án?**
Mỗi lần đoán sai phải chờ lâu hơn (1 → 5 giây), kiểm tra trên máy chủ. Đoán bừa vài
lần thì chịu được, dò bằng máy thì hết cửa.

**Vì sao lưu ảnh trong cơ sở dữ liệu?**
Máy chủ phải gửi ảnh cho máy người chơi khác; một file `game.db` là mang đi được cả bộ
câu. Đổi lại DB nặng (378 MB) — nên `LoadAll` cố ý **không** nạp byte ảnh cùng danh sách.

**Hai người chơi cùng lúc có xung đột không?**
Mỗi phòng có một khóa (`Room.Gate`); mọi đọc/ghi danh sách người chơi và chấm đáp án đi
qua khóa đó. Kiểm tra "phòng đầy" nằm **trong** khóa lúc thêm người, để hai người cùng
vào chỗ cuối không lọt cả hai.

**Vì sao dùng SignalR mà không dùng REST?**
Ván đấu cần máy chủ **chủ động đẩy** tin (câu mới, kết quả, hết giờ). SignalR giữ một
kênh WebSocket hai chiều. REST chỉ dùng cho đăng nhập và tải ảnh.

**Làm sao biết code đúng khi thay đổi?**
Xem mục 8: repo **chưa** có test tự động. Việc kiểm tra làm bằng bộ test viết riêng
(chạy ngầm, nối máy chủ thật, hai client chơi trọn ván). Đây là điểm cần cải thiện.

---

## 8. Điểm yếu — nên nói thành thật

Chủ động nêu điểm yếu còn tốt hơn bị hỏi khó. Mỗi điểm đi kèm hướng sửa:

| Điểm yếu | Hướng cải thiện |
|---|---|
| **Chưa có test tự động trong repo** (đã kiểm bằng bộ test riêng, chưa đưa vào) | Đưa bộ test vào dự án `xUnit`; phần `Core` rất dễ test vì thuần luật |
| `MatchViewModel` (856 dòng) và `GameViewModel` (659 dòng) quá lớn | Tách `LobbyViewModel` / `RoundViewModel` / `ConnectionService` |
| Tài khoản trên máy chủ dùng **mã tài khoản làm mật khẩu** (không phải mật khẩu người dùng gõ), mỗi máy một sổ tài khoản riêng | Dùng đúng tài khoản/mật khẩu của máy chủ, một sổ duy nhất |
| Mật khẩu/mã đi qua **HTTP thường** trong LAN | HTTPS nội bộ hoặc ký tên tin |
| Phòng và vé đăng nhập **giữ trong bộ nhớ**; khởi động lại máy chủ là mất | Lưu phòng/vé, hoặc cho phép nối lại |
| Chỉ chạy trên **Windows** (WPF) | Chuyển giao diện sang Avalonia/MAUI; `Core` và `Server` đã đa nền tảng |
| Chủ phòng không đổi được kiểu chơi giữa hai ván (bảng "Hết ván" che sảnh) | Đưa nút chọn kiểu chơi vào bảng đó |
| Cần máy khác trong LAN **cho phép tường lửa** (Windows mặc định chặn kết nối vào) | Hướng dẫn trong README, hoặc app tự đề nghị tạo luật |

---

## 9. Câu chuyện kỹ thuật hay để kể

Hai chuyện thật, có số liệu, cho thấy cách làm việc có kiểm chứng:

1. **Đo rồi mới kết luận.** Khi kiểm tra chế độ LAN, đo được mỗi request tới
   `localhost` mất **2,05 giây** trong khi `127.0.0.1` chỉ **1–5 ms**: máy chủ nghe
   `0.0.0.0` (chỉ IPv4), .NET thử `::1` trước và nó treo thay vì từ chối. Chủ phòng
   bị chậm 2 giây ở mỗi ảnh câu đố. Sửa gốc bằng cách chuẩn hóa `localhost → 127.0.0.1`.
2. **Dọn code cũng có thể phá hợp đồng ngầm.** Đổi vòng xáo tự viết sang
   `Random.Shuffle` cho code gọn hơn, nhưng hạt giống ván đang dở được **lưu vào DB**,
   thuật toán mới cho thứ tự khác → "Chơi tiếp" nhảy sai câu. Test lưu→đóng→mở lại bắt
   được, đã khôi phục thuật toán cũ kèm chú thích giải thích.

---

## 10. Kịch bản demo 5 phút

1. (1 phút) Đăng nhập `test` → **Cổ điển** → chọn lối chơi → trả lời một câu, dùng
   trợ giúp "Mở 1 chữ" (hỏi lại trước khi tiêu kim cương).
2. (1 phút) Mở **bảng xếp hạng** → giải thích kỷ lục theo ván, không cộng dồn.
3. (2 phút) **Nhiều người chơi → Tạo phòng**: chỉ vào dòng *"Bạn ở máy khác gõ địa chỉ
   192.168.x.x:5180"*; trên máy thứ hai **Vào phòng** → Sẵn sàng → chủ phòng bắt đầu ván
   **Thi đấu**: nhấn mạnh "máy chủ đo giờ, chấm điểm".
4. (1 phút) Nói về **Đố nhau** (luân phiên ra đề, thưởng cho người ra đề khi đối thủ
   không đoán ra) và nêu 2–3 điểm yếu ở mục 8.

**Trước giờ demo (checklist):** tắt chương trình khác đang giữ cổng **5180** (ví dụ
dự án Smart Bus), đảm bảo hai máy cùng mạng, và cho phép tường lửa (xem README).
