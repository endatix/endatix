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
    /// <summary>
    /// The ID of the tenant this user belongs to.
    /// </summary>
    public long TenantId { get; set; }
    
    /// <summary>
    /// The user's refresh token.
    /// </summary>
    public string? RefreshTokenHash { get; set; }

    /// <summary>
    /// The user's refresh token expiry time.
    /// </summary>
    public DateTime? RefreshTokenExpireAt { get; set; }

    public User ToUserEntity()
    {
        Guard.Against.NullOrEmpty(UserName);
        Guard.Against.NullOrEmpty(Email);

        var user = new User(
            id: Id,
            tenantId: TenantId,
            userName: UserName,
            email: Email,
            isVerified: EmailConfirmed
        );

        return user;
    }
}
