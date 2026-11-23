using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChipRoyale.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBlackjackStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Bankroll_BlackjackBiggestWin",
                table: "Profiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Bankroll_BlackjackNaturals",
                table: "Profiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Bankroll_BlackjackRounds",
                table: "Profiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Bankroll_BlackjackWon",
                table: "Profiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Bankroll_BlackjackBiggestWin",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "Bankroll_BlackjackNaturals",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "Bankroll_BlackjackRounds",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "Bankroll_BlackjackWon",
                table: "Profiles");
        }
    }
}
