using Endatix.Modules.Reporting.Contracts.Export;

namespace Endatix.Modules.Reporting.Features.Export;

/// <summary>
/// Plan for exporting columns.
/// </summary>
internal sealed class ExportColumnPlan(IReadOnlyList<ExportColumnDefinition> columns) : IExportColumnPlan
{
    public IReadOnlyList<ExportColumnDefinition> Columns { get; } = columns;
}
