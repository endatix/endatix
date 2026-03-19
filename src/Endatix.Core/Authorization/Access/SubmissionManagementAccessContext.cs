namespace Endatix.Core.Authorization.Access;

/// <summary>
/// Context for computing submission management access permissions for authenticated users.
/// </summary>
public class SubmissionManagementAccessContext
{
    public SubmissionManagementAccessContext(long formId, long submissionId)
    {
        FormId = formId;
        SubmissionId = submissionId;
    }

    /// <summary>
    /// The form ID.
    /// </summary>
    public long FormId { get; init; }

    /// <summary>
    /// The submission ID.
    /// </summary>
    public long SubmissionId { get; init; }
}
