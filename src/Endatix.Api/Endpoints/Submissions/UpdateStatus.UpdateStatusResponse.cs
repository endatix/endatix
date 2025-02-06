namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Response model for updating a form submission status.
/// </summary>
public record UpdateStatusResponse(
    long SubmissionId,
    string Status)
{
    /// <summary>
    /// The ID of the submission.
    /// </summary>
    public long SubmissionId { get; init; } = SubmissionId;

    /// <summary>
    /// The status of the submission.
    /// </summary>
    public string Status { get; init; } = Status;
}