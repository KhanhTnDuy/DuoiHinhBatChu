# Cơ sở dữ liệu

Từ bước 12 trở đi, tài khoản và tiến trình chơi không còn nằm ở file JSON nữa
mà nằm trong một cơ sở dữ liệu **SQLite**, đọc ghi qua **EF Core**.

## Vì sao chọn SQLite

SQLite là một cơ sở dữ liệu nằm gọn trong **một file** (`Data/game.db`), không
phải cài thêm máy chủ SQL nào cả — chép game sang máy khác là chạy được ngay.
So với cách cũ (mỗi tài khoản một file JSON) thì được thêm:

- **Ràng buộc ngay trong dữ liệu:** không cho trùng tên đăng nhập, không cho
  trùng số điện thoại, xóa tài khoản là tiến trình đi theo. Dù code C# quên
  kiểm tra thì dữ liệu vẫn không hỏng được.
- **Truy vấn:** hỏi "10 người điểm cao nhất" bằng một câu lệnh, thay vì mở
  từng file JSON ra đếm.
- **Ghi trọn gói:** một lần `SaveChanges()` hoặc ăn cả hoặc không ăn gì, không
  còn cảnh file lưu bị cắt đôi khi tắt máy giữa chừng.

## Các bảng

| Bảng | Giữ gì | Khóa chính |
|---|---|---|
| `Accounts` | tài khoản: tên đăng nhập, tên hiển thị, số điện thoại, mật khẩu đã băm | `Id` |
| `PlayerStates` | tiến trình chơi đơn: điểm, mạng, kim cương, câu đang chơi, tùy chọn âm thanh | `AccountId` |
| `PuzzleResults` | mỗi dòng là "tài khoản X đã giải câu Y" | `Id` |
| `Puzzles` | câu đố: đáp án, chủ đề, độ khó, **và byte của ảnh** | `Id` |
| `PuzzleAnswers` | các cách viết khác cũng được chấm đúng | `Id` |

Quan hệ:

- `Accounts` 1 ─ 1 `PlayerStates` (mỗi tài khoản một dòng tiến trình)
- `Accounts` 1 ─ n `PuzzleResults` (mỗi tài khoản nhiều câu đã giải)
- `Puzzles` 1 ─ n `PuzzleAnswers` (mỗi câu nhiều cách viết)

Cả ba khóa ngoại đều đặt `ON DELETE CASCADE`.

`PuzzleResults` cố ý **không** có khóa ngoại sang `Puzzles`: bạn có thể tạm rút
một ảnh ra khỏi `Assets/CauHoi` rồi bỏ lại, mà tiến trình của người chơi thì
không nên biến mất theo.

Danh sách câu đã giải tách hẳn thành bảng riêng chứ không nhét vào một ô của
`PlayerStates` — đó là nguyên tắc đầu tiên của cơ sở dữ liệu quan hệ: **một ô
chỉ giữ một giá trị**, cái gì "nhiều" thì cho ra bảng riêng.

Tài khoản `khach` được thêm sẵn ngay lúc tạo cơ sở dữ liệu, vì `PlayerStates`
có khóa ngoại trỏ về `Accounts` — không có dòng đó thì người chơi khách không
lưu được gì.

## Các lớp

| Lớp | Việc |
|---|---|
| `Data/GameDbContext.cs` | khai các bảng và ràng buộc |
| `Data/GameDatabase.cs` | biết file nằm ở đâu, tạo bảng lần đầu, mở phiên làm việc |
| `Data/PlayerState.cs`, `Data/PuzzleResult.cs`, `Data/StoredPuzzle.cs` | hình dáng một dòng của từng bảng |
| `Data/PuzzleSync.cs` | đối chiếu `Assets/CauHoi` với bảng `Puzzles` lúc khởi động |
| `Data/GameDbContextFactory.cs` | chỉ để `dotnet ef` dựng được context, không dùng khi chạy |
| `Services/AccountService.cs` | đăng ký, đăng nhập, quên mật khẩu |
| `Services/GameStateService.cs` | đọc ghi tiến trình, gom hai bảng thành `PlayerProfile` |
| `Services/PuzzleRepository.cs` | đọc câu đố và ảnh từ bảng |

**Luật dùng DbContext:** không giữ sẵn một cái dùng mãi. Mỗi việc mở một phiên
bằng `using GameDbContext db = GameDatabase.Open();` rồi đóng ngay. DbContext
không an toàn đa luồng, mà máy chủ thì phục vụ nhiều người cùng lúc.

