using System.Text.Json;
using Endatix.Modules.Reporting.Domain.SurveyJs;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Choices;

/// <summary>One choice a question can offer, with the definition element its labels are read from.</summary>
internal readonly record struct SourceChoice(string Value, string Text, JsonElement Element);

/// <summary>A question's <see cref="ChoiceSource"/> and the choices it publishes in one definition.</summary>
internal sealed class ResolvedChoices
{
    private readonly IReadOnlyList<SourceChoice>? _choices;
    private Dictionary<string, SourceChoice>? _choicesByValue;

    private ResolvedChoices(ChoiceSource source, IReadOnlyList<SourceChoice>? choices, bool isCopied)
    {
        Source = source;
        _choices = choices;
        IsCopied = isCopied;
    }

    public ChoiceSource Source { get; }

    /// <summary>Every choice the question can offer; empty when <see cref="IsDegraded"/>.</summary>
    public IReadOnlyList<SourceChoice> Choices => _choices ?? [];

    /// <summary>The choices belong to other questions, so their labels are read on those questions.</summary>
    public bool IsCopied { get; }

    /// <summary>A copy with a source the codebook cannot read: its answers export as text.</summary>
    public bool IsDegraded => _choices is null;

    /// <summary>The question's own choices, whatever its source says.</summary>
    public static ResolvedChoices Own(ChoiceSource source, IReadOnlyList<SourceChoice> choices) => new(source, choices, false);

    /// <summary>Choices copied from other questions; null when a source cannot be read.</summary>
    public static ResolvedChoices Copied(ChoiceSource source, IReadOnlyList<SourceChoice>? choices) =>
        new(source, choices, choices is not null);

    /// <summary>The first offered choice with this value; each column's label reads one, so the lookup is built once.</summary>
    public bool TryGetChoice(string value, out SourceChoice choice)
    {
        if (_choicesByValue is null)
        {
            _choicesByValue = new(StringComparer.Ordinal);
            foreach (var offered in Choices)
            {
                _choicesByValue.TryAdd(offered.Value, offered);
            }
        }

        return _choicesByValue.TryGetValue(value, out choice);
    }

    /// <summary>Writes <c>choiceSource</c> for a source that is not inline, and <c>degraded</c> when it is set.</summary>
    public void WriteTo(Utf8JsonWriter writer)
    {
        Source.WriteTo(writer);
        if (IsDegraded)
        {
            writer.WriteString(FormSchemaCodebookPropertyNames.Degraded, FormSchemaCodebookPropertyNames.DegradedToText);
        }
    }
}

/// <summary>
/// COMPILE TIME. Resolves a question's choices within one definition. A question that copies choices
/// (<see cref="ChoiceSource.CarryForward"/>) offers the union of every source's own choices, in source order, the
/// first occurrence of a value winning; a source that copies its own is followed. A missing source or one that is not
/// a select or a ranking is skipped, as Hub skips it. A source that is a data list (until #1160), loads its choices
/// from a URL or from answers, or closes a cycle leaves the copy degraded, and so does a copy with no choices at all.
/// <see cref="Resolve"/> applies this to a base select, single or multiple; <see cref="Offered"/> to any select or
/// ranking.
/// </summary>
internal sealed class ChoiceSources(IReadOnlyDictionary<string, JsonElement> questionsByName, CopyDefinition? definition = null)
{
    private readonly Dictionary<string, List<SourceChoice>?> _resolvedSources = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ResolvedChoices> _resolvedCopies = new(StringComparer.Ordinal);

    /// <summary>The choices a base select's columns and categories use; any other question's own choices.</summary>
    public ResolvedChoices Resolve(JsonElement question) =>
        question.IsSingleSelectBaseSelect() || question.IsMultiSelectBaseSelect()
            ? Offered(question)
            : ResolvedChoices.Own(ChoiceSource.Parse(question), OwnChoices(question));

