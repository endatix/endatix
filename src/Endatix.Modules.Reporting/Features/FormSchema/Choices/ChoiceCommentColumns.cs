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
        foreach (var choice in SurveyJsChoiceHelper.EnumerateChoiceElements(question).Where(choice => HasCommentBox(choice.Element)))
        {
            yield return new FormSchemaColumn(
                ExportPathBuilder.Join(key, choice.Value, SurveyJsPropertyNames.Comment),
                FormSchemaColumnKind.ChoiceComment, $"{question.GetSurveyJsTitle(name)} — {choice.Text} — Comment",
                "string", SourceQuestion: name, ChoiceValue: choice.Value, LoopPath: loopPath);
        }
    }

    internal static bool HasCommentBox(JsonElement choice) =>
        choice.ValueKind == JsonValueKind.Object && choice.GetBooleanProperty(SurveyJsPropertyNames.ShowCommentArea);

    private static bool SupportsChoiceComments(JsonElement question)
    {
        var type = question.GetSurveyJsType();
        return SurveyJsElementType.Checkbox.Matches(type) || SurveyJsElementType.Tagbox.Matches(type) ||
            SurveyJsElementType.Radiogroup.Matches(type) || SurveyJsElementType.Dropdown.Matches(type);
    }
}
