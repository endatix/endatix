using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Abstractions;

/// <summary>
/// Service contract for user role management operations.
/// </summary>
public interface IRoleManagementService
{
    /// <summary>
    /// Assigns a role to a user.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="roleName">The name of the role to assign.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Result indicating success or failure.</returns>
    Task<Result> AssignRoleToUserAsync(long userId, string roleName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a role from a user.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="roleName">The name of the role to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Result indicating success or failure.</returns>
    Task<Result> RemoveRoleFromUserAsync(long userId, string roleName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all roles assigned to a user.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Result containing the list of role names.</returns>
    Task<Result<IList<string>>> GetUserRolesAsync(long userId, CancellationToken cancellationToken = default);
}
