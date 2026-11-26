using System.Text.Json;
using Endatix.Modules.Reporting.Domain.SurveyJs;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Choices;

internal static class ChoiceCommentColumns
{
    public static IEnumerable<FormSchemaColumn> For(JsonElement question, string key, IReadOnlyList<LoopSegment>? loopPath = null)
    {
        if (!SupportsChoiceComments(question) || ChoiceSource.Parse(question) is not ChoiceSource.Inline)
        {
            yield break;
        }

        var name = question.GetSurveyJsName()!;
        foreach (var (value, text, _) in SurveyJsChoiceHelper.EnumerateChoiceElements(question).Where(choice => HasCommentBox(choice.Element)))
        {
            yield return new FormSchemaColumn(
                ExportPathBuilder.Join(key, value, SurveyJsPropertyNames.Comment),
                FormSchemaColumnKind.ChoiceComment, $"{question.GetSurveyJsTitle(name)} — {text} — Comment",
                "string", SourceQuestion: name, ChoiceValue: value, LoopPath: loopPath);
        }
    }

    internal static bool HasCommentBox(JsonElement choice) =>
        choice.ValueKind == JsonValueKind.Object && choice.GetBooleanProperty(SurveyJsPropertyNames.ShowCommentArea);

    // A historical key can outlive its question. Only the submitted definition identifies what its object means.
    public static bool CanRead(JsonElement question, FormSchemaColumn column, JsonElement? answer) =>
        question.GetSurveyJsName() == column.SourceQuestion &&
        SupportsChoiceComments(question) && ChoiceSource.Parse(question) is ChoiceSource.Inline &&
        IsSupportedAnswer(question, answer) &&
        SurveyJsChoiceHelper.EnumerateChoiceElements(question).Any(choice => choice.Value == column.ChoiceValue && HasCommentBox(choice.Element));

    private static bool IsSupportedAnswer(JsonElement question, JsonElement? answer) =>
        question.IsMultiSelectBaseSelect()
            ? answer is { ValueKind: JsonValueKind.Array } &&
              question.GetNonEmptyStringProperty(SurveyJsPropertyNames.ValuePropertyName) is null or SurveyJsPropertyNames.Value
            : answer is { ValueKind: JsonValueKind.Object };

    private static bool SupportsChoiceComments(JsonElement question)
    {
        var type = question.GetSurveyJsType();
        return SurveyJsElementType.Checkbox.Matches(type) || SurveyJsElementType.Tagbox.Matches(type) ||
            SurveyJsElementType.Radiogroup.Matches(type) || SurveyJsElementType.Dropdown.Matches(type);
    }
}
