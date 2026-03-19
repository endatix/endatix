namespace Endatix.Core.Abstractions.Submissions;

/// <summary>
/// Context for computing submission access permissions.
/// Public/token flows only: use FormId + Token + TokenType (submission is resolved from token).
/// </summary>
public class SubmissionAccessContext
{
    public SubmissionAccessContext(long formId, string? token = null, SubmissionTokenType? tokenType = null)
    {
        FormId = formId;
        Token = token;
        TokenType = tokenType;
    }

    /// <summary>
    /// The form ID.
    /// </summary>
    public long FormId { get; init; }

    /// <summary>
    /// The token required for public/token flows.
    /// </summary>
    public string? Token { get; init; }

    /// <summary>
    /// The type of token when Token is set.
    /// </summary>
    public SubmissionTokenType? TokenType { get; init; }

    /// <summary>
    /// True when no submission is identified
    /// </summary>
    public bool IsNewSubmission => string.IsNullOrEmpty(Token);
}
