namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Request payload for creating a submission on behalf of another user.
/// Used with <see cref="CreateOnBehalf"/> endpoint.
/// </summary>
public class CreateSubmissionOnBehalfRequest : BaseSubmissionRequest
{
    /// <summary>
    /// Optional identifier of the user on whose behalf the form is being submitted.
    /// </summary>
    public string? SubmittedBy { get; set; }
}
