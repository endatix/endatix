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
    /// <param name="principal">The claims principal to resolve the subject from.</param>
    /// <returns>The Endatix subject.</returns>
    public string? ResolveEndatixSubject(ClaimsPrincipal principal) => principal.GetUserId();

    /// <summary>
    /// Resolves the external subject from a claims principal.
    /// </summary>
    /// <param name="principal">The claims principal to resolve the subject from.</param>
    /// <returns>The external subject.</returns>

    public string? ResolveExternalSubject(ClaimsPrincipal principal)
    {
        var subject = principal.FindFirst(ClaimNames.UserId) ??
            principal.FindFirst(ClaimTypes.NameIdentifier);

        return Normalize(subject?.Value);
    }

    /// <summary>
    /// Resolves the display ID from a claims principal.
    /// </summary>
    /// <param name="principal">The claims principal to resolve the display ID from.</param>
    /// <param name="options">The submitter options.</param>
    /// <returns>The display ID.</returns>
    public string? ResolveDisplayId(ClaimsPrincipal principal, SubmitterOptions options)
    {
        foreach (var claimType in options.DisplayIdClaimTypes)
        {
            var value = Normalize(principal.FindFirst(claimType)?.Value);
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
    /// <param name="principal">The claims principal to resolve the profile from.</param>
    /// <param name="options">The submitter options.</param>
    /// <returns>The profile.</returns>
    public IReadOnlyDictionary<string, string>? ResolveProfile(ClaimsPrincipal principal, SubmitterOptions options)
    {
        if (options.ProfileSnapshotFields.Count is 0)
        {
            return null;
        }

        Dictionary<string, string> profile = [];
        foreach (var claimType in options.ProfileSnapshotFields)
        {
            var value = Normalize(principal.FindFirst(claimType)?.Value);
            if (value is not null)
            {
                profile[claimType] = value;
            }
        }

        return profile.Count is 0 ? null : profile;
    }

    /// <summary>
    /// Resolves the preferred user name from a claims principal.
    /// </summary>
    /// <param name="principal">The claims principal to resolve the preferred user name from.</param>
    /// <returns>The preferred user name.</returns>
    public string? ResolvePreferredUserName(ClaimsPrincipal principal)
    {
        return Normalize(principal.FindFirst("preferred_username")?.Value) ??
            Normalize(principal.Identity?.Name) ??
            ResolveEmail(principal);
    }

    /// <summary>
    /// Resolves the email from a claims principal.
    /// </summary>
    /// <param name="principal">The claims principal to resolve the email from.</param>
    /// <returns>The email.</returns>
    private static string? ResolveEmail(ClaimsPrincipal principal) =>
        Normalize(principal.FindFirst(ClaimNames.Email)?.Value) ??
        Normalize(principal.FindFirst(ClaimTypes.Email)?.Value);

    /// <summary>
    /// Normalizes a string value.
    /// </summary>
    /// <param name="value">The value to normalize.</param>
    /// <returns>The normalized value.</returns>
    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
