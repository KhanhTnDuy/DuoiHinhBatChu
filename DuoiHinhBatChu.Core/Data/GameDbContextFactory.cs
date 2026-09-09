using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DuoiHinhBatChu.Data;

/// <summary>
/// Chỉ dùng cho công cụ dòng lệnh <c>dotnet ef</c> lúc bạn tạo bước chuyển
/// (migration), KHÔNG dùng khi game chạy.
///
/// Công cụ đó cần dựng được một <see cref="GameDbContext"/> mà không chạy cả
/// ứng dụng, nên phải có lớp này chỉ cho nó biết dùng SQLite. Đường dẫn ở đây
/// chỉ để công cụ so sánh cấu trúc bảng, không có file thật cũng không sao.
/// </summary>
public class GameDbContextFactory : IDesignTimeDbContextFactory<GameDbContext>
{
    public GameDbContext CreateDbContext(string[] args)
    {
        DbContextOptions<GameDbContext> options =
            new DbContextOptionsBuilder<GameDbContext>()
                .UseSqlite("Data Source=thiet-ke.db")
                .Options;

        return new GameDbContext(options);
    }
}
