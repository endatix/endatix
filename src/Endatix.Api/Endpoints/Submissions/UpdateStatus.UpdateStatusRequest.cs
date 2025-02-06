namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Request model for updating a form submission status.
/// </summary>
public record UpdateStatusRequest(
    long SubmissionId,
    long FormId,
    string Status)
{
    /// <summary>
    /// The ID of the submission to update.
    /// </summary>
    public long SubmissionId { get; init; } = SubmissionId;

    /// <summary>
    /// The ID of the form.
    /// </summary>
    public long FormId { get; init; } = FormId;

    /// <summary>
    /// The status of the submission.
    /// </summary>
    public string Status { get; init; } = Status;
} 