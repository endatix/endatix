using Endatix.Modules.Reporting.Domain;

namespace Endatix.Modules.Reporting.Data;

/// <summary>
/// Repository for flattened submissions.
/// </summary>
public interface IFlattenedSubmissionRepository
{
    /// <summary>
    /// Gets a flattened submission by submission ID.
    /// </summary>
    /// <param name="tenantId">The ID of the tenant.</param>
    /// <param name="submissionId">The ID of the submission.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The flattened submission, or <c>null</c> if not found.</returns>
    Task<FlattenedSubmission?> GetBySubmissionIdAsync(long tenantId, long submissionId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a flattened submission by submission ID or creates a new one.
    /// </summary>
    /// <param name="tenantId">The ID of the tenant.</param>
    /// <param name="submissionId">The ID of the submission.</param>
    /// <param name="formId">The ID of the form.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The flattened submission.</returns>
    Task<FlattenedSubmission> GetOrCreateAsync(
        long tenantId,
        long submissionId,
        long formId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Saves a flattened submission.
    /// </summary>
    /// <param name="flattenedSubmission">The flattened submission.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The task.</returns>
    Task SaveAsync(FlattenedSubmission flattenedSubmission, CancellationToken cancellationToken);
}
