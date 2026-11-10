namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>
/// An access version: a random value that changes, as the last statement of the transaction, whenever an input to a
/// user's authorization data changes (a role, a role's permissions, a user's roles, or a user's tenant). Cached
/// authorization data is keyed on it, so every API instance stops serving the old data on its next request.
/// </summary>
/// <remarks>
/// One row per tenant. <see cref="SYSTEM_TENANT_ID"/> holds the version for system roles (and users without a
/// tenant); <see cref="ANY_TENANT_ID"/> changes with every change, for principals that carry no tenant;
/// <see cref="DEPLOYMENT_ID"/> is written once by the <c>RandomizeAccessVersions</c> migration, never changes, and
/// prefixes every access cache key. A tenant gets its row on its first change. A new row starts at a random value and the value is random, not a counter: a database restore or a cache
/// flush must never bring back a value that old cache entries are keyed on.
/// </remarks>
internal sealed class AccessVersion
{
    /// <summary>The row for system roles (system roles use <c>TenantId &lt;= 0</c>).</summary>
    public const long SYSTEM_TENANT_ID = 0;

    /// <summary>The row every change moves, read by principals without a tenant (external identity providers).</summary>
    public const long ANY_TENANT_ID = -1;

    /// <summary>The row holding this deployment's key prefix.</summary>
    public const long DEPLOYMENT_ID = -2;

    /// <summary>The value read for a tenant without a row.</summary>
    public static readonly Guid Initial = Guid.Empty;

    /// <summary>The tenant the version belongs to, or one of the reserved rows above.</summary>
    public long TenantId { get; init; }

    /// <summary>A new random value on every change to an authorization input.</summary>
    public Guid Value { get; init; }

    /// <summary>The row whose version a change to an input of <paramref name="tenantId"/> moves.</summary>
    public static long RowFor(long tenantId) => tenantId > 0 ? tenantId : SYSTEM_TENANT_ID;
}
