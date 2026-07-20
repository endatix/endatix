namespace Endatix.Modules.Reporting.Contracts.Export;

/// <summary>
/// Detects request-time filters that are not allowed for a given capability.
/// </summary>
public static class ExportRequestFilterGuard
{
    public static IReadOnlyList<string> GetDisallowedWireNames(
        ExportRequestFilterKind allowed,
        bool? includeTestSubmissions,
        DateTime? createdAfter,
        DateTime? createdBefore,
        DateTime? completedAfter,
        DateTime? completedBefore,
        long? minSubmissionId,
        long? maxSubmissionId,
        string? locale,
        IReadOnlyList<string>? columnScope)
    {
        List<string> disallowed = [];

        if (includeTestSubmissions.HasValue &&
            !allowed.HasFlag(ExportRequestFilterKind.IncludeTestSubmissions))
        {
            disallowed.Add(ExportRequestFilterWireNames.IncludeTestSubmissions);
        }

        if ((createdAfter.HasValue || createdBefore.HasValue) &&
            !allowed.HasFlag(ExportRequestFilterKind.CreatedAtRange))
        {
            disallowed.Add(ExportRequestFilterWireNames.CreatedAtRange);
        }

        if ((completedAfter.HasValue || completedBefore.HasValue) &&
            !allowed.HasFlag(ExportRequestFilterKind.CompletedAtRange))
        {
            disallowed.Add(ExportRequestFilterWireNames.CompletedAtRange);
        }

        if ((minSubmissionId.HasValue || maxSubmissionId.HasValue) &&
            !allowed.HasFlag(ExportRequestFilterKind.SubmissionIdRange))
        {
            disallowed.Add(ExportRequestFilterWireNames.SubmissionIdRange);
        }

        if (!string.IsNullOrWhiteSpace(locale) &&
            !allowed.HasFlag(ExportRequestFilterKind.Locale))
        {
            disallowed.Add(ExportRequestFilterWireNames.Locale);
        }

        if (columnScope is { Count: > 0 } &&
            !allowed.HasFlag(ExportRequestFilterKind.ColumnScope))
        {
            disallowed.Add(ExportRequestFilterWireNames.ColumnScope);
        }

        return disallowed;
    }
}
