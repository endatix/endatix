using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.ListUsers;

/// <summary>
/// Handles the ListUsersQuery to retrieve users for the current tenant with their roles.
/// </summary>
[DirectCallOnly]
public class ListUsersHandler(IUserService userService)
    : IQueryHandler<ListUsersQuery, Result<Paged<UserWithRoles>>>
{
    // Published direct callers receive the original generic error; dispatch retains the service's typed status.
    private bool UseServiceStatus { get; init; }

    /// <inheritdoc/>
    public async Task<Result<Paged<UserWithRoles>>> Handle(ListUsersQuery request, CancellationToken cancellationToken)
    {
        var result = await userService.ListUsersAsync(request.Paging, request.Criteria, cancellationToken);
        if (UseServiceStatus)
        {
            return result;
        }
        return result.IsSuccess ? Result.Success(result.Value)
            : Result<Paged<UserWithRoles>>.Error(new ErrorList(result.Errors ?? [], result.CorrelationId));
    }

    internal sealed class Registered(IUserService service)
        : RegisteredContentHandler<ListUsersQuery, Result<Paged<UserWithRoles>>>(new ListUsersHandler(service) { UseServiceStatus = true });
}
