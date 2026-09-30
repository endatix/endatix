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

    /// <summary>
    /// Hard-deletes all flattened submission rows for a form (including soft-deleted rows).
    /// Used after a replace-mode FormSchema compile to clear test flatten debris.
    /// </summary>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="formId">The form ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows deleted.</returns>
    Task<int> DeleteByFormIdAsync(long tenantId, long formId, CancellationToken cancellationToken);

    /// <summary>
    /// Hard-deletes the flattened submission row for a submission (including soft-deleted rows).
    /// Used when a submission is deleted so the reporting read model does not keep orphans.
    /// </summary>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="submissionId">The submission ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows deleted.</returns>
    Task<int> DeleteBySubmissionIdAsync(long tenantId, long submissionId, CancellationToken cancellationToken);

    /// <summary>
    /// Hard-deletes the flattened submission row of a submission of the given form, when there is one. Used when a
    /// flatten finds its submission gone, so it removes only the row it would have written.
    /// </summary>
    /// <returns>The number of rows deleted.</returns>
    Task<int> DeleteBySubmissionAsync(FlattenedSubmissionKey key, CancellationToken cancellationToken);

    /// <summary>
    /// Creates the tracking row for a submission unless it already exists, including when another flatten of the
    /// same submission creates it at the same time.
    /// </summary>
    Task EnsureExistsAsync(FlattenedSubmissionKey key, CancellationToken cancellationToken);

    /// <summary>Marks the row processing.</summary>
    /// <returns><c>false</c> when the row is newer than the write's revision, or missing, and nothing was written.</returns>
    Task<bool> TryMarkProcessingAsync(FlattenedRevision write, CancellationToken cancellationToken);

    /// <summary>
    /// Stores the flattened data built from the write's revision, with the revision and the submission's stamp it
    /// was built from.
    /// </summary>
    /// <returns><c>false</c> when the row is newer than the write's revision, or missing, and nothing was written.</returns>
    Task<bool> TryMarkProcessedAsync(FlattenedRevision write, string dataJson, CancellationToken cancellationToken);

    /// <summary>
    /// Marks the row skipped for a submission that was incomplete at the write's revision, and clears its data and
    /// its submission stamp.
    /// </summary>
    /// <returns><c>false</c> when the row is newer than the write's revision, or missing, and nothing was written.</returns>
    Task<bool> TryMarkSkippedAsync(FlattenedRevision write, CancellationToken cancellationToken);

    /// <summary>Marks the row failed with <paramref name="error"/>.</summary>
    /// <returns><c>false</c> when the row is newer than the write's revision, or missing, and nothing was written.</returns>
    Task<bool> TryMarkFailedAsync(FlattenedRevision write, string error, CancellationToken cancellationToken);
}
