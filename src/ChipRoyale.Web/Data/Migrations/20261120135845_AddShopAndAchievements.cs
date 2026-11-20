using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChipRoyale.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddShopAndAchievements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Bankroll_Crowns",
                table: "Profiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "EquippedItems",
                table: "Profiles",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OwnedItems",
                table: "Profiles",
                type: "TEXT",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UnlockedAchievements",
                table: "Profiles",
                type: "TEXT",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Bankroll_Crowns",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "EquippedItems",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "OwnedItems",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "UnlockedAchievements",
                table: "Profiles");
        }
    }
}
