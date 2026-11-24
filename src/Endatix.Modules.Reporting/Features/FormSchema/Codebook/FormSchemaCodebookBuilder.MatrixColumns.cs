using System.Text.Json;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Codebook;

internal static partial class FormSchemaCodebookBuilder
{
    private static void WriteMatrixColumns(Utf8JsonWriter writer, JsonElement element)
    {
        writer.WriteStartArray(SurveyJsPropertyNames.Columns);
        var categories = SurveyJsChoiceHelper.RadioMatrixCategories(element).ToList();
        if (categories.Any(c => c.IsNumeric) && categories.DistinctBy(Identity).Count() != categories.Count)
        {
            throw new PublishedMatrixCategories(element.GetSurveyJsName()!).Conflict();
        }

        foreach (var category in categories)
        {
            WriteMatrixCategory(writer, category);
        }

        writer.WriteEndArray();
    }

    private static MatrixCode Identity(SurveyJsChoiceHelper.MatrixCategory category) =>
        category.IsNumeric ? MatrixCode.Number(category.Value) : MatrixCode.Text(category.Value);

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
