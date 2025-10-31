using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.GetUserRoles;

/// <summary>
/// Handles the retrieval of roles assigned to a user.
/// </summary>
public class GetUserRolesHandler(IRoleManagementService roleManagementService) : IQueryHandler<GetUserRolesQuery, Result<IList<string>>>
{
    /// <summary>
    /// Handles the GetUserRolesQuery to retrieve user roles.
    /// </summary>
    /// <param name="request">The GetUserRolesQuery containing the user ID.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the work.</param>
    /// <returns>A Result containing the list of role names if successful, or an error if retrieval fails.</returns>
    public async Task<Result<IList<string>>> Handle(GetUserRolesQuery request, CancellationToken cancellationToken)
    {
        return await roleManagementService.GetUserRolesAsync(request.UserId, cancellationToken);
    }
}
