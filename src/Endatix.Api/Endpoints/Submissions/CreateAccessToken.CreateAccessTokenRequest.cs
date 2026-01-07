namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Request to generate access token.
/// </summary>
public record CreateAccessTokenRequest
{
    /// <summary>
    /// The ID of the form.
    /// </summary>
    public long FormId { get; set; }

    /// <summary>
    /// The ID of the submission.
    /// </summary>
    public long SubmissionId { get; set; }

    /// <summary>
    /// Token expiry time in minutes.
    /// </summary>
    public int? ExpiryMinutes { get; set; }

    /// <summary>
    /// Array of permissions to grant (view, edit, export).
    /// </summary>
    public List<string> Permissions { get; set; } = new();
}
