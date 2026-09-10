using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DuoiHinhBatChu.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class TienTrinhTheoMaCau : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Thứ tự ba bước dưới đây là cố ý, và KHÔNG được đổi.
            //
            // Bản tự sinh của EF đặt DropColumn lên trước AddColumn, tức là xóa
            // số thứ tự cũ đi rồi mới tạo cột mã câu — ai đang chơi dở cũng mất
            // sạch chỗ đang đứng. Phải thêm cột trước, chuyển dữ liệu sang, rồi
            // mới bỏ cột cũ.
            migrationBuilder.AddColumn<string>(
                name: "CurrentPuzzleId",
                table: "PlayerStates",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            // Đổi số thứ tự đang lưu thành mã câu. Cột Order của bảng Puzzles
            // đếm từ 1 còn số thứ tự cũ đếm từ 0, nên lệch đúng một nhịp.
            //
            // Chạy được là nhờ bước này diễn ra TRƯỚC PuzzleSync của lần khởi
            // động này (GameDatabase.EnsureReady gọi Migrate rồi App mới gọi
            // Sync), nên bảng Puzzles vẫn còn nguyên thứ tự mà số cũ trỏ tới.
            // Câu nào không tra ra thì để rỗng — vào game sẽ nhảy tới câu đầu
            // tiên chưa giải, xem GameViewModel.ResumeIndex.
            migrationBuilder.Sql(@"
                UPDATE PlayerStates
                SET CurrentPuzzleId = COALESCE(
                    (SELECT p.Id FROM Puzzles p
                      WHERE p.""Order"" = PlayerStates.CurrentPuzzleIndex + 1), '');");

            migrationBuilder.DropColumn(
                name: "CurrentPuzzleIndex",
                table: "PlayerStates");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentPuzzleId",
                table: "PlayerStates");

            migrationBuilder.AddColumn<int>(
                name: "CurrentPuzzleIndex",
                table: "PlayerStates",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
