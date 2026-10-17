using FlattenedSubmissionRow = Endatix.Modules.Reporting.Domain.FlattenedSubmission;

namespace Endatix.Modules.Reporting.Features.SearchCopy;

/// <summary>Which prepared rows answer filters can check.</summary>
internal static class SearchCopyRows
{
    /// <summary>
    /// The rows whose search copy holds every key a filter reads: written, and stamped with at least
    /// <paramref name="revisionNeeded"/> (<see cref="FormSearchKeys.RevisionNeededFor"/> of the filtered keys). Any
    /// other row has not been rebuilt for a key added since, so it counts as not checked, not as not matching.
    /// </summary>
    public static IQueryable<FlattenedSubmissionRow> HoldingSearchKeys(
        this IQueryable<FlattenedSubmissionRow> rows,
        int revisionNeeded) =>
        rows.Where(row => row.SearchJson != null && row.SearchKeysRevision >= revisionNeeded);
}
