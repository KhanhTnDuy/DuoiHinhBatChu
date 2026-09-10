using System.IO;
using DuoiHinhBatChu.Models;
using Microsoft.EntityFrameworkCore;

namespace DuoiHinhBatChu.Data;

/// <summary>
/// Nơi duy nhất biết cơ sở dữ liệu nằm ở đâu và mở nó ra thế nào.
///
/// Lần gọi đầu tiên sẽ tạo file <c>Data/game.db</c> nếu chưa có (hoặc áp nốt
/// các bước Migrations còn thiếu) rồi thêm sẵn tài khoản "khách".
/// </summary>
public static class GameDatabase
{
    private static readonly object Gate = new();
    private static DbContextOptions<GameDbContext>? _options;

    /// <summary>Đường dẫn mặc định của file cơ sở dữ liệu.</summary>
    public static string DefaultPath =>
        Path.Combine(AppContext.BaseDirectory, "Data", "game.db");

    /// <summary>Đường dẫn thực đang dùng, biết được sau khi đã chuẩn bị xong.</summary>
    public static string FilePath { get; private set; } = "";

    /// <summary>
    /// Chuẩn bị cơ sở dữ liệu. Gọi một lần lúc khởi động cho lỗi (nếu có) nổ ra
    /// sớm; những lần sau không làm gì thêm.
    /// </summary>
    /// <param name="dbPath">Đường dẫn thay thế, chỉ dùng khi test.</param>
    public static void EnsureReady(string? dbPath = null)
    {
        lock (Gate)
        {
            if (_options != null) return;

            string path = dbPath ?? DefaultPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            _options = new DbContextOptionsBuilder<GameDbContext>()
                .UseSqlite($"Data Source={path}")
                .Options;
            FilePath = path;

            using var db = new GameDbContext(_options);

            // Chạy các bước chuyển trong thư mục Migrations: chưa có file thì
            // tạo mới toàn bộ bảng, có rồi thì chỉ áp những bước còn thiếu.
            //
            // Trước đây dùng EnsureCreated, nhưng nó chỉ biết tạo mới: thêm một
            // bảng là những máy đã có game.db không bao giờ nhận được bảng đó.
            db.Database.Migrate();

            SeedGuest(db);
        }
    }

    /// <summary>
    /// Mở một phiên làm việc mới với cơ sở dữ liệu. Luôn dùng kèm <c>using</c>
    /// để đóng ngay sau khi xong — mỗi việc một phiên, không dùng lại.
    /// </summary>
    public static GameDbContext Open()
    {
        if (_options == null) EnsureReady();
        return new GameDbContext(_options!);
    }

    /// <summary>
    /// Giữ đúng một dòng "khách" trong bảng tài khoản, và luôn dọn sạch tiến
    /// trình của nó.
    ///
    /// Khách KHÔNG được lưu tiến trình (xem <see cref="Services.GameStateService"/>):
    /// hồ sơ khách là chung cho mọi người ngồi vào máy này, lưu vào đó thì người
    /// sau lại tiếp tục ván của người trước. Dòng dọn ở đây là để xóa nốt dữ
    /// liệu mà bản trước lỡ ghi vào.
    /// </summary>
    private static void SeedGuest(GameDbContext db)
    {
        Account guest = Account.Guest();

        if (!db.Accounts.Any(a => a.Id == guest.Id))
        {
            db.Accounts.Add(guest);
            db.SaveChanges();
        }

        // ExecuteDelete chạy thẳng xuống cơ sở dữ liệu ngay, nên phải để sau
        // SaveChanges ở trên chứ không xen vào giữa
        db.PuzzleResults.Where(r => r.AccountId == Account.GuestId).ExecuteDelete();
        db.PlayerStates.Where(s => s.AccountId == Account.GuestId).ExecuteDelete();
    }
}
