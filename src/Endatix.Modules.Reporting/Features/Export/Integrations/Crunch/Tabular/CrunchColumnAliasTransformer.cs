using Endatix.Core.Entities;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

namespace Endatix.Modules.Reporting.Features.Export.Integrations.Crunch.Tabular;

/// <summary>
/// Crunch-safe alias transform — sequential Q-number groups with per-member suffixes.
/// </summary>
internal sealed class CrunchColumnAliasTransformer : IColumnAliasTransformer
{
    internal static readonly CrunchColumnAliasTransformer _instance = new();

    private CrunchColumnAliasTransformer()
    {
    }

    public ColumnAliasProfile Profile => ColumnAliasProfile.Crunch;

    public IReadOnlyDictionary<string, string> BuildExportKeys(IReadOnlyList<ExportColumnAliasInput> columns)
    {
        Dictionary<string, string> exportKeys = new(StringComparer.Ordinal);
        Dictionary<string, List<ExportColumnAliasInput>> grouped = new(StringComparer.Ordinal);
        List<string> groupOrder = [];

        foreach (var column in columns)
        {
            if (SubmissionExportRow.SystemColumns.Contains(column.CanonicalKey))
            {
                exportKeys[column.CanonicalKey] = column.CanonicalKey;
                continue;
            }

            var groupKey = ResolveAliasGroupKey(column);
            if (!grouped.TryGetValue(groupKey, out var members))
            {
                members = [];
                grouped[groupKey] = members;
                groupOrder.Add(groupKey);
            }

            members.Add(column);
        }

        var questionNumber = 1;
        foreach (var groupKey in groupOrder)
        {
            var members = grouped[groupKey];
            if (members.Count == 1)
            {
                exportKeys[members[0].CanonicalKey] = $"Q{questionNumber}";
            }
            else
            {
                for (var memberIndex = 0; memberIndex < members.Count; memberIndex++)
                {
                    exportKeys[members[memberIndex].CanonicalKey] = $"Q{questionNumber}_{memberIndex + 1}";
                }
            }

            questionNumber++;
        }

        return exportKeys;
    }

    internal static string ResolveAliasGroupKey(ExportColumnAliasInput column)
    {
        if (!Enum.TryParse(column.ColumnKind, ignoreCase: true, out FormSchemaColumnKind kind))
        {
            return column.SourceQuestion ?? column.CanonicalKey;
        }

        return kind switch
        {
            FormSchemaColumnKind.ChoiceIndicator or FormSchemaColumnKind.RankingChoice or FormSchemaColumnKind.CheckboxOtherText
                => RemoveLastSegment(column.CanonicalKey),
            FormSchemaColumnKind.MatrixRow
                => column.SourceQuestion ?? column.CanonicalKey,
            FormSchemaColumnKind.MatrixCell
                => $"{column.SourceQuestion}__{column.MatrixRowValue}",
            FormSchemaColumnKind.MultipleTextItem
                => column.SourceQuestion ?? column.CanonicalKey,
            FormSchemaColumnKind.PanelDynamicIndex or FormSchemaColumnKind.NestedLoop or FormSchemaColumnKind.LoopSource
                => RemoveLastSegment(column.CanonicalKey),
            _ => column.SourceQuestion ?? column.CanonicalKey,
        };
    }

    private static string RemoveLastSegment(string key)
    {
        var separatorIndex = key.LastIndexOf("__", StringComparison.Ordinal);
        return separatorIndex < 0 ? key : key[..separatorIndex];
    }
}