## Chuyển dữ liệu cũ

Lần chạy đầu tiên, `GameDatabase.EnsureReady()` tự đọc `Data/accounts.json` và
`Data/saves/*.json` rồi đổ vào bảng, sau đó đổi tên chúng thành
`accounts.json.bak` và `saves-bak-<ngày giờ>` để lần sau không nhập lại. Người
đang chơi dở không mất gì.

## Ảnh câu đố nằm trong cơ sở dữ liệu

Bảng `Puzzles` giữ **byte của ảnh** (cột `ImageBytes`), không phải đường dẫn.
Hai lý do:

- Máy chủ phải gửi ảnh cho người chơi ở máy khác — họ không có sẵn file.
- Một file `game.db` là đủ để mang cả bộ câu đố sang máy khác.

Thư mục `Assets/CauHoi` vẫn là **nơi bạn soạn**: bỏ thêm một ảnh vào rồi chạy
lại game là có câu mới. Mỗi lần khởi động, `PuzzleSync.Sync()` đối chiếu thư
mục với bảng — thêm cái mới, cập nhật ảnh đã sửa, xóa câu mà ảnh không còn.
Ảnh không đổi thì không đọc lại byte (so kích thước và giờ sửa file), nên lần
khởi động thứ hai gần như không tốn gì.

Mã câu (`Puzzles.Id`) sinh từ đáp án đã chuẩn hóa: `"CÁ HEO"` → `"CAHEO"`. Cố
tình không đánh số `p001, p002` theo thứ tự quét, vì chèn một ảnh vào giữa là
mọi số phía sau xê dịch và tiến trình đã lưu sẽ trỏ sang câu khác.

**Đừng nạp ảnh cùng lúc với danh sách câu.** `PuzzleRepository.LoadAll()` chọn
sẵn từng cột và bỏ `ImageBytes` lại; ảnh lấy riêng bằng `LoadImage(id)` đúng
lúc cần hiện. Viết `db.Puzzles.ToList()` là kéo cả mấy chục megabyte ảnh lên bộ
nhớ một cách vô ích.

## Ảnh tài nguyên thì KHÔNG vào cơ sở dữ liệu

`Assets/TaiNguyen` chứa ảnh tài nguyên của app (mã QR ủng hộ, logo…), đọc thẳng
từ đĩa qua `AppImageLocator.Find("ten-tai-nguyen")`. Chúng không cần vào bảng vì
không phải gửi qua mạng cho ai, và thiếu một cái thì app chỉ hiện chỗ trống chứ
không hỏng.

Quy ước đặt tên giống ảnh câu đố: **tên file chính là tên tài nguyên**, đuôi gì
cũng được.

## Hai cơ sở dữ liệu riêng

Máy người chơi và máy chủ mỗi bên một file `game.db` riêng, vì mỗi bên chạy ở
một thư mục khác nhau. Đây là chủ ý, không phải lỗi: tài khoản chơi đơn ở máy
mình khác với tài khoản đấu online trên máy chủ — xem `MULTIPLAYER.md`.

## Đổi cấu trúc bảng về sau

Dùng **EF Migrations**. `GameDatabase.EnsureReady()` gọi `Database.Migrate()`:
file chưa có thì tạo mới toàn bộ, có rồi thì chỉ áp những bước còn thiếu.

Các bước chuyển nằm ở `DuoiHinhBatChu.Core/Data/Migrations/`. Sửa bảng xong thì
sinh thêm một bước:

```
dotnet ef migrations add TenBuocDoi \
  --project DuoiHinhBatChu.Core --startup-project DuoiHinhBatChu.Core \
  --output-dir Data/Migrations
```

(phải khai `--startup-project` là chính Core, vì dự án WPF không tham chiếu
`Microsoft.EntityFrameworkCore.Design`.)

Lần chạy sau, mọi máy đang có `game.db` tự động được sửa bảng — **không phải xóa
file đi làm lại**. Đây chính là chỗ hơn hẳn `EnsureCreated()` mà bản đầu dùng.

Cần công cụ một lần: `dotnet tool install --global dotnet-ef`.

## Xem thẳng vào file game.db

Không có công cụ nào sẵn thì dùng node (Node 22 trở lên có sẵn `node:sqlite`):

```
node -e "const{DatabaseSync}=require('node:sqlite');const d=new DatabaseSync('bin/Debug/net10.0-windows/Data/game.db');console.log(d.prepare('select * from Accounts').all())"
```
