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

    // multiple follows the current definition. Value storage is sticky: rows saved under it keep typed Other text in
    // the value, so a later comment-storage definition must not make the flatten forget it.
    private static void WriteSelectFields(Utf8JsonWriter writer, JsonElement existingCatalog, JsonElement currentCatalog)
    {
        if (currentCatalog.TryGetProperty(FormSchemaCodebookPropertyNames.Multiple, out var multiple))
        {
            writer.WriteBoolean(FormSchemaCodebookPropertyNames.Multiple, multiple.GetBoolean());
        }

        var storage = OtherStorageOf(existingCatalog) == OtherStorage.Value
            ? OtherStorage.Value
            : OtherStorageOf(currentCatalog);
        if (storage is { } stored)
        {
            writer.WriteString(FormSchemaCodebookPropertyNames.OtherStorage, ChoiceSet.ToJsonName(stored));
        }
    }

    private static OtherStorage? OtherStorageOf(JsonElement catalog) =>
        ChoiceSet.ParseOtherStorage(catalog.GetStringProperty(FormSchemaCodebookPropertyNames.OtherStorage));

    // A name the current definition gives to a question that is not a select (a dropdown turned text) keeps its choice
    // ids, but its answers are no longer read as a select's. A select that was removed keeps its fields: old rows hold it.
    private static void DropStaleSelectFields(
        Dictionary<string, JsonElement> choiceCatalogs,
        IReadOnlyDictionary<string, CollectedQuestion> questionElements)
    {
        foreach (var (name, question) in questionElements)
        {
            if (ChoiceSet.Read(question.Element) is null &&
                choiceCatalogs.TryGetValue(name, out var catalog) &&
                catalog.TryGetProperty(FormSchemaCodebookPropertyNames.Multiple, out _))
            {
                choiceCatalogs[name] = WriteQuestionEntry(writer =>
                {
                    foreach (var property in catalog.EnumerateObject().Where(property =>
                                 !property.NameEquals(FormSchemaCodebookPropertyNames.Multiple) &&
                                 !property.NameEquals(FormSchemaCodebookPropertyNames.OtherStorage)))
                    {
                        property.WriteTo(writer);
                    }
                });
            }
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
