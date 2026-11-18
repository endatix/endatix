using System.Text.Json;
using Endatix.Modules.Reporting.Domain.SurveyJs;
using Endatix.Modules.Reporting.Features.FormSchema.Choices;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Codebook;

/// <summary>The choice catalog of a base select: its select fields and kind-tagged choices (<see cref="ChoiceSet"/>).</summary>
internal static partial class FormSchemaCodebookBuilder
{
    // With the definition, a copy's catalog can record its shown rule (CopyDefinition); column labels do not need it.
    private static ChoiceSources ChoiceSourcesIn(
        IReadOnlyDictionary<string, CollectedQuestion> questionElements,
        JsonElement? definition = null) =>
        new(
            questionElements.ToDictionary(entry => entry.Key, entry => entry.Value.Element, StringComparer.Ordinal),
            definition is { } whole ? CopyDefinition.Of(whole) : null);

    private static JsonElement BuildChoiceCatalog(
        JsonElement element,
        SurveyChoiceDefaults surveyDefaults,
        ChoiceSources choiceSources)
    {
        if (ChoiceSet.Read(element, surveyDefaults, choiceSources.Resolve(element)) is { } choiceSet)
        {
            return WriteQuestionEntry(writer =>
            {
                choiceSet.WriteCatalog(writer, (item, id) => WriteChoice(writer, element, item, id));
                choiceSources.WriteCopyFields(writer, element);
            });
        }

        return WriteQuestionEntry(writer => WriteNonSelectCatalogChoices(writer, element, choiceSources));
    }

    private static void WriteNonSelectCatalogChoices(Utf8JsonWriter writer, JsonElement element, ChoiceSources choiceSources)
    {
        if (SurveyJsElementType.Ranking.Matches(element.GetSurveyJsType()))
        {
            WriteRankingChoices(writer, element, choiceSources.Offered(element));
            choiceSources.WriteCopyFields(writer, element);
            return;
        }

        writer.WritePropertyName(SurveyJsPropertyNames.Choices);
        writer.WriteStartArray();
        WriteCatalogChoices(writer, element);
        writer.WriteEndArray();
    }

    // A select whose choices cannot be read is marked degraded: a single select stays categorical and a multi-select
    // has no indicator columns, and exports read either as text.
    private static JsonElement BuildSelectEntry(CollectedQuestion question, ResolvedChoices choices) =>
        WriteQuestionEntry(writer =>
        {
            writer.WriteString(FormSchemaCodebookPropertyNames.SurveyJsType, question.Element.GetSurveyJsType()!);
            WriteTitle(writer, question.Element);
            WriteDescription(writer, question.Element);
            question.Placement.WriteTo(writer);
            var shape = question.Element.IsMultiSelectBaseSelect()
                ? FormSchemaCodebookExportShape.MultipleResponse
                : FormSchemaCodebookExportShape.Categorical;
            writer.WriteString(FormSchemaCodebookPropertyNames.ExportShape, shape.Name);
            choices.WriteTo(writer);
            WriteChoices(writer, question.Element, choices);
        });

    // multiple follows the current definition. Value storage is sticky: rows saved under it keep typed Other text in
    // the value, so a later comment-storage definition must not make the flatten forget it. A copy's shown rule is kept
    // only while every definition agrees on it (CopyVisibility.Merge).
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

        CopyVisibility.Merge(writer, existingCatalog, currentCatalog);
    }

    // A codebook compiled before the copied marker still names the copy on its stored question entry, which the merge
    // replaces with the current one: carry it onto the catalog first.
    private static JsonElement MarkedIfCopied(JsonElement existingCatalog, JsonElement existingQuestions, string name) =>
        existingQuestions.ValueKind == JsonValueKind.Object &&
        existingQuestions.TryGetPropertyValue(name) is { } question &&
        ChoiceCatalogs.IsCopy(question)
            ? CopyVisibility.MarkCopied(existingCatalog)
            : existingCatalog;

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
                choiceCatalogs[name] = WithoutSelectFields(catalog);
            }
        }
    }

    private static JsonElement WithoutSelectFields(JsonElement catalog) =>
        WriteQuestionEntry(writer =>
        {
            foreach (var property in catalog.EnumerateObject().Where(property =>
                         !property.NameEquals(FormSchemaCodebookPropertyNames.Multiple) &&
                         !property.NameEquals(FormSchemaCodebookPropertyNames.OtherStorage) &&
                         !property.NameEquals(FormSchemaCodebookPropertyNames.Shown)))
            {
                property.WriteTo(writer);
            }
        });

    // An inline choice keeps reading its label by value, as before choice kinds; a copied choice reads it on its source
    // question's choice; a special item reads the question's text property (otherText, noneText, refuseText, dontKnowText).
    private static void WriteChoice(Utf8JsonWriter writer, JsonElement question, ChoiceSetItem item, int id)
    {
        writer.WriteStartObject();
        writer.WriteString(SurveyJsPropertyNames.Value, item.Value);
        writer.WriteNumber(FormSchemaCodebookPropertyNames.Id, id);
        WriteChoiceKind(writer, item);
        writer.WritePropertyName(SurveyJsPropertyNames.Text);
        var label = item is { Kind: ChoiceKind.Choice, Copied: false }
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
