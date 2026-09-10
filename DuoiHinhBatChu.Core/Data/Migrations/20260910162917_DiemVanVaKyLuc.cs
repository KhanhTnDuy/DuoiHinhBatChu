using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DuoiHinhBatChu.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class DiemVanVaKyLuc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BestScore",
                table: "PlayerStates",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Điểm cũ cộng dồn qua mọi ván, và nó chính là con số bảng xếp hạng
            // đang dùng — nên lấy luôn nó làm kỷ lục khởi điểm. Để mặc 0 thì mọi
            // người rơi khỏi bảng ngay lần chạy sau.
            //
            // Đổi lại, ai chơi lâu sẽ mang một kỷ lục hơi cao so với luật mới và
            // phải một thời gian mới phá được. Chấp nhận: thà kỷ lục khó phá còn
            // hơn xóa trắng thành tích của người ta.
            //
            // Score giữ nguyên, không đưa về 0: ván đang chơi dở vẫn là ván đó,
            // chốt sổ bình thường khi hết mạng hoặc hết bộ câu.
            migrationBuilder.Sql("UPDATE PlayerStates SET BestScore = Score;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BestScore",
                table: "PlayerStates");
        }
    }
}
