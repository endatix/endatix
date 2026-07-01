namespace Endatix.IntegrationTests.Shared;

/// <summary>
/// JWT settings shared between integration test hosts and synthetic JWT helpers.
/// SaaS and OSS factories should align their <c>UseSetting</c> values with <see cref="FromEnvironment" />.
/// </summary>
/// <remarks>
/// Test-only — never reference from production configuration. Override locally via
/// <c>ENDATIX_TEST_JWT_SIGNING_KEY</c> when needed; defaults use a <c>TEST:</c> prefix so accidental
/// production reuse is obvious.
/// </remarks>
public sealed record IntegrationTestAuthSettings(
    string SigningKey,
    string Issuer,
    string Audience)
{
    /// <summary>
    /// Environment variable for overriding the integration-test JWT signing key.
    /// </summary>
    public const string SigningKeyEnvironmentVariable = "ENDATIX_TEST_JWT_SIGNING_KEY";

    /// <summary>
    /// Built-in fallback signing key for integration tests and local synthetic JWT helpers.
    /// Not a production secret — the <c>TEST:</c> prefix is intentional.
    /// </summary>
    public const string FallbackSigningKey =
        "TEST:Endatix.IntegrationTests.JwtSigningKey.NotForProduction.UseEnvOverride";

    /// <summary>
    /// The default integration test auth settings (resolved once from the environment).
    /// </summary>
    public static IntegrationTestAuthSettings Default { get; } = FromEnvironment();

    /// <summary>
    /// Reads auth settings from <see cref="SigningKeyEnvironmentVariable" /> when set; otherwise uses
    /// <see cref="FallbackSigningKey" />.
    /// </summary>
    public static IntegrationTestAuthSettings FromEnvironment()
    {
        var signingKey = Environment.GetEnvironmentVariable(SigningKeyEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            signingKey = FallbackSigningKey;
        }

        return new IntegrationTestAuthSettings(
            SigningKey: signingKey,
            Issuer: "endatix-api",
            Audience: "endatix-hub");
    }
}
