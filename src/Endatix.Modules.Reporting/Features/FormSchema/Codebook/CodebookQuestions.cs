using System.Text.Json;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Codebook;

/// <summary>
/// The codebook's question entries as an export reads them. A categorical question's <c>choices</c> are its merged
/// choice catalog, so a category id an export wrote once never moves when a later definition removes or adds a choice:
/// question entries describe the current definition only, the catalog the union of every one.
/// </summary>
internal static class CodebookQuestions
{
    public static Dictionary<string, JsonElement> Read(JsonElement codebook)
    {
        Dictionary<string, JsonElement> questions = new(StringComparer.Ordinal);
        if (codebook.ValueKind != JsonValueKind.Object ||
            !codebook.TryGetProperty(FormSchemaCodebookPropertyNames.Questions, out var entries) ||
            entries.ValueKind != JsonValueKind.Object)
        {
            return questions;
        }

        codebook.TryGetProperty(FormSchemaCodebookPropertyNames.ChoiceCatalogs, out var catalogs);
        foreach (var question in entries.EnumerateObject())
        {
            questions[question.Name] = TryGetCatalogChoices(question.Value, catalogs, question.Name, out var choices)
                ? WithChoices(question.Value, choices)
                : question.Value.Clone();
        }

        return questions;
    }

    private static bool TryGetCatalogChoices(
        JsonElement question,
        JsonElement catalogs,
        string questionName,
        out JsonElement choices)
    {
        choices = default;
        return question.GetStringProperty(FormSchemaCodebookPropertyNames.ExportShape) ==
               FormSchemaCodebookExportShape.Categorical.Name &&
               question.TryGetArrayProperty(SurveyJsPropertyNames.Choices, out _) &&
               catalogs.ValueKind == JsonValueKind.Object &&
               catalogs.TryGetProperty(questionName, out var catalog) &&
               catalog.TryGetArrayProperty(SurveyJsPropertyNames.Choices, out choices);
    }

    private static JsonElement WithChoices(JsonElement question, JsonElement choices)
    {
        System.Buffers.ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            foreach (var property in question.EnumerateObject())
            {
                if (property.NameEquals(SurveyJsPropertyNames.Choices))
                {
                    writer.WritePropertyName(SurveyJsPropertyNames.Choices);
                    choices.WriteTo(writer);
                    continue;
                }

                property.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }
}
