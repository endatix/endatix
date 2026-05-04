using System.Security.Cryptography;
using System.Text;
using Ardalis.GuardClauses;

namespace Endatix.Infrastructure.Caching;

/// <summary>
/// Shared helper to derive non-reversible, fixed-size cache-key segments from sensitive values.
/// </summary>
internal static class CacheKeyFingerprint
{
    /// <summary>
    /// Computes a HMAC-SHA256 hash of the value and returns the result as a hexadecimal string.
    /// </summary>
    /// <param name="value">The value to hash.</param>
    /// <param name="signingKeyMaterial">The signing key material to use for the HMAC-SHA256 hash.</param>
    /// <returns>The hexadecimal string of the HMAC-SHA256 hash.</returns>
    public static string ComputeHmacSha256Hex(string value, string signingKeyMaterial)
    {
        Guard.Against.NullOrWhiteSpace(value);
        Guard.Against.NullOrWhiteSpace(signingKeyMaterial);

        var keyBytes = Encoding.UTF8.GetBytes(signingKeyMaterial);
        var valueBytes = Encoding.UTF8.GetBytes(value);
        using HMACSHA256 hmac = new(keyBytes);
        var hash = hmac.ComputeHash(valueBytes);
        return Convert.ToHexString(hash);
    }
}
