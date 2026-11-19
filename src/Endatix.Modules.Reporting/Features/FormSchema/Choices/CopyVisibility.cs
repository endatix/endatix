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
    SourceCopyIsNotExact,
    SourceChoicesVisibleIf,
    NameShared,
    HiddenAnswersKept,
    CopyAlwaysHidden,
}

/// <summary>One source of an exact copy: its name and the regular choice values it offered.</summary>
internal sealed record SourceChoices(string Name, IReadOnlyList<string> Values)
{
    public bool Offers(string value) => Values.Contains(value, StringComparer.Ordinal);

    public void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString(FormSchemaCodebookPropertyNames.SourceName, Name);
        writer.WriteStartArray(FormSchemaCodebookPropertyNames.Values);
        foreach (var value in Values)
        {
            writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    public bool SameAs(SourceChoices other) =>
        Name == other.Name && Values.ToHashSet(StringComparer.Ordinal).SetEquals(other.Values);
}

/// <summary>
/// The one owner of "was this copied choice shown". COMPILE: <see cref="Of"/> decides whether the list a respondent
/// saw follows from the sources' answers alone, as Hub and SurveyJS build it from the sources' visible choices: only
/// under <c>all</c>, <c>selected</c> or <c>unselected</c>, with no cap, no <c>choicesVisibleIf</c>, sources with no
/// choice visibility rule that show their own choices or copy them (in <c>all</c> and <c>unselected</c> mode only from
/// a checkbox or tagbox copy that is exact itself; in <c>selected</c> mode a source's picks were on its list), no
/// question name the definition uses twice, and no static
/// <c>visible: false</c> on the copy or an enclosing panel or page. A survey that keeps hidden answers has no exact
/// copy once any page, panel or question has a <c>visibleIf</c>; otherwise a copy or source under a <c>visibleIf</c>
/// (its own, or an enclosing panel's or page's) is exact only for a completed submission (<see cref="WhenComplete"/>).
/// STORE: <see cref="WriteTo"/>, <see cref="Read"/> and
/// <see cref="Merge"/> keep the rule on the copy's choice catalog (<see cref="FormSchemaCodebookPropertyNames.Shown"/>).
/// RUN: <see cref="Shows"/> rebuilds one respondent's list. Absent or null means unknown, never a guess.
/// </summary>
internal sealed class CopyVisibility
{
    private CopyVisibility(CopyMode mode, IReadOnlyList<SourceChoices> sources, bool whenComplete)
    {
        Mode = mode;
        Sources = sources;
        WhenComplete = whenComplete;
    }

    public CopyMode Mode { get; }

    public IReadOnlyList<SourceChoices> Sources { get; }

    /// <summary>
    /// A copy or source shows only under a <c>visibleIf</c>, its own or an enclosing panel's or page's: the rule holds
    /// for a completed submission.
    /// </summary>
    public bool WhenComplete { get; }

    /// <summary>Whether the rule applies to a submission, complete or not.</summary>
    public bool HoldsFor(bool submissionComplete) => !WhenComplete || submissionComplete;

    /// <summary>The rule of a copy whose shown list follows from the answers; null otherwise.</summary>
    public static CopyVisibility? Of(JsonElement question, ChoiceSources offered, CopyDefinition definition) =>
        Compile(question, new Lookup(offered, definition)).Rule;

    /// <summary>Why <see cref="Of"/> returns null; null when it returns a rule.</summary>
    internal static UnknownBecause? WhyUnknown(
        JsonElement question,
        IReadOnlyDictionary<string, JsonElement> questionsByName,
        CopyDefinition definition) =>
        Compile(question, new Lookup(new ChoiceSources(questionsByName), definition)).Reason;

