namespace Endatix.Core.Abstractions.Exporting;

/// <summary>
/// Metadata keys used when streaming exports from the reporting read model.
/// </summary>
public static class SubmissionExportMetadataKeys
{
    public const string ColumnPlan = "SubmissionExportColumnPlan";
}

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
