namespace Endatix.Core.Abstractions.Submissions;

/// <summary>
/// Context for computing submission access permissions.
/// </summary>
public class SubmissionAccessContext
{
    public long FormId { get; init; }
    public long? SubmissionId { get; init; }
    public string? AccessToken { get; init; }
    public bool IsNewSubmission => !SubmissionId.HasValue;

    public SubmissionAccessContext(long formId, long? submissionId = null, string? accessToken = null)
    {
        FormId = formId;
        SubmissionId = submissionId;
        AccessToken = accessToken;
    }
}
