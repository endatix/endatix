using Ardalis.GuardClauses;
using Endatix.Core.Entities.Identity;
using Microsoft.AspNetCore.Identity;

namespace Endatix.Infrastructure.Identity;

/// <summary>
/// This class implements ASP.NET IdentityUser used for persistence and identity management, authentication and authorization.
/// </summary>
public class AppUser : IdentityUser
{
    internal User ToUserEntity()
    {
        Guard.Against.NullOrEmpty(UserName);
        Guard.Against.NullOrEmpty(Email);

        var user = new User(
            id: default,
            externalId: Id,
            userName: UserName,
            email: Email,
            isVerified: EmailConfirmed
        );

        return user;
    }
}
