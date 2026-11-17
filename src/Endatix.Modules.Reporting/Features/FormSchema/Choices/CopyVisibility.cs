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

    /// <summary>
    /// Writes a copy's catalog fields: <c>copied</c> on any copy, and <c>shown</c> on a checkbox or tagbox whose shown
    /// choices follow from the answers (only those have indicator columns).
    /// </summary>
    public static void WriteFor(Utf8JsonWriter writer, JsonElement question, IReadOnlyDictionary<string, JsonElement> questionsByName)
    {
        if (ChoiceSource.Parse(question) is not ChoiceSource.CarryForward)
        {
            return;
        }

        writer.WriteBoolean(FormSchemaCodebookPropertyNames.Copied, true);
        if (question.IsMultiSelectBaseSelect())
        {
            Of(question, questionsByName)?.WriteTo(writer);
        }
    }

    /// <summary>Whether a choice catalog is marked as a copy's.</summary>
    public static bool IsCopied(JsonElement catalog) =>
        catalog.ValueKind == JsonValueKind.Object && catalog.GetBooleanProperty(FormSchemaCodebookPropertyNames.Copied);

    /// <summary>The catalog with <c>copied</c> set, for a codebook compiled before the marker existed.</summary>
    public static JsonElement MarkCopied(JsonElement catalog)
    {
        if (IsCopied(catalog) || catalog.ValueKind != JsonValueKind.Object)
        {
            return catalog;
        }

        var marked = System.Text.Json.Nodes.JsonNode.Parse(catalog.GetRawText())!.AsObject();
        marked[FormSchemaCodebookPropertyNames.Copied] = true;
        return JsonSerializer.SerializeToElement(marked);
    }

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
    /// are the same and the copy offers the same choices and special items. Otherwise it is dropped, and stays dropped
    /// until a replace compile, so a row saved under an earlier definition is never judged by a rule it was not shown
    /// under. The <c>copied</c> marker is kept whenever either catalog has it.
    /// </summary>
    public static void Merge(Utf8JsonWriter writer, JsonElement existingCatalog, JsonElement currentCatalog)
    {
        if (IsCopied(existingCatalog) || IsCopied(currentCatalog))
        {
            writer.WriteBoolean(FormSchemaCodebookPropertyNames.Copied, true);
        }

        if (Read(existingCatalog) is { } stored &&
            Read(currentCatalog) is { } current &&
            stored.Mode == current.Mode &&
            stored.Sources.SequenceEqual(current.Sources, StringComparer.Ordinal) &&
            CatalogValues(existingCatalog).SetEquals(CatalogValues(currentCatalog)))
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
        List<bool?> verdicts =
        [
            .. Sources.Select(source =>
                new SourceAnswer(source, scope.ContextAnswering(source), catalogs.SelectOrNull(source)).Shows(Mode, value)),
        ];
        if (verdicts.Contains(true))
        {
            return true;
        }

        return verdicts.Contains(null) ? null : false;
    }

    // One source as the cell's scope sees it: the context that answers it, and its select catalog.
    private readonly record struct SourceAnswer(string Name, JsonElement? Context, SelectChoices? Select)
    {
        public bool? Shows(CopyMode mode, string value)
        {
            if (Context is not { } context || Select is not { } select)
            {
                return null;
            }

            var picked = select.Read(context, Name).Has(value);
            return mode switch
            {
                CopyMode.Selected => picked && select.Offers(value),
                CopyMode.All => select.Offers(value),
                _ => select.Offers(value) && !picked,
            };
        }
    }

    private static (CopyVisibility? Rule, UnknownBecause? Reason) Compile(
        JsonElement question,
        IReadOnlyDictionary<string, JsonElement> questionsByName)
    {
        if (ChoiceSource.Parse(question) is not ChoiceSource.CarryForward copy)
        {
            return Unknown(UnknownBecause.NotACopy);
        }

        var fromQuestion = question.GetNonEmptyStringProperty(SurveyJsPropertyNames.ChoicesFromQuestion) is not null;
        var mode = ModeOf(question, fromQuestion);
        var problem = mode is null ? UnknownBecause.UnrecognizedMode : ListProblem(question, fromQuestion);
        return problem is null
            ? FromSources(mode!.Value, [.. copy.Sources.Distinct(StringComparer.Ordinal).Where(questionsByName.ContainsKey).Select(name => (name, questionsByName[name]))])
            : Unknown(problem.Value);
    }

    // Hub and SurveyJS skip a missing source and one that offers no choices, so it adds nothing to the list.
    private static (CopyVisibility? Rule, UnknownBecause? Reason) FromSources(
        CopyMode mode,
        IReadOnlyList<(string Name, JsonElement Element)> sources)
    {
        if (sources.Select(source => SourceProblem(source.Element)).FirstOrDefault(problem => problem is not null) is { } problem)
        {
            return Unknown(problem);
        }

        List<string> selects = [.. sources.Where(source => IsSelect(source.Element)).Select(source => source.Name)];
        return selects.Count > 0 ? (new CopyVisibility(mode, selects), null) : Unknown(UnknownBecause.NoSelectSource);
    }

    private static (CopyVisibility? Rule, UnknownBecause? Reason) Unknown(UnknownBecause reason) => (null, reason);

    // A cap or a visibility rule on the copy shows fewer choices than its sources' answers give.
    private static UnknownBecause? ListProblem(JsonElement question, bool fromQuestion)
    {
        if (!fromQuestion && HasMaxChoices(question))
        {
            return UnknownBecause.MaxChoices;
        }

        return question.TryGetPropertyValue(SurveyJsPropertyNames.ChoicesVisibleIf) is null ? null : UnknownBecause.ChoicesVisibleIf;
    }

    private static bool IsSelect(JsonElement question) =>
        question.IsSingleSelectBaseSelect() || question.IsMultiSelectBaseSelect();

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
    private static CopyMode? ModeOf(JsonElement question, bool fromQuestion) =>
        fromQuestion
            ? SurveyJsMode(question.TryGetPropertyValue(SurveyJsPropertyNames.ChoicesFromQuestionMode))
            : HubMode(question.TryGetPropertyValue(SurveyJsPropertyNames.CarryForwardMode));

    private static CopyMode? SurveyJsMode(JsonElement? mode) =>
        mode switch
        {
            null or { ValueKind: JsonValueKind.Null } => CopyMode.All,
            { ValueKind: JsonValueKind.String } text => ParseExactMode(text.GetString()),
            _ => null,
        };

    private static CopyMode? HubMode(JsonElement? mode)
    {
        if (mode is not { ValueKind: JsonValueKind.String } text || string.IsNullOrWhiteSpace(text.GetString()))
        {
            return CopyMode.All;
        }

        return text.GetString()!.Trim().ToLowerInvariant() switch
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
            { ValueKind: JsonValueKind.String } text => !string.IsNullOrWhiteSpace(text.GetString()) &&
                (!double.TryParse(text.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var cap) || cap >= 1),
            _ => true,
        };

    // Every value with its kind: the copied choices and the copy's own special items.
    private static HashSet<(string, ChoiceKind)> CatalogValues(JsonElement catalog) =>
        catalog.TryGetArrayProperty(SurveyJsPropertyNames.Choices, out var entries)
            ? [.. entries.EnumerateArray()
                .Select(CatalogChoice.Read)
                .OfType<CatalogChoice>()
                .Select(choice => (choice.Value, choice.Kind))]
            : [];
}
