using System.Text.Json;
using Endatix.Modules.Reporting.Domain.SurveyJs;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Choices;

/// <summary>
/// COMPILE TIME. What a copy's <see cref="CopyVisibility"/> needs from the whole definition: the question names it
/// uses more than once (a root question and a loop's, or two loops'), whose one choice catalog cannot tell which
/// question a respondent saw, whether the survey clears a hidden question's answer when it is completed, and whether
/// any page, panel or question has a <c>visibleIf</c>.
/// </summary>
internal sealed record CopyDefinition(IReadOnlySet<string> SharedNames, bool ClearsHiddenAnswersOnComplete, bool HasVisibleIf)
{
    private const string ClearInvisibleValues = "clearInvisibleValues";
    private const string OnComplete = "onComplete";

    /// <summary>No definition read: no rule may rely on hidden answers being cleared.</summary>
    public static CopyDefinition None { get; } = new(new HashSet<string>(StringComparer.Ordinal), false, false);

    public static CopyDefinition Of(JsonElement definition)
    {
        DefinitionWalk walk = new();
        walk.Visit(definition);
        return new CopyDefinition(
            walk.Counts.Where(count => count.Value > 1).Select(count => count.Key).ToHashSet(StringComparer.Ordinal),
            ClearsOnComplete(definition),
            walk.HasVisibleIf);
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

    // Counts the data names, and notes any visibleIf on a page or on an element at any depth (panels and loops too).
    private sealed class DefinitionWalk
    {
        public Dictionary<string, int> Counts { get; } = new(StringComparer.Ordinal);

        public bool HasVisibleIf { get; private set; }

        public void Visit(JsonElement container)
        {
            if (container.TryGetArrayProperty(SurveyJsPropertyNames.Pages, out var pages))
            {
                foreach (var page in pages.EnumerateArray())
                {
                    NoteVisibleIf(page);
                    Visit(page);
                }
            }

            VisitElements(container, SurveyJsPropertyNames.Elements);
            VisitElements(container, SurveyJsPropertyNames.TemplateElements);
        }

        private void VisitElements(JsonElement container, string property)
        {
            if (!container.TryGetArrayProperty(property, out var elements))
            {
                return;
            }

            foreach (var element in elements.EnumerateArray())
            {
                NoteVisibleIf(element);
                if (!SurveyJsElementType.IsNonData(element.GetSurveyJsType()) && element.GetSurveyJsName() is { } name)
                {
                    Counts[name] = Counts.GetValueOrDefault(name) + 1;
                }

                Visit(element);
            }
        }

        private void NoteVisibleIf(JsonElement element) =>
            HasVisibleIf |= element.GetNonEmptyStringProperty(SurveyJsPropertyNames.VisibleIf) is not null;
    }
}
