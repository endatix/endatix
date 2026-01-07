namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Request to partially update submission using access token.
/// </summary>
public class PartialUpdateByAccessTokenRequest : BaseSubmissionRequest
{
    /// <summary>
    /// The access token.
    /// </summary>
    public string? Token { get; set; }
}
