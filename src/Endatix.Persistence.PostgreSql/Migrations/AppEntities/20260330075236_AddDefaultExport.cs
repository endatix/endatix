using Endatix.Framework.Scripts;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.PostgreSql.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class AddDefaultExport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS export_form_submissions(bigint);");
            var addExportFunctionScript = migrationBuilder.ReadEmbeddedSqlScript("Functions/export_form_submissions.sql");
            migrationBuilder.Sql(addExportFunctionScript);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
