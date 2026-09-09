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
            RecreateDefaultMappingIndexes(migrationBuilder, includeIsDeleted: true);
            migrationBuilder.Sql(SeedDefaultExportFormatsSql.Up);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SeedDefaultExportFormatsSql.DownXlsx);
            RecreateDefaultMappingIndexes(migrationBuilder, includeIsDeleted: false);
        }

        private static void RecreateDefaultMappingIndexes(MigrationBuilder migrationBuilder, bool includeIsDeleted)
        {
            string deletedClause = includeIsDeleted ? " AND \"IsDeleted\" = false" : string.Empty;

            migrationBuilder.DropIndex(
                name: "IX_SurveyTypeExportMappings_TenantId",
                schema: "reporting",
                table: "SurveyTypeExportMappings");

            migrationBuilder.DropIndex(
                name: "IX_SurveyTypeExportMappings_TenantId_SurveyTypeId",
                schema: "reporting",
                table: "SurveyTypeExportMappings");

            migrationBuilder.CreateIndex(
                name: "IX_SurveyTypeExportMappings_TenantId",
                schema: "reporting",
                table: "SurveyTypeExportMappings",
                column: "TenantId",
                unique: true,
                filter: "\"IsDefault\" = true AND \"SurveyTypeId\" IS NULL" + deletedClause);

            migrationBuilder.CreateIndex(
                name: "IX_SurveyTypeExportMappings_TenantId_SurveyTypeId",
                schema: "reporting",
                table: "SurveyTypeExportMappings",
                columns: new[] { "TenantId", "SurveyTypeId" },
                unique: true,
                filter: "\"IsDefault\" = true AND \"SurveyTypeId\" IS NOT NULL" + deletedClause);
        }
    }
}
