using Endatix.Framework.Scripts;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.PostgreSql.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class ExcludeTestSubmissionsFromLegacyExport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS export_form_submissions(bigint, bigint, int);");
            migrationBuilder.Sql(migrationBuilder.ReadEmbeddedSqlScript("Functions/export_form_submissions_v5.sql", GetType().Assembly));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS export_form_submissions(bigint, bigint, int, boolean);");
            migrationBuilder.Sql(migrationBuilder.ReadEmbeddedSqlScript("Functions/export_form_submissions_v4.sql", GetType().Assembly));
        }
    }
}
