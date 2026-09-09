using Endatix.Core.Abstractions;

namespace Endatix.IntegrationTests.Shared;

/// <summary>
/// Fixed-tenant <see cref="ITenantContext"/> for tests that construct a DbContext directly.
/// Tenant 0 (<see cref="Bypass"/>) turns the tenant filter off — never use it as the "other" tenant.
/// </summary>
public sealed class TestTenantContext(long tenantId) : ITenantContext
{
    public static TestTenantContext Bypass { get; } = new(0);

    public long TenantId { get; } = tenantId;
}