    /// <summary>
    /// Writes a copy's catalog fields: <c>copied</c> on any copy, and <c>shown</c> on a checkbox or tagbox whose shown
    /// choices follow from the answers (only those have indicator columns).
    /// </summary>
    public static void WriteFor(Utf8JsonWriter writer, JsonElement question, Func<CopyVisibility?> rule)
    {
        if (ChoiceSource.Parse(question) is not ChoiceSource.CarryForward)
        {
            return;
        }

        writer.WriteBoolean(FormSchemaCodebookPropertyNames.Copied, true);
        if (question.IsMultiSelectBaseSelect())
        {
            rule()?.WriteTo(writer);
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

    /// <summary>Writes <c>"shown": {mode, sources: [{name, values}], whenComplete}</c>.</summary>
    public void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject(FormSchemaCodebookPropertyNames.Shown);
        writer.WriteString(FormSchemaCodebookPropertyNames.Mode, ToJsonName(Mode));
        writer.WriteStartArray(FormSchemaCodebookPropertyNames.Sources);
        foreach (var source in Sources)
        {
            source.WriteTo(writer);
        }

        writer.WriteEndArray();
        if (WhenComplete)
        {
            writer.WriteBoolean(FormSchemaCodebookPropertyNames.WhenComplete, true);
        }

        writer.WriteEndObject();
    }

    /// <summary>The rule on a choice catalog; null when it has none or it is malformed (sources without values too).</summary>
    public static CopyVisibility? Read(JsonElement catalog)
    {
        if (catalog.ValueKind != JsonValueKind.Object ||
            catalog.TryGetPropertyValue(FormSchemaCodebookPropertyNames.Shown) is not { ValueKind: JsonValueKind.Object } shown ||
            ParseExactMode(shown.GetStringProperty(FormSchemaCodebookPropertyNames.Mode)) is not { } mode ||
            !shown.TryGetArrayProperty(FormSchemaCodebookPropertyNames.Sources, out var sourcesArray))
        {
            return null;
        }

        List<SourceChoices> sources = [.. sourcesArray.EnumerateArray().Select(ReadSource).OfType<SourceChoices>()];
        return sources.Count == sourcesArray.GetArrayLength() && sources.Count > 0
            ? new CopyVisibility(mode, sources, shown.GetBooleanProperty(FormSchemaCodebookPropertyNames.WhenComplete))
            : null;
    }

    private static SourceChoices? ReadSource(JsonElement source)
    {
        if (source.ValueKind != JsonValueKind.Object ||
            source.GetNonEmptyStringProperty(FormSchemaCodebookPropertyNames.SourceName) is not { } name ||
            !source.TryGetArrayProperty(FormSchemaCodebookPropertyNames.Values, out var values))
        {
            return null;
        }

        List<string> offered = [.. values.EnumerateArray().Select(value => value.ValueKind == JsonValueKind.String ? value.GetString() : null).OfType<string>()];
        return offered.Count == values.GetArrayLength() ? new SourceChoices(name, offered) : null;
    }

    /// <summary>
    /// MERGE (a compile over an earlier definition's codebook): the rule is kept only when the stored and the new rule
    /// are the same, each source offers the same values, and the copy offers the same choices and special items. Otherwise it is dropped, and stays dropped
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
            stored.SameAs(current) &&
            CatalogValues(existingCatalog).SetEquals(CatalogValues(currentCatalog)))
        {
            current.WriteTo(writer);
        }
    }

    /// <summary>
    /// RUN: whether the respondent was shown <paramref name="value"/>. With several sources a value is shown when any
    /// source showed it. Null when no source showed it and a source has no answer in the scope or no select catalog.
    /// A source that copies its choices showed what its own rule rebuilds; null when it has none.
    /// </summary>
    public bool? Shows(string value, AnswerScope scope, ChoiceCatalogs catalogs)
    {
        if (!HoldsFor(catalogs.SubmissionComplete))
        {
            return null;
        }

        Cell cell = new(value, scope, catalogs);
        List<bool?> verdicts = [.. Sources.Select(source => SourceShows(source, cell))];
        if (verdicts.Contains(true))
        {
            return true;
        }

        return verdicts.Contains(null) ? null : false;
    }

    private bool SameAs(CopyVisibility other) =>
        Mode == other.Mode &&
        WhenComplete == other.WhenComplete &&
        Sources.Count == other.Sources.Count &&
        Sources.Zip(other.Sources).All(pair => pair.First.SameAs(pair.Second));

    // One source as the cell's scope sees it: the context that answers it, and the select that reads its answer. What
    // it offered comes from the rule, not from the catalog, which is the union of every definition. A selected copy
    // shows the source's picks, which were on the source's list; the other modes need the list itself, which for a
    // copied source is its own rule's. The lifted & keeps a source's pick out of an unselected copy's list even when
    // the source's list is unknown.
    private bool? SourceShows(SourceChoices source, Cell cell)
    {
        var (value, scope, catalogs) = cell;
        if (scope.ContextAnswering(source.Name) is not { } context)
        {
            return null;
        }

        var picked = catalogs.For(source.Name).Read(context, source.Name).Has(value);
        if (Mode == CopyMode.Selected)
        {
            return picked && source.Offers(value);
        }

        var listed = catalogs.Copies(source.Name)
            ? catalogs.VisibilityOf(source.Name)?.Shows(value, scope, catalogs)
            : source.Offers(value);
        return Mode == CopyMode.All ? listed : listed & !picked;
    }

