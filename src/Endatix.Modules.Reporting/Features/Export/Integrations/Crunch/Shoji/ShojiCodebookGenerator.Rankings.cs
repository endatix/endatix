using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.Export.Integrations.Crunch.Shoji;

internal static partial class ShojiCodebookGenerator
{
    private sealed record RankingArray(string Alias, string Title, JsonElement Question, IReadOnlyList<string> Keys);

    private static void WriteRankingArrays(Utf8JsonWriter writer, ShojiCodebookWriteArgs args, ShojiVariableTracking tracking)
    {
        foreach (var rank in RankingArrays(args))
        {
            if (!TryTrackVariable(tracking.WrittenVariables, tracking.OrderAliases, rank.Alias))
            {
                continue;
            }

            writer.WriteStartObject(rank.Alias);
            writer.WriteString(ShojiCodebookPropertyNames.Type, "numeric_array");
            writer.WriteString(ShojiCodebookPropertyNames.Alias, rank.Alias);
            WriteUniqueName(writer, rank.Title, rank.Alias, tracking.UsedDisplayNames);
            WriteQuestionDescription(writer, rank.Question);
            WriteSubvariablesForKeys(writer, rank.Keys, args.CodebookColumns, FormSchemaCodebookPropertyNames.ChoiceLabel, args.KeySeparator);
            writer.WriteEndObject();
            tracking.WrittenVariables.UnionWith(rank.Keys.Select(key => ExportKeyTransformer.Transform(key, args.KeySeparator)));
        }
    }

    private static IEnumerable<RankingArray> RankingArrays(ShojiCodebookWriteArgs args)
    {
        var columns = args.FlatteningMap.Columns;
        var csvAliases = columns.Select(c => ExportKeyTransformer.Transform(c.Key, args.KeySeparator)).ToHashSet(StringComparer.Ordinal);
        var ranks = columns.Where(c => c.Kind is FormSchemaColumnKind.RankingChoice)
            .GroupBy(c => RankingAlias(c, args.KeySeparator));
        foreach (var rank in ranks)
        {
            // A real CSV column (for example a calculated value) needs its own variable. Keep the rank leaves in that case.
            var first = rank.First();
            if (!csvAliases.Contains(rank.Key) && args.Questions.TryGetValue(first.SourceQuestion ?? string.Empty, out var question))
            {
                var title = first.LoopPath is null ? ReadQuestionTitle(question) :
                    BuildLoopDisplayName(rank.Key, first, question, args.Questions, default, args.KeySeparator);
                yield return new(rank.Key, title, question, [.. rank.Select(c => c.Key)]);
            }
        }
    }

    private static string RankingAlias(FormSchemaColumn column, string separator) =>
        ExportKeyTransformer.Transform(column.LoopPath is null && column.SourceQuestion is { } name
            ? name : ExportKeyTransformer.RemoveLastSegment(column.Key), separator);
}
