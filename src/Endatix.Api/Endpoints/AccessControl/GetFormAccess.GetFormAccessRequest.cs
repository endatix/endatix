namespace Endatix.Api.Endpoints.AccessControl;

/// <summary>
/// Request model for getting form access permissions.
/// </summary>
public class GetFormAccessRequest
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
