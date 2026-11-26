using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Stats.Models;

namespace Endatix.Core.UseCases.Stats.GetDashboard;

public record GetStorageDashboardQuery(long? TenantId) : IQuery<Result<StorageDashboardModel>>
{
    internal bool TenantMetricsOnly { get; private init; }

    /// <summary>Reads tenant and form usage without querying deployment-wide table sizes.</summary>
    public static GetStorageDashboardQuery ForTenant(long tenantId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tenantId);
        return new(tenantId) { TenantMetricsOnly = true };
    }
}
