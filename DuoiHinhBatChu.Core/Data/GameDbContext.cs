using DuoiHinhBatChu.Models;
using Microsoft.EntityFrameworkCore;

namespace DuoiHinhBatChu.Data;

/// <summary>
/// Cửa vào cơ sở dữ liệu của game (SQLite, file <c>Data/game.db</c>).
///
/// Mỗi <see cref="DbSet{T}"/> là một bảng. Đừng dùng chung một đối tượng
/// DbContext cho nhiều luồng — nó không an toàn đa luồng; hãy mở một cái mới
/// cho mỗi việc bằng <see cref="GameDatabase.Open"/> rồi đóng ngay.
/// </summary>
public class GameDbContext : DbContext
{
    public GameDbContext(DbContextOptions<GameDbContext> options) : base(options) { }

    /// <summary>Bảng tài khoản: tên đăng nhập, số điện thoại, mật khẩu đã băm.</summary>
    public DbSet<Account> Accounts => Set<Account>();

    /// <summary>Bảng tiến trình chơi đơn, mỗi tài khoản một dòng.</summary>
    public DbSet<PlayerState> PlayerStates => Set<PlayerState>();

    /// <summary>Bảng câu đã giải, mỗi tài khoản nhiều dòng.</summary>
    public DbSet<PuzzleResult> PuzzleResults => Set<PuzzleResult>();

    /// <summary>Bảng câu đố, chứa luôn byte ảnh.</summary>
    public DbSet<StoredPuzzle> Puzzles => Set<StoredPuzzle>();

    /// <summary>Bảng các cách viết khác cũng được chấm đúng.</summary>
    public DbSet<StoredAnswer> PuzzleAnswers => Set<StoredAnswer>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Account>(e =>
        {
            e.HasKey(a => a.Id);

            // Không cho trùng tên đăng nhập, cũng không cho trùng số điện thoại:
            // ràng buộc đặt ngay ở cơ sở dữ liệu thì dù quên kiểm tra trong C#
            // dữ liệu vẫn không hỏng được.
            e.HasIndex(a => a.UserName).IsUnique();
            e.HasIndex(a => a.Phone).IsUnique();

            e.Property(a => a.UserName).IsRequired().HasMaxLength(20);
            e.Property(a => a.DisplayName).HasMaxLength(20);
            e.Property(a => a.Phone).HasMaxLength(15);
        });

        b.Entity<PlayerState>(e =>
        {
            e.HasKey(s => s.AccountId);

            // Xóa tài khoản thì tiến trình đi theo, không để lại rác
            e.HasOne<Account>()
             .WithOne()
             .HasForeignKey<PlayerState>(s => s.AccountId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PuzzleResult>(e =>
        {
            e.HasKey(r => r.Id);

            // Một người + một câu = nhiều nhất một dòng
            e.HasIndex(r => new { r.AccountId, r.PuzzleId }).IsUnique();

            e.Property(r => r.PuzzleId).IsRequired().HasMaxLength(120);

            e.HasOne<Account>()
             .WithMany()
             .HasForeignKey(r => r.AccountId)
             .OnDelete(DeleteBehavior.Cascade);

            // Cố ý KHÔNG đặt khóa ngoại sang bảng Puzzles: bạn có thể tạm rút
            // một ảnh câu đố ra khỏi Assets/CauHoi rồi bỏ lại, mà tiến trình
            // của người chơi thì không nên biến mất theo.
        });

        b.Entity<StoredPuzzle>(e =>
        {
            e.HasKey(p => p.Id);

            // Tên file ảnh là địa chỉ máy chủ dùng để gửi ảnh, phải là duy nhất
            e.HasIndex(p => p.ImageName).IsUnique();

            e.Property(p => p.Answer).IsRequired().HasMaxLength(120);
            e.Property(p => p.ImageName).IsRequired().HasMaxLength(260);
            e.Property(p => p.ContentType).HasMaxLength(60);
        });

        b.Entity<StoredAnswer>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasIndex(a => new { a.PuzzleId, a.Text }).IsUnique();
            e.Property(a => a.Text).IsRequired().HasMaxLength(120);

            e.HasOne<StoredPuzzle>()
             .WithMany()
             .HasForeignKey(a => a.PuzzleId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
