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
            disallowed.Add(AllowedExportFilters.IncludeTestSubmissions);
        }

        if ((filters.CreatedAfter.HasValue || filters.CreatedBefore.HasValue) &&
            !allowed.HasFlag(ExportRequestFilters.CreatedAtRange))
        {
            disallowed.Add(AllowedExportFilters.CreatedAtRange);
        }

        if ((filters.CompletedAfter.HasValue || filters.CompletedBefore.HasValue) &&
            !allowed.HasFlag(ExportRequestFilters.CompletedAtRange))
        {
            disallowed.Add(AllowedExportFilters.CompletedAtRange);
        }

        if ((filters.MinSubmissionId.HasValue || filters.MaxSubmissionId.HasValue) &&
            !allowed.HasFlag(ExportRequestFilters.SubmissionIdRange))
        {
            disallowed.Add(AllowedExportFilters.SubmissionIdRange);
        }

        if (!string.IsNullOrWhiteSpace(filters.Locale) &&
            !allowed.HasFlag(ExportRequestFilters.Locale))
        {
            disallowed.Add(AllowedExportFilters.Locale);
        }

        if (filters.ColumnScope is { Count: > 0 } &&
            !allowed.HasFlag(ExportRequestFilters.ColumnScope))
        {
            disallowed.Add(AllowedExportFilters.ColumnScope);
        }

        if (filters.CompletionStatus.HasValue &&
            !allowed.HasFlag(ExportRequestFilters.CompletionStatus))
        {
            disallowed.Add(AllowedExportFilters.CompletionStatus);
        }

        return disallowed;
    }
}
