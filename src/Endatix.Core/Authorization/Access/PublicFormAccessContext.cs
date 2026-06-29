namespace Endatix.Core.Authorization.Access;

/// <summary>
/// Context for computing form/submission access permissions.
/// Public/token flows only: use FormId + Token + TokenType (submission is resolved from token).
/// </summary>
/// <remarks>
/// Constructor for PublicFormAccessContext.
/// </remarks>
/// <param name="formId">The form ID.</param>
/// <param name="token">The token required for public/token flows.</param>
/// <param name="tokenType">The type of token when Token is set.</param>
public sealed class PublicFormAccessContext(
    long formId,
    string? token = null,
    SubmissionTokenType? tokenType = null)
{

    /// <summary>
    /// The form ID.
    /// </summary>
    public long FormId { get; init; } = formId;

    /// <summary>
    /// The token required for public/token flows.
    /// </summary>
    public string? Token { get; init; } = token;

    /// <summary>
    /// The type of token when Token is set.
    /// </summary>
    public SubmissionTokenType? TokenType { get; init; } = tokenType;

    /// <summary>
    /// True when no submission is identified
    /// </summary>
    public bool IsNewSubmission => string.IsNullOrEmpty(Token);
}
