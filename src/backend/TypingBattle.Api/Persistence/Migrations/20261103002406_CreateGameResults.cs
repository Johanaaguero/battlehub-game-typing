using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TypingBattle.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreateGameResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "game_results",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    MatchId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    GameType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    WinnerUserId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    PlayersCount = table.Column<int>(type: "int", nullable: false),
                    MetadataJson = table.Column<string>(type: "json", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_results", x => x.Id);
                    table.UniqueConstraint("AK_game_results_MatchId", x => x.MatchId);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_game_results_FinishedAt",
                table: "game_results",
                column: "FinishedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "game_results");
        }
    }
}
