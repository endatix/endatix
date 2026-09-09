using Endatix.Modules.Reporting.Features.ExportFormats;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Modules.Reporting.Persistence.Migrations.PostgreSql
{
    /// <summary>
    /// Backfills Native CSV, JSON, Excel, and Codebook (plus CSV default mapping) for tenants
    /// that missed <c>InitialReporting</c> seed or outbox <c>tenant.created</c>.
    /// SQL is generated from <see cref="DefaultExportFormats"/>.
    /// </summary>
    public partial class SeedMissingDefaultExportFormats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DefaultExportFormatsPostgresBackfill.UpSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Idempotent backfill of the same ids as InitialReporting — do not drop those rows.
        }
    }
}
