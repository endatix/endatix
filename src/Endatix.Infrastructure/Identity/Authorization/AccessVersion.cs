namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>
/// An access version: a random value that changes, as the last statement of the transaction, whenever an input to a
/// user's authorization data changes (a role, a role's permissions, a user's roles, or a user's tenant). Cached
/// authorization data is keyed on it, so every API instance stops serving the old data on its next request.
/// </summary>
/// <remarks>
/// One row per tenant; <see cref="SYSTEM_TENANT_ID"/> holds the version for system roles. Until per-tenant versions
/// ship, every change and every read uses the system row. The value is random, not a counter: a database restore or a
/// cache flush must never bring back a value that old cache entries are keyed on.
/// </remarks>
internal sealed class AccessVersion
{
    /// <summary>The tenant id of the system-roles row (system roles use <c>TenantId &lt;= 0</c>).</summary>
    public const long SYSTEM_TENANT_ID = 0;

    /// <summary>The value before the first change, and the value read when the row is missing.</summary>
    public static readonly Guid Initial = Guid.Empty;

    /// <summary>The tenant the version belongs to; <see cref="SYSTEM_TENANT_ID"/> for system roles.</summary>
    public long TenantId { get; init; }

    /// <summary>A new random value on every change to an authorization input.</summary>
    public Guid Value { get; init; }
}
