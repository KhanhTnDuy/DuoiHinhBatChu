# Đuổi hình bắt chữ

Game đoán chữ từ hình, viết bằng **WPF / C# / .NET 10** cho Windows. Người chơi
nhìn một tấm ảnh ghép rồi bấm các phím chữ cái để điền vào ô đáp án — kiểu chơi
quen thuộc của game đố chữ trên điện thoại.

Có hai chế độ: **một người** (chơi offline, lưu tiến trình) và **nhiều người**
(đấu qua mạng với một máy chủ ASP.NET Core + SignalR).

## Lối chơi

- Mỗi câu **60 giây**. Trả lời đúng trong giờ được điểm nền `10 × độ khó`; càng
  nhanh càng được thưởng thêm, tối đa gấp đôi.
- Hết giờ hoặc đoán sai quá nhiều: **mất 1 mạng** (bắt đầu với 5 ♥) và mất chuỗi
  đúng liên tiếp.
- Đúng **5 câu liền** được thưởng 1 💎.
- Ba trợ giúp, mỗi lần dùng tốn 1 💎: **mở 1 chữ**, **xóa chữ thừa**, **gợi ý lời**.

## Chạy thử

Cần [.NET 10 SDK](https://dotnet.microsoft.com/download) trên Windows.

```bash
git clone <repo>
cd DuoiHinhBatChu
dotnet run --project DuoiHinhBatChu.csproj
```

Lần chạy đầu, cơ sở dữ liệu `Data/game.db` được tạo tự động (EF Core Migrations)
và toàn bộ ảnh trong `Assets/CauHoi/` được nạp vào bảng câu đố.

Luồng màn hình: **Đăng nhập → Chọn chế độ → Menu → Màn chơi**. Muốn xem nhanh thì
bấm *Chơi khách* (khách không lưu tiến trình và không lên bảng xếp hạng).

### Chế độ đấu nhiều người

Chạy thêm máy chủ ở một cửa sổ khác:

```bash
dotnet run --project DuoiHinhBatChu.Server
```

Máy chủ lắng nghe ở `http://localhost:5180`. Tài khoản trên máy chủ là **sổ
riêng**, không dùng chung với tài khoản ở máy client — vào chế độ đấu phải đăng
nhập thêm một lần. Chi tiết: [`Docs/MULTIPLAYER.md`](Docs/MULTIPLAYER.md).

## Cấu trúc

| Dự án | Vai trò |
| --- | --- |
| `DuoiHinhBatChu.csproj` (thư mục gốc) | App WPF: các cửa sổ, ViewModel, giao diện sáng/tối |
| `DuoiHinhBatChu.Core` | Phần lõi không phụ thuộc WPF: luật chơi, tính điểm, tài khoản, EF Core. Nhắm `net10.0` thuần để máy chủ dùng lại y nguyên |
| `DuoiHinhBatChu.Server` | ASP.NET Core + SignalR: phòng đấu, bốc câu, phục vụ ảnh câu hỏi |

Trong app WPF:

```
LoginWindow / ModeWindow / MenuWindow / MainWindow / MatchWindow   cửa sổ
ViewModels/                GameViewModel, MatchViewModel, MenuViewModel...
Services/                  MatchClient (SignalR), ServerClient, ThemeService
Themes/                    Light.xaml, Dark.xaml
Assets/CauHoi/             ảnh câu đố — tên file chính là đáp án
Assets/TaiNguyen/          ảnh tài nguyên app (mã QR, logo...)
Assets/tongquanthietke/    tài liệu thiết kế, bảng màu
tools/                     gen.js — script sinh ảnh ghép từ nguyên liệu
```

Kiến trúc app theo **MVVM**: cửa sổ XAML chỉ lo hiển thị, mọi trạng thái ván chơi
nằm trong `ViewModels/GameViewModel.cs`.

## Thêm câu đố mới

Bỏ file ảnh vào `Assets/CauHoi/`, **đặt tên file chính là đáp án** (có dấu, có
khoảng trắng giữa các tiếng — `PuzzleImageLocator` tự chuẩn hóa khi so khớp).
Lần khởi động sau, `PuzzleSync.Sync()` tự đối chiếu thư mục với cơ sở dữ liệu.

Gợi ý và độ khó là phần bổ sung tùy chọn, khai trong `Data/puzzles.json`, ghép
theo đáp án. Hướng dẫn đầy đủ: [`Data/README.md`](Data/README.md).

Hiện có 39 ảnh câu đố; mục tiêu là 50.

## Dữ liệu

Tài khoản, tiến trình và câu đố nằm trong SQLite `Data/game.db`, đọc ghi qua
EF Core — các bảng `Accounts`, `PlayerStates`, `PuzzleResults`, `Puzzles`,
`PuzzleAnswers`. Client và máy chủ mỗi bên giữ một `game.db` riêng. Mã câu đố
chính là đáp án đã chuẩn hóa (`"CAHEO"`). Chi tiết và cách sinh migration mới:
[`Docs/DATABASE.md`](Docs/DATABASE.md).

File dữ liệu sinh lúc chạy đã được `.gitignore` bỏ qua, nên kho mã không mang
theo tài khoản hay tiến trình của ai.

## Tài liệu khác

- [`Docs/DATABASE.md`](Docs/DATABASE.md) — cơ sở dữ liệu, migrations
- [`Docs/MULTIPLAYER.md`](Docs/MULTIPLAYER.md) — thiết kế chế độ đấu nhiều người
- [`Assets/tongquanthietke/DESIGN.md`](Assets/tongquanthietke/DESIGN.md) — bảng màu, typography
- [`tools/nguyenlieu/HUONG DAN.md`](tools/nguyenlieu/HUONG%20DAN.md) — quy tắc đặt tên ảnh nguyên liệu

---

Dự án cá nhân, làm để học WPF và C#.
