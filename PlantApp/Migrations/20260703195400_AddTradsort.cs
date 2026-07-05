using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantApp.Migrations
{
    /// <inheritdoc />
    public partial class AddTradsort : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Tradsort",
                table: "DelOmraden",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Tradsort",
                table: "DelOmraden");
        }
    }
}
