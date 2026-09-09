using Endatix.Core.Abstractions;

namespace Endatix.IntegrationTests;

/// <summary>
/// Fixed-tenant <see cref="ITenantContext"/> for hand-built DbContexts in this project.
/// <see cref="Bypass"/> is filter-off (tenant 0), not a Core-handler caller.
/// </summary>
public sealed class TestTenantContext : ITenantContext
{
    public static TestTenantContext Bypass { get; } = new();

    private TestTenantContext() => TenantId = 0;

    public TestTenantContext(long tenantId)
    {
        if (tenantId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tenantId),
                tenantId,
                $"Use {nameof(TestTenantContext)}.{nameof(Bypass)} to disable the tenant filter.");
        }

        TenantId = tenantId;
    }

    public long TenantId { get; }
}
