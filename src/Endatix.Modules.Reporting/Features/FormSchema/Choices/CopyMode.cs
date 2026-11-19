using System.Text.Json;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Choices;

/// <summary>Which of its sources' choices a copy shows: SurveyJS <c>choicesFromQuestionMode</c>, Hub <c>edxCarryForwardMode</c>.</summary>
internal enum CopyMode
{
    All,
    Selected,
    Unselected,
}

/// <summary>Reads a copy's <see cref="CopyMode"/> from its definition and writes it to and from the codebook.</summary>
internal static class CopyModes
{
    // SurveyJS knows the three exact values and defaults to all. Hub also accepts its loop labels, trimmed and in any
    // case, and defaults to all; an unknown string Hub would read as all stays unknown here rather than a guess.
    public static CopyMode? Of(JsonElement question, bool fromQuestion) =>
        fromQuestion
            ? SurveyJsMode(question.TryGetPropertyValue(SurveyJsPropertyNames.ChoicesFromQuestionMode))
            : HubMode(question.TryGetPropertyValue(SurveyJsPropertyNames.CarryForwardMode));

    private static CopyMode? SurveyJsMode(JsonElement? mode) =>
        mode switch
        {
            null or { ValueKind: JsonValueKind.Null } => CopyMode.All,
            { ValueKind: JsonValueKind.String } text => ParseExact(text.GetString()),
            _ => null,
        };

    private static CopyMode? HubMode(JsonElement? mode)
    {
        if (mode is not { ValueKind: JsonValueKind.String } text || string.IsNullOrWhiteSpace(text.GetString()))
        {
            return CopyMode.All;
        }

        return text.GetString()!.Trim().ToLowerInvariant() switch
        {
            "selected only" => CopyMode.Selected,
            "unselected only" => CopyMode.Unselected,
            var label => ParseExact(label),
        };
    }

    public static CopyMode? ParseExact(string? mode) =>
        mode switch
        {
            "all" => CopyMode.All,
            "selected" => CopyMode.Selected,
            "unselected" => CopyMode.Unselected,
            _ => null,
        };

    public static string ToJsonName(CopyMode mode) =>
        mode switch
        {
            CopyMode.Selected => "selected",
            CopyMode.Unselected => "unselected",
            _ => "all",
        };
}
