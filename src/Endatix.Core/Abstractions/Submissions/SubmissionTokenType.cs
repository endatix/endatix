namespace Endatix.Core.Abstractions.Submissions;

/// <summary>
/// The type of token used for submission access.
/// </summary>
public enum SubmissionTokenType
{
    /// <summary>Short-lived signed token with explicit permissions (e.g. view, edit, export).</summary>
    AccessToken,

    /// <summary>Long-lived token stored on the submission, used for respondent editing.</summary>
    SubmissionToken
}
