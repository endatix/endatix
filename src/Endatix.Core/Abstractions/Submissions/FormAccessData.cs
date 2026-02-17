namespace Endatix.Core.Abstractions.Submissions;

/// <summary>
/// Simplified authorization data for form/submission access.
/// Returns flat permission arrays for O(1) client-side access.
/// Identity (who the user is) should be fetched from /auth/me endpoint.
/// </summary>
public class FormAccessData
{
    /// <summary>
    /// The form ID this access data applies to.
    /// </summary>
    public string FormId { get; init; } = string.Empty;

    /// <summary>
    /// The submission ID if applicable (null for new submissions).
    /// </summary>
    public string? SubmissionId { get; init; }

    /// <summary>
    /// Permissions for the form resource.
    /// </summary>
    public IEnumerable<string> FormPermissions { get; init; } = [];

    /// <summary>
    /// Permissions for the submission resource (or "new" submission when no submissionId provided).
    /// </summary>
    public IEnumerable<string> SubmissionPermissions { get; init; } = [];
}
