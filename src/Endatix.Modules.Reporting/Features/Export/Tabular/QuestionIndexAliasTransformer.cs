using Endatix.Core.Entities;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.Export.Tabular;

/// <summary>
/// Sequential question-index alias transform — Q1, Q1_1, Q2-style headers grouped by source question.
/// </summary>
internal sealed class QuestionIndexAliasTransformer : IColumnAliasTransformer
{
    internal static readonly QuestionIndexAliasTransformer _instance = new();

    private QuestionIndexAliasTransformer()
    {
    }

    public ColumnAliasProfile Profile => ColumnAliasProfile.Crunch;

    public string WireKey => ColumnAliasProfileWire.ToWireValue(Profile);

    public string Label => "Question index";

    public string Description =>
        "Sequential Q1, Q1_1, Q2-style headers grouped by source question.";

    public string? Example => "Q1, Q1_1, Q2";

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
                => column.SourceQuestion is null
                    ? column.CanonicalKey
                    : $"{column.SourceQuestion}__{column.MatrixRowValue}",
            FormSchemaColumnKind.MultipleTextItem
                => column.SourceQuestion ?? column.CanonicalKey,
            FormSchemaColumnKind.PanelDynamicIndex or FormSchemaColumnKind.NestedLoop or FormSchemaColumnKind.LoopSource
                => RemoveLastSegment(column.CanonicalKey),
            _ => column.SourceQuestion ?? column.CanonicalKey,
        };
    }

    private static string RemoveLastSegment(string key) =>
        ExportKeyTransformer.RemoveLastSegment(key);
}
