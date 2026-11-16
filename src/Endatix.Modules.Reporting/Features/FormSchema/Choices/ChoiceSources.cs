using System.Text.Json;
using Endatix.Modules.Reporting.Domain.SurveyJs;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Choices;

/// <summary>One choice a question can offer, with the definition element its labels are read from.</summary>
internal readonly record struct SourceChoice(string Value, string Text, JsonElement Element);

/// <summary>A question's <see cref="ChoiceSource"/> and the choices it resolves to in one definition.</summary>
/// <param name="Source">Where the choices come from.</param>
/// <param name="Choices">Every choice the question can offer, or null when the definition cannot tell them.</param>
internal sealed record ResolvedChoices(ChoiceSource Source, IReadOnlyList<SourceChoice>? Choices)
{
    /// <summary>The choices belong to other questions, so their labels are read on those questions.</summary>
    public bool IsCopied => Source is ChoiceSource.CarryForward && Choices is not null;

    /// <summary>A single select whose choices cannot be read exports its answers as text.</summary>
    public bool IsDegraded => Choices is null;

    /// <summary>Writes <c>choiceSource</c> for a source that is not inline, and <c>degraded</c> when it is set.</summary>
    public void WriteTo(Utf8JsonWriter writer)
    {
        Source.WriteTo(writer);
        if (IsDegraded)
        {
            writer.WriteString(FormSchemaCodebookPropertyNames.Degraded, FormSchemaCodebookPropertyNames.DegradedToText);
        }
    }
}

/// <summary>
/// COMPILE TIME. Resolves a single select's choices within one definition. Choices copied from other questions are
/// the union of every source's own choices, in source order, the first occurrence of a value winning; a source that
/// copies its own recurses. A data list (until #1160), an unresolvable source, a missing source or a cycle leaves the
/// choices unknown. A multi-select keeps its inline choices until #1441 gives its copied choices indicator columns.
/// </summary>
internal sealed class ChoiceSources(IReadOnlyDictionary<string, JsonElement> questionsByName)
{
    public ResolvedChoices Resolve(JsonElement question)
    {
        var source = ChoiceSource.Parse(question);
        if (source is ChoiceSource.Inline || !question.IsSingleSelectBaseSelect())
        {
            return new ResolvedChoices(source, Inline(question));
        }

        HashSet<string> visiting = new(StringComparer.Ordinal);
        if (question.GetSurveyJsName() is { } name)
        {
            visiting.Add(name);
        }

        return new ResolvedChoices(source, ChoicesOf(source, question, visiting));
    }

    private List<SourceChoice>? ChoicesOf(ChoiceSource source, JsonElement question, HashSet<string> visiting) =>
        source switch
        {
            ChoiceSource.Inline => Inline(question),
            ChoiceSource.CarryForward copy => Union(copy.Sources, visiting),
            _ => null,
        };

    private List<SourceChoice>? Union(IReadOnlyList<string> sourceNames, HashSet<string> visiting)
    {
        List<SourceChoice> union = [];
        HashSet<string> values = new(StringComparer.Ordinal);
        foreach (var name in sourceNames)
        {
            if (CopiedFrom(name, visiting) is not { } choices)
            {
                return null;
            }

            union.AddRange(choices.Where(choice => values.Add(choice.Value)));
        }

        return union;
    }

    private List<SourceChoice>? CopiedFrom(string name, HashSet<string> visiting)
    {
        if (!questionsByName.TryGetValue(name, out var source) || !OffersChoices(source) || !visiting.Add(name))
        {
            return null;
        }

        var choices = ChoicesOf(ChoiceSource.Parse(source), source, visiting);
        visiting.Remove(name);
        return choices;
    }

    private static bool OffersChoices(JsonElement question) =>
        question.IsSingleSelectBaseSelect() ||
        question.IsMultiSelectBaseSelect() ||
        SurveyJsElementType.Ranking.Matches(question.GetSurveyJsType());

    // Only the source's own choices: Hub and SurveyJS skip its Other, None and the other special items.
    private static List<SourceChoice> Inline(JsonElement question) =>
    [
        .. SurveyJsChoiceHelper.EnumerateChoiceElements(question)
            .Select(choice => new SourceChoice(choice.Value, choice.Text, choice.Element)),
    ];
}
