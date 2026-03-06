using Endatix.Framework.Scripts;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.PostgreSql.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class ExportsPaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS export_form_submissions(bigint);");
            var buildColumnPathScript = migrationBuilder.ReadEmbeddedSqlScript("Functions/build_column_path_with_jsonpath.sql");
            migrationBuilder.Sql(buildColumnPathScript);

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS export_form_submissions_nested_loops(bigint);");
            var exportNestedLoopsScript = migrationBuilder.ReadEmbeddedSqlScript("Functions/export_form_submissions_nested_loops.sql");
            migrationBuilder.Sql(exportNestedLoopsScript);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS export_form_submissions(bigint, bigint, int);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS export_form_submissions_nested_loops(bigint, bigint, int);");
        }
    }
}