    // The cell being judged: one choice value, the answers its scope sees, and the codebook's catalogs.
    private readonly record struct Cell(string Value, AnswerScope Scope, ChoiceCatalogs Catalogs);

    // What a compile reads besides the question: the choices each question offers (a copy's resolved through its
    // chain, over the definition's questions), and the definition-wide facts.
    private sealed record Lookup(ChoiceSources Offered, CopyDefinition Definition);

    private static (CopyVisibility? Rule, UnknownBecause? Reason) Compile(JsonElement question, Lookup lookup)
    {
        var (questionsByName, definition) = (lookup.Offered.QuestionsByName, lookup.Definition);
        if (ChoiceSource.Parse(question) is not ChoiceSource.CarryForward copy)
        {
            return Unknown(UnknownBecause.NotACopy);
        }

        var fromQuestion = question.GetNonEmptyStringProperty(SurveyJsPropertyNames.ChoicesFromQuestion) is not null;
        var mode = ModeOf(question, fromQuestion);
        var sources = SourcesIn(copy, questionsByName);
        var problem = mode is null
            ? UnknownBecause.UnrecognizedMode
            : ListProblem(question, fromQuestion) ??
              ScopeProblem(question, sources, definition) ??
              SourcesProblem(sources, mode.Value, lookup);
        return problem is null
            ? FromSources(mode!.Value, SelectChoicesIn(sources, lookup), UnderAVisibleIf(question, SelectsIn(sources), definition))
            : Unknown(problem.Value);
    }

    private static List<(string Name, JsonElement Element)> SourcesIn(
        ChoiceSource.CarryForward copy,
        IReadOnlyDictionary<string, JsonElement> questionsByName) =>
        [.. copy.Sources.Distinct(StringComparer.Ordinal).Where(questionsByName.ContainsKey).Select(name => (name, questionsByName[name]))];

    // Hub and SurveyJS skip a missing source and one that offers no choices, so it adds nothing to the list.
    private static (CopyVisibility? Rule, UnknownBecause? Reason) FromSources(
        CopyMode mode,
        List<SourceChoices> selects,
        bool whenComplete) =>
        selects.Count > 0 ? (new CopyVisibility(mode, selects, whenComplete), null) : Unknown(UnknownBecause.NoSelectSource);

    private static UnknownBecause? SourcesProblem(
        IReadOnlyList<(string Name, JsonElement Element)> sources,
        CopyMode mode,
        Lookup lookup) =>
        sources.Select(source => SourceProblem(source.Element, mode, lookup)).FirstOrDefault(problem => problem is not null);

    // Each select source with the values it offers: its own choices, or for a copy the union its chain resolves to.
    private static List<SourceChoices> SelectChoicesIn(IReadOnlyList<(string Name, JsonElement Element)> sources, Lookup lookup) =>
    [
        .. SelectsIn(sources).Select(source =>
            new SourceChoices(source.Name, [.. lookup.Offered.Offered(source.Element).Choices.Select(choice => choice.Value)])),
    ];

    // A name the definition uses twice has one catalog for two questions. A copy in a panel or page that is never shown
    // can hold an answer as one with its own visible: false can.
    private static UnknownBecause? ScopeProblem(
        JsonElement question,
        IReadOnlyList<(string Name, JsonElement Element)> sources,
        CopyDefinition definition)
    {
        if ((question.GetSurveyJsName() is { } name && definition.SharedNames.Contains(name)) ||
            sources.Any(source => definition.SharedNames.Contains(source.Name)))
        {
            return UnknownBecause.NameShared;
        }

        if (question.GetSurveyJsName() is { } copyName && definition.AlwaysHiddenNames.Contains(copyName))
        {
            return UnknownBecause.CopyAlwaysHidden;
        }

        return HiddenAnswerProblem(question, sources, definition);
    }

    // A survey that keeps hidden answers keeps one for a copy or source hidden by any visibleIf, its panel's or page's
    // included; an own visibleIf is checked too for a copy compiled without the definition. A copy or select source
    // whose own clearIfInvisible keeps its answer does the same under a visibleIf that can hide it.
    private static UnknownBecause? HiddenAnswerProblem(
        JsonElement question,
        IReadOnlyList<(string Name, JsonElement Element)> sources,
        CopyDefinition definition)
    {
        var hidden = definition.HasVisibleIf || UnderAVisibleIf(question, sources, definition);
        var kept = (hidden && !definition.ClearsHiddenAnswersOnComplete) ||
                   KeepsAHiddenAnswer(question, question.GetSurveyJsName(), definition) ||
                   SelectsIn(sources).Any(source => KeepsAHiddenAnswer(source.Element, source.Name, definition));
        return kept ? UnknownBecause.HiddenAnswersKept : null;
    }

