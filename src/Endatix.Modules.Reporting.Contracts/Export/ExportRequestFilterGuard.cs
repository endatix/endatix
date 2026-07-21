namespace Endatix.Modules.Reporting.Contracts.Export;

/// <summary>
/// Detects request-time filters that are not allowed for a given capability.
/// </summary>
public static class ExportRequestFilterGuard
{
    public static IReadOnlyList<string> GetDisallowedWireNames(
        ExportRequestFilters allowed,
        ExportFilterContext filters)
    {
        List<string> disallowed = [];

        if (filters.IncludeTestSubmissions.HasValue &&
            !allowed.HasFlag(ExportRequestFilters.IncludeTestSubmissions))
        {
            disallowed.Add(ExportRequestFilterWireNames.IncludeTestSubmissions);
        }

        if ((filters.CreatedAfter.HasValue || filters.CreatedBefore.HasValue) &&
            !allowed.HasFlag(ExportRequestFilters.CreatedAtRange))
        {
            disallowed.Add(ExportRequestFilterWireNames.CreatedAtRange);
        }

        if ((filters.CompletedAfter.HasValue || filters.CompletedBefore.HasValue) &&
            !allowed.HasFlag(ExportRequestFilters.CompletedAtRange))
        {
            disallowed.Add(ExportRequestFilterWireNames.CompletedAtRange);
        }

        if ((filters.MinSubmissionId.HasValue || filters.MaxSubmissionId.HasValue) &&
            !allowed.HasFlag(ExportRequestFilters.SubmissionIdRange))
        {
            disallowed.Add(ExportRequestFilterWireNames.SubmissionIdRange);
        }

        if (!string.IsNullOrWhiteSpace(filters.Locale) &&
            !allowed.HasFlag(ExportRequestFilters.Locale))
        {
            disallowed.Add(ExportRequestFilterWireNames.Locale);
        }

        if (filters.ColumnScope is { Count: > 0 } &&
            !allowed.HasFlag(ExportRequestFilters.ColumnScope))
        {
            disallowed.Add(ExportRequestFilterWireNames.ColumnScope);
        }

        if (filters.CompletionStatus.HasValue &&
            !allowed.HasFlag(ExportRequestFilters.CompletionStatus))
        {
            disallowed.Add(ExportRequestFilterWireNames.CompletionStatus);
        }

        return disallowed;
    }
}
