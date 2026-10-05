using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DuoiHinhBatChu.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class BoCotKhongDung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Dọn dòng mồ côi TRƯỚC khi bỏ cột.
            //
            // SQLite không biết "bỏ một cột": EF phải dựng bảng mới không có cột
            // đó rồi copy dữ liệu sang. Cú copy ấy là lần đầu tiên khóa ngoại
            // được kiểm thật, và bảng đang giữ tiến trình của một tài khoản đã
            // bị xóa khỏi Accounts (khóa ngoại khai ON DELETE CASCADE, nhưng có
            // lần xóa tài khoản không đi qua đó). Không dọn thì migration gãy
            // giữa đường với "FOREIGN KEY constraint failed" và game không mở
            // được.
            migrationBuilder.Sql(
                @"DELETE FROM ""PuzzleResults"" WHERE ""AccountId"" NOT IN (SELECT ""Id"" FROM ""Accounts"");");
            migrationBuilder.Sql(
                @"DELETE FROM ""PlayerStates"" WHERE ""AccountId"" NOT IN (SELECT ""Id"" FROM ""Accounts"");");

            migrationBuilder.DropColumn(
                name: "Hint",
                table: "Puzzles");

            migrationBuilder.DropColumn(
                name: "Stars",
                table: "PuzzleResults");

            migrationBuilder.DropColumn(
                name: "IsBgmEnabled",
                table: "PlayerStates");

            migrationBuilder.DropColumn(
                name: "IsTimerEnabled",
                table: "PlayerStates");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Hint",
                table: "Puzzles",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Stars",
                table: "PuzzleResults",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsBgmEnabled",
                table: "PlayerStates",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsTimerEnabled",
                table: "PlayerStates",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
