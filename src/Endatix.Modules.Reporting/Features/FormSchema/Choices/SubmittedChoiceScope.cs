using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Choices;

/// <summary>One answer scope in the submitted definition. Loop iterations share their panel template's rules.</summary>
internal sealed class SubmittedChoiceScope
{
    private readonly Dictionary<string, SubmittedChoiceScope> _panels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, bool>> _conditions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, JsonElement?> _singleChoices = new(StringComparer.Ordinal);

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
        if (question.IsSingleSelectBaseSelect() && !_singleChoices.TryAdd(name, question))
        {
            _singleChoices[name] = null;
        }

        NoteConditions(name, question);
    }

    private void NoteConditions(string name, JsonElement question)
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

        var all = ConditionalChoices.HasListCondition(question);
        foreach (var item in ChoiceSet.Read(question)!.Items)
        {
            values[item.Value] = values.GetValueOrDefault(item.Value) ||
                (item.Kind == ChoiceKind.Choice && (all || ConditionalChoices.HasItemCondition(item.LabelSource)));
        }
    }

    public JsonElement? PanelQuestion(FormSchemaColumn column)
    {
        var path = column.LoopPath?.Select(segment => segment.PanelValueName) ?? [column.PanelName!];
        var scope = this;
        foreach (var panel in path)
        {
            if (!scope._panels.TryGetValue(panel, out scope))
            {
                return null;
            }
        }

        return scope._singleChoices.GetValueOrDefault(column.SourceQuestion!);
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

        // A merge retains removed columns. Only an offered value can override its catalog condition.
        return scope._conditions.TryGetValue(name, out var values) && values.TryGetValue(value, out var conditional)
            ? conditional
            : null;
    }
}
