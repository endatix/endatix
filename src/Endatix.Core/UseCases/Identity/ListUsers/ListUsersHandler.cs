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
    public async Task<Result<Paged<UserWithRoles>>> Handle(
        ListUsersQuery request,
        CancellationToken cancellationToken)
    {
        var result = await userService.ListUsersAsync(
            request.Skip,
            request.PageSize,
            request.Search,
            request.Role,
            request.Status,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return Result<Paged<UserWithRoles>>.Error(
                new ErrorList(result.Errors ?? [], result.CorrelationId));
        }

        return Result.Success(result.Value!);
    }
}
