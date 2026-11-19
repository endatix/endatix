using System.Globalization;
using System.Text.Json;
using Endatix.Modules.Reporting.Domain.SurveyJs;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Choices;

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
/// STORE (<c>CopyVisibility.Store.cs</c>): <see cref="WriteTo"/>, <see cref="Read"/> and
/// <see cref="Merge"/> keep the rule on the copy's choice catalog (<see cref="FormSchemaCodebookPropertyNames.Shown"/>).
/// RUN: <see cref="Shows"/> rebuilds one respondent's list. Absent or null means unknown, never a guess.
/// </summary>
internal sealed partial class CopyVisibility
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
    /// RUN: whether the respondent was shown <paramref name="value"/>. With several sources a value is shown when any
    /// source showed it. Null when no source showed it and a source has no answer in the scope or no select catalog.
    /// A source that copies its choices showed what its own rule rebuilds; null when it has none.
    /// </summary>
    public bool? Shows(string value, AnswerScope scope, ChoiceCatalogs catalogs) =>
        ShowsAt(new Cell(
            value,
            scope,
            catalogs,
            Sources.Any(source => catalogs.VisibilityOf(source.Name) is not null) ? new(StringComparer.Ordinal) : null,
            0));

    private bool? ShowsAt(Cell cell)
    {
        if (!HoldsFor(cell.Catalogs.SubmissionComplete))
        {
            return null;
        }

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
    // copied source is its own rule's (Listed). The three-valued & keeps a source's pick out of an unselected copy's
    // list even when the source's list is unknown.
    private bool? SourceShows(SourceChoices source, Cell cell)
    {
        var (value, scope, catalogs, _, _) = cell;
        if (scope.ContextAnswering(source.Name) is not { } context)
        {
            return null;
        }

        var picked = catalogs.For(source.Name).Read(context, source.Name).Has(value);
        if (Mode == CopyMode.Selected)
        {
            return picked && source.Offers(value);
        }

        var listed = Listed(source, cell);

        return Mode == CopyMode.All ? listed : listed & !picked;
    }

    // The source's list as the cell sees it: a source with a stored rule showed what that rule rebuilds, one that copied
    // in any definition but has none is unknown, and any other offered its own choices.
    private static bool? Listed(SourceChoices source, Cell cell) =>
        cell.Catalogs.VisibilityOf(source.Name) switch
        {
            { } sourceRule => ListedBy(sourceRule, source.Name, cell),
            null when cell.Catalogs.Copies(source.Name) => null,
            _ => source.Offers(cell.Value),
        };

    // A copied source's list, read once per traversal: sources that share upstream copies would otherwise rebuild them
    // once per path, which doubles per level. A source still being read is unknown, so stored rules that name each
    // other, which no compile writes, stop. A chain deeper than MAX_CHAIN_DEPTH is unknown too: the run would otherwise
    // recurse once per link and overflow the stack on a long stored chain.
    private static bool? ListedBy(CopyVisibility sourceRule, string sourceName, Cell cell)
    {
        if (cell.Depth >= MAX_CHAIN_DEPTH)
        {
            return null;
        }

        var read = cell.ListsRead!;
        if (!read.TryAdd(sourceName, null))
        {
            return read[sourceName];
        }

        var shown = sourceRule.ShowsAt(cell with { Depth = cell.Depth + 1 });
        read[sourceName] = shown;
        return shown;
    }

    // The cell being judged: one choice value, the answers its scope sees, the codebook's catalogs, each copied
    // source's list read so far in this traversal (null when no source has a stored rule), and how many copied sources
    // deep the rule being read is.
    private readonly record struct Cell(
        string Value,
        AnswerScope Scope,
        ChoiceCatalogs Catalogs,
        Dictionary<string, bool?>? ListsRead,
        int Depth);

    // A fixed cap, not the stack's free space, so a chain is judged the same on every platform. A form's real chains
    // are a handful of links; one longer than this is unknown.
    private const int MAX_CHAIN_DEPTH = 64;

    // What a compile reads besides the question: the choices each question offers (a copy's resolved through its
    // chain, over the definition's questions), and the definition-wide facts.
    // Each source's rule is compiled once per lookup: both the exactness check and whenComplete read it, and a chain
    // would otherwise recompile every link twice per level.
    private sealed record Lookup(ChoiceSources Offered, CopyDefinition Definition)
    {
        private readonly Dictionary<string, CopyVisibility?> _sourceRules = new(StringComparer.Ordinal);
        private int _depth;

        // Null past MAX_CHAIN_DEPTH links, so the copy reads SourceCopyIsNotExact instead of overflowing the stack; that
        // null is not kept, since the same source may be reached again nearer the copy.
        public CopyVisibility? SourceRule((string Name, JsonElement Element) source)
        {
            if (_sourceRules.TryGetValue(source.Name, out var rule))
            {
                return rule;
            }

            if (_depth >= MAX_CHAIN_DEPTH)
            {
                return null;
            }

            _depth++;
            rule = Compile(source.Element, this).Rule;
            _depth--;
            _sourceRules[source.Name] = rule;
            return rule;
        }
    }

    private static (CopyVisibility? Rule, UnknownBecause? Reason) Compile(JsonElement question, Lookup lookup)
    {
        if (ChoiceSource.Parse(question) is not ChoiceSource.CarryForward copy)
        {
            return Unknown(UnknownBecause.NotACopy);
        }

        return CompileCopy(question, copy, lookup);
    }

    private static (CopyVisibility? Rule, UnknownBecause? Reason) CompileCopy(
        JsonElement question,
        ChoiceSource.CarryForward copy,
        Lookup lookup)
    {
        var fromQuestion = question.GetNonEmptyStringProperty(SurveyJsPropertyNames.ChoicesFromQuestion) is not null;
        if (CopyModes.Of(question, fromQuestion) is not { } mode)
        {
            return Unknown(UnknownBecause.UnrecognizedMode);
        }

        var sources = SourcesIn(copy, lookup.Offered.QuestionsByName);
        var problem = ListProblem(question, fromQuestion) ??
                      ScopeProblem(question, sources, lookup.Definition) ??
                      SourcesProblem(sources, mode, lookup);
        return problem is null
            ? FromSources(mode, SelectChoicesIn(sources, lookup), HoldsWhenComplete(question, (mode, sources), lookup))
            : Unknown(problem.Value);
    }

    // The copy or a select source under a visibleIf, or a copied source whose list the copy reads holding only then.
    private static bool HoldsWhenComplete(
        JsonElement question,
        (CopyMode Mode, IReadOnlyList<(string Name, JsonElement Element)> Sources) links,
        Lookup lookup) =>
        UnderAVisibleIf(question, SelectsIn(links.Sources), lookup.Definition) ||
        ChainHoldsWhenComplete(links.Sources, links.Mode, lookup);

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
        sources.Select(source => SourceProblem(source, mode, lookup)).FirstOrDefault(problem => problem is not null);

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

    // A ranking source has no select catalog to read; a data-list or loaded source's list is not known here, nor a
    // copy's that does not resolve (a cycle, a data list up the chain); a visibility rule makes the source's visible
    // choices depend on more than its answer.
    private static UnknownBecause? SourceProblem((string Name, JsonElement Element) source, CopyMode mode, Lookup lookup)
    {
        if (SurveyJsElementType.Ranking.Matches(source.Element.GetSurveyJsType()))
        {
            return UnknownBecause.SourceIsARanking;
        }

        if (HasChoiceVisibilityRule(source.Element))
        {
            return UnknownBecause.SourceChoicesVisibleIf;
        }

        return ChoiceSource.Parse(source.Element) switch
        {
            ChoiceSource.Inline => null,
            ChoiceSource.CarryForward when lookup.Offered.Offered(source.Element).IsDegraded => UnknownBecause.SourceCopyIsNotExact,
            ChoiceSource.CarryForward => CopiedSourceProblem(source, mode, lookup),
            _ => UnknownBecause.SourceIsNotOwnChoices,
        };
    }

    private static bool HasChoiceVisibilityRule(JsonElement source) =>
        source.TryGetPropertyValue(SurveyJsPropertyNames.ChoicesVisibleIf) is not null ||
        SurveyJsChoiceHelper.EnumerateChoiceElements(source).Any(choice =>
            choice.Element.ValueKind == JsonValueKind.Object &&
            choice.Element.TryGetPropertyValue(SurveyJsPropertyNames.VisibleIf) is not null);

    // A copy that reads its copied sources' lists holds only when their rules do; a selected copy reads only picks.
    private static bool ChainHoldsWhenComplete(
        IReadOnlyList<(string Name, JsonElement Element)> sources,
        CopyMode mode,
        Lookup lookup) =>
        mode != CopyMode.Selected &&
        SelectsIn(sources).Any(source => lookup.SourceRule(source) is { WhenComplete: true });

    // A selected copy needs only the source's picks. The other modes rebuild the source's list from its own rule, which
    // is stored only on a checkbox or tagbox. A cycle never gets here: ChoiceSources leaves every copy on it degraded.
    private static UnknownBecause? CopiedSourceProblem((string Name, JsonElement Element) source, CopyMode mode, Lookup lookup) =>
        mode == CopyMode.Selected ||
        (source.Element.IsMultiSelectBaseSelect() && lookup.SourceRule(source) is not null)
            ? null
            : UnknownBecause.SourceCopyIsNotExact;

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
}
