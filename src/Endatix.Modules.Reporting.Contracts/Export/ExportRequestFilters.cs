using System.Text.Json.Serialization;

namespace Endatix.Modules.Reporting.Contracts.Export;

/// <summary>
/// Request-time export filters that a capability may accept.
/// Extensible per export type — add flags as new filters are introduced.
/// </summary>
[Flags]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExportRequestFilters
{
    None = 0,
    IncludeTestSubmissions = 1 << 0,
    CreatedAtRange = 1 << 1,
    CompletedAtRange = 1 << 2,
    SubmissionIdRange = 1 << 3,
    Locale = 1 << 4,
    ColumnScope = 1 << 5,
    CompletionStatus = 1 << 6,
}

/// <summary>
/// Common <see cref="ExportRequestFilters"/> sets for built-in capabilities.
/// </summary>
public static class ExportRequestFilterSets
{
    /// <summary>
    /// Submission row exports. Locale is omitted: export values use keys, not labels;
    /// request locale is for codebook label projection.
    /// </summary>
    public const ExportRequestFilters Submissions =
        ExportRequestFilters.IncludeTestSubmissions |
        ExportRequestFilters.CreatedAtRange |
        ExportRequestFilters.CompletedAtRange |
        ExportRequestFilters.SubmissionIdRange |
        ExportRequestFilters.ColumnScope |
        ExportRequestFilters.CompletionStatus;

    /// <summary>
    /// Native codebook streams the compiled multi-locale codebook as-is (no request filters).
    /// </summary>
    public const ExportRequestFilters NativeCodebook = ExportRequestFilters.None;

    /// <summary>
    /// Shoji codebook projects display strings for a single request locale.
    /// </summary>
    public const ExportRequestFilters ShojiCodebook = ExportRequestFilters.Locale;
}

/// <summary>
/// API names for <see cref="ExportRequestFilters"/> and helpers to project allow-sets onto them.
/// </summary>
public static class AllowedExportFilters
{
    public const string IncludeTestSubmissions = "includeTestSubmissions";
    public const string CreatedAtRange = "createdAtRange";
    public const string CompletedAtRange = "completedAtRange";
    public const string SubmissionIdRange = "submissionIdRange";
    public const string Locale = "locale";
    public const string ColumnScope = "columnScope";
    public const string CompletionStatus = "completionStatus";

    private static readonly (ExportRequestFilters Filter, string Name)[] _all =
    [
        (ExportRequestFilters.IncludeTestSubmissions, IncludeTestSubmissions),
        (ExportRequestFilters.CreatedAtRange, CreatedAtRange),
        (ExportRequestFilters.CompletedAtRange, CompletedAtRange),
        (ExportRequestFilters.SubmissionIdRange, SubmissionIdRange),
        (ExportRequestFilters.Locale, Locale),
        (ExportRequestFilters.ColumnScope, ColumnScope),
        (ExportRequestFilters.CompletionStatus, CompletionStatus),
    ];

    /// <summary>
    /// Converts <see cref="ExportRequestFilters"/> flags to the <c>allowedFilters</c> API names.
    /// </summary>
    public static IReadOnlyList<string> ToAllowedFilterNames(ExportRequestFilters allowed) =>
        _all
            .Where(entry => allowed.HasFlag(entry.Filter))
            .Select(entry => entry.Name)
            .ToList();
}
