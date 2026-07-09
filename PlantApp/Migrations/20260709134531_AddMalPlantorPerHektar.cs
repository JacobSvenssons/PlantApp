using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantApp.Migrations
{
    /// <inheritdoc />
    public partial class AddMalPlantorPerHektar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MalPlantorPerHektar",
                table: "Trakter",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MalPlantorPerHektar",
                table: "DelOmraden",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MalPlantorPerHektar",
                table: "Trakter");

            migrationBuilder.DropColumn(
                name: "MalPlantorPerHektar",
                table: "DelOmraden");
        }
    }
}
