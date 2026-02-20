namespace Endatix.Api.Endpoints.AccessControl;

/// <summary>
/// Request model for getting RBAC permissions (authenticated users).
/// </summary>
public class GetPermissionsRequest
{
    /// <summary>
    /// The form (resource) ID.
    /// </summary>
    public long FormId { get; set; }

    /// <summary>
    /// The submission ID (optional - for submission-scoped permissions).
    /// </summary>
    public long? SubmissionId { get; set; }
}
