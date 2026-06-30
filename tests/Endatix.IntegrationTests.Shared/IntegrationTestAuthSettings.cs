namespace Endatix.IntegrationTests.Shared;

/// <summary>
/// JWT settings shared between test hosts and synthetic JWT helpers.
/// SaaS and OSS factories should align their <c>UseSetting</c> values with these defaults.
/// </summary>
public sealed record IntegrationTestAuthSettings(
    string SigningKey,
    string Issuer,
    string Audience)
{
    /// <summary>
    /// The default integration test auth settings.
    /// </summary>
    public static IntegrationTestAuthSettings Default { get; } = new(
        SigningKey: "L2yGC_Vpd3k#L[<9Zb,h?.HT:n'T/5CTDmBpDskU?NAaT$sLfRU",
        Issuer: "endatix-api",
        Audience: "endatix-hub");
}
