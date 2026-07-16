namespace Endatix.Modules.Reporting.Contracts.Export;

/// <summary>
/// Maps canonical flattening keys to export-time column aliases.
/// Register implementations via DI as <see cref="IColumnAliasTransformer"/>;
/// they are collected by <see cref="IColumnAliasTransformerRegistry"/>.
/// Catalog metadata (<see cref="WireKey"/>, <see cref="Label"/>, etc.) is exposed to Hub
/// via the export naming conventions API so UI copy stays with the strategy.
/// </summary>
public interface IColumnAliasTransformer
{
    ColumnAliasProfile Profile { get; }

    /// <summary>
    /// Stable wire value persisted in format settings and used by Hub (e.g. <c>native</c>).
    /// </summary>
    string WireKey { get; }

    /// <summary>
    /// Short display name for admin UI (e.g. "Survey keys").
    /// </summary>
    string Label { get; }

    /// <summary>
    /// One-line explanation shown in the column naming dropdown.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Optional sample export key(s) shown under the description.
    /// </summary>
    string? Example { get; }

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
