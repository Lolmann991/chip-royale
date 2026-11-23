using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChipRoyale.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Bankroll_TournamentsPlayed",
                table: "Profiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Bankroll_TournamentsWon",
                table: "Profiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Bankroll_TournamentsPlayed",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "Bankroll_TournamentsWon",
                table: "Profiles");
        }
    }
}
