using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace PlantApp.Migrations
{
    /// <inheritdoc />
    public partial class AddInventering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Inventeringar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TraktId = table.Column<int>(type: "int", nullable: false),
                    SkapadDatum = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InskickadDatum = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TilldeladAnvandare = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inventeringar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inventeringar_Trakter_TraktId",
                        column: x => x.TraktId,
                        principalTable: "Trakter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InventeringsProvytor",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InventeringId = table.Column<int>(type: "int", nullable: false),
                    DelOmradeId = table.Column<int>(type: "int", nullable: true),
                    AntalPlantor = table.Column<int>(type: "int", nullable: false),
                    Tradsort = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Kommentar = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Plats = table.Column<Point>(type: "geometry", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventeringsProvytor", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventeringsProvytor_Inventeringar_InventeringId",
                        column: x => x.InventeringId,
                        principalTable: "Inventeringar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Inventeringar_TraktId",
                table: "Inventeringar",
                column: "TraktId");

            migrationBuilder.CreateIndex(
                name: "IX_InventeringsProvytor_InventeringId",
                table: "InventeringsProvytor",
                column: "InventeringId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventeringsProvytor");

            migrationBuilder.DropTable(
                name: "Inventeringar");
        }
    }
}
