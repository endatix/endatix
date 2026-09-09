using Endatix.Modules.Reporting.Features.ExportFormats;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Modules.Reporting.Persistence.Migrations.PostgreSql
{
    /// <summary>
    /// Backfills Native CSV, JSON, Excel, and Codebook (plus CSV default mapping) for every tenant.
    /// SQL comes from <see cref="DefaultExportFormats"/>. Ids follow the <c>InitialReporting</c>
    /// <c>hashtextextended</c> convention.
    /// </summary>
    public partial class SeedDefaultExportFormats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DefaultExportFormatsPostgresBackfill.UpSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // CSV/JSON/Codebook also exist from InitialReporting — only reverse Excel (not in that seed).
            migrationBuilder.Sql(DefaultExportFormatsPostgresBackfill.DownXlsxSql);
        }
    }
}
