namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Request model for partial update of a form submission by token.
/// </summary>
public class PartialUpdateSubmissionByTokenRequest : BaseSubmissionRequest
{
    /// <summary>
    /// The token of the submission that will be updated
    /// </summary>
    public string SubmissionToken { get; set; }
}