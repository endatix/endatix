using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.Choices;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Codebook;

/// <summary>The choice catalog of a base select: its select fields and kind-tagged choices (<see cref="ChoiceSet"/>).</summary>
internal static partial class FormSchemaCodebookBuilder
{
    private static JsonElement BuildChoiceCatalog(JsonElement element, SurveyChoiceDefaults surveyDefaults) =>
        ChoiceSet.Read(element, surveyDefaults) is { } choiceSet
            ? WriteQuestionEntry(writer =>
                choiceSet.WriteCatalog(writer, (item, id) => WriteChoice(writer, element, item, id)))
            : WriteQuestionEntry(writer =>
            {
                writer.WritePropertyName(SurveyJsPropertyNames.Choices);
                writer.WriteStartArray();
                WriteCatalogChoices(writer, element);
                writer.WriteEndArray();
            });

    private static void WriteSelectFields(Utf8JsonWriter writer, JsonElement catalog)
    {
        foreach (var property in catalog.EnumerateObject().Where(property =>
                     property.NameEquals(FormSchemaCodebookPropertyNames.Multiple) ||
                     property.NameEquals(FormSchemaCodebookPropertyNames.OtherStorage)))
        {
            property.WriteTo(writer);
        }
    }

    // An inline choice keeps reading its label by value, as before choice kinds; a special item reads the question's
    // text property (otherText, noneText, refuseText, dontKnowText).
    private static void WriteChoice(Utf8JsonWriter writer, JsonElement question, ChoiceSetItem item, int id)
    {
        writer.WriteStartObject();
        writer.WriteString(SurveyJsPropertyNames.Value, item.Value);
        writer.WriteNumber(FormSchemaCodebookPropertyNames.Id, id);
        WriteChoiceKind(writer, item);
        writer.WritePropertyName(SurveyJsPropertyNames.Text);
        var label = item.Kind == ChoiceKind.Choice
            ? FindChoiceElement(question, item.Value)
            : item.LabelSource;
        WriteLocalizedStringsWithFallback(
            writer,
            SurveyJsLocalizationHelper.ReadLocalizedStrings(label, item.LabelProperty),
            item.FallbackLabel);
        writer.WriteEndObject();
    }

    private static void WriteChoiceKind(Utf8JsonWriter writer, ChoiceSetItem item)
    {
        if (item.Kind.JsonName() is { } kind)
        {
            writer.WriteString(FormSchemaCodebookPropertyNames.Kind, kind);
        }

        if (item.Exclusive)
        {
            writer.WriteBoolean(FormSchemaCodebookPropertyNames.Exclusive, true);
        }
    }
}
