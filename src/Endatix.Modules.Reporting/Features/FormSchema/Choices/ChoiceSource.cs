using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Choices;

/// <summary>
/// COMPILE TIME. Where a select question's choices come from, read once from its definition with Hub's precedence: a
/// data list, then SurveyJS <c>choicesFromQuestion</c>, then <c>choicesByUrl</c>, then Hub carry forward when it is on.
/// The codebook records it on a base select's or a ranking's entry (<see cref="FormSchemaCodebookPropertyNames.ChoiceSource"/>) for
/// every source but <see cref="Inline"/>, so an export never reads the definition again. Its <c>kind</c> and
/// <c>reason</c> values are the constants below; a reader treats an unknown kind as inline.
/// </summary>
internal abstract record ChoiceSource
{
    private ChoiceSource()
    {
    }

    public const string DataListKind = "dataList";
    public const string CarryForwardKind = "carryForward";
    public const string UnresolvableKind = "unresolvable";

    /// <summary><see cref="Unresolvable"/>: values copied from another question's answers (a matrix column, a panel).</summary>
    public const string ValuesFromAnswersReason = SurveyJsPropertyNames.ChoiceValuesFromQuestion;

    /// <summary><see cref="Unresolvable"/>: choices loaded from a web service at run time.</summary>
    public const string LoadedFromUrlReason = SurveyJsPropertyNames.ChoicesByUrl;

    /// <summary>The question's own <c>choices</c>.</summary>
    public sealed record Inline : ChoiceSource
    {
        public static Inline Instance { get; } = new();

        protected override string? Kind => null;

        protected override void WriteFields(Utf8JsonWriter writer)
        {
        }
    }

    /// <summary>A Hub data list (<c>edxDataListId</c>).</summary>
    public sealed record DataList(string ListId) : ChoiceSource
    {
        protected override string Kind => DataListKind;

        protected override void WriteFields(Utf8JsonWriter writer) =>
            writer.WriteString(FormSchemaCodebookPropertyNames.ListId, ListId);
    }

    /// <summary>
    /// The choices of other questions in this definition, in source order: SurveyJS <c>choicesFromQuestion</c> (one
    /// source) or Hub <c>edxCarryForwardSources</c>. Modes, caps and priority items only shape a respondent's list, so they
    /// change no choice here; <see cref="CopyVisibility"/> reads them to decide which cells may be 0.
    /// </summary>
    public sealed record CarryForward(IReadOnlyList<string> Sources) : ChoiceSource
    {
        protected override string Kind => CarryForwardKind;

        protected override void WriteFields(Utf8JsonWriter writer)
        {
            writer.WriteStartArray(FormSchemaCodebookPropertyNames.Sources);
            foreach (var source in Sources)
            {
                writer.WriteStringValue(source);
            }

            writer.WriteEndArray();
        }
    }

    /// <summary>Choices the definition cannot tell: values copied from answers, or choices loaded from a URL.</summary>
    public sealed record Unresolvable(string Reason) : ChoiceSource
    {
        protected override string Kind => UnresolvableKind;

        protected override void WriteFields(Utf8JsonWriter writer) =>
            writer.WriteString(FormSchemaCodebookPropertyNames.Reason, Reason);
    }

    /// <summary>The codebook <c>kind</c>; null for <see cref="Inline"/>, which the codebook leaves out.</summary>
    protected abstract string? Kind { get; }

    public static ChoiceSource Parse(JsonElement question)
    {
        if (question.GetNonEmptyStringProperty(SurveyJsPropertyNames.DataListId) is { } listId)
        {
            return new DataList(listId);
        }

        if (question.GetNonEmptyStringProperty(SurveyJsPropertyNames.ChoicesFromQuestion) is { } source)
        {
            return CopiedFromQuestion(question, source);
        }

        return LoadsChoicesFromUrl(question) ? new Unresolvable(LoadedFromUrlReason) : CarriedForward(question);
    }

    /// <summary>Writes <c>choiceSource</c>: <c>kind</c>, then what it names. Nothing for <see cref="Inline"/>.</summary>
    public void WriteTo(Utf8JsonWriter writer)
    {
        if (Kind is not { } kind)
        {
            return;
        }

        writer.WriteStartObject(FormSchemaCodebookPropertyNames.ChoiceSource);
        writer.WriteString(FormSchemaCodebookPropertyNames.Kind, kind);
        WriteFields(writer);
        writer.WriteEndObject();
    }

    protected abstract void WriteFields(Utf8JsonWriter writer);

    // With choiceValuesFromQuestion the source is a matrix or panel, and the choices are values its rows hold.
    private static ChoiceSource CopiedFromQuestion(JsonElement question, string source) =>
        question.GetNonEmptyStringProperty(SurveyJsPropertyNames.ChoiceValuesFromQuestion) is null
            ? new CarryForward([source])
            : new Unresolvable(ValuesFromAnswersReason);

    // Hub carries forward only when it is turned on; sources left on a question with it off are ignored.
    private static ChoiceSource CarriedForward(JsonElement question) =>
        question.GetBooleanProperty(SurveyJsPropertyNames.CarryForwardEnabled) &&
        CarryForwardSources(question) is [_, ..] sources
            ? new CarryForward(sources)
            : Inline.Instance;

    private static bool LoadsChoicesFromUrl(JsonElement question) =>
        question.TryGetProperty(SurveyJsPropertyNames.ChoicesByUrl, out var choicesByUrl) &&
        choicesByUrl.ValueKind == JsonValueKind.Object &&
        choicesByUrl.GetNonEmptyStringProperty(SurveyJsPropertyNames.Url) is not null;

    private static List<string> CarryForwardSources(JsonElement question) =>
        question.TryGetArrayProperty(SurveyJsPropertyNames.CarryForwardSources, out var sources)
            ? [.. sources.EnumerateArray().Select(source => source.GetNonEmptyStringValue()).OfType<string>()]
            : [];
}
