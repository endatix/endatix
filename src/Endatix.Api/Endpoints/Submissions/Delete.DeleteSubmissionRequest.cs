namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Request model for deleting a submission.
/// </summary>
public class DeleteSubmissionRequest : IManagedSubmissionTarget
{
    /// <summary>
    /// The ID of the form.
    /// </summary>
    public long FormId { get; init; }

    /// <summary>
    /// The ID of the submission to delete.
    /// </summary>
    public long SubmissionId { get; init; }
}