using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DuoiHinhBatChu.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class BanDau : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 15, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    PasswordSalt = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsGuest = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Puzzles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    Answer = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Hint = table.Column<string>(type: "TEXT", nullable: false),
                    Difficulty = table.Column<int>(type: "INTEGER", nullable: false),
                    ImageName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    ImageBytes = table.Column<byte[]>(type: "BLOB", nullable: false),
                    SourceSize = table.Column<long>(type: "INTEGER", nullable: false),
                    SourceModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Puzzles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlayerStates",
                columns: table => new
                {
                    AccountId = table.Column<string>(type: "TEXT", nullable: false),
                    Score = table.Column<int>(type: "INTEGER", nullable: false),
                    Rubies = table.Column<int>(type: "INTEGER", nullable: false),
                    CorrectStreak = table.Column<int>(type: "INTEGER", nullable: false),
                    Lives = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxLives = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentPuzzleIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    IsSoundEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsBgmEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsTimerEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerStates", x => x.AccountId);
                    table.ForeignKey(
                        name: "FK_PlayerStates_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PuzzleResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountId = table.Column<string>(type: "TEXT", nullable: false),
                    PuzzleId = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Stars = table.Column<int>(type: "INTEGER", nullable: false),
                    SolvedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PuzzleResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PuzzleResults_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PuzzleAnswers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PuzzleId = table.Column<string>(type: "TEXT", nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PuzzleAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PuzzleAnswers_Puzzles_PuzzleId",
                        column: x => x.PuzzleId,
                        principalTable: "Puzzles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Phone",
                table: "Accounts",
                column: "Phone",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_UserName",
                table: "Accounts",
                column: "UserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PuzzleAnswers_PuzzleId_Text",
                table: "PuzzleAnswers",
                columns: new[] { "PuzzleId", "Text" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PuzzleResults_AccountId_PuzzleId",
                table: "PuzzleResults",
                columns: new[] { "AccountId", "PuzzleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Puzzles_ImageName",
                table: "Puzzles",
                column: "ImageName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerStates");

            migrationBuilder.DropTable(
                name: "PuzzleAnswers");

            migrationBuilder.DropTable(
                name: "PuzzleResults");

            migrationBuilder.DropTable(
                name: "Puzzles");

            migrationBuilder.DropTable(
                name: "Accounts");
        }
    }
}
