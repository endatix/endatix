using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Choices;

/// <summary>One answer scope in the submitted definition. Loop iterations share their panel template's rules.</summary>
internal sealed class SubmittedChoiceScope
{
    private readonly Dictionary<string, SubmittedChoiceScope> _panels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _conditions = new(StringComparer.Ordinal);

    public SubmittedChoiceScope Panel(string name)
    {
        if (!_panels.TryGetValue(name, out var panel))
        {
            panel = new SubmittedChoiceScope();
            _panels.Add(name, panel);
        }

        return panel;
    }

    public void Note(string name, JsonElement question)
    {
        if (!question.IsMultiSelectBaseSelect() || ChoiceSource.Parse(question) is ChoiceSource.CarryForward)
        {
            return;
        }

        if (!_conditions.TryGetValue(name, out var values))
        {
            values = new(StringComparer.Ordinal);
            _conditions.Add(name, values);
        }

        values.UnionWith(ConditionalChoices.ValuesIn(question));
    }

    public bool? IsConditional(IReadOnlyList<LoopSegment> path, string name, string value)
    {
        var scope = this;
        foreach (var segment in path)
        {
            if (!scope._panels.TryGetValue(segment.PanelValueName, out scope))
            {
                return null;
            }
        }

        return scope._conditions.GetValueOrDefault(name)?.Contains(value);
    }
}
