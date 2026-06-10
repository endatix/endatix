using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Endatix.Infrastructure.Identity.Provisioning;

/// <summary>
/// Reads external identity claims from a claims principal.
/// </summary>
internal static class ExternalIdentityClaimReader
{
    private const string PREFERRED_USERNAME_CLAIM = "preferred_username";
    private const string NAME_CLAIM = "name";
    private const string GIVEN_NAME_CLAIM = "given_name";
    private const string FAMILY_NAME_CLAIM = "family_name";

    /// <summary>
    /// Reads external identity claims from a claims principal.
    /// </summary>
    /// <param name="principal">The claims principal to read the claims from.</param>
    /// <returns>The external identity profile.</returns>
    public static ExternalIdentityProfile FromClaimsPrincipal(ClaimsPrincipal principal)
    {
        var preferredUsername = FindFirstValue(principal, PREFERRED_USERNAME_CLAIM);
        var displayName = Coalesce(
            FindFirstValue(principal, NAME_CLAIM, ClaimTypes.Name),
            BuildFullName(
                FindFirstValue(principal, GIVEN_NAME_CLAIM, ClaimTypes.GivenName),
                FindFirstValue(principal, FAMILY_NAME_CLAIM, ClaimTypes.Surname)),
            preferredUsername);

        return new ExternalIdentityProfile(
            Email: FindFirstValue(principal, JwtRegisteredClaimNames.Email, ClaimTypes.Email),
            DisplayName: displayName);
    }

    private static string? FindFirstValue(ClaimsPrincipal principal, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = principal.FindFirst(claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static string? BuildFullName(string? givenName, string? familyName)
    {
        string?[] nameParts = [givenName, familyName];
        var fullName = string.Join(" ", nameParts.Where(part => !string.IsNullOrWhiteSpace(part)));

        return string.IsNullOrWhiteSpace(fullName) ? null : fullName;
    }

    private static string? Coalesce(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }
}
