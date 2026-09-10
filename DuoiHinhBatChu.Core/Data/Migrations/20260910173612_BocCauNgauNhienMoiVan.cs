using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DuoiHinhBatChu.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class BocCauNgauNhienMoiVan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Để mặc 0 cho mọi dòng cũ là đúng ý: 0 nghĩa là "chưa có ván nào",
            // nên ai đang chơi dở sẽ được coi như bắt đầu ván mới ở lần vào sau
            // — vẫn giữ điểm, mạng và kim cương, chỉ là thứ tự câu xáo lại từ
            // đầu. Không có cách nào dựng lại thứ tự cũ vì hồi đó nó là thứ tự
            // cố định theo tên file, không phải một ván có hạt giống riêng.
            migrationBuilder.AddColumn<int>(
                name: "RunSeed",
                table: "PlayerStates",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RunSeed",
                table: "PlayerStates");
        }
    }
}
