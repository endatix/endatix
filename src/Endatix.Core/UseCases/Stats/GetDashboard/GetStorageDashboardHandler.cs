using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Stats.Models;

namespace Endatix.Core.UseCases.Stats.GetDashboard;

public class GetStorageDashboardHandler(
    IStorageStatsRepository storageStatsRepository
    ) : IQueryHandler<GetStorageDashboardQuery, Result<StorageDashboardModel>>
{
    public async Task<Result<StorageDashboardModel>> Handle(GetStorageDashboardQuery request, CancellationToken cancellationToken)
    {
        if (request.TenantMetricsOnly && request.TenantId is not > 0)
        {
            return Result.Forbidden("Tenant context is required.");
        }
        var tenantStats = await storageStatsRepository.GetTenantStats(request.TenantId, cancellationToken);
        var formStats = await storageStatsRepository.GetFormStats(request.TenantId, cancellationToken);
        var tableStats = request.TenantMetricsOnly ? [] : await storageStatsRepository.GetTableStats(cancellationToken);

        var dashboard = new StorageDashboardModel(tenantStats, formStats, tableStats);

        return Result.Success(dashboard);
    }
}
