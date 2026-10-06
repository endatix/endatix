using Endatix.Framework.Scripts;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.PostgreSql.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class AddCollectionStatusToLegacyExport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS export_form_submissions(bigint);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS export_form_submissions(bigint, bigint, int);");
            migrationBuilder.Sql(migrationBuilder.ReadEmbeddedSqlScript("Functions/export_form_submissions_v4.sql", GetType().Assembly));

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS export_form_submissions_nested_loops(bigint);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS export_form_submissions_nested_loops(bigint, bigint, int);");
            migrationBuilder.Sql(migrationBuilder.ReadEmbeddedSqlScript("Functions/export_form_submissions_nested_loops_v4.sql", GetType().Assembly));

            migrationBuilder.Sql(migrationBuilder.ReadEmbeddedSqlScript("Functions/export_form_metadata_shoji_v2.sql", GetType().Assembly));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS export_form_submissions(bigint);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS export_form_submissions(bigint, bigint, int);");
            migrationBuilder.Sql(migrationBuilder.ReadEmbeddedSqlScript("Functions/export_form_submissions_v3.sql", GetType().Assembly));

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS export_form_submissions_nested_loops(bigint);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS export_form_submissions_nested_loops(bigint, bigint, int);");
            migrationBuilder.Sql(migrationBuilder.ReadEmbeddedSqlScript("Functions/export_form_submissions_nested_loops_v3.sql", GetType().Assembly));

            migrationBuilder.Sql(migrationBuilder.ReadEmbeddedSqlScript("Functions/export_form_metadata_shoji.sql", GetType().Assembly));
        }
    }
}
