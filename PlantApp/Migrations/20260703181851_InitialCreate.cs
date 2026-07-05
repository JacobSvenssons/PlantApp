using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace PlantApp.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Trakter",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Namn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SkapadDatum = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trakter", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DelOmraden",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TraktId = table.Column<int>(type: "int", nullable: false),
                    Areal = table.Column<double>(type: "float", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AntalPlantor = table.Column<int>(type: "int", nullable: true),
                    AntalPlantorProvyta = table.Column<int>(type: "int", nullable: true),
                    Geometri = table.Column<Polygon>(type: "geometry", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DelOmraden", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DelOmraden_Trakter_TraktId",
                        column: x => x.TraktId,
                        principalTable: "Trakter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DelOmraden_TraktId",
                table: "DelOmraden",
                column: "TraktId");

            // Spatialt index på polygon-kolumnen. Kan inte uttryckas via EF:s modell, därför rå SQL.
            // BOUNDING_BOX = (xmin, ymin, xmax, ymax) i SWEREF99 TM-meter (SRID 3006) – "hela Sverige".
            migrationBuilder.Sql(@"
                CREATE SPATIAL INDEX [SIX_DelOmraden_Geometri]
                ON [dbo].[DelOmraden] ([Geometri])
                USING GEOMETRY_AUTO_GRID
                WITH (
                    BOUNDING_BOX = (200000, 6100000, 1000000, 7700000),
                    CELLS_PER_OBJECT = 16
                );");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Släpp det spatiala indexet innan tabellen tas bort (måste ske före DropTable).
            migrationBuilder.Sql(
                "DROP INDEX IF EXISTS [SIX_DelOmraden_Geometri] ON [dbo].[DelOmraden];");

            migrationBuilder.DropTable(
                name: "DelOmraden");

            migrationBuilder.DropTable(
                name: "Trakter");
        }
    }
}
