using System.Globalization;
using System.Text.Json;
using Endatix.Modules.Reporting.Domain.SurveyJs;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Choices;

/// <summary>Which of its sources' choices a copy shows: SurveyJS <c>choicesFromQuestionMode</c>, Hub <c>edxCarryForwardMode</c>.</summary>
internal enum CopyMode
{
    All,
    Selected,
    Unselected,
}

/// <summary>Why a copy's shown choices cannot be rebuilt from the sources' answers. For tests and logs; never persisted.</summary>
internal enum UnknownBecause
{
    NotACopy,
    UnrecognizedMode,
    MaxChoices,
    ChoicesVisibleIf,
    NoSelectSource,
    SourceIsARanking,
    SourceIsNotOwnChoices,
    SourceChoicesVisibleIf,
}

/// <summary>
/// The one owner of "was this copied choice shown". COMPILE: <see cref="Of"/> decides whether the list a respondent
/// saw follows from the sources' answers alone, as Hub and SurveyJS build it from the sources' visible choices: only
/// under <c>all</c>, <c>selected</c> or <c>unselected</c>, with no cap, no <c>choicesVisibleIf</c>, and sources that
/// show their own choices with no visibility rule. STORE: <see cref="WriteTo"/>, <see cref="Read"/> and
/// <see cref="Merge"/> keep the rule on the copy's choice catalog (<see cref="FormSchemaCodebookPropertyNames.Shown"/>).
/// RUN: <see cref="Shows"/> rebuilds one respondent's list. Absent or null means unknown, never a guess.
/// </summary>
internal sealed class CopyVisibility
{
    private CopyVisibility(CopyMode mode, IReadOnlyList<string> sources)
    {
        Mode = mode;
        Sources = sources;
    }

    public CopyMode Mode { get; }

    public IReadOnlyList<string> Sources { get; }

    /// <summary>The rule of a copy whose shown list follows from the answers; null otherwise.</summary>
    public static CopyVisibility? Of(JsonElement question, IReadOnlyDictionary<string, JsonElement> questionsByName) =>
        Compile(question, questionsByName).Rule;

    /// <summary>Why <see cref="Of"/> returns null; null when it returns a rule.</summary>
    internal static UnknownBecause? WhyUnknown(JsonElement question, IReadOnlyDictionary<string, JsonElement> questionsByName) =>
        Compile(question, questionsByName).Reason;

