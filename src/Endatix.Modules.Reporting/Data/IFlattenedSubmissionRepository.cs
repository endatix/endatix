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
    /// Counts a form's live (not deleted) rows by integration status code.
    /// </summary>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="formId">The form ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of rows per status code; codes with no rows are absent.</returns>
    Task<IReadOnlyDictionary<string, int>> CountByStatusAsync(long tenantId, long formId, CancellationToken cancellationToken);

    /// <summary>
    /// The oldest <see cref="FlattenedSubmission.SearchKeysRevision"/> among a form's live processed rows: a search key
    /// added after it is still missing from some rows. A processed row without a search copy counts, as the rebuild
    /// rewrites it and the answer filters cannot check it until then.
    /// </summary>
    /// <returns>The revision, or <c>null</c> when the form has no such row.</returns>
    Task<int?> OldestSearchKeysRevisionAsync(long tenantId, long formId, CancellationToken cancellationToken);

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
    /// flatten finds its submission deleted after it wrote the row, so it removes only the row it wrote.
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
    /// Stores the flattened data and its search copy built from the write's revision, in one update, with the
    /// revision and the submission's stamp it was built from.
    /// </summary>
    /// <returns><c>false</c> when the row is newer than the write's revision, or missing, and nothing was written.</returns>
    Task<bool> TryMarkProcessedAsync(FlattenedRevision write, FlattenedRowData data, CancellationToken cancellationToken);

    /// <summary>
    /// Marks the row skipped for a submission that was incomplete at the write's revision, and clears its data, its
    /// search copy and its submission stamp.
    /// </summary>
    /// <returns><c>false</c> when the row is newer than the write's revision, or missing, and nothing was written.</returns>
    Task<bool> TryMarkSkippedAsync(FlattenedRevision write, CancellationToken cancellationToken);

    /// <summary>Marks the row failed with <paramref name="error"/>.</summary>
    /// <returns><c>false</c> when the row is newer than the write's revision, or missing, and nothing was written.</returns>
    Task<bool> TryMarkFailedAsync(FlattenedRevision write, string error, CancellationToken cancellationToken);
}
