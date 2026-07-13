using System.Runtime.CompilerServices;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Data;

namespace Endatix.Modules.Reporting.Features.Export;

/// <summary>
/// Provides submission export functionality for the reporting module.
/// </summary>
internal sealed class ReportingSubmissionExportProvider(
    IFormSchemaRepository formSchemaRepository,
    IReportingExportRepository reportingExportRepository) : ISubmissionExportReadModelProvider
{
    private const string MissingSchemaMessage =
        "Form schema has not been compiled for this form. Save or publish the form definition to trigger compilation.";

    private const string MissingRowsMessage =
        "No processed flattened submissions found for this form. Run admin backfill to populate the reporting read model before exporting.";

    /// <summary>
    /// Prepares a submission export plan for a given form.
    /// </summary>
    /// <param name="tenantId">The ID of the tenant.</param>
    /// <param name="formId">The ID of the form.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A result containing the submission export plan.</returns>
    public async Task<Result<SubmissionExportColumnPlan>> PrepareSubmissionExportAsync(
        long tenantId,
        long formId,
        CancellationToken cancellationToken)
    {
        var schema = await formSchemaRepository.GetByFormIdAsync(tenantId, formId, cancellationToken);
        if (schema is null)
        {
            return Result<SubmissionExportColumnPlan>.Error(MissingSchemaMessage);
        }

        var hasRows = await reportingExportRepository.HasExportableRowsAsync(tenantId, formId, cancellationToken);
        if (!hasRows)
        {
            return Result<SubmissionExportColumnPlan>.Error(MissingRowsMessage);
        }

        var plan = ExportColumnPlanBuilder.Build(schema);
        return Result.Success(MapColumnPlan(plan));
    }

    /// <summary>
    /// Streams submission export rows for a given form.
    /// </summary>
    /// <param name="tenantId">The ID of the tenant.</param>
    /// <param name="formId">The ID of the form.</param>
    /// <param name="exportPageSize">The page size for the export.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>An async enumerable of submission export rows.</returns>
    public async IAsyncEnumerable<SubmissionExportRow> StreamSubmissionExportRowsAsync(
        long tenantId,
        long formId,
        int? exportPageSize,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ExportQueryOptions options = new(PageSize: exportPageSize ?? 500);

        await foreach (var row in reportingExportRepository.StreamFlattenedSubmissionsAsync(
                           tenantId,
                           formId,
                           options,
                           cancellationToken))
        {
            yield return MapSubmissionRow(row);
        }
    }

    /// <summary>
    /// Generates a codebook JSON for a given form.
    /// </summary>
    /// <param name="tenantId">The ID of the tenant.</param>
    /// <param name="formId">The ID of the form.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A result containing the codebook JSON.</returns>
    public async Task<Result<string>> GenerateReportingCodebookJsonAsync(
        long tenantId,
        long formId,
        CancellationToken cancellationToken)
    {
        var schema = await formSchemaRepository.GetByFormIdAsync(tenantId, formId, cancellationToken);
        if (schema is null)
        {
            return Result<string>.Error(MissingSchemaMessage);
        }

        var codebookJson = ShojiCodebookGenerator.Generate(schema.FlatteningMap, schema.Codebook);
        return Result.Success(codebookJson);
    }

    private static SubmissionExportColumnPlan MapColumnPlan(IExportColumnPlan plan) =>
        new(plan.Columns.Select(column => new SubmissionExportColumnPlanEntry(
            column.CanonicalKey,
            column.ExportKey,
            column.Source.ToString(),
            column.HeaderLabel,
            column.DataType)).ToList());

    private static SubmissionExportRow MapSubmissionRow(FlattenedExportRow row) =>
        new()
        {
            FormId = row.FormId,
            Id = row.SubmissionId,
            IsComplete = row.IsComplete,
            CreatedAt = row.CreatedAt,
            ModifiedAt = row.ModifiedAt,
            CompletedAt = row.CompletedAt,
            SubmitterId = row.SubmitterId,
            SubmitterDisplayId = row.SubmitterDisplayId,
            AnswersModel = row.DataJson,
        };
}
