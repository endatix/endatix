using Endatix.Core.Infrastructure.Result;

namespace Endatix.Infrastructure.Features.PlatformAdmin.ListPlatformTenants;

/// <summary>
/// Read contract for the platform tenant list query.
/// </summary>
public interface IListPlatformTenants
{
    Task<Result<Paged<PlatformTenantListItem>>> ExecuteAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken);
}
