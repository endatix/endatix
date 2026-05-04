using System.Security.Cryptography;
using System.Text;
using Ardalis.GuardClauses;

namespace Endatix.Infrastructure.Features.AccessControl;

/// <summary>
/// Builds a non-reversible cache key segment for form access JWTs so raw bearer material never appears in HybridCache keys.
/// Uses HMAC-SHA256 with the same signing key material as JWT configuration (server secret, never sent to clients).
/// </summary>
internal static class FormAccessTokenCacheKeyFingerprint
{
    /// <summary>
    /// Returns a fixed-length uppercase hex digest suitable for cache keys.
    /// </summary>
    public static string ComputeHmacSha256Hex(string rawToken, string signingKeyMaterial)
    {
        Guard.Against.NullOrWhiteSpace(rawToken);
        Guard.Against.NullOrWhiteSpace(signingKeyMaterial);

        var keyBytes = Encoding.UTF8.GetBytes(signingKeyMaterial);
        var tokenBytes = Encoding.UTF8.GetBytes(rawToken);
        using HMACSHA256 hmac = new(keyBytes);
        var hash = hmac.ComputeHash(tokenBytes);
        return Convert.ToHexString(hash);
    }
}