    // Whether the copy or a source shows only under a visibleIf: its own, or one on an enclosing panel, dynamic panel or
    // page.
    private static bool UnderAVisibleIf(
        JsonElement question,
        IReadOnlyList<(string Name, JsonElement Element)> sources,
        CopyDefinition definition) =>
        definition.UnderAVisibleIf(question, question.GetSurveyJsName()) ||
        sources.Any(source => definition.UnderAVisibleIf(source.Element, source.Name));

    // A question whose own clearIfInvisible keeps its answer while a visibleIf hides it.
    private static bool KeepsAHiddenAnswer(JsonElement question, string? name, CopyDefinition definition) =>
        definition.KeepsHiddenAnswer(question) && definition.UnderAVisibleIf(question, name);

    // Only select sources add choices to the copy's list.
    private static List<(string Name, JsonElement Element)> SelectsIn(IReadOnlyList<(string Name, JsonElement Element)> sources) =>
        [.. sources.Where(source => IsSelect(source.Element))];

    private static (CopyVisibility? Rule, UnknownBecause? Reason) Unknown(UnknownBecause reason) => (null, reason);

    // A cap or a visibility rule on the copy shows fewer choices than its sources' answers give. A copy that is never
    // shown can still hold an answer (a default or a value set by an expression); a source that is never shown cannot
    // differ from the list it gave the copy, so only the copy is checked. The visible check serves CopyDefinition.None;
    // with a definition, AlwaysHiddenNames covers it.
    private static UnknownBecause? ListProblem(JsonElement question, bool fromQuestion)
    {
        if (question.TryGetPropertyValue(SurveyJsPropertyNames.Visible) is { ValueKind: JsonValueKind.False })
        {
            return UnknownBecause.CopyAlwaysHidden;
        }

        if (!fromQuestion && HasMaxChoices(question))
        {
            return UnknownBecause.MaxChoices;
        }

        return question.TryGetPropertyValue(SurveyJsPropertyNames.ChoicesVisibleIf) is null ? null : UnknownBecause.ChoicesVisibleIf;
    }

    private static bool IsSelect(JsonElement question) =>
        question.IsSingleSelectBaseSelect() || question.IsMultiSelectBaseSelect();

    // A ranking source has no select catalog to read; a data-list or loaded source's list, or a copy's that does not
    // resolve, is not known here; a visibility rule makes the source's visible choices depend on more than its answer.
    private static UnknownBecause? SourceProblem(JsonElement source, CopyMode mode, Lookup lookup)
    {
        if (SurveyJsElementType.Ranking.Matches(source.GetSurveyJsType()))
        {
            return UnknownBecause.SourceIsARanking;
        }

        if (HasChoiceVisibilityRule(source))
        {
            return UnknownBecause.SourceChoicesVisibleIf;
        }

        return ChoiceSource.Parse(source) switch
        {
            ChoiceSource.Inline => null,
            ChoiceSource.CarryForward when lookup.Offered.Offered(source).IsDegraded => UnknownBecause.SourceIsNotOwnChoices,
            ChoiceSource.CarryForward => CopiedSourceProblem(source, mode, lookup),
            _ => UnknownBecause.SourceIsNotOwnChoices,
        };
    }

    private static bool HasChoiceVisibilityRule(JsonElement source) =>
        source.TryGetPropertyValue(SurveyJsPropertyNames.ChoicesVisibleIf) is not null ||
        SurveyJsChoiceHelper.EnumerateChoiceElements(source).Any(choice =>
            choice.Element.ValueKind == JsonValueKind.Object &&
            choice.Element.TryGetPropertyValue(SurveyJsPropertyNames.VisibleIf) is not null);

    // A selected copy needs only the source's picks. The other modes rebuild the source's list from its own rule, which
    // is stored only on a checkbox or tagbox. A cycle never gets here: ChoiceSources leaves every copy on it degraded.
    private static UnknownBecause? CopiedSourceProblem(JsonElement source, CopyMode mode, Lookup lookup) =>
        mode == CopyMode.Selected ||
        (source.IsMultiSelectBaseSelect() && Compile(source, lookup).Rule is not null)
            ? null
            : UnknownBecause.SourceCopyIsNotExact;

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
