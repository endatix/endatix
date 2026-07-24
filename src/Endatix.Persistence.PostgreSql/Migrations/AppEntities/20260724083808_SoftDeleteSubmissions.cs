using Endatix.Framework.Scripts;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.PostgreSql.Migrations.AppEntities
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
                filter: "\"RestrictionKey\" IS NOT NULL AND \"IsDeleted\" = false");

             // Exclude soft-deleted rows from legacy SQL export (reporting flag-off path).
            // New script versions — older migrations still ReadEmbeddedSqlScript(v2) at runtime.
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS export_form_submissions(bigint);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS export_form_submissions(bigint, bigint, int);");
            migrationBuilder.Sql(migrationBuilder.ReadEmbeddedSqlScript("Functions/export_form_submissions_v3.sql"));

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS export_form_submissions_nested_loops(bigint);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS export_form_submissions_nested_loops(bigint, bigint, int);");
            migrationBuilder.Sql(migrationBuilder.ReadEmbeddedSqlScript("Functions/export_form_submissions_nested_loops_v3.sql"));
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
                filter: "\"RestrictionKey\" IS NOT NULL");

             migrationBuilder.Sql("DROP FUNCTION IF EXISTS export_form_submissions(bigint);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS export_form_submissions(bigint, bigint, int);");
            migrationBuilder.Sql(migrationBuilder.ReadEmbeddedSqlScript("Functions/export_form_submissions_v2.sql"));

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS export_form_submissions_nested_loops(bigint);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS export_form_submissions_nested_loops(bigint, bigint, int);");
            migrationBuilder.Sql(migrationBuilder.ReadEmbeddedSqlScript("Functions/export_form_submissions_nested_loops_v2.sql"));
        }
    }
}
