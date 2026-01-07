using Endatix.Framework.Configuration;

namespace Endatix.Infrastructure.Features.Submissions;

/// <summary>
/// Configuration options for submission access tokens.
/// </summary>
public sealed class SubmissionAccessTokenOptions : EndatixOptionsBase
{
    /// <inheritdoc/>
    public override string SectionPath => "Submissions";

    /// <summary>
    /// Signing key for HMAC-SHA256 signature used in access tokens.
    /// Must be at least 32 characters for security.
    /// </summary>
    public string AccessTokenSigningKey { get; init; } = string.Empty;
}