    /// <summary>Writes <c>"shown": {mode, sources}</c>.</summary>
    public void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject(FormSchemaCodebookPropertyNames.Shown);
        writer.WriteString(FormSchemaCodebookPropertyNames.Mode, ToJsonName(Mode));
        writer.WriteStartArray(FormSchemaCodebookPropertyNames.Sources);
        foreach (var source in Sources)
        {
            writer.WriteStringValue(source);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    /// <summary>The rule on a choice catalog; null when it has none or it is malformed.</summary>
    public static CopyVisibility? Read(JsonElement catalog)
    {
        if (catalog.ValueKind != JsonValueKind.Object ||
            catalog.TryGetPropertyValue(FormSchemaCodebookPropertyNames.Shown) is not { ValueKind: JsonValueKind.Object } shown ||
            ParseExactMode(shown.GetStringProperty(FormSchemaCodebookPropertyNames.Mode)) is not { } mode ||
            !shown.TryGetArrayProperty(FormSchemaCodebookPropertyNames.Sources, out var sourcesArray))
        {
            return null;
        }

        List<string> sources = [.. sourcesArray.EnumerateArray().Select(source => source.GetNonEmptyStringValue()).OfType<string>()];
        return sources.Count == sourcesArray.GetArrayLength() && sources.Count > 0 ? new CopyVisibility(mode, sources) : null;
    }

    /// <summary>
    /// MERGE (a compile over an earlier definition's codebook): the rule is kept only when the stored and the new rule
    /// are the same and the copy offers the same choices. Otherwise it is dropped, and stays dropped until a replace
    /// compile, so a row saved under an earlier definition is never judged by a rule it was not shown under.
    /// </summary>
    public static void Merge(Utf8JsonWriter writer, JsonElement existingCatalog, JsonElement currentCatalog)
    {
        if (Read(existingCatalog) is { } stored &&
            Read(currentCatalog) is { } current &&
            stored.Mode == current.Mode &&
            stored.Sources.SequenceEqual(current.Sources, StringComparer.Ordinal) &&
            CopiedValues(existingCatalog).SetEquals(CopiedValues(currentCatalog)))
        {
            current.WriteTo(writer);
        }
    }

    /// <summary>
    /// RUN: whether the respondent was shown <paramref name="value"/>. With several sources a value is shown when any
    /// source showed it. Null when no source showed it and a source has no answer in the scope or no select catalog.
    /// </summary>
    public bool? Shows(string value, AnswerScope scope, ChoiceCatalogs catalogs)
    {
        var unknown = false;
        foreach (var source in Sources)
        {
            switch (ShownBy(source, value, scope, catalogs))
            {
                case true:
                    return true;
                case null:
                    unknown = true;
                    break;
            }
        }

        return unknown ? null : false;
    }

    private bool? ShownBy(string source, string value, AnswerScope scope, ChoiceCatalogs catalogs)
    {
        if (scope.ContextAnswering(source) is not { } context || catalogs.SelectOrNull(source) is not { } select)
        {
            return null;
        }

        var picked = select.Read(context, source).Has(value);
        return Mode switch
        {
            CopyMode.Selected => picked,
            CopyMode.All => select.Offers(value),
            _ => select.Offers(value) && !picked,
        };
    }

    private static (CopyVisibility? Rule, UnknownBecause? Reason) Compile(JsonElement question, IReadOnlyDictionary<string, JsonElement> questionsByName)
    {
        if (ChoiceSource.Parse(question) is not ChoiceSource.CarryForward copy)
        {
            return Unknown(UnknownBecause.NotACopy);
        }

        var fromQuestion = question.GetNonEmptyStringProperty(SurveyJsPropertyNames.ChoicesFromQuestion) is not null;
        if (ModeOf(question, fromQuestion) is not { } mode)
        {
            return Unknown(UnknownBecause.UnrecognizedMode);
        }

        if (!fromQuestion && HasMaxChoices(question))
        {
            return Unknown(UnknownBecause.MaxChoices);
        }

        if (question.TryGetPropertyValue(SurveyJsPropertyNames.ChoicesVisibleIf) is not null)
        {
            return Unknown(UnknownBecause.ChoicesVisibleIf);
        }

        // Hub and SurveyJS skip a missing source and one that offers no choices, so it adds nothing to the list.
        List<string> sources = [];
        foreach (var name in copy.Sources)
        {
            if (!questionsByName.TryGetValue(name, out var source))
            {
                continue;
            }

            if (SourceProblem(source) is { } problem)
            {
                return Unknown(problem);
            }

            if (source.IsSingleSelectBaseSelect() || source.IsMultiSelectBaseSelect())
            {
                sources.Add(name);
            }
        }

        return sources.Count > 0 ? (new CopyVisibility(mode, sources), null) : Unknown(UnknownBecause.NoSelectSource);
    }

    private static (CopyVisibility? Rule, UnknownBecause? Reason) Unknown(UnknownBecause reason) => (null, reason);

    // A ranking source has no select catalog to read; a copied, data-list or loaded source's list is not its own; a
    // visibility rule makes the source's visible choices depend on more than its answer.
    private static UnknownBecause? SourceProblem(JsonElement source)
    {
        if (SurveyJsElementType.Ranking.Matches(source.GetSurveyJsType()))
        {
            return UnknownBecause.SourceIsARanking;
        }

        if (ChoiceSource.Parse(source) is not ChoiceSource.Inline)
        {
            return UnknownBecause.SourceIsNotOwnChoices;
        }

        return source.TryGetPropertyValue(SurveyJsPropertyNames.ChoicesVisibleIf) is not null ||
               SurveyJsChoiceHelper.EnumerateChoiceElements(source).Any(choice =>
                   choice.Element.ValueKind == JsonValueKind.Object &&
                   choice.Element.TryGetPropertyValue(SurveyJsPropertyNames.VisibleIf) is not null)
            ? UnknownBecause.SourceChoicesVisibleIf
            : null;
    }

    // SurveyJS knows the three exact values and defaults to all. Hub also accepts its loop labels, trimmed and in any
    // case, and defaults to all; an unknown string Hub would read as all stays unknown here rather than a guess.
    private static CopyMode? ModeOf(JsonElement question, bool fromQuestion)
    {
        var property = fromQuestion ? SurveyJsPropertyNames.ChoicesFromQuestionMode : SurveyJsPropertyNames.CarryForwardMode;
        var mode = question.TryGetPropertyValue(property);
        if (fromQuestion)
        {
            return mode switch
            {
                null or { ValueKind: JsonValueKind.Null } => CopyMode.All,
                { ValueKind: JsonValueKind.String } surveyJsMode => ParseExactMode(surveyJsMode.GetString()),
                _ => null,
            };
        }

        if (mode is not { ValueKind: JsonValueKind.String } hubMode || string.IsNullOrWhiteSpace(hubMode.GetString()))
        {
            return CopyMode.All;
        }

        return hubMode.GetString()!.Trim().ToLowerInvariant() switch
        {
            "selected only" => CopyMode.Selected,
            "unselected only" => CopyMode.Unselected,
            var label => ParseExactMode(label),
        };
    }

    private static CopyMode? ParseExactMode(string? mode) =>
        mode switch
        {
            "all" => CopyMode.All,
            "selected" => CopyMode.Selected,
            "unselected" => CopyMode.Unselected,
            _ => null,
        };

    private static string ToJsonName(CopyMode mode) =>
        mode switch
        {
            CopyMode.Selected => "selected",
            CopyMode.Unselected => "unselected",
            _ => "all",
        };

    // Hub caps at a number of at least 1, from a number or a numeric string; a value it cannot read is taken as a cap.
    private static bool HasMaxChoices(JsonElement question) =>
        question.TryGetPropertyValue(SurveyJsPropertyNames.CarryForwardMaxChoices) switch
        {
            null or { ValueKind: JsonValueKind.Null } => false,
            { ValueKind: JsonValueKind.Number } number => number.GetDouble() >= 1,
            { ValueKind: JsonValueKind.String } text => string.IsNullOrWhiteSpace(text.GetString())
                ? false
                : !double.TryParse(text.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var cap) || cap >= 1,
            _ => true,
        };

    private static HashSet<string> CopiedValues(JsonElement catalog) =>
        catalog.TryGetArrayProperty(SurveyJsPropertyNames.Choices, out var entries)
            ? [.. entries.EnumerateArray()
                .Select(CatalogChoice.Read)
                .OfType<CatalogChoice>()
                .Where(choice => choice.Kind == ChoiceKind.Choice)
                .Select(choice => choice.Value)]
            : [];
}
