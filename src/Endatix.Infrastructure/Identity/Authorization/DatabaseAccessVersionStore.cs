using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>
/// Keeps access versions in <c>identity.AccessVersions</c>, in the identity database, so a change commits with the
/// role change that caused it. Every read goes to the database, deployment prefix included: a cached copy could trail
/// a commit on another instance, or a restore.
/// </summary>
internal sealed class DatabaseAccessVersionStore(AppIdentityDbContext db) : IAccessVersionStore
{
    /// <inheritdoc />
    public async Task<AccessVersionKey> ReadAsync(long tenantId, CancellationToken cancellationToken)
    {
        var scopeRow = tenantId > 0 ? tenantId : AccessVersion.ANY_TENANT_ID;
        var rows = await db.AccessVersions
            .AsNoTracking()
            .Where(version => version.TenantId == AccessVersion.DEPLOYMENT_ID
                || version.TenantId == AccessVersion.SYSTEM_TENANT_ID
                || version.TenantId == scopeRow)
            .Select(version => new { version.TenantId, version.Value })
            .ToListAsync(cancellationToken);

        Guid ValueOf(long row) => rows.Find(version => version.TenantId == row)?.Value ?? AccessVersion.Initial;
        return new AccessVersionKey(
            RequireDeployment(ValueOf(AccessVersion.DEPLOYMENT_ID)), ValueOf(AccessVersion.SYSTEM_TENANT_ID), ValueOf(scopeRow));
    }

    /// <summary>Fails closed: a shared constant prefix would let two deployments on one cache read each other's entries.</summary>
    private static Guid RequireDeployment(Guid deployment) =>
        deployment != AccessVersion.Initial
            ? deployment
            : throw new InvalidOperationException(
                "The access version deployment row is missing. Apply the AppIdentity migrations (RandomizeAccessVersions).");

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

    /// <summary>Sets row <c>{0}</c> to value <c>{1}</c>, inserting it when missing; safe when two first changes race.</summary>
    /// <remarks>Only identifiers from the model are interpolated, delimited; the values are parameters.</remarks>
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
