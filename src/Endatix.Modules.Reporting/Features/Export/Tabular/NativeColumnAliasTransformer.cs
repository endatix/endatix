using Endatix.Modules.Reporting.Contracts.Export;

namespace Endatix.Modules.Reporting.Features.Export.Tabular;

/// <summary>
/// Identity alias transform — export keys match canonical keys.
/// </summary>
internal sealed class NativeColumnAliasTransformer : IColumnAliasTransformer
{
    internal static readonly NativeColumnAliasTransformer _instance = new();

    private NativeColumnAliasTransformer()
    {
    }

    public ColumnAliasProfile Profile => ColumnAliasProfile.Native;

    public string WireKey => ColumnAliasProfileWire.ToWireValue(Profile);

    public string Label => "Survey keys";

    public string Description =>
        "Use canonical column keys from the compiled form schema.";

    public string? Example => "question__choice";

    public IReadOnlyDictionary<string, string> BuildExportKeys(IReadOnlyList<ExportColumnAliasInput> columns) =>
        columns.ToDictionary(
            column => column.CanonicalKey,
            column => column.CanonicalKey,
            StringComparer.Ordinal);
}
