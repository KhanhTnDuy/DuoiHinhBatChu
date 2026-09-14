using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DuoiHinhBatChu.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChonLoiChoiMoiVan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Chỉ thêm cột, không đụng dữ liệu. Mặc định 0 = Ngẫu nhiên, đúng
            // với mọi ván đang dở từ trước: hồi đó chỉ có một cách xáo là xáo
            // hết, nên dòng cũ nhận đúng lối chơi mà nó đã được dựng.
            migrationBuilder.AddColumn<int>(
                name: "RunOrder",
                table: "PlayerStates",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RunOrder",
                table: "PlayerStates");
        }
    }
}
