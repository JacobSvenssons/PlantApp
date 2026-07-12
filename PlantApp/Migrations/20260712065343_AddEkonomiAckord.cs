using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantApp.Migrations
{
    /// <inheritdoc />
    public partial class AddEkonomiAckord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ErsattningsTyp",
                table: "Trakter",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErsattningsTyp",
                table: "DelOmraden",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RegistreradDatum",
                table: "DelOmraden",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EkonomiInstallningar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StandardErsattningsTyp = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EkonomiInstallningar", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "EkonomiInstallningar",
                columns: new[] { "Id", "StandardErsattningsTyp" },
                values: new object[] { 1, "HelAckord" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EkonomiInstallningar");

            migrationBuilder.DropColumn(
                name: "ErsattningsTyp",
                table: "Trakter");

            migrationBuilder.DropColumn(
                name: "ErsattningsTyp",
                table: "DelOmraden");

            migrationBuilder.DropColumn(
                name: "RegistreradDatum",
                table: "DelOmraden");
        }
    }
}
