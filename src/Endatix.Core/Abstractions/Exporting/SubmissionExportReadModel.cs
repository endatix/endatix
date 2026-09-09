using Endatix.Core.Infrastructure.Paging;

namespace Endatix.Core.Abstractions.Exporting;

/// <summary>
/// Metadata keys used when streaming exports from the reporting read model.
/// </summary>
public static class SubmissionExportMetadataKeys
{
    public const string ColumnPlan = "SubmissionExportColumnPlan";

    public const string ExecutionSettings = "SubmissionExportExecutionSettings";

    public const string ResolvedFormatSettings = "SubmissionExportResolvedFormatSettings";
}

/// <summary>
/// Per-request export execution settings resolved from format definition and request overrides.
/// </summary>
public sealed record SubmissionExportExecutionSettings(
    long? ExportFormatId = null,
    string? SettingsJson = null,
    bool? IncludeTestSubmissions = null,
    IReadOnlyList<string>? ColumnScope = null,
    string? Locale = null,

    /// <summary>
    /// When true, export writers emit boolean values as category ids <c>0</c>/<c>1</c>
    /// (Crunch/Shoji). When false, CSV writes lowercase <c>true</c>/<c>false</c> and XLSX
    /// writes a native Excel boolean cell.
    /// </summary>
    bool EncodeBooleansAsCategoryIds = false,
    UtcDateTimeRange Created = default,
    UtcDateTimeRange Modified = default,
    UtcDateTimeRange Started = default,
    UtcDateTimeRange Completed = default,
    long? MinSubmissionId = null,
    long? MaxSubmissionId = null,
    bool? IsComplete = null);

/// <summary>
/// Serialized <see cref="SubmissionExportColumnPlanEntry.Source"/> values.
/// Must stay aligned with <c>ExportColumnSource</c> in Reporting.Contracts.
/// </summary>
public static class SubmissionExportColumnSources
{
    public const string System = nameof(System);
    public const string DataJson = nameof(DataJson);
}

/// <summary>
/// One column in a schema-driven submission export plan.
/// </summary>
public sealed record SubmissionExportColumnPlanEntry(
    string CanonicalKey,
    string ExportKey,
    string Source,
    string? HeaderLabel,
    string? DataType);

/// <summary>
/// Ordered export columns built once per export request.
/// </summary>
public sealed record SubmissionExportColumnPlan(
    IReadOnlyList<SubmissionExportColumnPlanEntry> Columns);
