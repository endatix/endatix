using System.Security.Claims;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;
using Microsoft.AspNetCore.Identity;

namespace Endatix.Infrastructure.Identity.Users;

/// <summary>
/// Implements the user service by leveraging the integration with ASP.NET Core Identity including the registered <see cref="AppUser" /> persisted object />
/// </summary>
public class AppUserService(UserManager<AppUser> userManager) : IUserService
{
    /// <inheritdoc />
    public async Task<Result<User>> GetUserAsync(ClaimsPrincipal claimsPrincipal, CancellationToken cancellationToken = default){
        if (claimsPrincipal == null){
            return Result.NotFound();
        }
        
        var user = await userManager.GetUserAsync(claimsPrincipal);
        if (user == null)
        {
            return Result.NotFound();
        }

        return Result.Success(user.ToUserEntity());
    }
}
