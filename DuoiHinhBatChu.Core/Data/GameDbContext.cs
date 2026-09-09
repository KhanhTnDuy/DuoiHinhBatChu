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
        });
    }
}
