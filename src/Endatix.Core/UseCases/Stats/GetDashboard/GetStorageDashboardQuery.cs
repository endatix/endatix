using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Stats.Models;

namespace Endatix.Core.UseCases.Stats.GetDashboard;

public record GetStorageDashboardQuery(long? TenantId) : IQuery<Result<StorageDashboardModel>>;
