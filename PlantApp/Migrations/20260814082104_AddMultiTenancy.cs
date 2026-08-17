using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantApp.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "EkonomiInstallningar",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "OrganisationId",
                table: "Trakter",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OrganisationId",
                table: "Tidrapporter",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OrganisationId",
                table: "Inventeringar",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OrganisationId",
                table: "EkonomiInstallningar",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OrganisationId",
                table: "DelOmraden",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OrganisationId",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Organisationer",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Namn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SkapadDatum = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ArAktiv = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organisationer", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Trakter_OrganisationId",
                table: "Trakter",
                column: "OrganisationId");

            migrationBuilder.CreateIndex(
                name: "IX_Tidrapporter_OrganisationId",
                table: "Tidrapporter",
                column: "OrganisationId");

            migrationBuilder.CreateIndex(
                name: "IX_Inventeringar_OrganisationId",
                table: "Inventeringar",
                column: "OrganisationId");

            migrationBuilder.CreateIndex(
                name: "IX_EkonomiInstallningar_OrganisationId",
                table: "EkonomiInstallningar",
                column: "OrganisationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DelOmraden_OrganisationId",
                table: "DelOmraden",
                column: "OrganisationId");

            // Data-migration: koppla befintlig data till en Standardorganisation så inget
            // blir föräldralöst (OrganisationId = 0). Organisationen skapas ENDAST om det
            // faktiskt finns föräldralös data att backfilla – på en tom databas skapas
            // ingen Standardorganisation.
            migrationBuilder.Sql(@"
                DECLARE @orgId int;
                DECLARE @behoverOrg bit = 0;

                IF EXISTS (SELECT 1 FROM [Trakter]       WHERE [OrganisationId] = 0)
                   OR EXISTS (SELECT 1 FROM [DelOmraden]    WHERE [OrganisationId] = 0)
                   OR EXISTS (SELECT 1 FROM [Inventeringar] WHERE [OrganisationId] = 0)
                   OR EXISTS (SELECT 1 FROM [Tidrapporter]  WHERE [OrganisationId] = 0)
                   OR EXISTS (SELECT 1 FROM [EkonomiInstallningar] WHERE [OrganisationId] = 0)
                   OR EXISTS (SELECT 1 FROM [AspNetUsers]   WHERE [OrganisationId] IS NULL AND ([Roll] IS NULL OR [Roll] <> N'SuperAdmin'))
                    SET @behoverOrg = 1;

                IF @behoverOrg = 1
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [Organisationer] WHERE [Namn] = N'Standardorganisation')
                    BEGIN
                        INSERT INTO [Organisationer] ([Namn], [SkapadDatum], [ArAktiv])
                        VALUES (N'Standardorganisation', SYSUTCDATETIME(), 1);
                    END

                    SELECT @orgId = [Id] FROM [Organisationer] WHERE [Namn] = N'Standardorganisation';

                    UPDATE [Trakter]       SET [OrganisationId] = @orgId WHERE [OrganisationId] = 0;
                    UPDATE [DelOmraden]    SET [OrganisationId] = @orgId WHERE [OrganisationId] = 0;
                    UPDATE [Inventeringar] SET [OrganisationId] = @orgId WHERE [OrganisationId] = 0;
                    UPDATE [Tidrapporter]  SET [OrganisationId] = @orgId WHERE [OrganisationId] = 0;
                    UPDATE [EkonomiInstallningar] SET [OrganisationId] = @orgId WHERE [OrganisationId] = 0;
                    UPDATE [AspNetUsers]   SET [OrganisationId] = @orgId
                        WHERE [OrganisationId] IS NULL AND ([Roll] IS NULL OR [Roll] <> N'SuperAdmin');

                    IF NOT EXISTS (SELECT 1 FROM [EkonomiInstallningar] WHERE [OrganisationId] = @orgId)
                    BEGIN
                        INSERT INTO [EkonomiInstallningar] ([OrganisationId], [StandardErsattningsTyp])
                        VALUES (@orgId, N'HelAckord');
                    END
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Organisationer");

            migrationBuilder.DropIndex(
                name: "IX_Trakter_OrganisationId",
                table: "Trakter");

            migrationBuilder.DropIndex(
                name: "IX_Tidrapporter_OrganisationId",
                table: "Tidrapporter");

            migrationBuilder.DropIndex(
                name: "IX_Inventeringar_OrganisationId",
                table: "Inventeringar");

            migrationBuilder.DropIndex(
                name: "IX_EkonomiInstallningar_OrganisationId",
                table: "EkonomiInstallningar");

            migrationBuilder.DropIndex(
                name: "IX_DelOmraden_OrganisationId",
                table: "DelOmraden");

            migrationBuilder.DropColumn(
                name: "OrganisationId",
                table: "Trakter");

            migrationBuilder.DropColumn(
                name: "OrganisationId",
                table: "Tidrapporter");

            migrationBuilder.DropColumn(
                name: "OrganisationId",
                table: "Inventeringar");

            migrationBuilder.DropColumn(
                name: "OrganisationId",
                table: "EkonomiInstallningar");

            migrationBuilder.DropColumn(
                name: "OrganisationId",
                table: "DelOmraden");

            migrationBuilder.DropColumn(
                name: "OrganisationId",
                table: "AspNetUsers");

            migrationBuilder.InsertData(
                table: "EkonomiInstallningar",
                columns: new[] { "Id", "StandardErsattningsTyp" },
                values: new object[] { 1, "HelAckord" });
        }
    }
}
