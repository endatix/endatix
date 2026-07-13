namespace Endatix.Modules.Reporting.Contracts.Export;

/// <summary>
/// Source of an export column.
/// </summary>
public enum ExportColumnSource
{
    System,
    DataJson,
}

/// <summary>
/// One export column in schema-driven submission export.
/// </summary>
public sealed record ExportColumnDefinition(
    string CanonicalKey,
    string ExportKey,
    ExportColumnSource Source,
    string? HeaderLabel = null,
    string? DataType = null);

/// <summary>
/// Ordered export columns built once per export request from form schema.
/// </summary>
public interface IExportColumnPlan
{
    IReadOnlyList<ExportColumnDefinition> Columns { get; }
}
