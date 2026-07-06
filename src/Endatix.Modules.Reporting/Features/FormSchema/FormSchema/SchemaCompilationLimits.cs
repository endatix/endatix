namespace Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

internal sealed class SchemaCompilationLimits
{
    public int MaxNestingDepth { get; init; } = 5;

    public int MaxPanelCount { get; init; } = 10;

    public int MaxMatrixRowCount { get; init; } = 10;

    public int MaxQuestions { get; init; } = 1_500;

    public int MaxChoicesPerQuestion { get; init; } = 2_500;

    public int MaxColumns { get; init; } = 16_000;

    /// <summary>
    /// Maximum nested-loop choice combinations per panel path before column emission.
    /// </summary>
    public int MaxLoopCombinations { get; init; } = 10_000;

    public static SchemaCompilationLimits Default { get; } = new();
}

