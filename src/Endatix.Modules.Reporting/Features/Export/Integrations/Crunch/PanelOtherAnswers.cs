using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.Choices;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.Export.Integrations.Crunch;

internal sealed record PanelOtherAnswers(SubmittedQuestions? Definition, JsonElement Submission)
{
    public (JsonElement? Category, JsonElement? Text) Read(FormSchemaColumn column, JsonElement raw)
    {
        var literal = Literal(raw);
        if (literal is null || Definition?.Choices.PanelQuestion(column) is not { } question ||
            Context(column) is not { } context || context.TryGetPropertyValue(column.SourceQuestion!) is not { } stored ||
            !JsonElement.DeepEquals(stored, raw))
        {
            return (null, literal);
        }

        if (SubmittedChoices(question) is not { } reader)
        {
            return (null, literal);
        }

        var answer = reader.Read(context, column.SourceQuestion!);
        return Classify(reader, answer, literal);
    }

    private SelectChoices? SubmittedChoices(JsonElement question)
    {
        var offered = Definition!.Sources.Resolve(question);
        return !offered.IsDegraded && offered.Source is not (ChoiceSource.Unresolvable or ChoiceSource.DataList) &&
            ChoiceSet.Read(question, Definition.Defaults, offered) is { IsMultiple: false } set
            ? SelectChoices.FromChoiceSet(set) : null;
    }

    private static (JsonElement?, JsonElement?) Classify(SelectChoices reader, SelectAnswer answer, JsonElement? literal)
    {
        if (answer.Values.Count != 1 || reader.KindOf(answer.Values[0]) is not { } kind)
        {
            return (null, literal);
        }

        return (answer.SingleValueJson(), kind == ChoiceKind.Other ? answer.OtherText : null);
    }

    internal static JsonElement? Literal(JsonElement raw)
    {
        var value = raw.TryGetPropertyValue(SurveyJsPropertyNames.Value) ?? raw;
        return value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String when value.GetString() == string.Empty => null,
            JsonValueKind.String => value,
            _ => JsonSerializer.SerializeToElement(value.GetRawText()),
        };
    }

    private JsonElement? Context(FormSchemaColumn column)
    {
        if (column.Kind is FormSchemaColumnKind.NestedLoop)
        {
            return AnswerScope.Root(Submission).Within(column.LoopPath)?.Own;
        }

        return column.PanelIndex is >= 0 && column.PanelName is { } name &&
            Submission.TryGetArrayProperty(name, out var items) && column.PanelIndex < items.GetArrayLength()
            ? items[column.PanelIndex.Value] : null;
    }
}
