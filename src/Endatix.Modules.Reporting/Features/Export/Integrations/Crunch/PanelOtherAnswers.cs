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
            AnswerScope.Root(Submission).WithinPanel(column) is not { } scope || scope.Own.TryGetPropertyValue(column.SourceQuestion!) is not { } stored ||
            !JsonElement.DeepEquals(stored, raw))
        {
            return (null, literal);
        }

        if (SubmittedChoices(question) is not { } reader)
        {
            return (null, literal);
        }

        return ReadScoped(reader, (scope, column.SourceQuestion!), literal);
    }

    private (JsonElement?, JsonElement?) ReadScoped(SelectChoices reader, (AnswerScope Scope, string Name) source, JsonElement? literal)
    {
        var (scope, name) = source;
        var answer = reader.Read(scope.Own, name);
        var (category, text) = Classify(reader, answer, literal);
        return reader.OtherFrom is not null && answer.Has(SurveyJsPropertyNames.Other) && !CopyVisibility.IsText(text)
            ? (category, Definition!.CopiedText(scope, name) ?? text) : (category, text);
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

}
