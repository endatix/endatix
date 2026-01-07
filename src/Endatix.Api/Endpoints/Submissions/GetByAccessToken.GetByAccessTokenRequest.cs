namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Request to get submission using access token.
/// </summary>
public class GetByAccessTokenRequest
{
    /// <summary>
    /// The ID of the form.
    /// </summary>
    public long FormId { get; set; }

    /// <summary>
    /// The access token.
    /// </summary>
    public string? Token { get; set; }
}
