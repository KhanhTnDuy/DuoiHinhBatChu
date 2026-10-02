using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DuoiHinhBatChu.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class LuuDongHoKhiTamDung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "SecondsLeft",
                table: "PlayerStates",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SecondsLeft",
                table: "PlayerStates");
        }
    }
}
