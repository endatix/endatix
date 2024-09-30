using Ardalis.GuardClauses;
using Endatix.Core.Entities.Identity;
using Microsoft.AspNetCore.Identity;

namespace Endatix.Infrastructure.Identity;

/// <summary>
/// This class implements ASP.NET IdentityUser used for persistence and identity management, authentication and authorization.
/// using <see cref="long" /> type to match the <see cref="Endatix.Core.Abstractions.IIdGenerator" />
/// </summary>
public class AppUser : IdentityUser<long>
{
    public User ToUserEntity()
    {
        Guard.Against.NullOrEmpty(UserName);
        Guard.Against.NullOrEmpty(Email);

        var user = new User(
            id: Id,
            userName: UserName,
            email: Email,
            isVerified: EmailConfirmed
        );

        return user;
    }
}
