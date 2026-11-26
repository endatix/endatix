using System.Text.Json;
using Endatix.Modules.Reporting.Domain.SurveyJs;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Codebook;

internal static partial class FormSchemaCodebookBuilder
{
    private static bool HasCurrentQuestion(FormSchemaColumn column, IReadOnlyDictionary<string, CollectedQuestion> questions) =>
        TryGetQuestionElement(column, questions, out var question) && !IsHistoricalMatrixColumn(column, question);

    private static bool IsHistoricalMatrixColumn(FormSchemaColumn column, JsonElement current) =>
        column.Kind switch
        {
            FormSchemaColumnKind.MatrixRow => FormSchemaCodebookExportShape.FromQuestionElement(current) != FormSchemaCodebookExportShape.CategoricalArray,
            FormSchemaColumnKind.MatrixCell => FormSchemaCodebookExportShape.FromQuestionElement(current) == FormSchemaCodebookExportShape.CategoricalArray,
            _ => false,
        };

    private static void WriteMatrixColumns(Utf8JsonWriter writer, JsonElement element)
    {
        writer.WriteStartArray(SurveyJsPropertyNames.Columns);
        foreach (var category in DistinctMatrixCategories(element))
        {
            WriteMatrixCategory(writer, category);
        }

        writer.WriteEndArray();
    }

    private static List<SurveyJsChoiceHelper.MatrixCategory> DistinctMatrixCategories(JsonElement element)
    {
        var categories = SurveyJsChoiceHelper.RadioMatrixCategories(element).ToList();
        if (categories.Any(c => c.IsNumeric) && categories.DistinctBy(Identity).Count() != categories.Count)
        {
            throw new PublishedMatrixCategories(element.GetSurveyJsName()!).Conflict();
        }

        return categories;
    }

    internal static void ValidateSubmittedMatrices(JsonElement definition)
    {
        foreach (var question in CollectQuestionElements(definition).Values.Where(q => IsRadioMatrix(q.Element)))
        {
            DistinctMatrixCategories(question.Element);
        }
    }

    private static bool IsRadioMatrix(JsonElement element) => SurveyJsElementType.Matrix.Matches(element.GetSurveyJsType()) &&
        !FormSchemaCodebookExportShape.HasMatrixCheckboxCells(element);

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
