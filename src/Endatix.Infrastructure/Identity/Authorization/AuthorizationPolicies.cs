using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>
/// Named authorization policies for ASP.NET Core
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>
    /// Public resource based access policy. Requires a ReBAC JWT without <c>sub</c> claim.
    /// </summary>
    public const string PublicResourceAccess = nameof(PublicResourceAccess);

    /// <summary>
    /// Platform admin access policy. Requires the PlatformAdmin role.
    /// </summary>
    public static readonly string PlatformAdminAccess = SystemRole.PlatformAdmin.Name;
}
