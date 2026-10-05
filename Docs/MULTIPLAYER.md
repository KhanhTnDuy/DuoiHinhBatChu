# Chế độ đấu nhiều người

Tài liệu thiết kế của chế độ đấu, viết trước khi làm và giữ lại làm căn cứ.
Chế độ này **đã chạy được** (`DuoiHinhBatChu.Server` + `MatchWindow`); luật
điểm và luồng ván hiện tại xem mục "Chế độ đấu nhiều người" trong README gốc.

## Vì sao 1 người offline, 2 người trở lên phải online

Một mình chơi thì chỉ cần dữ liệu trên máy: ảnh câu đố và file lưu tiến trình.
Từ hai người trở lên thì phải có một chỗ **cả hai cùng tin**, để quyết định:

- Ai bấm đúng trước — hai máy không thể tự so đồng hồ của nhau.
- Không ai xem trước được đáp án của câu sắp ra.
- Điểm ghi lại được, không sửa bằng cách chỉnh file trên máy mình.

Chỗ đó là máy chủ. Vì vậy chế độ đấu bắt buộc phải đăng nhập tài khoản thật,
không cho chơi khách.

## Kiến trúc định làm

```
WPF client  ──SignalR (WebSocket)──►  ASP.NET Core server
                                       ├─ xác thực tài khoản
                                       ├─ phòng chơi (mã 6 ký tự)
                                       ├─ phát câu đố + chấm điểm
                                       └─ bảng xếp hạng
```

Chọn **ASP.NET Core + SignalR** vì cùng ngôn ngữ C#, dùng lại được `Puzzle`,
`AnswerChecker` mà không phải viết lại, và chạy được cả trên máy trong nhà (LAN)
lẫn đem lên máy chủ thật sau này.

## Việc phải làm, theo thứ tự

1. ~~**Tách phần dùng chung** thành thư viện lớp `DuoiHinhBatChu.Core`.~~ **Xong.**
   Nhắm `net10.0` thuần, không tham chiếu WPF. Hiện chứa `Puzzle`, `Account`,
   `AnswerChecker`, `PuzzleRepository`, `PuzzleImageLocator`, `AccountService`.
2. ~~**Dựng server** `DuoiHinhBatChu.Server` (ASP.NET Core).~~ **Xong.**
   - `POST /api/auth/register`, `POST /api/auth/login` → trả vé đăng nhập.
   - `GET /api/health` để client thử kết nối.
   - `GET /api/puzzles/{ten}/image` phục vụ ảnh, nên máy người chơi không cần
     có sẵn đúng bộ ảnh.
   - Còn nợ: đang dùng lại `AccountService` ghi JSON, chưa đổi sang SQLite.
     Vé để trong bộ nhớ nên khởi động lại máy chủ là phải đăng nhập lại.
3. ~~**Hub phòng chơi** `GameHub`.~~ **Xong.**
   - `CreateRoom` → mã 6 ký tự; `JoinRoom(code)`; `StartMatch()`.
   - Máy chủ phát `RoundStarted` kèm số ô và bộ phím chữ, **không kèm đáp án**.
   - `SubmitAnswer` — máy chủ chấm và tự đo thời gian; đáp án chỉ lộ ở `RoundEnded`.
   - Chưa có: đá người treo máy, chơi lại ngay trong phòng cũ.
4. **Client** — *đang làm dở*. Đã có màn chọn chế độ sau khi đăng nhập và phép
   thử kết nối máy chủ. Còn thiếu màn phòng chờ và màn đấu: cần thêm gói
   `Microsoft.AspNetCore.SignalR.Client` và một `MatchViewModel` nghe các
   sự kiện `RoundStarted` / `AnswerJudged` / `RoundEnded` / `MatchEnded`.

## Cách tính điểm theo tốc độ

Server giữ mốc `t0` lúc phát câu. Người trả lời đúng ở giây `t`:

```
diem = diem_co_ban * do_kho * he_so_toc_do
he_so_toc_do = max(0.2, 1 - t / thoi_gian_toi_da)
```

Với `thoi_gian_toi_da` khoảng 20 giây. Trả lời đúng ngay được gần trọn điểm,
trả lời sát giờ vẫn được 20% — thua nhưng không mất trắng. Trả lời sai thì trừ
một lượt và phải chờ đến câu sau.

Toàn bộ phép tính này chạy **trên server**. Client chỉ hiển thị kết quả nhận về;
không bao giờ để client tự khai thời gian của mình.

## Phần đăng nhập hiện tại khớp vào đâu

`AccountService` bây giờ băm mật khẩu bằng PBKDF2-SHA256 và lưu ở
`Data/accounts.json` **ngay trên máy người chơi**. Như vậy đủ để tách tiến trình
giữa mấy anh em dùng chung một máy, nhưng **không phải là bảo mật thật**: ai mở
được thư mục game cũng xóa hay sửa được file đó.

Khi lên server, phần băm mật khẩu giữ nguyên thuật toán, chỉ chuyển chỗ chạy từ
client sang server — nên `Account` và `AccountService` không phải viết lại.
