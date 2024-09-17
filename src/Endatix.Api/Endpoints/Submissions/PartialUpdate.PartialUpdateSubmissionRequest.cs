namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Request model for partial update of a form submission.
/// </summary>
public class PartialUpdateSubmissionRequest
{
    /// <summary>
    /// The ID the submission that will be updated
    /// </summary>
    public long SubmissionId { get; set; }

    /// <summary>
    /// The ID of the form for which the submission is made.
    /// </summary>
    public long FormId { get; set; }

    /// <summary>
    /// Stringified form submission data
    /// </summary>
    public string? JsonData { get; set; }

    /// <summary>
    /// Boolean flag to mark the form completion status
    /// </summary>
    public bool? IsComplete { get; set; }

    /// <summary>
    /// The current page of the form
    /// </summary>
    public int? CurrentPage { get; set; }

    /// <summary>
    /// Stringified metadata related to the form submission
    /// </summary>
    public string? Metadata { get; set; }
}