    /// <summary>Every choice a select or a ranking can offer: its own, or the ones it copies.</summary>
    public ResolvedChoices Offered(JsonElement question)
    {
        var source = ChoiceSource.Parse(question);
        if (source is not ChoiceSource.CarryForward copy || !OffersChoices(question))
        {
            return ResolvedChoices.Own(source, OwnChoices(question));
        }

        // A copy's union depends only on its own name (a cycle back to it) and its sources, and every column of the
        // copy asks for it, so it is resolved once per compile.
        var name = question.GetSurveyJsName();
        var key = string.Join('\0', [name ?? string.Empty, .. copy.Sources]);
        if (!_resolvedCopies.TryGetValue(key, out var resolved))
        {
            HashSet<string> visiting = new(StringComparer.Ordinal);
            if (name is not null)
            {
                visiting.Add(name);
            }

            resolved = ResolvedChoices.Copied(source, Union(copy.Sources, visiting) is { Count: > 0 } union ? union : null);
            _resolvedCopies[key] = resolved;
        }

        return resolved;
    }

    /// <summary>
    /// The values a <c>loopSource</c> driver turns into panels: the <see cref="Offered"/> choices, then Other when the
    /// question shows it. Empty for a question that is neither a select nor a ranking.
    /// </summary>
    public IReadOnlyList<string> OfferedValues(JsonElement question)
    {
        if (!OffersChoices(question))
        {
            return [];
        }

        var offered = Offered(question);
        return ChoiceSet.Read(question, choices: offered) is { } choiceSet
            ? [.. choiceSet.LoopDriverValues]
            : [.. offered.Choices.Select(choice => choice.Value)];
    }

    /// <summary><see cref="OfferedValues(JsonElement)"/> of the question with this name; empty when there is none.</summary>
    public IReadOnlyList<string> OfferedValues(string questionName) =>
        questionsByName.TryGetValue(questionName, out var question) ? OfferedValues(question) : [];

    /// <summary>Writes a copy's catalog fields (<see cref="CopyVisibility.WriteFor"/>).</summary>
    public void WriteCopyFields(Utf8JsonWriter writer, JsonElement question) =>
        CopyVisibility.WriteFor(writer, question, () => CopyVisibility.Of(question, questionsByName, definition ?? CopyDefinition.None));

    private List<SourceChoice>? Union(IReadOnlyList<string> sourceNames, HashSet<string> visiting)
    {
        List<SourceChoice> union = [];
        HashSet<string> values = new(StringComparer.Ordinal);
        foreach (var name in sourceNames)
        {
            if (ChoicesOfSource(name, visiting) is not { } choices)
            {
                return null;
            }

            union.AddRange(choices.Where(choice => values.Add(choice.Value)));
        }

        return union;
    }

    // A source's choices depend only on the definition, so each is resolved once per compile. A question on a cycle
    // resolves to null wherever the walk starts, so its cached null holds for every later walk too.
    private List<SourceChoice>? ChoicesOfSource(string name, HashSet<string> visiting)
    {
        if (_resolvedSources.TryGetValue(name, out var cached))
        {
            return cached;
        }

        if (!questionsByName.TryGetValue(name, out var source) || !OffersChoices(source))
        {
            return [];
        }

        return visiting.Add(name) ? ResolveOnce(name, source, visiting) : null;
    }

    private List<SourceChoice>? ResolveOnce(string name, JsonElement source, HashSet<string> visiting)
    {
        var choices = ChoicesOf(source, visiting);
        visiting.Remove(name);
        _resolvedSources[name] = choices;
        return choices;
    }

    private List<SourceChoice>? ChoicesOf(JsonElement source, HashSet<string> visiting) =>
        ChoiceSource.Parse(source) switch
        {
            ChoiceSource.Inline => OwnChoices(source),
            ChoiceSource.CarryForward copy => Union(copy.Sources, visiting),
            _ => null,
        };

    private static bool OffersChoices(JsonElement question) =>
        question.IsSingleSelectBaseSelect() ||
        question.IsMultiSelectBaseSelect() ||
        SurveyJsElementType.Ranking.Matches(question.GetSurveyJsType());

    // Only the question's own choices: Hub (getChoicesFromSourceQuestion) and SurveyJS skip a source's Other, None and
    // the other special items, which ChoiceSet adds from the destination's own flags.
    private static List<SourceChoice> OwnChoices(JsonElement question) =>
    [
        .. SurveyJsChoiceHelper.EnumerateChoiceElements(question)
            .Select(choice => new SourceChoice(choice.Value, choice.Text, choice.Element)),
    ];
}
