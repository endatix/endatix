using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Abstractions.Exporting;

/// <summary>
/// Metadata keys used when streaming exports from the reporting read model.
/// </summary>
public static class SubmissionExportMetadataKeys
{
    public const string ColumnPlan = "SubmissionExportColumnPlan";
}

/// <summary>
/// One column in a schema-driven submission export plan.
/// </summary>
public sealed record SubmissionExportColumnPlanEntry(
    string CanonicalKey,
    string ExportKey,
    string Source,
    string? HeaderLabel,
    string? DataType);

/// <summary>
/// Ordered export columns built once per export request.
/// </summary>
public sealed record SubmissionExportColumnPlan(
    IReadOnlyList<SubmissionExportColumnPlanEntry> Columns);

/// <summary>
/// Optional reporting-module export provider. Registered when the reporting module is enabled.
/// </summary>
public interface ISubmissionExportReadModelProvider
{
    Task<Result<SubmissionExportColumnPlan>> PrepareSubmissionExportAsync(
        long tenantId,
        long formId,
        CancellationToken cancellationToken);

    IAsyncEnumerable<SubmissionExportRow> StreamSubmissionExportRowsAsync(
        long tenantId,
        long formId,
        int? exportPageSize,
        CancellationToken cancellationToken);

    Task<Result<string>> GenerateReportingCodebookJsonAsync(
        long tenantId,
        long formId,
        CancellationToken cancellationToken);
}
