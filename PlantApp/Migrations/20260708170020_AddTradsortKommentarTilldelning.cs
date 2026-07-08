using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantApp.Migrations
{
    /// <inheritdoc />
    public partial class AddTradsortKommentarTilldelning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StandardTradsort",
                table: "Trakter",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kommentar",
                table: "DelOmraden",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TilldeladAnvandare",
                table: "DelOmraden",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StandardTradsort",
                table: "Trakter");

            migrationBuilder.DropColumn(
                name: "Kommentar",
                table: "DelOmraden");

            migrationBuilder.DropColumn(
                name: "TilldeladAnvandare",
                table: "DelOmraden");
        }
    }
}
