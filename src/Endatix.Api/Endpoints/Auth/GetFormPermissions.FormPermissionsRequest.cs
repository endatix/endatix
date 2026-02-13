namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Request model for getting form permissions.
/// </summary>
public class GetFormPermissionsRequest
{
    /// <summary>
    /// The form ID (required)
    /// </summary>
    public long FormId { get; set; }

    /// <summary>
    /// The submission ID (optional - for existing submission context)
    /// </summary>
    public long? SubmissionId { get; set; }

    /// <summary>
    /// Access token for token-based access (optional)
    /// </summary>
    public string? Token { get; set; }
}
