using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Identity.Authentication;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// Stand-in services for EF Core design-time DbContext construction.
/// </summary>
public static class DesignTimeDbContextDependencies
{
    public static IIdGenerator<long> IdGenerator { get; } = new DesignTimeIdGenerator();

    public static ITenantContext TenantContext { get; } = new DesignTimeTenantContext();

    private sealed class DesignTimeIdGenerator : IIdGenerator<long>
    {
        public long CreateId() => DateTime.UtcNow.Ticks;
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public long TenantId => AuthConstants.DEFAULT_TENANT_ID;
    }
}
