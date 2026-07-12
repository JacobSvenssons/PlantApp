using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantApp.Migrations
{
    /// <inheritdoc />
    public partial class AddTidrapport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tidrapporter",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Anvandare = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    VeckoStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AntalTimmar = table.Column<double>(type: "float", nullable: false),
                    ErsattningsTyp = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TraktId = table.Column<int>(type: "int", nullable: true),
                    Kommentar = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SkapadDatum = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tidrapporter", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tidrapporter_Trakter_TraktId",
                        column: x => x.TraktId,
                        principalTable: "Trakter",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tidrapporter_Anvandare_VeckoStart",
                table: "Tidrapporter",
                columns: new[] { "Anvandare", "VeckoStart" });

            migrationBuilder.CreateIndex(
                name: "IX_Tidrapporter_TraktId",
                table: "Tidrapporter",
                column: "TraktId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tidrapporter");
        }
    }
}
