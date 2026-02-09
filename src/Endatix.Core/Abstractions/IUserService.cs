using System.Security.Claims;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Abstractions;
/// <summary>
/// Defines operations related to the User entity, providing access to user data and functionality across various scenarios and modules.
/// This is not aware of the existance of ASP.NET Core identity, so it's purely domain driven
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Retrieves a User entity based on the provided ClaimsPrincipal.
    /// </summary>
    /// <param name="claimsPrincipal">The ClaimsPrincipal object representing the user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The User entity associated with the provided ClaimsPrincipal.</returns>
    Task<Result<User>> GetUserAsync(ClaimsPrincipal claimsPrincipal, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user by user ID.
    /// </summary>
    /// <param name="userId">The user ID to search for.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation if needed.</param>
    /// <returns>A Result containing the User if found, or NotFound if not found.</returns>
    Task<Result<User>> GetUserAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user by email address.
    /// </summary>
    /// <param name="email">The email address to search for.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation if needed.</param>
    /// <returns>A Result containing the User if found, or NotFound if not found.</returns>
    Task<Result<User>> GetUserAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all users for the current tenant with their role names. Multi-tenancy is assumed; tenant filter is applied by the implementation.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation if needed.</param>
    /// <returns>A Result containing the list of users with roles for the current tenant.</returns>
    Task<Result<IReadOnlyList<UserWithRoles>>> ListUsersAsync(CancellationToken cancellationToken = default);
}