using Endatix.Framework.Scripts;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.SqlServer.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class AddCollectionStatusToLegacyExport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(migrationBuilder.ReadEmbeddedSqlScript("Procedures/export_form_submissions_v5.sql", GetType().Assembly));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(migrationBuilder.ReadEmbeddedSqlScript("Procedures/export_form_submissions_v4.sql", GetType().Assembly));
        }
    }
}
