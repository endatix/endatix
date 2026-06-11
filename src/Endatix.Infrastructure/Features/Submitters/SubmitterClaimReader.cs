using System.Security.Claims;
using Endatix.Infrastructure.Identity;

namespace Endatix.Infrastructure.Features.Submitters;

/// <summary>
/// Reads submitter claims from a claims principal.
/// </summary>
public sealed class SubmitterClaimReader
{
    /// <summary>
    /// Resolves the Endatix subject from a claims principal.
    /// </summary>
    public string? ResolveEndatixSubject(ClaimsPrincipal principal) => principal.GetUserId();

    /// <summary>
    /// Resolves the original bearer token subject, ignoring hydrated authorization identities.
    /// </summary>
    public string? ResolveNativeTokenSubject(ClaimsPrincipal principal)
    {
        foreach (var identity in principal.Identities)
        {
            if (!identity.IsAuthenticated || identity is AuthorizedIdentity)
            {
                continue;
            }

            var subject = identity.FindFirst(ClaimNames.UserId) ??
                identity.FindFirst(ClaimTypes.NameIdentifier);

            var normalizedSubject = Normalize(subject?.Value);
            if (normalizedSubject is not null)
            {
                return normalizedSubject;
            }
        }

        return IdentityClaimsReader.ReadFirstValue(principal, ClaimNames.UserId, ClaimTypes.NameIdentifier);
    }

    /// <summary>
    /// Resolves the external subject from a claims principal.
    /// </summary>
    public string? ResolveExternalSubject(ClaimsPrincipal principal) =>
        ResolveNativeTokenSubject(principal);

    /// <summary>
    /// Resolves the display ID from a claims principal.
    /// </summary>
    public string? ResolveDisplayId(ClaimsPrincipal principal, SubmitterOptions options)
    {
        foreach (var claimType in options.DisplayIdClaimTypes)
        {
            var value = IdentityClaimsReader.ReadFirstValue(principal, claimType);
            if (value is not null)
            {
                return value;
            }
        }

        return ResolvePreferredUserName(principal);
    }

    /// <summary>
    /// Resolves the profile from a claims principal.
    /// </summary>
    public IReadOnlyDictionary<string, string>? ResolveProfile(ClaimsPrincipal principal, SubmitterOptions options) =>
        IdentityClaimsReader.ReadFields(principal, options.ProfileSnapshotFields);

    /// <summary>
    /// Resolves the preferred user name from a claims principal.
    /// </summary>
    public string? ResolvePreferredUserName(ClaimsPrincipal principal) =>
        IdentityClaimsReader.ReadPreferredUserName(principal);

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
