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
    public Task<Result<User>> GetUserAsync(ClaimsPrincipal claimsPrincipal, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes the password for a user after validating their current password.
    /// </summary>
    /// <param name="user">The user whose password should be changed.</param>
    /// <param name="currentPassword">The user's current password for verification.</param>
    /// <param name="newPassword">The new password to set for the user.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A Result containing a message if successful, or an error if the operation fails.</returns>
    public Task<Result<string>> ChangePasswordAsync(User user, string currentPassword, string newPassword, CancellationToken cancellationToken = default);
}