using System.Text.Json;
using Endatix.Modules.Reporting.Domain.SurveyJs;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Choices;

/// <summary>
/// COMPILE TIME. What a copy's <see cref="CopyVisibility"/> needs from the whole definition: the question names it
/// uses more than once (a root question and a loop's, or two loops'), whose one choice catalog cannot tell which
/// question a respondent saw, whether the survey clears a hidden question's answer when it is completed, whether any
/// page, panel or question has a <c>visibleIf</c>, and the questions a static <c>visible: false</c> on themselves or on
/// an enclosing panel or page always hides.
/// </summary>
internal sealed record CopyDefinition(
    IReadOnlySet<string> SharedNames,
    bool ClearsHiddenAnswersOnComplete,
    bool HasVisibleIf,
    IReadOnlySet<string> AlwaysHiddenNames)
{
    private const string ClearInvisibleValues = "clearInvisibleValues";
    private const string OnComplete = "onComplete";

    /// <summary>No definition read: no rule may rely on hidden answers being cleared.</summary>
    public static CopyDefinition None { get; } =
        new(new HashSet<string>(StringComparer.Ordinal), false, false, new HashSet<string>(StringComparer.Ordinal));

    public static CopyDefinition Of(JsonElement definition)
    {
        DefinitionWalk walk = new();
        walk.Visit(definition);
        return new CopyDefinition(
            walk.Counts.Where(count => count.Value > 1).Select(count => count.Key).ToHashSet(StringComparer.Ordinal),
            ClearsOnComplete(definition),
            walk.HasVisibleIf,
            walk.AlwaysHidden);
    }

    // SurveyJS clears a hidden question's answer on complete by default (clearInvisibleValues "onComplete"); "none",
    // "onHidden" and "onHiddenContainer" keep or clear it at other times.
    private static bool ClearsOnComplete(JsonElement definition) =>
        definition.TryGetPropertyValue(ClearInvisibleValues) switch
        {
            null or { ValueKind: JsonValueKind.Null } => true,
            { ValueKind: JsonValueKind.String } mode => mode.GetString() == OnComplete,
            _ => false,
        };

    // Counts the data names, notes any visibleIf on a page or on an element at any depth (panels and loops too), and
    // collects the data names under a static visible: false.
    private sealed class DefinitionWalk
    {
        public Dictionary<string, int> Counts { get; } = new(StringComparer.Ordinal);

        public HashSet<string> AlwaysHidden { get; } = new(StringComparer.Ordinal);

        public bool HasVisibleIf { get; private set; }

        public void Visit(JsonElement container, bool hidden = false)
        {
            if (container.TryGetArrayProperty(SurveyJsPropertyNames.Pages, out var pages))
            {
                foreach (var page in pages.EnumerateArray())
                {
                    NoteVisibleIf(page);
                    Visit(page, hidden || IsAlwaysHidden(page));
                }
            }

            VisitElements(container, SurveyJsPropertyNames.Elements, hidden);
            VisitElements(container, SurveyJsPropertyNames.TemplateElements, hidden);
        }

        private void VisitElements(JsonElement container, string property, bool hidden)
        {
            if (!container.TryGetArrayProperty(property, out var elements))
            {
                return;
            }

            foreach (var element in elements.EnumerateArray())
            {
                NoteVisibleIf(element);
                var elementHidden = hidden || IsAlwaysHidden(element);
                if (!SurveyJsElementType.IsNonData(element.GetSurveyJsType()) && element.GetSurveyJsName() is { } name)
                {
                    Counts[name] = Counts.GetValueOrDefault(name) + 1;
                    NoteAlwaysHidden(name, elementHidden);
                }

                Visit(element, elementHidden);
            }
        }

        private void NoteVisibleIf(JsonElement element) =>
            HasVisibleIf |= element.GetNonEmptyStringProperty(SurveyJsPropertyNames.VisibleIf) is not null;

        private void NoteAlwaysHidden(string name, bool hidden)
        {
            if (hidden)
            {
                AlwaysHidden.Add(name);
            }
        }

        private static bool IsAlwaysHidden(JsonElement element) =>
            element.TryGetPropertyValue(SurveyJsPropertyNames.Visible) is { ValueKind: JsonValueKind.False };
    }
}
