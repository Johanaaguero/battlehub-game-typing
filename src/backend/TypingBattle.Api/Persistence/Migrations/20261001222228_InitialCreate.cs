using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace TypingBattle.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
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
                    table.PrimaryKey("PK_game_results", x => x.MatchId);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "player_results",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    MatchId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    UserId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Wpm = table.Column<double>(type: "double", nullable: true),
                    Accuracy = table.Column<double>(type: "double", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_results", x => x.Id);
                    table.ForeignKey(
                        name: "FK_player_results_game_results_MatchId",
                        column: x => x.MatchId,
                        principalTable: "game_results",
                        principalColumn: "MatchId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_game_results_FinishedAt",
                table: "game_results",
                column: "FinishedAt");

            migrationBuilder.CreateIndex(
                name: "IX_player_results_MatchId_UserId",
                table: "player_results",
                columns: new[] { "MatchId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_player_results_UserId",
                table: "player_results",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "player_results");

            migrationBuilder.DropTable(
                name: "game_results");
        }
    }
}
