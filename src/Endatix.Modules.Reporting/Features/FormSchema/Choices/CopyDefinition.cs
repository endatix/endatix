using System.Text.Json;
using Endatix.Modules.Reporting.Domain.SurveyJs;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Choices;

/// <summary>
/// COMPILE TIME. What a copy's <see cref="CopyVisibility"/> needs from the whole definition: the question names it
/// uses more than once (a root question and a loop's, or two loops'), whose one choice catalog cannot tell which
/// question a respondent saw, whether the survey clears a hidden question's answer when it is completed, whether any
/// page, panel or question has a <c>visibleIf</c>, the questions a <c>visibleIf</c> on an enclosing panel, dynamic panel
/// or page can hide, and the questions a static <c>visible: false</c> on themselves or on an enclosing panel or page
/// always hides. At run time the same walk lists the question names of the definition a submission was made on
/// (<see cref="QuestionsIn"/>).
/// </summary>
internal sealed record CopyDefinition(
    IReadOnlySet<string> SharedNames,
    bool ClearsHiddenAnswersOnComplete,
    bool HasVisibleIf,
    IReadOnlySet<string> ContainerVisibleIfNames,
    IReadOnlySet<string> AlwaysHiddenNames)
{
    private const string CLEAR_INVISIBLE_VALUES = "clearInvisibleValues";
    private const string ON_COMPLETE = "onComplete";
    private const string DEFAULT = "default";

    /// <summary>No definition read: no rule may rely on hidden answers being cleared.</summary>
    public static CopyDefinition None { get; } =
        new(
            new HashSet<string>(StringComparer.Ordinal),
            false,
            false,
            new HashSet<string>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal));

    public static CopyDefinition Of(JsonElement definition)
    {
        DefinitionWalk walk = new();
        walk.Visit(definition);
        return new CopyDefinition(
            walk.Counts.Where(count => count.Value > 1).Select(count => count.Key).ToHashSet(StringComparer.Ordinal),
            ClearsOnComplete(definition),
            walk.HasVisibleIf,
            walk.UnderVisibleIf,
            walk.AlwaysHidden);
    }

    /// <summary>
    /// RUN TIME. Every question name (<c>name</c>, not <c>valueName</c>, as the indicator columns are keyed) in
    /// <paramref name="definitionJson"/>, in pages, panels, loops and matrix detail panels, and those that show an Other
    /// of their own; null when there is no definition to read, or one that is not a JSON object, so an unreadable stored
    /// definition never fails a flatten.
    /// </summary>
    public static SubmittedQuestions? QuestionsIn(string? definitionJson)
    {
        if (string.IsNullOrWhiteSpace(definitionJson))
        {
            return null;
        }

        try
        {
            var definition = JsonSerializer.Deserialize<JsonElement>(definitionJson);
            return definition.ValueKind == JsonValueKind.Object ? NamesIn(definition) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static SubmittedQuestions NamesIn(JsonElement definition)
    {
        Dictionary<string, JsonElement> questions = new(StringComparer.Ordinal);
        SubmittedChoiceScope choices = new();
        DefinitionWalk walk = new(questions, choices);
        walk.Visit(definition);
        return new(walk.Counts, new ChoiceSources(questions), SurveyChoiceDefaults.Of(definition), choices);
    }

    // SurveyJS clears a hidden question's answer on complete by default (clearInvisibleValues "onComplete"); "none",
    // "onHidden" and "onHiddenContainer" keep or clear it at other times.
    /// <summary>
    /// Whether <paramref name="question"/> keeps its answer while hidden: its own <c>clearIfInvisible</c>, or the survey's
    /// <c>clearInvisibleValues</c> when it has none or <c>default</c>.
    /// </summary>
    public bool KeepsHiddenAnswer(JsonElement question) =>
        question.GetNonEmptyStringProperty(SurveyJsPropertyNames.ClearIfInvisible) is { } own && own != DEFAULT
            ? own != ON_COMPLETE
            : !ClearsHiddenAnswersOnComplete;

    /// <summary>
    /// Whether <paramref name="question"/> shows only under a <c>visibleIf</c>: its own, or one on an enclosing panel,
    /// dynamic panel or page.
    /// </summary>
    public bool UnderAVisibleIf(JsonElement question, string? name) =>
        question.GetNonEmptyStringProperty(SurveyJsPropertyNames.VisibleIf) is not null ||
        (name is not null && ContainerVisibleIfNames.Contains(name));

    /// <summary>
    /// Whether a respondent can miss <paramref name="question"/>: a <c>visibleIf</c> (<see cref="UnderAVisibleIf"/>), or a
    /// static <c>visible: false</c> on it or on an enclosing panel or page (<see cref="AlwaysHiddenNames"/>). A name the definition uses twice can be hidden:
    /// only one of its questions is read here, and the other's own <c>visibleIf</c> is not.
    /// </summary>
    public bool CanHide(JsonElement question, string? name) =>
        (name is not null && SharedNames.Contains(name)) ||
        UnderAVisibleIf(question, name) ||
        (name is not null && AlwaysHiddenNames.Contains(name));

    private static bool ClearsOnComplete(JsonElement definition) =>
        definition.TryGetPropertyValue(CLEAR_INVISIBLE_VALUES) switch
        {
            null or { ValueKind: JsonValueKind.Null } => true,
            { ValueKind: JsonValueKind.String } mode => mode.GetString() == ON_COMPLETE,
            _ => false,
        };

    // Counts the data names, notes any visibleIf on a page or on an element at any depth (panels, loops and matrix
    // detail panels too), and
    // collects the data names under a container visibleIf and those under a static visible: false.
    private sealed class DefinitionWalk(Dictionary<string, JsonElement>? questions = null, SubmittedChoiceScope? choices = null)
    {
        public Dictionary<string, int> Counts { get; } = new(StringComparer.Ordinal);

        public HashSet<string> AlwaysHidden { get; } = new(StringComparer.Ordinal);

        public HashSet<string> UnderVisibleIf { get; } = new(StringComparer.Ordinal);

        public bool HasVisibleIf { get; private set; }

        public void Visit(JsonElement container) => Visit(container, new WalkContext(false, false, choices));

        private void Visit(JsonElement container, WalkContext context)
        {
            if (container.TryGetArrayProperty(SurveyJsPropertyNames.Pages, out var pages))
            {
                foreach (var page in pages.EnumerateArray())
                {
                    Visit(page, context.Inside(page, HasVisibleIfNoted(page)));
                }
            }

            VisitElements(container, SurveyJsPropertyNames.Elements, context);
            VisitElements(container, SurveyJsPropertyNames.TemplateElements, context.TemplateOf(container));
            VisitElements(container, SurveyJsPropertyNames.DetailElements, context);
        }

        private void VisitElements(JsonElement container, string property, WalkContext context)
        {
            if (!container.TryGetArrayProperty(property, out var elements))
            {
                return;
            }

            foreach (var element in elements.EnumerateArray())
            {
                var inside = context.Inside(element, HasVisibleIfNoted(element));
                if (!SurveyJsElementType.IsNonData(element.GetSurveyJsType()) && element.GetSurveyJsName() is { } name)
                {
                    NoteQuestion(element, name, context with { Always = inside.Always });
                }

                Visit(element, inside);
            }
        }

        // Always is the question's own or an enclosing static visible: false; ByVisibleIf an enclosing visibleIf only.
        private void NoteQuestion(JsonElement question, string name, WalkContext context)
        {
            Counts[name] = Counts.GetValueOrDefault(name) + 1;
            questions?.TryAdd(name, question);
            context.Scope?.Note(name, question);
            Note(AlwaysHidden, name, context.Always);
            Note(UnderVisibleIf, name, context.ByVisibleIf);
        }

        private bool HasVisibleIfNoted(JsonElement element)
        {
            var has = element.GetNonEmptyStringProperty(SurveyJsPropertyNames.VisibleIf) is not null;
            HasVisibleIf |= has;
            return has;
        }

        private static void Note(HashSet<string> names, string name, bool applies)
        {
            if (applies)
            {
                names.Add(name);
            }
        }
    }

    // What hides an element's contents: a static visible: false on it or an enclosing panel or page (Always), or a
    // visibleIf on an enclosing panel, dynamic panel or page (ByVisibleIf).
    private readonly record struct WalkContext(bool Always, bool ByVisibleIf, SubmittedChoiceScope? Scope)
    {
        public WalkContext Inside(JsonElement container, bool visibleIf) =>
            new(
                Always || container.TryGetPropertyValue(SurveyJsPropertyNames.Visible) is { ValueKind: JsonValueKind.False },
                ByVisibleIf || visibleIf, Scope);

        public WalkContext TemplateOf(JsonElement container) =>
            Scope is not null && SurveyJsElementType.PanelDynamic.Matches(container.GetSurveyJsType()) &&
            (container.GetSurveyJsValueName() ?? container.GetSurveyJsName()) is { } name
                ? this with { Scope = Scope.Panel(name) }
                : this;
    }
}

/// <summary>
/// RUN TIME. The questions of the definition a submission was made on (<see cref="CopyDefinition.QuestionsIn"/>): every
/// name, which questions show their own Other, and the source a copy offered on that version.
/// </summary>
internal sealed record SubmittedQuestions(
    IReadOnlyDictionary<string, int> NameCounts,
    ChoiceSources Sources,
    SurveyChoiceDefaults Defaults,
    SubmittedChoiceScope Choices)
{
    private readonly Dictionary<string, SelectChoices> _sourceReaders = new(StringComparer.Ordinal);

    public bool ShowsOwnOther(string name) =>
        NameCounts.GetValueOrDefault(name) == 1 && ChoiceSet.ShowsOther(Sources.QuestionsByName[name]);

    public JsonElement? CopiedText(AnswerScope scope, string question) =>
        OtherSourceOf(question) is { } other
            ? CopyVisibility.CopiedOtherText(other.SourceName, scope, ReaderFor(other))
            : null;

    private SelectChoices ReaderFor(CopiedOther other)
    {
        if (!_sourceReaders.TryGetValue(other.SourceName, out var reader))
        {
            var choices = ChoiceSet.Read(other.Source, Defaults, Sources.Resolve(other.Source))!;
            reader = SelectChoices.FromChoiceSet(choices);
            _sourceReaders.Add(other.SourceName, reader);
        }

        return reader;
    }

    // Two questions with the same name share one catalog. It cannot identify either one's Other.
    private CopiedOther? OtherSourceOf(string name)
    {
        if (NameCounts.GetValueOrDefault(name) != 1 ||
            !Sources.QuestionsByName.TryGetValue(name, out var question) ||
            Sources.Offered(question) is not { Other: { } other } offered ||
            offered.TryGetChoice(SurveyJsPropertyNames.Other, out _) ||
            NameCounts.GetValueOrDefault(other.SourceName) != 1)
        {
            return null;
        }

        return other;
    }
}
