namespace Endatix.Core.Authorization.Access;

/// <summary>
/// Context for computing submission management access permissions for authenticated users.
/// </summary>
public sealed class SubmissionAccessContext
{
    /// <summary>
    /// Constructor for the SubmissionAccessContext
    /// </summary>
    /// <param name="formId">The id of the form</param>
    /// <param name="submissionId">The id of the submission</param>
    public SubmissionAccessContext(long formId, long submissionId)
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
