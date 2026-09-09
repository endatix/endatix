using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;

namespace Endatix.IntegrationTests;

/// <summary>
/// Fixed-tenant <see cref="ITenantContext"/> for DbContexts built by hand in this project.
/// </summary>
public sealed class IntegrationTenantContext : ITenantContext
{
    /// <summary>
    /// Tenant 0 — turns the EF tenant query filter off. DbContext construction only:
    /// Core handlers guard against tenant 0, and it is never the "other" tenant in an isolation test.
    /// </summary>
    public static IntegrationTenantContext Bypass { get; } = new();

    private IntegrationTenantContext() => TenantId = 0;

    public IntegrationTenantContext(long tenantId)
    {
        Guard.Against.NegativeOrZero(
            tenantId,
            nameof(tenantId),
            $"Tenant id must be positive. Use {nameof(IntegrationTenantContext)}.{nameof(Bypass)} to disable the tenant filter.");

        TenantId = tenantId;
    }

    public long TenantId { get; }
}
