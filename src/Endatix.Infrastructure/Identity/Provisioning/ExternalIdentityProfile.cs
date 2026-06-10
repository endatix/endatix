namespace Endatix.Infrastructure.Identity.Provisioning;

/// <summary>
/// Represents an external identity profile.
/// </summary>
/// <param name="Email">The email address of the external identity.</param>
/// <param name="DisplayName">The display name of the external identity.</param>
public sealed record ExternalIdentityProfile(
    string? Email,
    string? DisplayName);
