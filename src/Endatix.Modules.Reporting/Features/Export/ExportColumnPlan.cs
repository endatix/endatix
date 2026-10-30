using Endatix.Modules.Reporting.Contracts.Export;

namespace Endatix.Modules.Reporting.Features.Export;

/// <summary>
/// Plan for exporting columns.
/// </summary>
internal sealed class ExportColumnPlan(
    IReadOnlyList<ExportColumnDefinition> columns,
    IReadOnlyList<string> omittedAudienceKeys) : IExportColumnPlan
{
    public IReadOnlyList<ExportColumnDefinition> Columns { get; } = columns;

    /// <summary>The audience keys a question of the form exports under, so the plan has no audience column for them.</summary>
    public IReadOnlyList<string> OmittedAudienceKeys { get; } = omittedAudienceKeys;
}
