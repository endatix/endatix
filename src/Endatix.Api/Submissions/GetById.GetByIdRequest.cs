namespace Endatix.Api.Submissions;

/// <summary>
/// Request model for getting a form submission by ID.
/// </summary>
public class GetByIdRequest
{
    /// <summary>
    /// The ID of the form.
    /// </summary>
    public long FormId { get; set; }

    /// <summary>
    /// The ID of the form submission.
    /// </summary>
    public long SubmissionId { get; set; }
}
