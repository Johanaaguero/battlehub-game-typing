
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TypingBattle.Api.Persistence.Migrations
{
    public partial class AddGuidPrimaryKeyToGameResults : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Retirar la clave foránea antes de cambiar la clave primaria.
            migrationBuilder.DropForeignKey(
                name: "FK_player_results_game_results_MatchId",
                table: "player_results");

            migrationBuilder.Sql(
                "ALTER TABLE `game_results` DROP PRIMARY KEY;");

            // Agregar Id como nullable para poder rellenar las filas existentes.
            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "game_results",
                type: "char(36)",
                nullable: true);

            // Generar un UUID diferente para cada partida existente.
            migrationBuilder.Sql(
                "UPDATE `game_results` SET `Id` = UUID() WHERE `Id` IS NULL;");

            // Exigir que cada partida tenga un Id.
            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "game_results",
                type: "char(36)",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "char(36)",
                oldNullable: true);

            // Conservar MatchId como identificador único de la partida.
            migrationBuilder.AddUniqueConstraint(
                name: "AK_game_results_MatchId",
                table: "game_results",
                column: "MatchId");

            migrationBuilder.Sql(
                "ALTER TABLE `game_results` ADD PRIMARY KEY (`Id`);");

            // Restaurar la relación de los jugadores mediante MatchId.
            migrationBuilder.AddForeignKey(
                name: "FK_player_results_game_results_MatchId",
                table: "player_results",
                column: "MatchId",
                principalTable: "game_results",
                principalColumn: "MatchId",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Retirar la relación antes de restaurar la clave primaria original.
            migrationBuilder.DropForeignKey(
                name: "FK_player_results_game_results_MatchId",
                table: "player_results");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_game_results_MatchId",
                table: "game_results");

            migrationBuilder.Sql(
                "ALTER TABLE `game_results` DROP PRIMARY KEY;");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "game_results");

            migrationBuilder.Sql(
                "ALTER TABLE `game_results` ADD PRIMARY KEY (`MatchId`);");

            // Restaurar la relación original.
            migrationBuilder.AddForeignKey(
                name: "FK_player_results_game_results_MatchId",
                table: "player_results",
                column: "MatchId",
                principalTable: "game_results",
                principalColumn: "MatchId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
