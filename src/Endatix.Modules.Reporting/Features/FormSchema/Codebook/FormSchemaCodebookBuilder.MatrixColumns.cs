using System.Text.Json;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Codebook;

internal static partial class FormSchemaCodebookBuilder
{
    private static void WriteMatrixColumns(Utf8JsonWriter writer, JsonElement element)
    {
        writer.WriteStartArray(SurveyJsPropertyNames.Columns);
        foreach (var category in SurveyJsChoiceHelper.RadioMatrixCategories(element))
        {
            WriteMatrixCategory(writer, category);
        }

        writer.WriteEndArray();
    }

    private static void WriteMatrixCategory(Utf8JsonWriter writer, SurveyJsChoiceHelper.MatrixCategory column)
    {
        writer.WriteStartObject();
        if (column.IsNumeric)
        {
            writer.WriteBoolean(FormSchemaCodebookPropertyNames.NUMERIC_CODE, true);
        }

        writer.WriteString(SurveyJsPropertyNames.Value, column.Value);
        writer.WriteNumber(FormSchemaCodebookPropertyNames.Id, column.Id);
        writer.WritePropertyName(SurveyJsPropertyNames.Text);
        var labels = SurveyJsLocalizationHelper.ReadLocalizedStrings(column.Element, SurveyJsPropertyNames.Text);
        WriteLocalizedStringsWithFallback(writer, labels, column.Label);
        writer.WriteEndObject();
    }
}
