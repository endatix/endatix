using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Identity.SigningKeys;

/// <summary>
/// The one rule every signing key the API uses must pass: set, at least 32 bytes, and not a value that has been
/// published (a shipped default, a documented example or a sample placeholder).
/// </summary>
internal static class SigningKeyRule
{
    /// <summary>The minimum key size for HMAC-SHA256, in UTF-8 bytes.</summary>
    public const int MIN_KEY_BYTES = 32;

    /// <summary>The configuration path of the submission access token signing key.</summary>
    public const string SUBMISSION_KEY_PATH = "Endatix:Submissions:AccessTokenSigningKey";

    /// <summary>The configuration path of the sign-in (Endatix JWT) signing key.</summary>
    public const string JWT_KEY_PATH = "Endatix:Auth:Providers:EndatixJwt:SigningKey";

    /// <summary>The legacy configuration path of the sign-in signing key; it fills the current one when that is empty.</summary>
    public const string LEGACY_JWT_KEY_PATH = "Endatix:Jwt:SigningKey";

    /// <summary>
    /// SHA-256 (lower-case hex) of key values that are public: never the values themselves.
    /// </summary>
    public static readonly IReadOnlySet<string> KnownPublicKeyHashes = new HashSet<string>(StringComparer.Ordinal)
    {
        // Submission key shipped in the API host's appsettings.json from 0.6.0 to 0.9.1.
        "cb5ad31f8f6e274994daca1008c303b68ab654b37e9a9c61e1dc08129a09a2c1",
        // Sign-in key example in the NuGet setup guide and the API template.
        "a1ba724ff623317c6e652ea26effe9e275e457591efda70436687413878a74c0",
        // Submission key example in the NuGet setup guide and the API template.
        "0d16296d3f1beaa208964ab6e30ed75125018716277998409d6e6199c3834686",
        // Sign-in key literal shipped in the docker compose quick start up to 0.9.1.
        "8978f6ac8b78ec5cf8a911428da9a7690120dd1c4bd8f50ba460450d45f0906e",
        // Sign-in key placeholder shipped in the Azure deployment sample parameters up to 0.9.1.
        "b8ba8530daf797048375d0b78910221505cd6b6b882ef052a525e105ae013dee",
        // Submission key placeholder shipped in the Azure deployment sample parameters up to 0.9.1.
        "cd61d9614b0784dec873e5a98eed8c21dd637ffe79848223942b460f8749028c",
        // Sign-in key example once in the security configuration and auth settings docs, up to 0.9.1.
        "1d58e624acb542bbb785bd62b51ed7eb9dafbbd37fef015e1bd498972b86044c",
    };

    private static readonly IReadOnlySet<string> _emptyDenyList = new HashSet<string>();

    /// <summary>
    /// Checks a key. Returns null when it passes, otherwise an error that names the setting and how to fix it,
    /// without the value.
    /// </summary>
    public static string? Check(string? key, string configPath, IReadOnlySet<string> deniedHashes)
    {
        var problem = Problem(key, deniedHashes);
        return problem is null ? null : $"{configPath} {problem}. {HowToFix(configPath)}";
    }

    /// <summary>
    /// Throws the same failure startup validation reports when a key fails the rule, for code that reads a key while
    /// the host is still being built.
    /// </summary>
    public static void EnsureValid<TOptions>(string? key, string configPath)
    {
        var error = Check(key, configPath, _emptyDenyList);
        if (error is not null)
        {
            throw new OptionsValidationException(configPath, typeof(TOptions), [error]);
        }
    }

    /// <summary>The fix, with the environment variable name, for an error or a warning.</summary>
    public static string HowToFix(string configPath) =>
        $"Set {configPath.Replace(":", "__", StringComparison.Ordinal)} to a random value of at least {MIN_KEY_BYTES} bytes, " +
        "for example the output of `openssl rand -base64 48`, and keep it secret.";

    /// <summary>SHA-256 of a key, in lower-case hex.</summary>
    public static string Hash(string key) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    private static string? Problem(string? key, IReadOnlySet<string> deniedHashes)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return "is not set";
        }

        if (Encoding.UTF8.GetByteCount(key) < MIN_KEY_BYTES)
        {
            return $"is shorter than {MIN_KEY_BYTES} bytes";
        }

        var hash = Hash(key);
        if (KnownPublicKeyHashes.Contains(hash) || deniedHashes.Contains(hash))
        {
            return "is a publicly known example value";
        }

        return null;
    }
}
