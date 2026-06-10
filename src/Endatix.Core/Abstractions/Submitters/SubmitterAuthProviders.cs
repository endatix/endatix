namespace Endatix.Core.Abstractions.Submitters;

/// <summary>
/// Well-known submitter auth provider identifiers used by the platform.
/// External identity providers (for example Keycloak) are defined in infrastructure.
/// </summary>
public static class SubmitterAuthProviders
{
    /// <summary>Anonymous submissions that are not persisted as submitter records.</summary>
    public const string Anonymous = "Anonymous";

    /// <summary>Native Endatix platform users.</summary>
    public const string Native = "Endatix";

    /// <summary>Trusted server-side integration payloads (for example create-on-behalf).</summary>
    public const string Integration = "Integration";
}
