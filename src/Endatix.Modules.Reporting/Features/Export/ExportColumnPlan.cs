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

    /// <summary>The audience columns left out because a question of the form exports under their key.</summary>
    public IReadOnlyList<string> OmittedAudienceKeys { get; } = omittedAudienceKeys;
}
