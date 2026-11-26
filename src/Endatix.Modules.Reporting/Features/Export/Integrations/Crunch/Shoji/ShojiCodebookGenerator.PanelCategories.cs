using System.Text.Json;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.Export.Integrations.Crunch.Shoji;

internal static partial class ShojiCodebookGenerator
{
    internal sealed record Dataset(string KeySeparator, string Locale, string? Name, string? Description,
        Contracts.Export.SpecialChoiceCodes? Codes = null, ShojiPanelAliases? PanelAliases = null);

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
            if (args.Format.PanelAliases is { } aliases && args.Catalog.OtherTextKeys.ContainsKey(column.Key))
            {
                WritePanelText(writer, (aliases.Native.CompanionAlias(column.Key), name), tracking);
            }
        }
    }

    private static void WritePanelText(Utf8JsonWriter writer, (string Alias, string Name) variable, ShojiVariableTracking tracking)
    {
        var (alias, name) = variable;
        if (TryTrackVariable(tracking.WrittenVariables, tracking.OrderAliases, alias))
        {
            writer.WritePropertyName(alias);
            writer.WriteStartObject();
            writer.WriteString(ShojiCodebookPropertyNames.Type, ShojiCodebookPropertyNames.VariableTypeText);
            writer.WriteString(ShojiCodebookPropertyNames.Alias, alias);
            WriteUniqueName(writer, AppendOtherSuffix(name), alias, tracking.UsedDisplayNames);
            writer.WriteEndObject();
        }
    }
}
