using Endatix.Core.Abstractions.Submissions;

namespace Endatix.Api.Endpoints.Forms;

/// <summary>
/// Request model for getting public form access (anonymous/token).
/// </summary>
public class GetAccessRequest
{
    /// <summary>
    /// The form ID (from route).
    /// </summary>
    public long FormId { get; set; }

    /// <summary>
    /// The token (access token or submission token). When set, TokenType must be set.
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// The type of token when Token is provided.
    /// </summary>
    public SubmissionTokenType? TokenType { get; set; }
}
