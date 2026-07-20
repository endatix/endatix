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
}

/// <summary>
/// Common <see cref="ExportRequestFilters"/> sets for built-in capabilities.
/// </summary>
public static class ExportRequestFilterSets
{
    public const ExportRequestFilters Submissions =
        ExportRequestFilters.IncludeTestSubmissions |
        ExportRequestFilters.CreatedAtRange |
        ExportRequestFilters.CompletedAtRange |
        ExportRequestFilters.SubmissionIdRange |
        ExportRequestFilters.Locale |
        ExportRequestFilters.ColumnScope;

    /// <summary>
    /// Native codebook streams persisted FormSchema codebook JSON as-is (no request filters applied).
    /// </summary>
    public const ExportRequestFilters NativeCodebook = ExportRequestFilters.None;

    /// <summary>
    /// Shoji codebook applies request locale to display strings; columnScope is not applied.
    /// </summary>
    public const ExportRequestFilters ShojiCodebook = ExportRequestFilters.Locale;
}

/// <summary>
/// Wire names and helpers for <see cref="ExportRequestFilters"/>.
/// </summary>
public static class ExportRequestFilterWireNames
{
    public const string IncludeTestSubmissions = "includeTestSubmissions";
    public const string CreatedAtRange = "createdAtRange";
    public const string CompletedAtRange = "completedAtRange";
    public const string SubmissionIdRange = "submissionIdRange";
    public const string Locale = "locale";
    public const string ColumnScope = "columnScope";

    private static readonly (ExportRequestFilters Filter, string Name)[] _all =
    [
        (ExportRequestFilters.IncludeTestSubmissions, IncludeTestSubmissions),
        (ExportRequestFilters.CreatedAtRange, CreatedAtRange),
        (ExportRequestFilters.CompletedAtRange, CompletedAtRange),
        (ExportRequestFilters.SubmissionIdRange, SubmissionIdRange),
        (ExportRequestFilters.Locale, Locale),
        (ExportRequestFilters.ColumnScope, ColumnScope),
    ];

    public static IReadOnlyList<string> ToWireNames(ExportRequestFilters allowed) =>
        _all
            .Where(entry => allowed.HasFlag(entry.Filter))
            .Select(entry => entry.Name)
            .ToList();
}
