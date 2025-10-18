using Endatix.Framework.Scripts;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.PostgreSql.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class NestedLoopsExportSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create helper function for building column paths with JSONPath expressions
            var helperScript = migrationBuilder.ReadEmbeddedSqlScript("Functions/build_column_path_with_jsonpath.sql");
            migrationBuilder.Sql(helperScript);

            // Create main export function with nested loops support
            var exportScript = migrationBuilder.ReadEmbeddedSqlScript("Functions/export_form_submissions_nested_loops.sql");
            migrationBuilder.Sql(exportScript);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the functions in reverse order
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS export_form_submissions_nested_loops(bigint);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS build_column_path_with_jsonpath(text[], text[], text[], text[], text);");
        }
    }
}
