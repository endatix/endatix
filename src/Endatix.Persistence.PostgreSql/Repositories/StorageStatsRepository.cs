using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.UseCases.Stats.Models;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace Endatix.Persistence.PostgreSql.Repositories;

/// <summary>
/// Repository for storage statistics for PostgreSQL.
/// </summary>
public sealed class StorageStatsRepository : IStorageStatsRepository
{
    private readonly AppDbContext _dbContext;

    public StorageStatsRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private string GetFullTableName<T>() where T : class
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(T));
        var schema = entityType?.GetSchema();
        var tableName = entityType?.GetTableName();

        var effectiveSchema = string.IsNullOrEmpty(schema) ? "public" : schema;
        return $"\"{effectiveSchema}\".\"{tableName}\"";
    }

    public async Task<TenantStorageStats> GetTenantStats(long? tenantId = null, CancellationToken cancellationToken = default)
    {
        var submissionsTable = GetFullTableName<Submission>();
        var versionsTable = GetFullTableName<SubmissionVersion>();

        var tenantFilter = tenantId.HasValue ? $@"WHERE ""TenantId"" = {tenantId.Value}" : "";
        var versionTenantJoin = tenantId.HasValue ? $@"WHERE s.""TenantId"" = {tenantId.Value}" : "";
        var effectiveTenantId = tenantId ?? 0;

        var sql = $@"
            WITH submission_counts AS (
                SELECT
                    COUNT(*) AS submission_count
                FROM {submissionsTable}
                {tenantFilter}
            ),
            version_counts AS (
                SELECT
                    COUNT(*) AS version_count
                FROM {versionsTable} sv
                JOIN {submissionsTable} s ON s.""Id"" = sv.""SubmissionId""
                {versionTenantJoin}
            ),
            submission_table AS (
                SELECT
                    reltuples::bigint AS total_rows,
                    pg_total_relation_size('{submissionsTable}'::regclass) AS total_bytes
                FROM pg_class
                WHERE oid = '{submissionsTable}'::regclass
            ),
            version_table AS (
                SELECT
                    reltuples::bigint AS total_rows,
                    pg_total_relation_size('{versionsTable}'::regclass) AS total_bytes
                FROM pg_class
                WHERE oid = '{versionsTable}'::regclass
            )
            SELECT
                {effectiveTenantId} AS ""TenantId"",
                sc.submission_count AS ""SubmissionCount"",
                vc.version_count AS ""VersionCount"",
                (
                    sc.submission_count * st.total_bytes / NULLIF(st.total_rows, 0)
                    +
                    vc.version_count * vt.total_bytes / NULLIF(vt.total_rows, 0)
                )::bigint AS ""EstimatedStorageBytes""
            FROM submission_counts sc
            CROSS JOIN version_counts vc
            CROSS JOIN submission_table st
            CROSS JOIN version_table vt";

        var result = await _dbContext.Database.SqlQueryRaw<TenantStorageStats>(sql)
            .FirstOrDefaultAsync(cancellationToken);

        return result ?? new TenantStorageStats(effectiveTenantId, 0, 0, 0);
    }

    public async Task<IReadOnlyList<FormStorageStats>> GetFormStats(long? tenantId = null, CancellationToken cancellationToken = default)
    {
        var submissionsTable = GetFullTableName<Submission>();
        var versionsTable = GetFullTableName<SubmissionVersion>();
        var formsTable = GetFullTableName<Form>();

        var sql = $@"
            WITH submission_counts AS (
                SELECT
                    ""FormId"",
                    ""TenantId"",
                    COUNT(*) AS submission_count
                FROM {submissionsTable}
                GROUP BY ""FormId"", ""TenantId""
            ),
            version_counts AS (
                SELECT
                    s.""FormId"",
                    COUNT(*) AS version_count
                FROM {versionsTable} sv
                JOIN {submissionsTable} s ON s.""Id"" = sv.""SubmissionId""
                GROUP BY s.""FormId""
            ),
            submission_table AS (
                SELECT
                    reltuples::bigint AS total_rows,
                    pg_total_relation_size('{submissionsTable}'::regclass) AS total_bytes
                FROM pg_class
                WHERE oid = '{submissionsTable}'::regclass
            ),
            version_table AS (
                SELECT
                    reltuples::bigint AS total_rows,
                    pg_total_relation_size('{versionsTable}'::regclass) AS total_bytes
                FROM pg_class
                WHERE oid = '{versionsTable}'::regclass
            )
            SELECT
                sc.""TenantId"" AS ""TenantId"",
                sc.""FormId"" AS ""FormId"",
                f.""Name"" AS ""FormName"",
                sc.submission_count AS ""SubmissionCount"",
                COALESCE(vc.version_count, 0) AS ""VersionCount"",
                (
                    sc.submission_count * st.total_bytes / NULLIF(st.total_rows, 0)
                    +
                    COALESCE(vc.version_count, 0) * vt.total_bytes / NULLIF(vt.total_rows, 0)
                )::bigint AS ""EstimatedStorageBytes""
            FROM submission_counts sc
            LEFT JOIN version_counts vc ON sc.""FormId"" = vc.""FormId""
            LEFT JOIN {formsTable} f ON f.""Id"" = sc.""FormId""
            CROSS JOIN submission_table st
            CROSS JOIN version_table vt";

        var query = _dbContext.Database.SqlQueryRaw<FormStorageStats>(sql);

        if (tenantId.HasValue)
        {
            query = query.Where(f => f.TenantId == tenantId.Value);
        }

        return await query.OrderByDescending(f => f.EstimatedStorageBytes).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TableStorageStats>> GetTableStats(CancellationToken cancellationToken = default)
    {
        var formsTable = GetFullTableName<Form>();
        var submissionsTable = GetFullTableName<Submission>();
        var versionsTable = GetFullTableName<SubmissionVersion>();
        var tenantsTable = GetFullTableName<TenantSettings>();

        var sql = $@"
            SELECT
                relname AS ""TableName"",
                pg_table_size(relid) AS ""TableSizeBytes"",
                pg_indexes_size(relid) AS ""IndexSizeBytes"",
                pg_total_relation_size(relid) AS ""TotalSizeBytes""
            FROM pg_catalog.pg_statio_user_tables
            WHERE relid IN (
                '{formsTable}'::regclass, 
                '{submissionsTable}'::regclass, 
                '{versionsTable}'::regclass, 
                '{tenantsTable}'::regclass
            )";

        return await _dbContext.Database.SqlQueryRaw<TableStorageStats>(sql)
            .OrderByDescending(t => t.TotalSizeBytes)
            .ToListAsync(cancellationToken);
    }
}
