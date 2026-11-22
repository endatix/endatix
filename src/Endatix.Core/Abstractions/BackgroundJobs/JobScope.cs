namespace Endatix.Core.Abstractions.BackgroundJobs;

/// <summary>
/// Whose work a background job is: one tenant's, or the application's own.
/// </summary>
/// <remarks>
/// <para>
/// A tenant job carries a real tenant, greater than zero. A system job carries none: it does work that belongs to no
/// tenant, such as processing a signup before its tenant exists. It is never made from a tenant id; only
/// <see cref="System"/> names it.
/// </para>
/// <para>
/// How a scope is stored is the Jobs module's concern, kept in one place there. Nothing else compares a job's
/// tenant to a sentinel.
/// </para>
/// </remarks>
public sealed record JobScope
{
    private readonly long? _tenantId;

    private JobScope(long? tenantId)
    {
        _tenantId = tenantId;
    }

    /// <summary>The scope of work that belongs to no tenant.</summary>
    public static JobScope System { get; } = new(tenantId: null);

    /// <summary>The scope of <paramref name="tenantId"/>'s work.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="tenantId"/> is zero or negative.</exception>
    public static JobScope ForTenant(long tenantId)
    {
        // Zero is what the tenant context reports when there is no tenant at all, so a job carrying it would belong
        // to nobody. Work that belongs to no tenant is a system job, named as one.
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tenantId);
        return new JobScope(tenantId);
    }

    /// <summary>The owning tenant, or <see langword="null"/> for a system job.</summary>
    public long? TenantId => _tenantId;

    /// <summary>Whether this is <see cref="System"/>.</summary>
    public bool IsSystem => _tenantId is null;

    /// <inheritdoc />
    public override string ToString() => _tenantId is { } tenantId ? $"Tenant {tenantId}" : "System";
}
