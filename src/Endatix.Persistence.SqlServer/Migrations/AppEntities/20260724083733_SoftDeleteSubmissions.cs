using Endatix.Framework.Scripts;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.SqlServer.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class SoftDeleteSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Submissions_RestrictionKey",
                table: "Submissions");

            migrationBuilder.CreateIndex(
                name: "UX_Submissions_RestrictionKey",
                table: "Submissions",
                column: "RestrictionKey",
                unique: true,
                filter: "[RestrictionKey] IS NOT NULL AND [IsDeleted] = 0");

            // Exclude soft-deleted rows from legacy SQL export (reporting flag-off path).
            // New script version — older migrations still ReadEmbeddedSqlScript(v3) at runtime.
            migrationBuilder.Sql(migrationBuilder.ReadEmbeddedSqlScript("Procedures/export_form_submissions_v4.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Submissions_RestrictionKey",
                table: "Submissions");

            migrationBuilder.CreateIndex(
                name: "UX_Submissions_RestrictionKey",
                table: "Submissions",
                column: "RestrictionKey",
                unique: true,
                filter: "[RestrictionKey] IS NOT NULL");

             migrationBuilder.Sql(migrationBuilder.ReadEmbeddedSqlScript("Procedures/export_form_submissions_v3.sql"));
        }
    }
}
