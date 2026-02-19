namespace Endatix.Api.Endpoints.Forms;

/// <summary>
/// Request model for getting public form access (anonymous/token).
/// </summary>
public class GetAccessRequest
{
    /// <summary>
    /// The form ID (from route).
    /// </summary>
    public long FormId { get; set; }

    /// <summary>
    /// The submission ID (optional - for existing submission context).
    /// </summary>
    public long? SubmissionId { get; set; }

    /// <summary>
    /// Access token for token-based access (optional).
    /// </summary>
    public string? Token { get; set; }
}
