using System.Text.Json;
using Endatix.Modules.Reporting.Configuration;

namespace Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

internal sealed record SchemaCompilationLimits
{
    public int MaxNestingDepth { get; init; } = 5;

    public int MaxPanelCount { get; init; } = 10;

    public int MaxMatrixRowCount { get; init; } = 10;

    public int MaxQuestions { get; init; } = ReportingOptions.DEFAULT_MAX_QUESTIONS;

    public int MaxChoicesPerQuestion { get; init; } = 2_500;

    public int MaxColumns { get; init; } = 16_000;

    /// <summary>
    /// Maximum nested-loop choice combinations per panel path before column emission.
    /// </summary>
    public int MaxLoopCombinations { get; init; } = 10_000;

    public static SchemaCompilationLimits Default { get; } = new();

    // Only the check supplies a collector. The compiler still fails at the first limit.
    internal Action<JsonElement, SchemaCompilationLimitExceededException>? OnIncomplete { get; init; }

    internal bool RunBranch(JsonElement element, Action emit)
    {
        try
        {
            emit();
            return true;
        }
        catch (SchemaCompilationLimitExceededException limit) when (OnIncomplete is not null)
        {
            OnIncomplete(element, limit);
            return false;
        }
    }
}
