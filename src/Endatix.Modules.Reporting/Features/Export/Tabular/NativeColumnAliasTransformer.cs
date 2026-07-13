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

    public IReadOnlyDictionary<string, string> BuildExportKeys(IReadOnlyList<ExportColumnAliasInput> columns)
    {
        Dictionary<string, string> exportKeys = new(StringComparer.Ordinal);
        foreach (var column in columns)
        {
            exportKeys[column.CanonicalKey] = column.CanonicalKey;
        }

        return exportKeys;
    }
}
