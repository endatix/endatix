using System.Text.Json;
using System.Text.Json.Nodes;
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

        var catalogs = codebook.TryGetPropertyValue(FormSchemaCodebookPropertyNames.ChoiceCatalogs);
        foreach (var question in entries.EnumerateObject())
        {
            questions[question.Name] = CatalogChoices(question, catalogs) is { } choices
                ? WithChoices(question.Value, choices)
                : question.Value.Clone();
        }

        return questions;
    }

    private static JsonElement? CatalogChoices(JsonProperty question, JsonElement? catalogs) =>
        question.Value.GetStringProperty(FormSchemaCodebookPropertyNames.ExportShape) ==
        FormSchemaCodebookExportShape.Categorical.Name &&
        question.Value.TryGetArrayProperty(SurveyJsPropertyNames.Choices, out _) &&
        catalogs?.TryGetPropertyValue(question.Name) is { } catalog &&
        catalog.TryGetArrayProperty(SurveyJsPropertyNames.Choices, out var choices)
            ? choices
            : null;

    private static JsonElement WithChoices(JsonElement question, JsonElement choices)
    {
        var entry = JsonNode.Parse(question.GetRawText())!.AsObject();
        entry[SurveyJsPropertyNames.Choices] = JsonNode.Parse(choices.GetRawText());
        return JsonSerializer.SerializeToElement(entry);
    }
}
