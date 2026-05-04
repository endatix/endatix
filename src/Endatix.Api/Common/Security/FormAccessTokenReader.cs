using Microsoft.AspNetCore.Http;

namespace Endatix.Api.Common.Security;

/// <summary>
/// Reads the form access JWT from <c>Authorization: Bearer</c> (minted via <c>POST .../forms/{formId}/access-tokens</c>).
/// </summary>
internal static class FormAccessTokenReader
{
    private const string BEARER_PREFIX = "Bearer ";
    private static readonly int _bearerPrefixLength = BEARER_PREFIX.Length;

    /// <summary>
    /// Reads the form access JWT from the <c>Authorization: Bearer</c> header of the HTTP request.
    /// </summary>
    /// <param name="httpRequest">The HTTP request.</param>
    /// <returns>The form access JWT or <c>null</c> if not found.</returns>
    internal static string? ReadToken(HttpRequest httpRequest)
    {
        string? authHeader = httpRequest?.Headers?.Authorization;
        if (string.IsNullOrWhiteSpace(authHeader)
            || !authHeader.StartsWith(BEARER_PREFIX, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var raw = authHeader[_bearerPrefixLength..].Trim();

        return raw.Length > 0 ? raw : null;
    }
}
