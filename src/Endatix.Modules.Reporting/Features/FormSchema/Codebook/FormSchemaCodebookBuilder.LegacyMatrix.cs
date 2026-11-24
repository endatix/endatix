using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.Choices;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Codebook;

internal static partial class FormSchemaCodebookBuilder
{
    internal static JsonElement? ReplayLegacyMatrix(string definition, string name, JsonElement stored)
    {
        if (HistoricalRadio(definition, name) is not { } element)
        {
            return null;
        }

        var replayed = WriteQuestionEntry(w => WriteLegacyColumns(w, element)).GetProperty(SurveyJsPropertyNames.Columns);
        if (!JsonElement.DeepEquals(stored, replayed))
        {
            return null;
        }

        var typed = WriteQuestionEntry(w => WriteMatrixColumns(w, element)).GetProperty(SurveyJsPropertyNames.Columns);
        // Primitive numbers and valueless localized labels omitted by the old writer never published an ID.
        return JsonSerializer.SerializeToElement(typed.EnumerateArray()
            .Where(c => c.GetProperty(FormSchemaCodebookPropertyNames.Id).GetInt32() <= stored.GetArrayLength()));
    }

    private static JsonElement? HistoricalRadio(string definition, string name)
    {
        using var doc = FormSchemaCompiler.ReadHistoricalDefinition(definition);
        if (doc?.RootElement is not { ValueKind: JsonValueKind.Object } root || CopyDefinition.Of(root).SharedNames.Contains(name))
        {
            throw new LegacyMatrixSourceRequiredException(name);
        }

        return CollectQuestionElements(root).TryGetValue(name, out var question) && IsRadioMatrix(question.Element)
            ? question.Element.Clone() : null;
    }

    internal static JsonElement HistoricalStringMatrix(string definition, string name)
    {
        if (HistoricalRadio(definition, name) is not { } matrix || !matrix.TryGetColumns(out var columns))
        {
            throw new LegacyMatrixSourceRequiredException(name);
        }

        var categories = SurveyJsChoiceHelper.RadioMatrixCategories(matrix).ToList();
        var values = categories.Select(c => c.Value).ToList();
        if (categories.Exists(c => !IsLiteralString(c)) || values.Distinct(StringComparer.Ordinal).Count() != columns.GetArrayLength())
        {
            throw new LegacyMatrixSourceRequiredException(name);
        }

        return WriteQuestionEntry(writer =>
        {
            writer.WriteString(FormSchemaCodebookPropertyNames.ExportShape, FormSchemaCodebookExportShape.CategoricalArray.Name);
            WriteMatrixColumns(writer, matrix);
        });
    }

    private static bool IsLiteralString(SurveyJsChoiceHelper.MatrixCategory category)
    {
        var column = category.Element;
        var literal = column.ValueKind == JsonValueKind.Object ? column.TryGetPropertyValue(SurveyJsPropertyNames.Value) : column;
        return literal is { ValueKind: JsonValueKind.String } text && text.GetString() == category.Value;
    }

    private static void WriteLegacyColumns(Utf8JsonWriter writer, JsonElement element)
    {
        writer.WriteStartArray(SurveyJsPropertyNames.Columns);
        var id = 1;
        foreach (var (value, text, column) in SurveyJsChoiceHelper.EnumerateMatrixColumns(element))
        {
            var labels = SurveyJsLocalizationHelper.ReadLocalizedStrings(column, SurveyJsPropertyNames.Text);
            WriteChoice(writer, value, id++, labels, text);
        }

        writer.WriteEndArray();
    }
}
