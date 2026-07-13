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
        CancellationToken cancellationToken)
    {
        var row = await flattenedSubmissionRepository.GetOrCreateAsync(
            tenantId,
            submissionId,
            formId,
            cancellationToken);

        row.MarkProcessing();
        await flattenedSubmissionRepository.SaveAsync(row, cancellationToken);

        SubmissionWithDefinitionAndFormSpec submissionSpec = new(formId, submissionId);
        var submission = await submissionRepository.SingleOrDefaultAsync(submissionSpec, cancellationToken);
        if (submission is null)
        {
            throw new InvalidOperationException(
                $"Submission {submissionId} for form {formId} was not found while flattening.");
        }

        if (submission.TenantId != tenantId || submission.FormId != formId)
        {
            await FailAsync(row, SubmissionMismatchMessage, cancellationToken);
            return;
        }

        if (!submission.IsComplete)
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

        row.MarkProcessed(dataJson);
        await flattenedSubmissionRepository.SaveAsync(row, cancellationToken);

        logger.LogInformation(
            "Flattened submission {SubmissionId} for form {FormId}",
            submissionId,
            formId);
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
