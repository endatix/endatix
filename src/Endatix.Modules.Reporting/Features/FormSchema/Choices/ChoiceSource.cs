using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Choices;

/// <summary>
/// COMPILE TIME. Where a select question's choices come from, read once from its definition with Hub's precedence: a
/// data list, then SurveyJS <c>choicesFromQuestion</c>, then <c>choicesByUrl</c>, then Hub carry forward when it is on.
/// The codebook records it per question (<see cref="FormSchemaCodebookPropertyNames.ChoiceSource"/>) for every source
/// but <see cref="Inline"/>, so an export never reads the definition again.
/// </summary>
internal abstract record ChoiceSource
{
    private ChoiceSource()
    {
    }

    /// <summary>The question's own <c>choices</c>; also <c>choicesByUrl</c>, whose choices load at run time.</summary>
    public sealed record Inline : ChoiceSource
    {
        public static Inline Instance { get; } = new();
    }

    /// <summary>A Hub data list (<c>edxDataListId</c>).</summary>
    public sealed record DataList(string ListId) : ChoiceSource;

    /// <summary>
    /// The choices of other questions in this definition, in source order: SurveyJS <c>choicesFromQuestion</c> (one
    /// source) or Hub <c>edxCarryForwardSources</c>. Modes, caps and priority items only shape a respondent's list.
    /// </summary>
    public sealed record CarryForward(IReadOnlyList<string> Sources) : ChoiceSource;

    /// <summary>Choices the definition cannot tell, such as values copied from another question's answers.</summary>
    public sealed record Unresolvable(string Reason) : ChoiceSource;

    public static ChoiceSource Parse(JsonElement question)
    {
        if (question.GetNonEmptyStringProperty(SurveyJsPropertyNames.DataListId) is { } listId)
        {
            return new DataList(listId);
        }

        if (question.GetNonEmptyStringProperty(SurveyJsPropertyNames.ChoicesFromQuestion) is { } source)
        {
            return question.GetNonEmptyStringProperty(SurveyJsPropertyNames.ChoiceValuesFromQuestion) is null
                ? new CarryForward([source])
                : new Unresolvable(SurveyJsPropertyNames.ChoiceValuesFromQuestion);
        }

        return !LoadsChoicesFromUrl(question) &&
               question.GetBooleanProperty(SurveyJsPropertyNames.CarryForwardEnabled) &&
               CarryForwardSources(question) is [_, ..] sources
            ? new CarryForward(sources)
            : Inline.Instance;
    }

    /// <summary>Writes <c>choiceSource</c>: <c>kind</c>, then what it names. Nothing for <see cref="Inline"/>.</summary>
    public void WriteTo(Utf8JsonWriter writer)
    {
        if (this is Inline)
        {
            return;
        }

        writer.WriteStartObject(FormSchemaCodebookPropertyNames.ChoiceSource);
        switch (this)
        {
            case DataList list:
                writer.WriteString(FormSchemaCodebookPropertyNames.Kind, "dataList");
                writer.WriteString(FormSchemaCodebookPropertyNames.ListId, list.ListId);
                break;
            case CarryForward copy:
                writer.WriteString(FormSchemaCodebookPropertyNames.Kind, "carryForward");
                writer.WriteStartArray(FormSchemaCodebookPropertyNames.Sources);
                copy.Sources.ToList().ForEach(writer.WriteStringValue);
                writer.WriteEndArray();
                break;
            case Unresolvable unresolvable:
                writer.WriteString(FormSchemaCodebookPropertyNames.Kind, "unresolvable");
                writer.WriteString(FormSchemaCodebookPropertyNames.Reason, unresolvable.Reason);
                break;
        }

        writer.WriteEndObject();
    }

    private static bool LoadsChoicesFromUrl(JsonElement question) =>
        question.TryGetProperty(SurveyJsPropertyNames.ChoicesByUrl, out var choicesByUrl) &&
        choicesByUrl.ValueKind == JsonValueKind.Object &&
        choicesByUrl.GetNonEmptyStringProperty(SurveyJsPropertyNames.Url) is not null;

    private static List<string> CarryForwardSources(JsonElement question) =>
        question.TryGetArrayProperty(SurveyJsPropertyNames.CarryForwardSources, out var sources)
            ? [.. sources.EnumerateArray().Select(source => source.GetNonEmptyStringValue()).OfType<string>()]
            : [];
}
