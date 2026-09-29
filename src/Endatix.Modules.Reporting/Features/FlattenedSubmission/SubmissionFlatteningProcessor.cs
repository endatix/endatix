using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Features.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Microsoft.Extensions.Logging;
using FlattenedSubmissionRow = Endatix.Modules.Reporting.Domain.FlattenedSubmission;

namespace Endatix.Modules.Reporting.Features.FlattenedSubmission;

/// <summary>
/// Processes a submission into the reporting flattened read model.
/// </summary>
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
        // Load the submission before touching the row: a submission that cannot be found must not
        // leave a row created, or stuck in Processing.
        SubmissionWithDefinitionAndFormSpec submissionSpec = new(formId, submissionId);
        var submission = await submissionRepository.SingleOrDefaultAsync(submissionSpec, cancellationToken);
        if (submission is null)
        {
            await HandleMissingSubmissionAsync(tenantId, formId, submissionId, cancellationToken);
            return;
        }

        var row = await flattenedSubmissionRepository.GetOrCreateAsync(
            tenantId,
            submissionId,
            formId,
            cancellationToken);

        row.MarkProcessing();
        await flattenedSubmissionRepository.SaveAsync(row, cancellationToken);

        if (submission.TenantId != tenantId || submission.FormId != formId)
        {
            await FailAsync(row, SubmissionMismatchMessage, cancellationToken);
            return;
        }

        if (!submission.IsComplete && !includeIncomplete)
        {
            row.MarkSkipped();
            await flattenedSubmissionRepository.SaveAsync(row, cancellationToken);
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

        row.MarkProcessed(dataJson, submission.ModifiedAt ?? submission.CreatedAt);
        await flattenedSubmissionRepository.SaveAsync(row, cancellationToken);

        logger.LogInformation(
            "Flattened submission {SubmissionId} for form {FormId}",
            submissionId,
            formId);
    }

    /// <summary>
    /// A submission soft-deleted after it was queued or paged mirrors the deletion onto an existing row.
    /// Anything else (wrong form, wrong tenant, never existed) throws, so the outbox retries and the
    /// backfill counts it as failed instead of hiding it.
    /// </summary>
    private async Task HandleMissingSubmissionAsync(
        long tenantId,
        long formId,
        long submissionId,
        CancellationToken cancellationToken)
    {
        var state = await submissionRepository.SingleOrDefaultAsync(
            new SubmissionDeletionStateSpec(submissionId),
            cancellationToken);

        var deletedHere = state is { IsDeleted: true } &&
            state.TenantId == tenantId &&
            state.FormId == formId;
        if (!deletedHere)
        {
            throw new InvalidOperationException(
                $"Submission {submissionId} for form {formId} was not found while flattening.");
        }

        var row = await flattenedSubmissionRepository.GetBySubmissionIdAsync(
            tenantId,
            submissionId,
            cancellationToken);
        if (row is null)
        {
            return;
        }

        row.MarkDeleted();
        await flattenedSubmissionRepository.SaveAsync(row, cancellationToken);
    }

    private async Task FailAsync(
        FlattenedSubmissionRow row,
        string message,
        CancellationToken cancellationToken)
    {
        row.MarkFailed(message);
        await flattenedSubmissionRepository.SaveAsync(row, cancellationToken);
    }
}
