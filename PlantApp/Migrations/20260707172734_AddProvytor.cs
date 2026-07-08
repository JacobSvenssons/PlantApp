using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantApp.Migrations
{
    /// <inheritdoc />
    public partial class AddProvytor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AntalPlantorProvyta",
                table: "DelOmraden");

            migrationBuilder.CreateTable(
                name: "Provytor",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DelOmradeId = table.Column<int>(type: "int", nullable: false),
                    AntalPlantor = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Provytor", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Provytor_DelOmraden_DelOmradeId",
                        column: x => x.DelOmradeId,
                        principalTable: "DelOmraden",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Provytor_DelOmradeId",
                table: "Provytor",
                column: "DelOmradeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Provytor");

            migrationBuilder.AddColumn<int>(
                name: "AntalPlantorProvyta",
                table: "DelOmraden",
                type: "int",
                nullable: true);
        }
    }
}
