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
/// always hides.
/// </summary>
internal sealed record CopyDefinition(
    IReadOnlySet<string> SharedNames,
    bool ClearsHiddenAnswersOnComplete,
    bool HasVisibleIf,
    IReadOnlySet<string> ContainerVisibleIfNames,
    IReadOnlySet<string> AlwaysHiddenNames)
{
    private const string ClearInvisibleValues = "clearInvisibleValues";
    private const string OnComplete = "onComplete";
    private const string Default = "default";

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

    // SurveyJS clears a hidden question's answer on complete by default (clearInvisibleValues "onComplete"); "none",
    // "onHidden" and "onHiddenContainer" keep or clear it at other times.
    /// <summary>
    /// Whether <paramref name="question"/> keeps its answer while hidden: its own <c>clearIfInvisible</c>, or the survey's
    /// <c>clearInvisibleValues</c> when it has none or <c>default</c>.
    /// </summary>
    public bool KeepsHiddenAnswer(JsonElement question) =>
        question.GetNonEmptyStringProperty(SurveyJsPropertyNames.ClearIfInvisible) is { } own && own != Default
            ? own != OnComplete
            : !ClearsHiddenAnswersOnComplete;

    /// <summary>
    /// Whether <paramref name="question"/> shows only under a <c>visibleIf</c>: its own, or one on an enclosing panel,
    /// dynamic panel or page.
    /// </summary>
    public bool UnderAVisibleIf(JsonElement question, string? name) =>
        question.GetNonEmptyStringProperty(SurveyJsPropertyNames.VisibleIf) is not null ||
        name is not null && ContainerVisibleIfNames.Contains(name);

    /// <summary>
    /// Whether a respondent can miss <paramref name="question"/>: a <c>visibleIf</c> (<see cref="UnderAVisibleIf"/>), or a
    /// static <c>visible: false</c> on it or on an enclosing panel or page.
    /// </summary>
    public bool CanHide(JsonElement question, string? name) =>
        UnderAVisibleIf(question, name) ||
        question.TryGetPropertyValue(SurveyJsPropertyNames.Visible) is { ValueKind: JsonValueKind.False } ||
        name is not null && AlwaysHiddenNames.Contains(name);

    private static bool ClearsOnComplete(JsonElement definition) =>
        definition.TryGetPropertyValue(ClearInvisibleValues) switch
        {
            null or { ValueKind: JsonValueKind.Null } => true,
            { ValueKind: JsonValueKind.String } mode => mode.GetString() == OnComplete,
            _ => false,
        };

    // Counts the data names, notes any visibleIf on a page or on an element at any depth (panels, loops and matrix
    // detail panels too), and
    // collects the data names under a container visibleIf and those under a static visible: false.
    private sealed class DefinitionWalk
    {
        public Dictionary<string, int> Counts { get; } = new(StringComparer.Ordinal);

        public HashSet<string> AlwaysHidden { get; } = new(StringComparer.Ordinal);

        public HashSet<string> UnderVisibleIf { get; } = new(StringComparer.Ordinal);

        public bool HasVisibleIf { get; private set; }

        public void Visit(JsonElement container) => Visit(container, default);

        private void Visit(JsonElement container, Hiding hiding)
        {
            if (container.TryGetArrayProperty(SurveyJsPropertyNames.Pages, out var pages))
            {
                foreach (var page in pages.EnumerateArray())
                {
                    Visit(page, hiding.Inside(page, HasVisibleIfNoted(page)));
                }
            }

            VisitElements(container, SurveyJsPropertyNames.Elements, hiding);
            VisitElements(container, SurveyJsPropertyNames.TemplateElements, hiding);
            VisitElements(container, SurveyJsPropertyNames.DetailElements, hiding);
        }

        private void VisitElements(JsonElement container, string property, Hiding hiding)
        {
            if (!container.TryGetArrayProperty(property, out var elements))
            {
                return;
            }

            foreach (var element in elements.EnumerateArray())
            {
                var inside = hiding.Inside(element, HasVisibleIfNoted(element));
                if (!SurveyJsElementType.IsNonData(element.GetSurveyJsType()) && element.GetSurveyJsName() is { } name)
                {
                    Counts[name] = Counts.GetValueOrDefault(name) + 1;
                    Note(AlwaysHidden, name, inside.Always);
                    Note(UnderVisibleIf, name, hiding.ByVisibleIf);
                }

                Visit(element, inside);
            }
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
    private readonly record struct Hiding(bool Always, bool ByVisibleIf)
    {
        public Hiding Inside(JsonElement container, bool visibleIf) =>
            new(
                Always || container.TryGetPropertyValue(SurveyJsPropertyNames.Visible) is { ValueKind: JsonValueKind.False },
                ByVisibleIf || visibleIf);
    }
}
