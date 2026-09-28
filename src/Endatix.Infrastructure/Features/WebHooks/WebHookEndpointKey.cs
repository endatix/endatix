using System.Security.Cryptography;
using System.Text;

namespace Endatix.Infrastructure.Features.WebHooks;

/// <summary>
/// Identifies a configured webhook endpoint without naming its URL.
/// </summary>
/// <remarks>
/// The lowercase hex SHA-256 of the URL exactly as configured: stable while the URL is unchanged, so a retry reaches
/// the same endpoint however the list around it changes, and free of anything a URL may carry, such as a token in
/// its query string. Two entries with the same URL are one endpoint.
/// </remarks>
public static class WebHookEndpointKey
{
    public static string Of(string url) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(url)));
}
