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
}