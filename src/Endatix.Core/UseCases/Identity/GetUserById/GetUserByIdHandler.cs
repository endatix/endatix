using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.GetUserById;

/// <summary>
/// Handles the GetUserByIdQuery to retrieve a user for the current tenant.
/// </summary>
public sealed class GetUserByIdHandler(IUserService userService)
    : IQueryHandler<GetUserByIdQuery, Result<UserWithRoles>>
{
    /// <inheritdoc/>
    public Task<Result<UserWithRoles>> Handle(
        GetUserByIdQuery request,
        CancellationToken cancellationToken)
    {
        return userService.GetUserWithRolesAsync(request.UserId, cancellationToken);
    }
}
