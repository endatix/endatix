using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.UseCases.Stats.Models;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Persistence.SqlServer.Repositories;

/// <summary>
/// Repository for storage statistics for SQL Server.
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

        return string.IsNullOrEmpty(schema) ? $"[{tableName}]" : $"[{schema}].[{tableName}]";
    }

    public async Task<TenantStorageStats> GetTenantStats(long? tenantId = null, CancellationToken cancellationToken = default)
    {
        var submissionsTable = GetFullTableName<Submission>();
        var versionsTable = GetFullTableName<SubmissionVersion>();
        var subName = _dbContext.Model.FindEntityType(typeof(Submission))?.GetTableName() ?? "Submissions";
        var verName = _dbContext.Model.FindEntityType(typeof(SubmissionVersion))?.GetTableName() ?? "SubmissionVersions";

        var tenantFilter = tenantId.HasValue ? $@"WHERE TenantId = {tenantId.Value}" : "";
        var versionTenantJoin = tenantId.HasValue ? $@"WHERE s.TenantId = {tenantId.Value}" : "";
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
                JOIN {submissionsTable} s ON s.Id = sv.SubmissionId
                {versionTenantJoin}
            ),
            submission_table AS (
                SELECT
                    SUM(s.row_count) AS total_rows,
                    SUM(s.used_page_count) * 8 * 1024 AS total_bytes
                FROM sys.dm_db_partition_stats s
                WHERE object_id = OBJECT_ID('{subName}')
            ),
            version_table AS (
                SELECT
                    SUM(s.row_count) AS total_rows,
                    SUM(s.used_page_count) * 8 * 1024 AS total_bytes
                FROM sys.dm_db_partition_stats s
                WHERE object_id = OBJECT_ID('{verName}')
            )
            SELECT
                {effectiveTenantId} AS TenantId,
                sc.submission_count AS SubmissionCount,
                vc.version_count AS VersionCount,
                CAST((
                    sc.submission_count * CAST(st.total_bytes AS FLOAT) / NULLIF(st.total_rows, 0)
                    +
                    vc.version_count * CAST(vt.total_bytes AS FLOAT) / NULLIF(vt.total_rows, 0)
                ) AS BIGINT) AS EstimatedStorageBytes
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
        var subName = _dbContext.Model.FindEntityType(typeof(Submission))?.GetTableName() ?? "Submissions";
        var verName = _dbContext.Model.FindEntityType(typeof(SubmissionVersion))?.GetTableName() ?? "SubmissionVersions";

        var sql = $@"
            WITH submission_counts AS (
                SELECT
                    FormId,
                    TenantId,
                    COUNT(*) AS submission_count
                FROM {submissionsTable}
                GROUP BY FormId, TenantId
            ),
            version_counts AS (
                SELECT
                    s.FormId,
                    COUNT(*) AS version_count
                FROM {versionsTable} sv
                JOIN {submissionsTable} s ON s.Id = sv.SubmissionId
                GROUP BY s.FormId
            ),
            submission_table AS (
                SELECT
                    SUM(s.row_count) AS total_rows,
                    SUM(s.used_page_count) * 8 * 1024 AS total_bytes
                FROM sys.dm_db_partition_stats s
                WHERE object_id = OBJECT_ID('{subName}')
            ),
            version_table AS (
                SELECT
                    SUM(s.row_count) AS total_rows,
                    SUM(s.used_page_count) * 8 * 1024 AS total_bytes
                FROM sys.dm_db_partition_stats s
                WHERE object_id = OBJECT_ID('{verName}')
            )
            SELECT
                sc.TenantId AS TenantId,
                sc.FormId AS FormId,
                f.Name AS FormName,
                sc.submission_count AS SubmissionCount,
                COALESCE(vc.version_count, 0) AS VersionCount,
                CAST((
                    sc.submission_count * CAST(st.total_bytes AS FLOAT) / NULLIF(st.total_rows, 0)
                    +
                    COALESCE(vc.version_count, 0) * CAST(vt.total_bytes AS FLOAT) / NULLIF(vt.total_rows, 0)
                ) AS BIGINT) AS EstimatedStorageBytes
            FROM submission_counts sc
            LEFT JOIN version_counts vc ON sc.FormId = vc.FormId
            LEFT JOIN {formsTable} f ON f.Id = sc.FormId
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
        var formsName = _dbContext.Model.FindEntityType(typeof(Form))?.GetTableName() ?? "Forms";
        var submissionsName = _dbContext.Model.FindEntityType(typeof(Submission))?.GetTableName() ?? "Submissions";
        var versionsName = _dbContext.Model.FindEntityType(typeof(SubmissionVersion))?.GetTableName() ?? "SubmissionVersions";
        var tenantsName = _dbContext.Model.FindEntityType(typeof(TenantSettings))?.GetTableName() ?? "TenantSettings";

        var sql = $@"
            SELECT 
                t.NAME AS TableName,
                SUM(CASE WHEN i.index_id <= 1 THEN a.used_pages ELSE 0 END) * 8 * 1024 AS TableSizeBytes,
                SUM(CASE WHEN i.index_id > 1 THEN a.used_pages ELSE 0 END) * 8 * 1024 AS IndexSizeBytes,
                SUM(a.total_pages) * 8 * 1024 AS TotalSizeBytes
            FROM sys.tables t
            INNER JOIN sys.indexes i ON t.OBJECT_ID = i.object_id
            INNER JOIN sys.partitions p ON i.object_id = p.OBJECT_ID AND i.index_id = p.index_id
            INNER JOIN sys.allocation_units a ON p.partition_id = a.container_id
            WHERE t.NAME IN ('{formsName}', '{submissionsName}', '{versionsName}', '{tenantsName}', 'Users')
            GROUP BY t.NAME";

        return await _dbContext.Database.SqlQueryRaw<TableStorageStats>(sql)
            .OrderByDescending(t => t.TotalSizeBytes)
            .ToListAsync(cancellationToken);
    }
}
