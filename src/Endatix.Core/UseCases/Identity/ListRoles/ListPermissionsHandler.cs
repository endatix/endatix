using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.ListRoles;

/// <summary>
/// Handler for the ListPermissionsQuery to retrieve permissions for the current tenant.
/// </summary>
public sealed class ListPermissionsHandler(IRoleManagementService roleManagementService)
    : IQueryHandler<ListPermissionsQuery, Result<IReadOnlyList<PermissionListItem>>>
{
    /// <inheritdoc/>
    public Task<Result<IReadOnlyList<PermissionListItem>>> Handle(ListPermissionsQuery request, CancellationToken cancellationToken)
        => roleManagementService.ListPermissionsAsync(cancellationToken);
}
