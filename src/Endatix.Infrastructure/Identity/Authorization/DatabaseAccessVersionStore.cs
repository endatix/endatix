using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>
/// Keeps access versions in <c>identity.AccessVersions</c>, in the identity database, so a change commits with the
/// role change that caused it. The versions are read on every request and never cached: a cached copy could trail a
/// commit on another instance. Only the deployment prefix, which never changes, is read once per database.
/// </summary>
internal sealed class DatabaseAccessVersionStore(AppIdentityDbContext db) : IAccessVersionStore
{
    /// <summary>The deployment prefix of each database this process has read, by data source and database name.</summary>
    private static readonly ConcurrentDictionary<string, Guid> _deployments = new();

    /// <inheritdoc />
    public async Task<AccessVersionKey> ReadAsync(long tenantId, CancellationToken cancellationToken)
    {
        var scopeRow = tenantId > 0 ? tenantId : AccessVersion.ANY_TENANT_ID;
        var rows = await db.AccessVersions
            .AsNoTracking()
            .Where(version => version.TenantId == AccessVersion.SYSTEM_TENANT_ID || version.TenantId == scopeRow)
            .Select(version => new { version.TenantId, version.Value })
            .ToListAsync(cancellationToken);
        var deployment = await DeploymentAsync(cancellationToken);

        Guid ValueOf(long row) => rows.Find(version => version.TenantId == row)?.Value ?? AccessVersion.Initial;
        return new AccessVersionKey(deployment, ValueOf(AccessVersion.SYSTEM_TENANT_ID), ValueOf(scopeRow));
    }

    /// <inheritdoc />
    public void Change(IReadOnlyCollection<long> tenantIds)
    {
        var sql = UpsertSql();
        foreach (var row in RowsToChange(tenantIds))
        {
            db.Database.ExecuteSqlRaw(sql, row, Guid.NewGuid());
        }
    }

    /// <inheritdoc />
    public async Task ChangeAsync(IReadOnlyCollection<long> tenantIds, CancellationToken cancellationToken)
    {
        var sql = UpsertSql();
        foreach (var row in RowsToChange(tenantIds))
        {
            await db.Database.ExecuteSqlRawAsync(sql, [row, Guid.NewGuid()], cancellationToken);
        }
    }

    /// <summary>The rows to write, in ascending order so two transactions never take the same rows in opposite order.</summary>
    private static SortedSet<long> RowsToChange(IReadOnlyCollection<long> tenantIds) =>
        [AccessVersion.ANY_TENANT_ID, .. tenantIds.Select(AccessVersion.RowFor)];

    /// <summary>The deployment prefix, created on the first read of a database without one.</summary>
    private async Task<Guid> DeploymentAsync(CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var database = $"{connection.DataSource}/{connection.Database}";
        if (_deployments.TryGetValue(database, out var known))
        {
            return known;
        }

        await db.Database.ExecuteSqlRawAsync(InsertIfAbsentSql(), [AccessVersion.DEPLOYMENT_ID, Guid.NewGuid()], cancellationToken);
        var deployment = await db.AccessVersions
            .AsNoTracking()
            .Where(version => version.TenantId == AccessVersion.DEPLOYMENT_ID)
            .Select(version => version.Value)
            .SingleAsync(cancellationToken);
        return _deployments.GetOrAdd(database, deployment);
    }

    /// <summary>Sets row <c>{0}</c> to value <c>{1}</c>, inserting it when missing; safe when two first changes race.</summary>
    private string UpsertSql()
    {
        var (table, tenant, value) = Names();
        if (db.Database.IsNpgsql())
        {
            return $"INSERT INTO {table} ({tenant}, {value}) VALUES ({{0}}, {{1}}) " +
                   $"ON CONFLICT ({tenant}) DO UPDATE SET {value} = EXCLUDED.{value}";
        }

        if (db.Database.IsSqlServer())
        {
            // HOLDLOCK keeps the key range locked when the row is missing, so a racing first change waits and then updates.
            return $"UPDATE {table} WITH (UPDLOCK, HOLDLOCK) SET {value} = {{1}} WHERE {tenant} = {{0}}; " +
                   $"IF @@ROWCOUNT = 0 INSERT INTO {table} ({tenant}, {value}) VALUES ({{0}}, {{1}});";
        }

        throw UnsupportedProvider();
    }

    /// <summary>Inserts row <c>{0}</c> with value <c>{1}</c> unless it exists; safe when two instances start at once.</summary>
    private string InsertIfAbsentSql()
    {
        var (table, tenant, value) = Names();
        if (db.Database.IsNpgsql())
        {
            return $"INSERT INTO {table} ({tenant}, {value}) VALUES ({{0}}, {{1}}) ON CONFLICT ({tenant}) DO NOTHING";
        }

        if (db.Database.IsSqlServer())
        {
            return $"IF NOT EXISTS (SELECT 1 FROM {table} WITH (UPDLOCK, HOLDLOCK) WHERE {tenant} = {{0}}) " +
                   $"INSERT INTO {table} ({tenant}, {value}) VALUES ({{0}}, {{1}});";
        }

        throw UnsupportedProvider();
    }

    /// <summary>The delimited table and column names, from the model.</summary>
    private (string Table, string Tenant, string Value) Names()
    {
        var entityType = db.Model.FindEntityType(typeof(AccessVersion))!;
        var table = StoreObjectIdentifier.Table(entityType.GetTableName()!, entityType.GetSchema());
        var sql = db.GetService<ISqlGenerationHelper>();
        string Column(string property) =>
            sql.DelimitIdentifier(entityType.FindProperty(property)!.GetColumnName(table) ?? property);
        return (sql.DelimitIdentifier(table.Name, table.Schema), Column(nameof(AccessVersion.TenantId)), Column(nameof(AccessVersion.Value)));
    }

    private NotSupportedException UnsupportedProvider() =>
        new($"Access versions support PostgreSQL and SQL Server, not '{db.Database.ProviderName}'.");
}
