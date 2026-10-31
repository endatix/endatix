using Endatix.Modules.Reporting.Contracts.Export;

namespace Endatix.Modules.Reporting.Features.Export;

/// <summary>
/// Plan for exporting columns.
/// </summary>
internal sealed class ExportColumnPlan(
    IReadOnlyList<ExportColumnDefinition> columns,
    OmittedColumnKeys omittedKeys) : IExportColumnPlan
{
    public IReadOnlyList<ExportColumnDefinition> Columns { get; } = columns;

    /// <summary>The audience and submitter profile columns left out because a question of the form exports under their key.</summary>
    public OmittedColumnKeys OmittedKeys { get; } = omittedKeys;
}

/// <summary>The keys of the audience and submitter profile columns a plan left out.</summary>
internal sealed record OmittedColumnKeys(IReadOnlyList<string> Audience, IReadOnlyList<string> SubmitterProfile);
