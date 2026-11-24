using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.Choices;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Codebook;

internal static partial class FormSchemaCodebookBuilder
{
    internal static LegacyMatrixWitness ReplayLegacyMatrix(JsonElement matrix)
    {
        var replayed = WriteQuestionEntry(w => WriteLegacyColumns(w, matrix)).GetProperty(SurveyJsPropertyNames.Columns);
        var typed = WriteQuestionEntry(w => WriteMatrixColumns(w, matrix)).GetProperty(SurveyJsPropertyNames.Columns);
        // Primitive numbers and valueless localized labels omitted by the old writer never published an ID.
        var published = typed.EnumerateArray()
            .Where(c => c.GetProperty(FormSchemaCodebookPropertyNames.Id).GetInt32() <= replayed.GetArrayLength());
        return new(replayed, JsonSerializer.SerializeToElement(published));
    }

    internal static JsonElement? HistoricalRadio(string definition, string name)
    {
        using var doc = FormSchemaCompiler.ReadHistoricalDefinition(definition);
        if (doc?.RootElement is not { } root || !ReadableLayout(root, name) || CopyDefinition.Of(root).SharedNames.Contains(name))
        {
            throw new LegacyMatrixSourceRequiredException(name);
        }

        if (!CollectQuestionElements(root).TryGetValue(name, out var question))
        {
            return null;
        }

        return IsRadioMatrix(question.Element) && question.Element.TryGetColumns(out _)
            ? question.Element.Clone() : throw new LegacyMatrixSourceRequiredException(name);
    }

    private static readonly string[] _containers =
        [SurveyJsPropertyNames.Pages, SurveyJsPropertyNames.Elements, SurveyJsPropertyNames.TemplateElements, SurveyJsPropertyNames.DetailElements];

    // A broken container or a different question using the same answer key is not evidence of absence.
    private static bool ReadableLayout(JsonElement node, string name) =>
        node.ValueKind == JsonValueKind.Object &&
        (node.GetSurveyJsName() == name || node.GetSurveyJsValueName() != name) &&
        _containers.All(key => !node.TryGetProperty(key, out var children) ||
            (children.ValueKind == JsonValueKind.Array && children.EnumerateArray().All(child => ReadableLayout(child, name))));

    internal static JsonElement HistoricalStringMatrix(JsonElement matrix, string name)
    {
        var columns = matrix.GetProperty(SurveyJsPropertyNames.Columns);
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
