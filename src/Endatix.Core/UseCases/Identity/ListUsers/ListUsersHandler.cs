using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.ListUsers;

/// <summary>
/// Handles the ListUsersQuery to retrieve users for the current tenant with their roles.
/// </summary>
public class ListUsersHandler(IUserService userService)
    : IQueryHandler<ListUsersQuery, Result<IEnumerable<UserWithRoles>>>
{
    /// <inheritdoc/>
    public async Task<Result<IEnumerable<UserWithRoles>>> Handle(
        ListUsersQuery request,
        CancellationToken cancellationToken)
    {
        var result = await userService.ListUsersAsync(cancellationToken);
        if (!result.IsSuccess)
        {
            return Result<IEnumerable<UserWithRoles>>.Error(
                new ErrorList(result.Errors ?? [], result.CorrelationId));
        }

        return Result.Success<IEnumerable<UserWithRoles>>(result.Value);
    }
}
