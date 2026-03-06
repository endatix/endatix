using Endatix.Framework.Scripts;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.SqlServer.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class ExportsPaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.export_form_submissions;");
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
