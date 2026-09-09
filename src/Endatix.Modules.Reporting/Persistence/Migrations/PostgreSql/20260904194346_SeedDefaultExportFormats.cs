using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Modules.Reporting.Persistence.Migrations.PostgreSql
{
    /// <summary>
    /// Backfills Native CSV, JSON, Excel, and Codebook (plus CSV default mapping) for every tenant.
    /// SQL is a frozen literal — see <see cref="SeedDefaultExportFormatsSql"/>.
    /// </summary>
    public partial class SeedDefaultExportFormats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SeedDefaultExportFormatsSql.Up);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SeedDefaultExportFormatsSql.DownXlsx);
        }
    }
}
