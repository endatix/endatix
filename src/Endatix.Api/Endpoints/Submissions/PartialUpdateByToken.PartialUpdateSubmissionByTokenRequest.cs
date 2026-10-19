namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Request model for partial update of a form submission by token.
/// </summary>
public class PartialUpdateSubmissionByTokenRequest : BasePublicSubmissionRequest
{
    /// <summary>
    /// Creates a request with an empty token; route binding fills it in.
    /// </summary>
    public PartialUpdateSubmissionByTokenRequest()
    {
        SubmissionToken = string.Empty;
    }

    /// <summary>
    /// The token of the submission that will be updated
    /// </summary>
    public string SubmissionToken { get; set; }
}