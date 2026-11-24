using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChipRoyale.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCasinoStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CasinoStats",
                table: "Profiles",
                type: "TEXT",
                maxLength: 8000,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CasinoStats",
                table: "Profiles");
        }
    }
}
