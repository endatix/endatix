namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Request model for getting a form submission by token.
/// </summary>
public class GetByTokenRequest
{
    /// <summary>
    /// The ID of the form.
    /// </summary>
    public long FormId { get; set; }

    /// <summary>
    /// The token of the form submission.
    /// </summary>
    public string? SubmissionToken { get; set; }
}
