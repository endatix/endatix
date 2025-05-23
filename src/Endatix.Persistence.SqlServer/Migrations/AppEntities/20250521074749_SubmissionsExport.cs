using Microsoft.EntityFrameworkCore.Migrations;
using Endatix.Framework.Scripts;

#nullable disable

namespace Endatix.Persistence.SqlServer.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class SubmissionsExport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var script = migrationBuilder.ReadEmbeddedSqlScript("Procedures/export_form_submissions.sql");
            migrationBuilder.Sql(script);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.export_form_submissions;");
        }
    }
}
