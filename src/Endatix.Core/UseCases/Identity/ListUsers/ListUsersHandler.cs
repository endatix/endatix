using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.ListUsers;

/// <summary>
/// Handles the ListUsersQuery to retrieve users for the current tenant with their roles.
/// </summary>
public class ListUsersHandler(IUserService userService, AccountStanding standing)
    : IQueryHandler<ListUsersQuery, Result<Paged<UserWithRoles>>>
{
    /// <inheritdoc/>
    public async Task<Result<Paged<UserWithRoles>>> Handle(
        ListUsersQuery request,
        CancellationToken cancellationToken)
        => await standing.RefusalAsync<Paged<UserWithRoles>>(cancellationToken)
            ?? await ListUsersAsync(request, cancellationToken);

    private async Task<Result<Paged<UserWithRoles>>> ListUsersAsync(ListUsersQuery request, CancellationToken cancellationToken)
    {
        var result = await userService.ListUsersAsync(request.Paging, request.Criteria, cancellationToken);
        return result.IsSuccess ? Result.Success(result.Value) : Result<Paged<UserWithRoles>>.Error(new ErrorList(result.Errors ?? [], result.CorrelationId));
    }
}
