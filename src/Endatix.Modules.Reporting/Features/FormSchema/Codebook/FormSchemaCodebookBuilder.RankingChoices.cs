using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.Choices;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Codebook;

/// <summary>
/// The choices of a ranking: its own, or the ones it copies (<see cref="ChoiceSources.Offered"/>), whatever the copy's
/// mode, each copied choice labelled on its source question.
/// </summary>
internal static partial class FormSchemaCodebookBuilder
{
    private static void WriteRankingSourceAndChoices(Utf8JsonWriter writer, JsonElement ranking, ResolvedChoices offered)
    {
        offered.Source.WriteTo(writer);
        WriteRankingChoices(writer, ranking, offered);
    }

    // A ranking's own choices are written as before; copied ones with the source's labels, Other last when shown. A
    // copy whose source cannot be read has no columns, so it lists no choices: never its leftover inline ones.
    private static void WriteRankingChoices(Utf8JsonWriter writer, JsonElement ranking, ResolvedChoices offered)
    {
        writer.WritePropertyName(SurveyJsPropertyNames.Choices);
        writer.WriteStartArray();
        if (offered.IsCopied)
        {
            WriteCopiedChoices(writer, offered.Choices);
            WriteOtherChoice(writer, ranking, offered.Choices.Count + 1);
        }
        else if (!offered.IsDegraded)
        {
            WriteCatalogChoices(writer, ranking);
        }

        writer.WriteEndArray();
    }

    private static void WriteCopiedChoices(Utf8JsonWriter writer, IReadOnlyList<SourceChoice> choices)
    {
        for (var index = 0; index < choices.Count; index++)
        {
            writer.WriteStartObject();
            writer.WriteString(SurveyJsPropertyNames.Value, choices[index].Value);
            writer.WriteNumber(FormSchemaCodebookPropertyNames.Id, index + 1);
            writer.WritePropertyName(SurveyJsPropertyNames.Text);
            WriteCopiedChoiceText(writer, choices[index]);
            writer.WriteEndObject();
        }
    }

    private static void WriteColumnChoiceLabel(
        Utf8JsonWriter writer,
        JsonElement question,
        string choiceValue,
        ChoiceSources choiceSources)
    {
        if (CopiedChoice(question, choiceValue, choiceSources) is not { } copied)
        {
            WriteChoiceLabel(writer, question, choiceValue);
            return;
        }

        writer.WritePropertyName(FormSchemaCodebookPropertyNames.ChoiceLabel);
        WriteCopiedChoiceText(writer, copied);
    }

    // A select or a ranking that copies its choices reads each label on the source question's choice; its special
    // items are its own, so they are not found here.
    private static SourceChoice? CopiedChoice(JsonElement question, string value, ChoiceSources choiceSources)
    {
        if (choiceSources.Offered(question) is not { IsCopied: true } offered)
        {
            return null;
        }

        foreach (var choice in offered.Choices)
        {
            if (string.Equals(choice.Value, value, StringComparison.Ordinal))
            {
                return choice;
            }
        }

        return null;
    }

    private static void WriteCopiedChoiceText(Utf8JsonWriter writer, SourceChoice choice) =>
        WriteLocalizedStringsWithFallback(
            writer,
            SurveyJsLocalizationHelper.ReadLocalizedStrings(choice.Element, SurveyJsPropertyNames.Text),
            choice.Text);
}
