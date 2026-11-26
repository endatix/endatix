using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.ListUsers;

/// <summary>
/// Handles the ListUsersQuery to retrieve users for the current tenant with their roles.
/// </summary>
public class ListUsersHandler(IUserService userService)
    : IQueryHandler<ListUsersQuery, Result<Paged<UserWithRoles>>>
{
    /// <inheritdoc/>
    public Task<Result<Paged<UserWithRoles>>> Handle(
        ListUsersQuery request,
        CancellationToken cancellationToken) =>
        userService.ListUsersAsync(request.Paging, request.Criteria, cancellationToken);
}
