using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.ListRoles;

/// <summary>
/// Handler for the ListRolesQuery to retrieve roles for the current tenant with paging and optional role type filtering.
/// </summary>
public sealed class ListRolesHandler(IRoleManagementService roleManagementService)
    : IQueryHandler<ListRolesQuery, Result<Paged<RoleListItem>>>
{
    /// <inheritdoc/>
    public Task<Result<Paged<RoleListItem>>> Handle(ListRolesQuery request, CancellationToken cancellationToken)
        => roleManagementService.ListRolesAsync(request.Skip, request.PageSize, request.RoleType, request.Search, cancellationToken);
}
