using Endatix.Modules.Reporting.Features.ExportFormats;

namespace Endatix.Modules.Reporting.Persistence;

/// <summary>
/// Idempotent PG script: insert any missing Native defaults for live tenants.
/// Same <c>hashtextextended</c> ids as <c>InitialReporting</c>.
/// </summary>
internal static class DefaultExportFormatsPostgresBackfill
{
    public static string UpSql
    {
        get
        {
            IEnumerable<string> formatInserts = DefaultExportFormats.All.Select(BuildFormatInsert);
            return string.Join("\n\n", formatInserts) + "\n\n" + BuildDefaultMappingInsert();
        }
    }

    /// <summary>Excel is the only catalog row <c>InitialReporting</c> does not insert.</summary>
    public static string DownXlsxSql
    {
        get
        {
            string xlsxId = HashId(DefaultExportFormats.Xlsx.IdSuffix);
            return $"""
                DELETE FROM reporting."SurveyTypeExportMappings" mapping
                USING "Tenants" t
                WHERE mapping."TenantId" = t."Id"
                  AND mapping."ExportFormatId" = {xlsxId};

                DELETE FROM reporting."ExportFormats" format
                USING "Tenants" t
                WHERE format."TenantId" = t."Id"
                  AND format."Id" = {xlsxId};
                """;
        }
    }

    private static string HashId(string suffix) =>
        $"GREATEST(1, hashtextextended(t.\"Id\"::text || ':{suffix}', 0) & 9223372036854775807::bigint)";

    private static string BuildFormatInsert(DefaultExportFormat format)
    {
        string id = HashId(format.IdSuffix);
        return $"""
            INSERT INTO reporting."ExportFormats"
                ("Id", "TenantId", "Name", "ExportTarget", "DeliveryFormat", "Profile",
                 "Description", "SettingsJson", "CreatedAt", "IsDeleted")
            SELECT
                {id},
                t."Id",
                '{Escape(format.Name)}',
                '{format.Target}',
                '{format.Delivery}',
                'Native',
                '{Escape(format.Description)}',
                '{Escape(format.SettingsJson)}'::jsonb,
                NOW(),
                FALSE
            FROM "Tenants" t
            WHERE t."IsDeleted" = FALSE
              AND NOT EXISTS (
                  SELECT 1
                  FROM reporting."ExportFormats" existing
                  WHERE existing."TenantId" = t."Id"
                    AND existing."ExportTarget" = '{format.Target}'
                    AND existing."DeliveryFormat" = '{format.Delivery}'
                    AND existing."Profile" = 'Native')
            ON CONFLICT DO NOTHING;
            """;
    }

    private static string BuildDefaultMappingInsert()
    {
        DefaultExportFormat csv = DefaultExportFormats.TenantDefault;
        string mappingId = HashId(DefaultExportFormats.MappingIdSuffix);
        return $"""
            INSERT INTO reporting."SurveyTypeExportMappings"
                ("Id", "TenantId", "SurveyTypeId", "ExportFormatId", "IsDefault",
                 "CreatedAt", "IsDeleted")
            SELECT
                {mappingId},
                t."Id",
                NULL,
                csv."Id",
                TRUE,
                NOW(),
                FALSE
            FROM "Tenants" t
            JOIN reporting."ExportFormats" csv
              ON csv."TenantId" = t."Id"
             AND csv."ExportTarget" = '{csv.Target}'
             AND csv."DeliveryFormat" = '{csv.Delivery}'
             AND csv."Profile" = 'Native'
             AND csv."IsDeleted" = FALSE
            WHERE t."IsDeleted" = FALSE
              AND NOT EXISTS (
                  SELECT 1
                  FROM reporting."SurveyTypeExportMappings" mapping
                  WHERE mapping."TenantId" = t."Id"
                    AND mapping."IsDefault" = TRUE
                    AND mapping."SurveyTypeId" IS NULL)
            ON CONFLICT DO NOTHING;
            """;
    }

    private static string Escape(string value) => value.Replace("'", "''");
}
