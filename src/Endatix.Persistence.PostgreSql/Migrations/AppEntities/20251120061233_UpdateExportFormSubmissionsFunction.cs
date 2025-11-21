using Microsoft.EntityFrameworkCore.Migrations;
using Endatix.Framework.Scripts;

#nullable disable

namespace Endatix.Persistence.PostgreSql.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class UpdateExportFormSubmissionsFunction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the existing function first since we're changing its signature
            // PostgreSQL doesn't allow changing return types with CREATE OR REPLACE
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS export_form_submissions(bigint);");

            // Create the function with the new signature
            var script = migrationBuilder.ReadEmbeddedSqlScript("Functions/export_form_submissions.sql");
            migrationBuilder.Sql(script);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the updated function
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS export_form_submissions(bigint);");

            // Note: To fully rollback, you would need to recreate the old version
            // For now, we're just dropping it
        }
    }
}
