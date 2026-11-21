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
public abstract record JobScope
{
    private JobScope()
    {
    }

    /// <summary>The scope of work that belongs to no tenant.</summary>
    public static JobScope System { get; } = new SystemScope();

    /// <summary>The scope of <paramref name="tenantId"/>'s work.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="tenantId"/> is zero or negative.</exception>
    public static JobScope ForTenant(long tenantId) => new TenantScope(tenantId);

    /// <summary>The owning tenant, or <see langword="null"/> for a system job.</summary>
    public abstract long? TenantId { get; }

    /// <summary>Whether this is <see cref="System"/>.</summary>
    public bool IsSystem => TenantId is null;

    private sealed record SystemScope : JobScope
    {
        public override long? TenantId => null;

        public override string ToString() => "System";
    }

    private sealed record TenantScope : JobScope
    {
        public TenantScope(long tenantId)
        {
            // Zero is what the tenant context reports when there is no tenant at all, so a job carrying it would
            // belong to nobody. Work that belongs to no tenant is a system job, named as one.
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tenantId);
            Id = tenantId;
        }

        private long Id { get; }

        public override long? TenantId => Id;

        public override string ToString() => $"Tenant {Id}";
    }
}
