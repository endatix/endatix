using System.Text;
using System.Text.Json;

namespace Endatix.Infrastructure.Identity.Authentication;

/// <summary>
/// Lightweight JWT token inspector that extracts issuer without signature validation.
/// Optimized for performance in token routing scenarios.
/// </summary>
internal sealed class LightweightJwtTokenInspector : IJwtTokenInspector
{
    /// <inheritdoc />
    public string? GetIssuer(string token)
    {
        try
        {
            using var doc = ParseJwtPayload(token);
            return doc?.RootElement.TryGetProperty("iss", out var iss) == true 
                ? iss.GetString() 
                : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parses JWT payload without signature validation for performance.
    /// </summary>
    private static JsonDocument? ParseJwtPayload(string token)
    {
        var parts = token.Split('.');
        if (parts.Length < 2)
        {
            return null;
        }

        var payload = parts[1];

        // Add Base64URL padding if missing
        var padding = 4 - payload.Length % 4;
        if (padding < 4)
        {
            payload += new string('=', padding);
        }

        try
        {
            var bytes = Convert.FromBase64String(payload.Replace('-', '+').Replace('_', '/'));
            return JsonDocument.Parse(bytes);
        }
        catch
        {
            return null;
        }
    }
} 