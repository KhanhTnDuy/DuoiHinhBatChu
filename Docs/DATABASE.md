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
| `PuzzleResults` | mỗi dòng là "tài khoản X đã giải câu Y", kèm số sao | `Id` |

Quan hệ:

- `Accounts` 1 ─ 1 `PlayerStates` (mỗi tài khoản một dòng tiến trình)
- `Accounts` 1 ─ n `PuzzleResults` (mỗi tài khoản nhiều câu đã giải)

Cả hai khóa ngoại đều đặt `ON DELETE CASCADE`.

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
| `Data/PlayerState.cs`, `Data/PuzzleResult.cs` | hình dáng một dòng của hai bảng |
| `Services/AccountService.cs` | đăng ký, đăng nhập, quên mật khẩu |
| `Services/GameStateService.cs` | đọc ghi tiến trình, gom hai bảng thành `PlayerProfile` |

**Luật dùng DbContext:** không giữ sẵn một cái dùng mãi. Mỗi việc mở một phiên
bằng `using GameDbContext db = GameDatabase.Open();` rồi đóng ngay. DbContext
không an toàn đa luồng, mà máy chủ thì phục vụ nhiều người cùng lúc.

## Chuyển dữ liệu cũ

Lần chạy đầu tiên, `GameDatabase.EnsureReady()` tự đọc `Data/accounts.json` và
`Data/saves/*.json` rồi đổ vào bảng, sau đó đổi tên chúng thành
`accounts.json.bak` và `saves-bak-<ngày giờ>` để lần sau không nhập lại. Người
đang chơi dở không mất gì.

## Hai cơ sở dữ liệu riêng

Máy người chơi và máy chủ mỗi bên một file `game.db` riêng, vì mỗi bên chạy ở
một thư mục khác nhau. Đây là chủ ý, không phải lỗi: tài khoản chơi đơn ở máy
mình khác với tài khoản đấu online trên máy chủ — xem `MULTIPLAYER.md`.

## Đổi cấu trúc bảng về sau

Hiện dùng `Database.EnsureCreated()`: chỉ tạo bảng khi file chưa có, **không**
sửa được bảng đã tạo. Nên khi thêm cột mới thì hoặc xóa `Data/game.db` đi cho
tạo lại (mất dữ liệu thử), hoặc chuyển sang EF Migrations:

```
dotnet tool install --global dotnet-ef
dotnet ef migrations add TenBuocDoi --project DuoiHinhBatChu.Core
```

rồi đổi `EnsureCreated()` thành `Migrate()` trong `GameDatabase`.

## Xem thẳng vào file game.db

Không có công cụ nào sẵn thì dùng node (Node 22 trở lên có sẵn `node:sqlite`):

```
node -e "const{DatabaseSync}=require('node:sqlite');const d=new DatabaseSync('bin/Debug/net10.0-windows/Data/game.db');console.log(d.prepare('select * from Accounts').all())"
```
