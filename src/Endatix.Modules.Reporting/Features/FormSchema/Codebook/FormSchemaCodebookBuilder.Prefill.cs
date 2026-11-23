using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.Personalization;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Codebook;

internal static partial class FormSchemaCodebookBuilder
{
    internal const string PREFILLED_FROM = "prefilledFrom";

    private static JsonElement WithParticipantPrefill(JsonElement entry, JsonElement element, MergedFormSchema merged)
    {
        if (element.GetStringProperty("defaultValueExpression") is not ['{', .. var name, '}'] ||
            !merged.TryGetColumn(PersonalizationColumns.KeyOf(name), out var column) ||
            column.Kind is not FormSchemaColumnKind.Personalization)
        {
            return entry;
        }

        var question = JsonNode.Parse(entry.GetRawText())!.AsObject();
        question[PREFILLED_FROM] = name;
        return JsonSerializer.SerializeToElement(question);
    }
}
