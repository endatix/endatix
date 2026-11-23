using System.Text.Json;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.Export.Integrations.Crunch.Shoji;

internal static partial class ShojiCodebookGenerator
{
    internal sealed record Dataset(string KeySeparator, string Locale, string? Name, string? Description,
        Contracts.Export.SpecialChoiceCodes? Codes = null);

    private static void WritePanelCategories(Utf8JsonWriter writer, ShojiCodebookWriteArgs args, ShojiVariableTracking tracking)
    {
        foreach (var column in args.FlatteningMap.Columns.Where(args.Catalog.IsCodedPanel))
        {
            var alias = ExportKeyTransformer.Transform(column.Key, args.KeySeparator);
            if (!TryTrackVariable(tracking.WrittenVariables, tracking.OrderAliases, alias))
            {
                continue;
            }

            var question = args.Questions[column.SourceQuestion!];
            args.CodebookColumns.TryGetValue(column.Key, out var metadata);
            var name = BuildLeafDisplayName(alias, column, question, metadata, args.KeySeparator);
            WriteCategoricalVariable(writer, alias, alias, question, name, tracking.UsedDisplayNames);
        }
    }
}
