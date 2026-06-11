using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;

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
    private static readonly string[] _profileClaimTypes =
    [
        JwtRegisteredClaimNames.Email,
        PREFERRED_USERNAME_CLAIM,
        NAME_CLAIM,
        GIVEN_NAME_CLAIM,
        FAMILY_NAME_CLAIM
    ];

    /// <summary>
    /// Reads external identity claims from a claims principal.
    /// </summary>
    /// <param name="principal">The claims principal to read the claims from.</param>
    /// <returns>The external identity profile.</returns>
    public static ExternalIdentityProfile FromClaimsPrincipal(ClaimsPrincipal principal)
    {
        return FromClaims(principal.Claims);
    }

    public static ExternalIdentityProfile FromClaims(IEnumerable<Claim> claims)
    {
        var claimsList = claims.ToList();
        var preferredUsername = FindFirstValue(claimsList, PREFERRED_USERNAME_CLAIM);
        var displayName = Coalesce(
            FindFirstValue(claimsList, NAME_CLAIM, ClaimTypes.Name),
            BuildFullName(
                FindFirstValue(claimsList, GIVEN_NAME_CLAIM, ClaimTypes.GivenName),
                FindFirstValue(claimsList, FAMILY_NAME_CLAIM, ClaimTypes.Surname)),
            preferredUsername);

        return new ExternalIdentityProfile(
            Email: FindFirstValue(claimsList, JwtRegisteredClaimNames.Email, ClaimTypes.Email),
            DisplayName: displayName);
    }

    public static ExternalIdentityProfile FromJsonObject(string json)
    {
        using var document = JsonDocument.Parse(json);
        return FromJsonObject(document.RootElement);
    }

    public static ExternalIdentityProfile FromJsonObject(JsonElement rootElement)
    {
        var claims = new List<Claim>();

        foreach (var claimType in _profileClaimTypes)
        {
            if (!rootElement.TryGetProperty(claimType, out var claimElement) ||
                claimElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var claimValue = claimElement.GetString();
            if (string.IsNullOrWhiteSpace(claimValue))
            {
                continue;
            }

            claims.Add(new Claim(claimType, claimValue.Trim()));
        }

        return FromClaims(claims);
    }

    private static string? FindFirstValue(IEnumerable<Claim> claims, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = claims.FirstOrDefault(claim => claim.Type == claimType)?.Value;
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
