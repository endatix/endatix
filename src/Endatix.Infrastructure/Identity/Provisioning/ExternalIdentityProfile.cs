namespace Endatix.Infrastructure.Identity.Provisioning;

/// <summary>
/// Represents an external identity profile.
/// </summary>
/// <param name="Email">The email address of the external identity.</param>
/// <param name="DisplayName">The display name of the external identity.</param>
public sealed record ExternalIdentityProfile(
    string? Email,
    string? DisplayName)
{
    internal static ExternalIdentityProfile Merge(
        ExternalIdentityProfile primary,
        ExternalIdentityProfile fallback)
    {
        return new ExternalIdentityProfile(
            Email: Coalesce(primary.Email, fallback.Email),
            DisplayName: Coalesce(primary.DisplayName, fallback.DisplayName));
    }

    private static string? Coalesce(string? primary, string? fallback)
    {
        if (!string.IsNullOrWhiteSpace(primary))
        {
            return primary.Trim();
        }

        return string.IsNullOrWhiteSpace(fallback)
            ? null
            : fallback.Trim();
    }
}
