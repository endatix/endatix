namespace Endatix.Modules.Reporting.Domain.SurveyJs;

internal sealed class FlatteningLimits
{
    public int MaxNestingDepth { get; init; } = 5;

    public int MaxPanelCount { get; init; } = 10;

    public int MaxQuestions { get; init; } = 1_500;

    public int MaxChoicesPerQuestion { get; init; } = 2_500;

    public int MaxColumns { get; init; } = 16_000;

    public static FlatteningLimits Default { get; } = new();
}
