using System.Net;

namespace Endatix.Infrastructure.Email;

/// <summary>
/// Thrown when an email provider answers a send with a non-success status, so the email was not accepted.
/// </summary>
/// <param name="provider">The provider name, such as Mailgun or SendGrid.</param>
/// <param name="statusCode">The status code the provider returned.</param>
/// <param name="responseBody">The provider's response body, for diagnostics.</param>
public sealed class EmailProviderException(string provider, HttpStatusCode statusCode, string responseBody)
    : Exception($"{provider} rejected the email with status {(int)statusCode} ({statusCode}): {responseBody}")
{
    /// <summary>
    /// The provider name, such as Mailgun or SendGrid.
    /// </summary>
    public string Provider { get; } = provider;

    /// <summary>
    /// The status code the provider returned.
    /// </summary>
    public HttpStatusCode StatusCode { get; } = statusCode;
}
