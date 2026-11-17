using System.Text.Json;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Choices;

/// <summary>
/// The answers one cell can see: the submission root, then each loop panel item down to the column's own item. A
/// question is looked up innermost first, so a copy inside a loop reads its source in the same loop item, then outside.
/// </summary>
internal sealed class AnswerScope
{
    private readonly IReadOnlyList<JsonElement> _contexts;

    private AnswerScope(IReadOnlyList<JsonElement> contexts)
    {
        _contexts = contexts;
    }

    /// <summary>The context the column's own answers are in: the root, or the innermost loop item.</summary>
    public JsonElement Own => _contexts[^1];

    /// <summary>The scope of a column; null when the submission has no item for a segment of its loop path.</summary>
    public static AnswerScope? Resolve(JsonElement submission, IReadOnlyList<LoopSegment>? loopPath)
    {
        List<JsonElement> contexts = [submission];
        foreach (var segment in loopPath ?? [])
        {
            if (FindLoopItem(contexts[^1], segment) is not { } item)
            {
                return null;
            }

            contexts.Add(item);
        }

        return new AnswerScope(contexts);
    }

    /// <summary>
    /// The innermost context that holds an answer to <paramref name="question"/>; null when none does. A missing value,
    /// JSON null, an empty string and an empty array are no answer.
    /// </summary>
    public JsonElement? ContextAnswering(string question)
    {
        for (var index = _contexts.Count - 1; index >= 0; index--)
        {
            if (IsAnswer(_contexts[index].TryGetPropertyValue(question)))
            {
                return _contexts[index];
            }
        }

        return null;
    }

    /// <summary>Whether the column's own context answers <paramref name="question"/>.</summary>
    public bool OwnAnswers(string question) => IsAnswer(Own.TryGetPropertyValue(question));

    private static bool IsAnswer(JsonElement? value) =>
        value switch
        {
            null => false,
            { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => false,
            { ValueKind: JsonValueKind.Array } array => array.GetArrayLength() > 0,
            { ValueKind: JsonValueKind.String } text => text.GetString()!.Length > 0,
            _ => true,
        };

    private static JsonElement? FindLoopItem(JsonElement context, LoopSegment segment)
    {
        if (context.TryGetPropertyValue(segment.PanelValueName) is not { ValueKind: JsonValueKind.Array } items)
        {
            return null;
        }

        foreach (var item in items.EnumerateArray())
        {
            if (string.Equals(
                    item.TryGetPropertyValue(segment.PropertyName)?.GetScalarStringValue(),
                    segment.ChoiceValue,
                    StringComparison.Ordinal))
            {
                return item;
            }
        }

        return null;
    }
}
