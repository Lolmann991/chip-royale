using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChipRoyale.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Profiles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Bankroll_Chips = table.Column<int>(type: "INTEGER", nullable: false),
                    Bankroll_TableChips = table.Column<int>(type: "INTEGER", nullable: false),
                    Bankroll_Run = table.Column<int>(type: "INTEGER", nullable: false),
                    Bankroll_RunPeak = table.Column<int>(type: "INTEGER", nullable: false),
                    Bankroll_BestPeak = table.Column<int>(type: "INTEGER", nullable: false),
                    Bankroll_HandsPlayed = table.Column<int>(type: "INTEGER", nullable: false),
                    Bankroll_HandsWon = table.Column<int>(type: "INTEGER", nullable: false),
                    Bankroll_BiggestPot = table.Column<int>(type: "INTEGER", nullable: false),
                    Bankroll_Busts = table.Column<int>(type: "INTEGER", nullable: false),
                    Bankroll_TablesCleared = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Profiles", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_Profiles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_Bankroll_BestPeak",
                table: "Profiles",
                column: "Bankroll_BestPeak");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Profiles");
        }
    }
}
