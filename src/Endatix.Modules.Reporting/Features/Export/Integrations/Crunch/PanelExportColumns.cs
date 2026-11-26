using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.Export.Integrations.Crunch.Shoji;

namespace Endatix.Modules.Reporting.Features.Export.Integrations.Crunch;

/// <summary>Allocate panel companions after ordinary output aliases are known, before narrowing to a column scope.</summary>
internal sealed class PanelExportColumns
{
    private readonly Dictionary<string, ExportColumnDefinition> _ordinary;
    private readonly Dictionary<string, ExportColumnDefinition> _companions = new(StringComparer.Ordinal);

    public PanelExportColumns(ExportColumnPlan full, CrunchQuestions catalog, string separator)
    {
        _ordinary = full.Columns.ToDictionary(c => c.CanonicalKey, StringComparer.Ordinal);
        var used = full.Columns.Select(c => c.ExportKey).ToHashSet(StringComparer.Ordinal);
        if (catalog.Map is { } map)
        {
            // A grouped parent has no CSV header of its own, but a companion that takes its name drops it from the codebook.
            used.UnionWith(ShojiCodebookGenerator.VariableAliases(map, catalog.Entries, separator));
        }
        foreach (var (parentKey, textKey) in catalog.OtherTextKeys)
        {
            var parent = _ordinary[parentKey];
            var alias = CrunchQuestions.FreeKey(parent.ExportKey + separator + "other_text", used);
            _companions.Add(parentKey, new(textKey, alias, ExportColumnSource.DataJson, parent.HeaderLabel + " -- Other", "string"));
        }
    }

    public string CompanionAlias(string parentKey) => _companions[parentKey].ExportKey;

    /// <summary>The companion's name: its parent's export name and <c>other_text</c>, never suffixed.</summary>
    public static string CompanionName(string parentExportKey, string separator) => parentExportKey + separator + "other_text";

    private IEnumerable<ExportColumnDefinition> All => _ordinary.Values.Concat(_companions.Values);

    public Dictionary<string, string> AliasesFrom(PanelExportColumns native)
    {
        var target = All.ToDictionary(c => c.CanonicalKey, c => c.ExportKey, StringComparer.Ordinal);
        return native.All.Where(c => target.ContainsKey(c.CanonicalKey))
            .ToDictionary(c => c.ExportKey, c => target[c.CanonicalKey], StringComparer.Ordinal);
    }

    public ExportColumnPlan WithCompanions(ExportColumnPlan scoped)
    {
        var parents = scoped.Columns.Select(c => c.CanonicalKey).ToArray();
        var columns = parents.Select(key => _ordinary[key])
            .Concat(parents.Where(_companions.ContainsKey).Select(key => _companions[key])).ToList();
        return new(columns, scoped.OmittedKeys);
    }
}
