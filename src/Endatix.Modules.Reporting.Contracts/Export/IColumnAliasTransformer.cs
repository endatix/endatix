namespace Endatix.Modules.Reporting.Contracts.Export;

/// <summary>
/// Maps canonical flattening keys to export-time column aliases.
/// </summary>
public interface IColumnAliasTransformer
{
    ColumnAliasProfile Profile { get; }

    /// <summary>
    /// Returns export keys keyed by canonical column key.
    /// System columns are included and typically map to themselves.
    /// </summary>
    IReadOnlyDictionary<string, string> BuildExportKeys(IReadOnlyList<ExportColumnAliasInput> columns);
}

/// <summary>
/// One column participating in alias assignment.
/// </summary>
public sealed record ExportColumnAliasInput(
    string CanonicalKey,
    string? SourceQuestion,
    string? ChoiceValue,
    string? MatrixRowValue,
    string ColumnKind);
