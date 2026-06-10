namespace Endatix.Infrastructure.Features.Submitters;

/// <summary>
/// Well-known auth provider identifiers used by the submitter resolution pipeline.
/// Identity providers such as Endatix and Keycloak are defined in <see cref="Identity.Authentication.AuthProviders"/>.
/// </summary>
public static class SubmitterAuthProviders
{
    /// <summary>Anonymous submissions that are not persisted as submitter records.</summary>
    public const string Anonymous = "Anonymous";

    /// <summary>Trusted server-side integration payloads (for example create-on-behalf).</summary>
    public const string Integration = "Integration";
}
