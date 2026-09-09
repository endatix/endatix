using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Modules.Reporting.Persistence.Migrations.PostgreSql
{
    /// <summary>
    /// Backfills the Excel (XLSX) submissions export format for every tenant. Reporting lists the
    /// rows in <c>reporting.ExportFormats</c>, so the capability alone is not selectable in Hub.
    /// Deterministic ids follow the <c>InitialReporting</c> convention — see the comment there for
    /// why <c>hashtextextended</c> is used instead of arithmetic on snowflake tenant ids.
    /// </summary>
    public partial class SeedXlsxExportFormat : Migration
    {
        private const string XlsxIdExpression =
            "GREATEST(1, hashtextextended(t.\"Id\"::text || ':xlsx', 0) & 9223372036854775807::bigint)";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOT EXISTS (unfiltered by IsDeleted) keeps a tenant's own Excel format — or a
            // deliberately deleted one — from being duplicated or resurrected.
            migrationBuilder.Sql($$"""
                INSERT INTO reporting."ExportFormats"
                    ("Id", "TenantId", "Name", "ExportTarget", "DeliveryFormat", "Profile",
                     "Description", "SettingsJson", "CreatedAt", "IsDeleted")
                SELECT
                    {{XlsxIdExpression}},
                    t."Id",
                    'Excel (XLSX)',
                    'Submissions',
                    'Xlsx',
                    'Native',
                    'Default Excel export for form submissions',
                    '{"aliasProfile":"native","keySeparator":"__","includeTestSubmissions":false}',
                    NOW(),
                    FALSE
                FROM "Tenants" t
                WHERE t."IsDeleted" = FALSE
                  AND NOT EXISTS (
                      SELECT 1
                      FROM reporting."ExportFormats" existing
                      WHERE existing."TenantId" = t."Id"
                        AND existing."ExportTarget" = 'Submissions'
                        AND existing."DeliveryFormat" = 'Xlsx'
                        AND existing."Profile" = 'Native')
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only rows this migration created (deterministic id). Mappings go first: the
            // ExportFormatId foreign key is ON DELETE RESTRICT.
            migrationBuilder.Sql($$"""
                DELETE FROM reporting."SurveyTypeExportMappings" mapping
                USING "Tenants" t
                WHERE mapping."TenantId" = t."Id"
                  AND mapping."ExportFormatId" = {{XlsxIdExpression}};

                DELETE FROM reporting."ExportFormats" format
                USING "Tenants" t
                WHERE format."TenantId" = t."Id"
                  AND format."Id" = {{XlsxIdExpression}};
                """);
        }
    }
}
