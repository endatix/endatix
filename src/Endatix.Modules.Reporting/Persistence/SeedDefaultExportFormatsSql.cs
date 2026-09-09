namespace Endatix.Modules.Reporting.Persistence;

/// <summary>
/// Frozen PG SQL for <c>20260904194346_SeedDefaultExportFormats</c>. Do not generate this from
/// <c>DefaultExportFormats</c> — applied history never re-runs, so catalog drift would fork
/// existing vs new databases. New defaults = a new migration.
/// </summary>
internal static class SeedDefaultExportFormatsSql
{
    public const string Up = """
        INSERT INTO reporting."ExportFormats"
            ("Id", "TenantId", "Name", "ExportTarget", "DeliveryFormat", "Profile",
             "Description", "SettingsJson", "CreatedAt", "IsDeleted")
        SELECT
            GREATEST(1, hashtextextended(t."Id"::text || ':csv', 0) & 9223372036854775807::bigint),
            t."Id",
            'CSV',
            'Submissions',
            'Csv',
            'Native',
            'Default CSV export for form submissions',
            '{"aliasProfile":"native","keySeparator":"__","includeTestSubmissions":false}'::jsonb,
            NOW(),
            FALSE
        FROM "Tenants" t
        WHERE t."IsDeleted" = FALSE
          AND NOT EXISTS (
              SELECT 1
              FROM reporting."ExportFormats" existing
              WHERE existing."TenantId" = t."Id"
                AND existing."ExportTarget" = 'Submissions'
                AND existing."DeliveryFormat" = 'Csv'
                AND existing."Profile" = 'Native')
        ON CONFLICT DO NOTHING;

        INSERT INTO reporting."ExportFormats"
            ("Id", "TenantId", "Name", "ExportTarget", "DeliveryFormat", "Profile",
             "Description", "SettingsJson", "CreatedAt", "IsDeleted")
        SELECT
            GREATEST(1, hashtextextended(t."Id"::text || ':json', 0) & 9223372036854775807::bigint),
            t."Id",
            'JSON',
            'Submissions',
            'Json',
            'Native',
            'Default JSON export for form submissions',
            '{"aliasProfile":"native","keySeparator":"__","includeTestSubmissions":false}'::jsonb,
            NOW(),
            FALSE
        FROM "Tenants" t
        WHERE t."IsDeleted" = FALSE
          AND NOT EXISTS (
              SELECT 1
              FROM reporting."ExportFormats" existing
              WHERE existing."TenantId" = t."Id"
                AND existing."ExportTarget" = 'Submissions'
                AND existing."DeliveryFormat" = 'Json'
                AND existing."Profile" = 'Native')
        ON CONFLICT DO NOTHING;

        INSERT INTO reporting."ExportFormats"
            ("Id", "TenantId", "Name", "ExportTarget", "DeliveryFormat", "Profile",
             "Description", "SettingsJson", "CreatedAt", "IsDeleted")
        SELECT
            GREATEST(1, hashtextextended(t."Id"::text || ':xlsx', 0) & 9223372036854775807::bigint),
            t."Id",
            'Excel (XLSX)',
            'Submissions',
            'Xlsx',
            'Native',
            'Default Excel export for form submissions',
            '{"aliasProfile":"native","keySeparator":"__","includeTestSubmissions":false}'::jsonb,
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

        INSERT INTO reporting."ExportFormats"
            ("Id", "TenantId", "Name", "ExportTarget", "DeliveryFormat", "Profile",
             "Description", "SettingsJson", "CreatedAt", "IsDeleted")
        SELECT
            GREATEST(1, hashtextextended(t."Id"::text || ':codebook', 0) & 9223372036854775807::bigint),
            t."Id",
            'Codebook',
            'Codebook',
            'Json',
            'Native',
            'Default form definition codebook export',
            '{"aliasProfile":"native","keySeparator":"__"}'::jsonb,
            NOW(),
            FALSE
        FROM "Tenants" t
        WHERE t."IsDeleted" = FALSE
          AND NOT EXISTS (
              SELECT 1
              FROM reporting."ExportFormats" existing
              WHERE existing."TenantId" = t."Id"
                AND existing."ExportTarget" = 'Codebook'
                AND existing."DeliveryFormat" = 'Json'
                AND existing."Profile" = 'Native')
        ON CONFLICT DO NOTHING;

        INSERT INTO reporting."SurveyTypeExportMappings"
            ("Id", "TenantId", "SurveyTypeId", "ExportFormatId", "IsDefault",
             "CreatedAt", "IsDeleted")
        SELECT
            GREATEST(1, hashtextextended(t."Id"::text || ':default-map', 0) & 9223372036854775807::bigint),
            t."Id",
            NULL,
            csv."Id",
            TRUE,
            NOW(),
            FALSE
        FROM "Tenants" t
        JOIN reporting."ExportFormats" csv
          ON csv."TenantId" = t."Id"
         AND csv."ExportTarget" = 'Submissions'
         AND csv."DeliveryFormat" = 'Csv'
         AND csv."Profile" = 'Native'
         AND csv."IsDeleted" = FALSE
        WHERE t."IsDeleted" = FALSE
          AND NOT EXISTS (
              SELECT 1
              FROM reporting."SurveyTypeExportMappings" mapping
              WHERE mapping."TenantId" = t."Id"
                AND mapping."SurveyTypeId" IS NULL)
        ON CONFLICT DO NOTHING;
        """;

    public const string DownXlsx = """
        DELETE FROM reporting."SurveyTypeExportMappings" mapping
        USING "Tenants" t
        WHERE mapping."TenantId" = t."Id"
          AND mapping."ExportFormatId" = GREATEST(1, hashtextextended(t."Id"::text || ':xlsx', 0) & 9223372036854775807::bigint);

        DELETE FROM reporting."ExportFormats" format
        USING "Tenants" t
        WHERE format."TenantId" = t."Id"
          AND format."Id" = GREATEST(1, hashtextextended(t."Id"::text || ':xlsx', 0) & 9223372036854775807::bigint);
        """;
}
