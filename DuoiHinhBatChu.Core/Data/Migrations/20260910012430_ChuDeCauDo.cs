using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DuoiHinhBatChu.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChuDeCauDo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Puzzles",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                table: "Puzzles");
        }
    }
}
