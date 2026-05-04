using System.Security.Cryptography;
using System.Text;
using Ardalis.GuardClauses;

namespace Endatix.Infrastructure.Caching;

/// <summary>
/// Shared helper to derive non-reversible, fixed-size cache-key segments from sensitive values.
/// </summary>
internal static class CacheKeyFingerprint
{
    public static string ComputeHmacSha256Hex(string value, string signingKeyMaterial)
    {
        Guard.Against.NullOrWhiteSpace(value);
        Guard.Against.NullOrWhiteSpace(signingKeyMaterial);

        byte[] keyBytes = Encoding.UTF8.GetBytes(signingKeyMaterial);
        byte[] valueBytes = Encoding.UTF8.GetBytes(value);
        using HMACSHA256 hmac = new(keyBytes);
        byte[] hash = hmac.ComputeHash(valueBytes);
        return Convert.ToHexString(hash);
    }
}
