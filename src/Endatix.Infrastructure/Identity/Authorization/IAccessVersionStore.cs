namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>
/// Reads and changes <see cref="AccessVersion"/> values: the cache reads the versions a request keys on, and the commit
/// hook changes the versions of the tenants a transaction touched. The database store is the only one for now; a
/// distributed-cache store can replace it behind this interface.
/// </summary>
internal interface IAccessVersionStore
{
    /// <summary>
    /// Reads, uncached, the versions a request in <paramref name="tenantId"/> keys on, in one query. A tenant without a
    /// row reads <see cref="AccessVersion.Initial"/>; <paramref name="tenantId"/> <c>&lt;= 0</c> reads the version every
    /// change moves.
    /// </summary>
    Task<AccessVersionKey> ReadAsync(long tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Writes a new random version for each tenant row in <paramref name="tenantIds"/> (from
    /// <see cref="AccessVersion.RowFor"/>) and for <see cref="AccessVersion.ANY_TENANT_ID"/>, in ascending order,
    /// inserting rows that are missing.
    /// </summary>
    void Change(IReadOnlyCollection<long> tenantIds);

    /// <inheritdoc cref="Change"/>
    Task ChangeAsync(IReadOnlyCollection<long> tenantIds, CancellationToken cancellationToken);
}
