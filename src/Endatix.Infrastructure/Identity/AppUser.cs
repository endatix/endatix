using Ardalis.GuardClauses;
using Endatix.Core.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Data;

namespace Endatix.Infrastructure.Identity;

/// <summary>
/// This class implements ASP.NET IdentityUser used for persistence and identity management, authentication and authorization.
/// using <see cref="long" /> type to match the <see cref="Endatix.Core.Abstractions.IIdGenerator" />
/// </summary>
public class AppUser : IdentityUser<long>
{
    private static IIdentityEntityFactory _factory = new DefaultIdentityEntityFactory();

    public static void ConfigureFactory(IIdentityEntityFactory factory)
    {
        _factory = factory;
    }

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
        
        var user = _factory.CreateUser(
            Id,
            UserName!,
            Email!,
            EmailConfirmed
        );

        return user;
    }
}
