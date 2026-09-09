using Endatix.Core.Abstractions;

namespace Endatix.IntegrationTests.Shared;

/// <summary>
/// Fixed-tenant <see cref="ITenantContext"/> for tests that construct a DbContext directly rather
/// than resolving one through the host.
/// </summary>
/// <remarks>
/// Tenant <c>0</c> (<see cref="Bypass"/>) switches the tenant query filter off instead of scoping
/// it, so it is never the "other tenant" in an isolation test — use two distinct non-zero ids.
/// </remarks>
public sealed class TestTenantContext(long tenantId) : ITenantContext
{
    /// <summary>App-level context: no tenant scope, so every tenant's rows are visible.</summary>
    public static TestTenantContext Bypass { get; } = new(0);

    public long TenantId { get; } = tenantId;
}
