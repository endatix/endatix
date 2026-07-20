using System.Text.Json.Serialization;

namespace Endatix.Modules.Reporting.Contracts.Export;

/// <summary>
/// Request-time export filters that a capability may accept.
/// Extensible per export type — add flags as new filters are introduced.
/// </summary>
[Flags]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExportRequestFilterKind
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
/// Common <see cref="ExportRequestFilterKind"/> sets for built-in capabilities.
/// </summary>
public static class ExportRequestFilterSets
{
    public const ExportRequestFilterKind Submissions =
        ExportRequestFilterKind.IncludeTestSubmissions |
        ExportRequestFilterKind.CreatedAtRange |
        ExportRequestFilterKind.CompletedAtRange |
        ExportRequestFilterKind.SubmissionIdRange |
        ExportRequestFilterKind.Locale |
        ExportRequestFilterKind.ColumnScope;

    /// <summary>
    /// Native codebook streams persisted FormSchema codebook JSON as-is (no request filters applied).
    /// </summary>
    public const ExportRequestFilterKind NativeCodebook = ExportRequestFilterKind.None;

    /// <summary>
    /// Shoji codebook applies request locale to display strings; columnScope is not applied.
    /// </summary>
    public const ExportRequestFilterKind ShojiCodebook = ExportRequestFilterKind.Locale;
}

/// <summary>
/// Wire names and helpers for <see cref="ExportRequestFilterKind"/>.
/// </summary>
public static class ExportRequestFilterWireNames
{
    public const string IncludeTestSubmissions = "includeTestSubmissions";
    public const string CreatedAtRange = "createdAtRange";
    public const string CompletedAtRange = "completedAtRange";
    public const string SubmissionIdRange = "submissionIdRange";
    public const string Locale = "locale";
    public const string ColumnScope = "columnScope";

    private static readonly (ExportRequestFilterKind Kind, string Name)[] _all =
    [
        (ExportRequestFilterKind.IncludeTestSubmissions, IncludeTestSubmissions),
        (ExportRequestFilterKind.CreatedAtRange, CreatedAtRange),
        (ExportRequestFilterKind.CompletedAtRange, CompletedAtRange),
        (ExportRequestFilterKind.SubmissionIdRange, SubmissionIdRange),
        (ExportRequestFilterKind.Locale, Locale),
        (ExportRequestFilterKind.ColumnScope, ColumnScope),
    ];

    public static IReadOnlyList<string> ToWireNames(ExportRequestFilterKind allowed) =>
        _all
            .Where(entry => allowed.HasFlag(entry.Kind))
            .Select(entry => entry.Name)
            .ToList();
}
