using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Choices;

/// <summary>One answer scope in the submitted definition. Loop iterations share their panel template's rules.</summary>
internal sealed class SubmittedChoiceScope
{
    private readonly Dictionary<string, SubmittedChoiceScope> _panels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, bool>> _conditions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, JsonElement?> _questionsByAnswerKey = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string?> _answerKeysByName = new(StringComparer.Ordinal);

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
        var key = question.GetNonEmptyStringProperty(SurveyJsPropertyNames.ValueName) ?? name;
        if (!_answerKeysByName.TryAdd(name, key))
        {
            _answerKeysByName[name] = null;
        }

        NoteAnswerKey(key, question);
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

    private void NoteAnswerKey(string key, JsonElement question)
    {
        if (!_questionsByAnswerKey.TryAdd(key, question))
        {
            _questionsByAnswerKey[key] = null;
        }
    }

    public bool? IsConditional(IReadOnlyList<LoopSegment> path, string name, string value)
    {
        var scope = At(path.Select(segment => segment.PanelValueName));
        return scope is not null && scope._conditions.TryGetValue(name, out var values) && values.TryGetValue(value, out var conditional)
            ? conditional
            : null;
    }

    public JsonElement? Question(FormSchemaColumn column)
    {
        var path = column.PanelName is { } panel
            ? [panel]
            : column.LoopPath?.Select(segment => segment.PanelValueName) ?? [];
        var scope = At(path);
        return scope?._answerKeysByName.GetValueOrDefault(column.SourceQuestion!) is { } key
            ? scope._questionsByAnswerKey.GetValueOrDefault(key) : null;
    }

    private SubmittedChoiceScope? At(IEnumerable<string> path)
    {
        var scope = this;
        foreach (var panel in path)
        {
            if (!scope._panels.TryGetValue(panel, out scope))
            {
                return null;
            }
        }

        return scope;
    }
}
