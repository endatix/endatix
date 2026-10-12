namespace Endatix.Modules.Reporting.Persistence;

/// <summary>
/// Frozen T-SQL that seeds the default export formats at the end of the SQL Server <c>InitialReporting</c>
/// migration: the rows <see cref="SeedDefaultExportFormatsSql"/> inserts on PostgreSQL, for every tenant that is
/// not deleted. Do not generate this from <c>DefaultExportFormats</c>: applied history never re-runs, so catalog
/// drift would fork existing and new databases. New defaults need a new migration.
/// </summary>
/// <remarks>
/// SQL Server has no <c>ON CONFLICT DO NOTHING</c>, so each insert skips, through <c>NOT EXISTS</c>, every row the
/// PostgreSQL seed's conflict clause would skip: a format the tenant already has, a name it already uses, and a
/// default mapping it already has. Ids are derived from the tenant id, so a rerun derives the same ids and finds
/// them taken.
/// </remarks>
internal static class SeedDefaultExportFormatsSqlServerSql
{
    public const string Up = """
        INSERT INTO [reporting].[ExportFormats]
            ([Id], [TenantId], [Name], [ExportTarget], [DeliveryFormat], [Profile],
             [Description], [SettingsJson], [CreatedAt], [IsDeleted])
        SELECT
            GREATEST(CAST(1 AS bigint), CONVERT(bigint, HASHBYTES('SHA2_256', CONCAT(t.[Id], ':csv'))) & CAST(9223372036854775807 AS bigint)),
            t.[Id],
            N'CSV',
            N'Submissions',
            N'Csv',
            N'Native',
            N'Default CSV export for form submissions',
            N'{"aliasProfile":"native","keySeparator":"__","includeTestSubmissions":false}',
            SYSUTCDATETIME(),
            0
        FROM [Tenants] t
        WHERE t.[IsDeleted] = 0
          AND NOT EXISTS (
              SELECT 1
              FROM [reporting].[ExportFormats] existing
              WHERE existing.[TenantId] = t.[Id]
                AND ((existing.[ExportTarget] = N'Submissions'
                      AND existing.[DeliveryFormat] = N'Csv'
                      AND existing.[Profile] = N'Native')
                     OR (existing.[Name] = N'CSV' AND existing.[IsDeleted] = 0)));

        INSERT INTO [reporting].[ExportFormats]
            ([Id], [TenantId], [Name], [ExportTarget], [DeliveryFormat], [Profile],
             [Description], [SettingsJson], [CreatedAt], [IsDeleted])
        SELECT
            GREATEST(CAST(1 AS bigint), CONVERT(bigint, HASHBYTES('SHA2_256', CONCAT(t.[Id], ':json'))) & CAST(9223372036854775807 AS bigint)),
            t.[Id],
            N'JSON',
            N'Submissions',
            N'Json',
            N'Native',
            N'Default JSON export for form submissions',
            N'{"aliasProfile":"native","keySeparator":"__","includeTestSubmissions":false}',
            SYSUTCDATETIME(),
            0
        FROM [Tenants] t
        WHERE t.[IsDeleted] = 0
          AND NOT EXISTS (
              SELECT 1
              FROM [reporting].[ExportFormats] existing
              WHERE existing.[TenantId] = t.[Id]
                AND ((existing.[ExportTarget] = N'Submissions'
                      AND existing.[DeliveryFormat] = N'Json'
                      AND existing.[Profile] = N'Native')
                     OR (existing.[Name] = N'JSON' AND existing.[IsDeleted] = 0)));

        INSERT INTO [reporting].[ExportFormats]
            ([Id], [TenantId], [Name], [ExportTarget], [DeliveryFormat], [Profile],
             [Description], [SettingsJson], [CreatedAt], [IsDeleted])
        SELECT
            GREATEST(CAST(1 AS bigint), CONVERT(bigint, HASHBYTES('SHA2_256', CONCAT(t.[Id], ':xlsx'))) & CAST(9223372036854775807 AS bigint)),
            t.[Id],
            N'Excel (XLSX)',
            N'Submissions',
            N'Xlsx',
            N'Native',
            N'Default Excel export for form submissions',
            N'{"aliasProfile":"native","keySeparator":"__","includeTestSubmissions":false}',
            SYSUTCDATETIME(),
            0
        FROM [Tenants] t
        WHERE t.[IsDeleted] = 0
          AND NOT EXISTS (
              SELECT 1
              FROM [reporting].[ExportFormats] existing
              WHERE existing.[TenantId] = t.[Id]
                AND ((existing.[ExportTarget] = N'Submissions'
                      AND existing.[DeliveryFormat] = N'Xlsx'
                      AND existing.[Profile] = N'Native')
                     OR (existing.[Name] = N'Excel (XLSX)' AND existing.[IsDeleted] = 0)));

        INSERT INTO [reporting].[ExportFormats]
            ([Id], [TenantId], [Name], [ExportTarget], [DeliveryFormat], [Profile],
             [Description], [SettingsJson], [CreatedAt], [IsDeleted])
        SELECT
            GREATEST(CAST(1 AS bigint), CONVERT(bigint, HASHBYTES('SHA2_256', CONCAT(t.[Id], ':codebook'))) & CAST(9223372036854775807 AS bigint)),
            t.[Id],
            N'Codebook',
            N'Codebook',
            N'Json',
            N'Native',
            N'Default form definition codebook export',
            N'{"aliasProfile":"native","keySeparator":"__"}',
            SYSUTCDATETIME(),
            0
        FROM [Tenants] t
        WHERE t.[IsDeleted] = 0
          AND NOT EXISTS (
              SELECT 1
              FROM [reporting].[ExportFormats] existing
              WHERE existing.[TenantId] = t.[Id]
                AND ((existing.[ExportTarget] = N'Codebook'
                      AND existing.[DeliveryFormat] = N'Json'
                      AND existing.[Profile] = N'Native')
                     OR (existing.[Name] = N'Codebook' AND existing.[IsDeleted] = 0)));

        INSERT INTO [reporting].[SurveyTypeExportMappings]
            ([Id], [TenantId], [SurveyTypeId], [ExportFormatId], [IsDefault],
             [CreatedAt], [IsDeleted])
        SELECT
            GREATEST(CAST(1 AS bigint), CONVERT(bigint, HASHBYTES('SHA2_256', CONCAT(t.[Id], ':default-map'))) & CAST(9223372036854775807 AS bigint)),
            t.[Id],
            NULL,
            csv.[Id],
            1,
            SYSUTCDATETIME(),
            0
        FROM [Tenants] t
        CROSS APPLY (
            SELECT TOP (1) format.[Id]
            FROM [reporting].[ExportFormats] format
            WHERE format.[TenantId] = t.[Id]
              AND format.[ExportTarget] = N'Submissions'
              AND format.[DeliveryFormat] = N'Csv'
              AND format.[Profile] = N'Native'
              AND format.[IsDeleted] = 0
            ORDER BY format.[Id]) csv
        WHERE t.[IsDeleted] = 0
          AND NOT EXISTS (
              SELECT 1
              FROM [reporting].[SurveyTypeExportMappings] mapping
              WHERE mapping.[TenantId] = t.[Id]
                AND mapping.[SurveyTypeId] IS NULL);
        """;
}
