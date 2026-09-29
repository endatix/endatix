using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Features.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Microsoft.Extensions.Logging;

namespace Endatix.Modules.Reporting.Features.FlattenedSubmission;

/// <summary>
/// Processes a submission into the reporting flattened read model.
/// </summary>
/// <remarks>
/// <para>
/// Flattens run as independent jobs, so they can overlap, retry, and finish in any order relative to each other and
/// to the deletion syncs. Two rules keep the read model right whatever the order. A submission that is gone —
/// missing, soft-deleted, or on a deleted form or definition — leaves no row: the flatten removes its row, if any,
/// and succeeds. And every write carries the submission's revision and lands only when the row was not written from
/// a newer one, so an older flatten finishing last changes nothing.
/// </para>
/// </remarks>
internal sealed class SubmissionFlatteningProcessor(
    IRepository<Submission> submissionRepository,
    IFlattenedSubmissionRepository flattenedSubmissionRepository,
    IFormSchemaProvider schemaProvider,
    ILogger<SubmissionFlatteningProcessor> logger) : ISubmissionFlatteningProcessor
{
    private const string SubmissionMismatchMessage = "Submission tenant or form does not match the flatten request.";

    public async Task ProcessAsync(
        long tenantId,
        long formId,
        long submissionId,
        CancellationToken cancellationToken,
        bool includeIncomplete = false)
    {
        // Read before any write, so a submission that is already gone never gets a row. The query filters hide a
        // soft-deleted submission, and its required form and definition joins hide one whose form or definition
        // was deleted; a failure to read throws and is retried rather than taken for gone.
        var submission = await submissionRepository.SingleOrDefaultAsync(
            new SubmissionWithDefinitionAndFormSpec(formId, submissionId),
            cancellationToken);
        if (submission is null)
        {
            await RemoveRowOfGoneSubmissionAsync(tenantId, formId, submissionId, cancellationToken);
            return;
        }

        await flattenedSubmissionRepository.EnsureExistsAsync(tenantId, submissionId, formId, cancellationToken);
        var revision = submission.Revision;

        if (submission.TenantId != tenantId || submission.FormId != formId)
        {
            if (!await flattenedSubmissionRepository.TryMarkFailedAsync(
                    tenantId, submissionId, revision, SubmissionMismatchMessage, cancellationToken))
            {
                LogSuperseded(submissionId, revision);
            }

            return;
        }

        if (!await flattenedSubmissionRepository.TryMarkProcessingAsync(tenantId, submissionId, revision, cancellationToken))
        {
            LogSuperseded(submissionId, revision);
            return;
        }

        if (!submission.IsComplete && !includeIncomplete)
        {
            if (!await flattenedSubmissionRepository.TryMarkSkippedAsync(tenantId, submissionId, revision, cancellationToken))
            {
                LogSuperseded(submissionId, revision);
            }

            return;
        }

        var schema = await schemaProvider.GetOrCompileAsync(
            tenantId,
            formId,
            submission.FormDefinitionId,
            cancellationToken);
        if (schema is null)
        {
            throw new InvalidOperationException(
                $"Form export schema is not available for form {formId}, definition {submission.FormDefinitionId}.");
        }

        var mergedSchema = FormSchemaFlatteningMap.FromJson(schema.FlatteningMap);
        using var submissionDocument = JsonDocument.Parse(submission.JsonData);
        var flattened = FlattenedSubmissionFlattener.Flatten(
            submissionDocument.RootElement,
            mergedSchema);
        var dataJson = FlattenedSubmissionFlattener.ToJson(mergedSchema, flattened);

        if (!await flattenedSubmissionRepository.TryMarkProcessedAsync(
                tenantId, submissionId, revision, dataJson, cancellationToken))
        {
            LogSuperseded(submissionId, revision);
            return;
        }

        // A deletion whose cleanup ran between this flatten's read and its write would otherwise leave the row
        // behind. The deletion is committed before its cleanup runs, so reading again after the write sees it.
        if (!await submissionRepository.AnyAsync(
                new SubmissionWithDefinitionAndFormSpec(formId, submissionId),
                cancellationToken))
        {
            await RemoveRowOfGoneSubmissionAsync(tenantId, formId, submissionId, cancellationToken);
            return;
        }

        logger.LogInformation(
            "Flattened submission {SubmissionId} for form {FormId}",
            submissionId,
            formId);
    }

    private async Task RemoveRowOfGoneSubmissionAsync(
        long tenantId,
        long formId,
        long submissionId,
        CancellationToken cancellationToken)
    {
        var removed = await flattenedSubmissionRepository.DeleteBySubmissionAsync(
            tenantId, formId, submissionId, cancellationToken);
        logger.LogInformation(
            "Submission {SubmissionId} of form {FormId} is gone; removed {Removed} flattened row(s) instead of flattening it",
            submissionId,
            formId,
            removed);
    }

    private void LogSuperseded(long submissionId, long revision) =>
        logger.LogDebug(
            "Flattened submission {SubmissionId} was already written from a revision newer than {Revision}; left as it is",
            submissionId,
            revision);
}
