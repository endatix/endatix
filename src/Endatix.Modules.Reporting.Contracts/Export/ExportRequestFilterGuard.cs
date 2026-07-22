namespace Endatix.Modules.Reporting.Contracts.Export;

/// <summary>
/// Detects request-time filters that are not allowed for a given capability.
/// </summary>
public static class ExportRequestFilterGuard
{
    /// <summary>
    /// Gets the disallowed filters for a given capability.
    /// </summary>
    /// <param name="allowed">The allowed filters.</param>
    /// <param name="filters">The filters to check.</param>
    /// <returns>The disallowed filters.</returns>
    public static IReadOnlyList<string> GetDisallowedWireNames(
        ExportRequestFilters allowed,
        ExportFilterContext filters)
    {
        List<string> disallowed = [];

        AddWhenDisallowed(
            disallowed,
            allowed,
            ExportRequestFilters.IncludeTestSubmissions,
            AllowedExportFilters.IncludeTestSubmissions,
            filters.IncludeTestSubmissions.HasValue);

        AddWhenDisallowed(
            disallowed,
            allowed,
            ExportRequestFilters.CreatedAtRange,
            AllowedExportFilters.CreatedAtRange,
            HasEitherBound(filters.CreatedAfter, filters.CreatedBefore));

        AddWhenDisallowed(
            disallowed,
            allowed,
            ExportRequestFilters.StartedAtRange,
            AllowedExportFilters.StartedAtRange,
            HasEitherBound(filters.StartedAfter, filters.StartedBefore));

        AddWhenDisallowed(
            disallowed,
            allowed,
            ExportRequestFilters.CompletedAtRange,
            AllowedExportFilters.CompletedAtRange,
            HasEitherBound(filters.CompletedAfter, filters.CompletedBefore));

        AddWhenDisallowed(
            disallowed,
            allowed,
            ExportRequestFilters.SubmissionIdRange,
            AllowedExportFilters.SubmissionIdRange,
            HasEitherBound(filters.MinSubmissionId, filters.MaxSubmissionId));

        AddWhenDisallowed(
            disallowed,
            allowed,
            ExportRequestFilters.Locale,
            AllowedExportFilters.Locale,
            !string.IsNullOrWhiteSpace(filters.Locale));

        AddWhenDisallowed(
            disallowed,
            allowed,
            ExportRequestFilters.ColumnScope,
            AllowedExportFilters.ColumnScope,
            filters.ColumnScope is { Count: > 0 });

        AddWhenDisallowed(
            disallowed,
            allowed,
            ExportRequestFilters.CompletionStatus,
            AllowedExportFilters.CompletionStatus,
            filters.CompletionStatus.HasValue);

        return disallowed;
    }

    private static void AddWhenDisallowed(
        List<string> disallowed,
        ExportRequestFilters allowed,
        ExportRequestFilters flag,
        string wireName,
        bool isPresent)
    {
        if (!isPresent || allowed.HasFlag(flag))
        {
            return;
        }

        disallowed.Add(wireName);
    }

    private static bool HasEitherBound<T>(T? lower, T? upper)
        where T : struct =>
        lower.HasValue || upper.HasValue;
}
