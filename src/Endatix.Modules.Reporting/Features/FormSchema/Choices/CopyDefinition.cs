using System.Text.Json;
using Endatix.Modules.Reporting.Domain.SurveyJs;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Choices;

/// <summary>
/// COMPILE TIME. What a copy's <see cref="CopyVisibility"/> needs from the whole definition: the question names it
/// uses more than once (a root question and a loop's, or two loops'), whose one choice catalog cannot tell which
/// question a respondent saw, and whether the survey clears a hidden question's answer when it is completed.
/// </summary>
internal sealed record CopyDefinition(IReadOnlySet<string> SharedNames, bool ClearsHiddenAnswersOnComplete)
{
    private const string ClearInvisibleValues = "clearInvisibleValues";
    private const string OnComplete = "onComplete";

    /// <summary>No definition read: no rule may rely on hidden answers being cleared.</summary>
    public static CopyDefinition None { get; } = new(new HashSet<string>(StringComparer.Ordinal), false);

    public static CopyDefinition Of(JsonElement definition)
    {
        Dictionary<string, int> counts = new(StringComparer.Ordinal);
        CountNames(definition, counts);
        return new CopyDefinition(
            counts.Where(count => count.Value > 1).Select(count => count.Key).ToHashSet(StringComparer.Ordinal),
            ClearsOnComplete(definition));
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

    private static void CountNames(JsonElement container, Dictionary<string, int> counts)
    {
        if (container.TryGetArrayProperty(SurveyJsPropertyNames.Pages, out var pages))
        {
            foreach (var page in pages.EnumerateArray())
            {
                CountNames(page, counts);
            }
        }

        CountElements(container, SurveyJsPropertyNames.Elements, counts);
        CountElements(container, SurveyJsPropertyNames.TemplateElements, counts);
    }

    private static void CountElements(JsonElement container, string property, Dictionary<string, int> counts)
    {
        if (!container.TryGetArrayProperty(property, out var elements))
        {
            return;
        }

        foreach (var element in elements.EnumerateArray())
        {
            if (!SurveyJsElementType.IsNonData(element.GetSurveyJsType()) && element.GetSurveyJsName() is { } name)
            {
                counts[name] = counts.GetValueOrDefault(name) + 1;
            }

            CountNames(element, counts);
        }
    }
}
