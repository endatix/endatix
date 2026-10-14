using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.Personalization;

namespace Endatix.Modules.Reporting.Features.Export.Integrations.Crunch.Shoji;

/// <summary>Shoji variables for the audience values frozen on each response.</summary>
internal static partial class ShojiCodebookGenerator
{
    private const string PersonalizationDescription = "Audience value frozen when the response was opened.";

    private sealed record ShojiVariableTracking(
        HashSet<string> WrittenVariables,
        HashSet<string> UsedDisplayNames,
        List<string> OrderAliases);

    /// <summary>The Shoji type of an audience data type, and the resolution of a datetime.</summary>
    private sealed record PersonalizationShape(string Type, string? Resolution = null)
    {
        private const string DayResolution = "D";

        // A choice list has no category catalog in the schema yet, so it ships as text, like a calculated value.
        public static PersonalizationShape Of(string dataType) =>
            dataType switch
            {
                PersonalizationColumns.NumberType => new(ShojiCodebookPropertyNames.VariableTypeNumeric),
                PersonalizationColumns.BooleanType => new(ShojiCodebookPropertyNames.VariableTypeCategorical),
                PersonalizationColumns.DateType => new(ShojiCodebookPropertyNames.VariableTypeDatetime, DayResolution),
                PersonalizationColumns.DateTimeType => new(
                    ShojiCodebookPropertyNames.VariableTypeDatetime,
                    ShojiCodebookPropertyNames.DatetimeResolutionSeconds),
                _ => new(ShojiCodebookPropertyNames.VariableTypeText),
            };
    }

    // The alias is the column key as stored, the same text as the CSV header: the export plan never runs these keys
    // through an alias profile or key separator, and Crunch matches CSV columns to variables by alias.
    private static IEnumerable<string> PersonalizationAliases(MergedFormSchema flatteningMap) =>
        PersonalizationColumns.InBlockOrder(flatteningMap.Columns).Select(column => column.Key);

    private static void WritePersonalizationVariables(
        Utf8JsonWriter writer,
        ShojiCodebookWriteArgs args,
        ShojiVariableTracking tracking)
    {
        foreach (var column in PersonalizationColumns.InBlockOrder(args.FlatteningMap.Columns))
        {
            if (!TryTrackVariable(tracking.WrittenVariables, tracking.OrderAliases, column.Key))
            {
                continue;
            }

            args.CodebookColumns.TryGetValue(column.Key, out var columnMetadata);
            WritePersonalizationVariable(
                writer,
                new PersonalizationVariable(column, ReadColumnTitle(columnMetadata, column.Label)),
                tracking.UsedDisplayNames);
        }
    }

    private sealed record PersonalizationVariable(FormSchemaColumn Column, string DisplayName);

    private static void WritePersonalizationVariable(
        Utf8JsonWriter writer,
        PersonalizationVariable variable,
        HashSet<string> usedDisplayNames)
    {
        var alias = variable.Column.Key;
        var shape = PersonalizationShape.Of(variable.Column.DataType);
        writer.WritePropertyName(alias);
        writer.WriteStartObject();
        writer.WriteString(ShojiCodebookPropertyNames.Type, shape.Type);
        writer.WriteString(ShojiCodebookPropertyNames.Alias, alias);
        WriteUniqueName(writer, variable.DisplayName, alias, usedDisplayNames);
        writer.WriteString(ShojiCodebookPropertyNames.Description, PersonalizationDescription);
        WritePersonalizationShapeDetails(writer, shape);
        writer.WriteEndObject();
    }

    private static void WritePersonalizationShapeDetails(Utf8JsonWriter writer, PersonalizationShape shape)
    {
        if (shape.Resolution is not null)
        {
            writer.WriteString(ShojiCodebookPropertyNames.Resolution, shape.Resolution);
        }

        if (shape.Type == ShojiCodebookPropertyNames.VariableTypeCategorical)
        {
            WriteCategories(writer, _booleanCategories);
        }
    }
}
