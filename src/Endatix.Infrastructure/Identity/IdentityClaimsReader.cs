using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Endatix.Infrastructure.Identity.Provisioning;

namespace Endatix.Infrastructure.Identity;

/// <summary>
/// Reads normalized identity profile values from JWT claims and JSON token payloads.
/// </summary>
internal static class IdentityClaimsReader
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
    /// Reads the external identity profile from a claims principal.
    /// </summary>
    /// <param name="principal">The claims principal.</param>
    /// <returns>The external identity profile.</returns>
    public static ExternalIdentityProfile FromClaimsPrincipal(ClaimsPrincipal principal)
    {
        return FromClaims(principal.Claims);
    }

    /// <summary>
    /// Reads the external identity profile from a claims principal.
    /// </summary>
    /// <param name="claims">The claims.</param>
    /// <returns>The external identity profile.</returns>
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

    /// <summary>
    /// Reads the external identity profile from a JSON object.
    /// </summary>
    /// <param name="json">The JSON object.</param>
    /// <returns>The external identity profile.</returns>
    public static ExternalIdentityProfile FromJsonObject(string json)
    {
        using var document = JsonDocument.Parse(json);
        return FromJsonObject(document.RootElement);
    }

    /// <summary>
    /// Reads the external identity profile from a JSON object.
    /// </summary>
    /// <param name="rootElement">The JSON object.</param>
    /// <returns>The external identity profile.</returns>
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

            var claimValue = Normalize(claimElement.GetString());
            if (claimValue is null)
            {
                continue;
            }

            claims.Add(new Claim(claimType, claimValue));
        }

        return FromClaims(claims);
    }

    /// <summary>
    /// Reads the fields from a claims principal.
    /// </summary>
    /// <param name="principal">The claims principal.</param>
    /// <param name="claimTypes">The claim types.</param>
    /// <returns>The fields.</returns>
    public static IReadOnlyDictionary<string, string>? ReadFields(
        ClaimsPrincipal principal,
        IReadOnlyCollection<string> claimTypes)
    {
        if (claimTypes.Count is 0)
        {
            return null;
        }

        Dictionary<string, string> profile = [];
        foreach (var claimType in claimTypes)
        {
            var value = ReadFirstValue(principal, claimType);
            if (value is not null)
            {
                profile[claimType] = value;
            }
        }

        return profile.Count is 0 ? null : profile;
    }

    /// <summary>
    /// Reads the first value from a claims principal.
    /// </summary>
    /// <param name="principal">The claims principal.</param>
    /// <param name="claimTypes">The claim types.</param>
    /// <returns>The first value.</returns>
    public static string? ReadFirstValue(ClaimsPrincipal principal, params string[] claimTypes)
    {
        return FindFirstValue(principal.Claims, claimTypes);
    }

    /// <summary>
    /// Reads the preferred user name from a claims principal.
    /// </summary>
    /// <param name="principal">The claims principal.</param>
    /// <returns>The preferred user name.</returns>
    public static string? ReadPreferredUserName(ClaimsPrincipal principal)
    {
        return ReadFirstValue(principal, PREFERRED_USERNAME_CLAIM) ??
            Normalize(principal.Identity?.Name) ??
            ReadFirstValue(principal, ClaimNames.Email, ClaimTypes.Email);
    }

    private static string? FindFirstValue(IEnumerable<Claim> claims, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = Normalize(claims.FirstOrDefault(claim => claim.Type == claimType)?.Value);
            if (value is not null)
            {
                return value;
            }
        }

        return null;
    }

    private static string? BuildFullName(string? givenName, string? familyName)
    {
        string?[] nameParts = [givenName, familyName];
        var fullName = string.Join(" ", nameParts.Where(part => !string.IsNullOrWhiteSpace(part)));

        return Normalize(fullName);
    }

    private static string? Coalesce(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
