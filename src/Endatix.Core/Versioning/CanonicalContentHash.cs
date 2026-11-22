using System.Security.Cryptography;
using System.Text;

namespace Endatix.Core.Versioning;

/// <summary>The content hash of a <see cref="IContentVersion"/>: SHA-256 of its canonical JSON, as lowercase hex.</summary>
internal static class CanonicalContentHash
{
    /// <summary>The length of a hash, in characters.</summary>
    public const int LENGTH = 64;

    /// <summary>The hash of <paramref name="canonicalJson"/>, which must already be in canonical form.</summary>
    public static string Of(string canonicalJson)
    {
        ArgumentNullException.ThrowIfNull(canonicalJson);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson)));
    }
}
