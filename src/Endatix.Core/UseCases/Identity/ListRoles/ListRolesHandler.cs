using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.ListRoles;

/// <summary>
/// Handler for the ListRolesQuery to retrieve roles for the current tenant with paging and optional role type filtering.
/// </summary>
public sealed class ListRolesHandler(IRoleManagementService roleManagementService, AccountStanding standing)
    : IQueryHandler<ListRolesQuery, Result<Paged<RoleListItem>>>
{
    /// <inheritdoc/>
    public async Task<Result<Paged<RoleListItem>>> Handle(ListRolesQuery request, CancellationToken cancellationToken)
        => await standing.RefusalAsync<Paged<RoleListItem>>(cancellationToken)
            ?? await roleManagementService.ListRolesAsync(
                request.Paging,
                request.Criteria,
                cancellationToken);
}
