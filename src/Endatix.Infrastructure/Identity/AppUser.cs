using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;
using Endatix.Infrastructure.Identity.Authentication;
using Microsoft.AspNetCore.Identity;

namespace Endatix.Infrastructure.Identity;

/// <summary>
/// This class implements ASP.NET IdentityUser used for persistence and identity management, authentication and authorization.
/// </summary>
public class AppUser : IdentityUser<long>, ITenantOwned
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

    /// <summary>
    /// The authentication provider that owns this user identity.
    /// </summary>
    public string AuthProvider { get; set; } = AuthProviders.Endatix;

    /// <summary>
    /// The external provider subject identifier for SSO identities.
    /// </summary>
    public string? ExternalSubjectId { get; set; }

    /// <summary>
    /// The display name provided by the external identity provider.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// The last time this user authenticated successfully.
    /// </summary>
    public DateTimeOffset? LastLoginAt { get; set; }

    /// <summary>
    /// Last mapped external roles snapshot for read-only user directory display.
    /// </summary>
    public string? ExternalRolesJson { get; set; }

    public bool IsExternal => AuthProvider != AuthProviders.Endatix;

    public bool IsVerified => IsExternal || EmailConfirmed;

    public User ToUserEntity()
    {
        Guard.Against.NullOrEmpty(UserName);
        var email = Email ?? string.Empty;

        User user;
        if (TenantId > 0)
        {
            user = new User(
                id: Id,
                tenantId: TenantId,
                userName: UserName,
                email: email,
                isVerified: IsVerified
            );
        }
        else
        {
            // Used when a new user is created and still does not have a tenant
            user = new User(
                id: Id,
                userName: UserName,
                email: email,
                isVerified: IsVerified
            );
        }

        return user;
    }
}
