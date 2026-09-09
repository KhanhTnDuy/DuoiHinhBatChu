using System.IO;
using System.Text.Json;
using DuoiHinhBatChu.Models;
using Microsoft.EntityFrameworkCore;

namespace DuoiHinhBatChu.Data;

/// <summary>
/// Nơi duy nhất biết cơ sở dữ liệu nằm ở đâu và mở nó ra thế nào.
///
/// Lần gọi đầu tiên sẽ tạo file <c>Data/game.db</c> nếu chưa có, thêm sẵn tài
/// khoản "khách", rồi chuyển nốt dữ liệu cũ từ thời còn lưu bằng JSON
/// (<c>Data/accounts.json</c> và <c>Data/saves/*.json</c>) vào bảng — người
/// đang chơi dở không mất tiến trình.
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

            // Tạo bảng theo đúng hình dáng khai trong GameDbContext.
            // Sau này đổi cấu trúc bảng thì phải chuyển sang EF Migrations,
            // vì EnsureCreated chỉ tạo mới chứ không sửa bảng đã có.
            db.Database.EnsureCreated();

            SeedGuest(db);
            ImportLegacyJson(db, Path.GetDirectoryName(path)!);
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
    /// Tài khoản "khách" phải có thật trong bảng, vì bảng tiến trình có khóa
    /// ngoại trỏ về bảng tài khoản — không có dòng này thì người chơi khách
    /// không lưu được gì.
    /// </summary>
    private static void SeedGuest(GameDbContext db)
    {
        Account guest = Account.Guest();
        if (db.Accounts.Any(a => a.Id == guest.Id)) return;

        db.Accounts.Add(guest);
        db.SaveChanges();
    }

    // ----- Chuyển dữ liệu cũ từ JSON sang -----

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static void ImportLegacyJson(GameDbContext db, string dataDir)
    {
        ImportAccounts(db, Path.Combine(dataDir, "accounts.json"));
        ImportSaves(db, Path.Combine(dataDir, "saves"));
    }

    private static void ImportAccounts(GameDbContext db, string file)
    {
        if (!File.Exists(file)) return;

        try
        {
            var old = JsonSerializer.Deserialize<List<Account>>(File.ReadAllText(file), JsonOptions);
            if (old != null)
            {
                foreach (Account a in old)
                {
                    // Bỏ qua tài khoản đã có trong bảng, kể cả trùng tên hay
                    // trùng số điện thoại, vì hai cột đó không cho trùng
                    bool trung = db.Accounts.Any(x =>
                        x.Id == a.Id || x.UserName == a.UserName || x.Phone == a.Phone);

                    if (!trung) db.Accounts.Add(a);
                }
                db.SaveChanges();
            }

            // Đổi tên file cũ để lần chạy sau không nhập lại lần nữa
            File.Move(file, file + ".bak", overwrite: true);
        }
        catch
        {
            // File cũ hỏng thì thôi, thà mất dữ liệu cũ còn hơn không mở được game
        }
    }

    private static void ImportSaves(GameDbContext db, string dir)
    {
        if (!Directory.Exists(dir)) return;

        foreach (string file in Directory.GetFiles(dir, "*.json"))
        {
            string accountId = Path.GetFileNameWithoutExtension(file);

            try
            {
                if (!db.Accounts.Any(a => a.Id == accountId)) continue;
                if (db.PlayerStates.Any(s => s.AccountId == accountId)) continue;

                var p = JsonSerializer.Deserialize<PlayerProfile>(
                    File.ReadAllText(file), JsonOptions);
                if (p == null) continue;

                db.PlayerStates.Add(new PlayerState
                {
                    AccountId = accountId,
                    Score = p.Score,
                    Rubies = p.Rubies,
                    CorrectStreak = p.CorrectStreak,
                    Lives = p.Lives,
                    MaxLives = p.MaxLives,
                    CurrentPuzzleIndex = p.CurrentPuzzleIndex,
                    IsSoundEnabled = p.IsSoundEnabled,
                    IsBgmEnabled = p.IsBgmEnabled,
                    IsTimerEnabled = p.IsTimerEnabled,
                });

                foreach (string puzzleId in p.SolvedPuzzleIds.Distinct())
                {
                    db.PuzzleResults.Add(new PuzzleResult
                    {
                        AccountId = accountId,
                        PuzzleId = puzzleId,
                        Stars = p.PuzzleStars.TryGetValue(puzzleId, out int s) ? s : 0,
                    });
                }

                db.SaveChanges();
            }
            catch
            {
                // Một file lưu hỏng thì bỏ qua file đó, vẫn nhập tiếp các file còn lại
            }
        }

        try
        {
            Directory.Move(dir, dir + "-bak-" + DateTime.Now.ToString("yyyyMMddHHmmss"));
        }
        catch
        {
        }
    }
}
