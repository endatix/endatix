using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Modules.Reporting.Contracts;
using Endatix.Modules.Reporting.Data;
using Microsoft.Extensions.Logging;
using FlattenedSubmissionRow = Endatix.Modules.Reporting.Domain.FlattenedSubmission;

namespace Endatix.Modules.Reporting.Features.FlattenedSubmission;

/// <summary>
/// Backfills flattened submission rows for completed historical submissions.
/// </summary>
internal sealed class SubmissionBackfillProcessor(
    IRepository<Submission> submissionRepository,
    IFlattenedSubmissionRepository flattenedSubmissionRepository,
    ISubmissionFlatteningProcessor flatteningProcessor,
    ILogger<SubmissionBackfillProcessor> logger) : ISubmissionBackfillProcessor
{
    private const int DefaultBatchSize = 100;
    private const int MaxBatchSize = 500;

    public async Task<SubmissionBackfillResult> BackfillFormAsync(
        long tenantId,
        long formId,
        SubmissionBackfillOptions options,
        CancellationToken cancellationToken)
    {
        var batchSize = NormalizeBatchSize(options.BatchSize);
        var fetchSize = batchSize + 1;

        CompletedSubmissionIdsForBackfillSpec spec = new(
            formId,
            options.AfterSubmissionId,
            fetchSize);

        var submissionIds = await submissionRepository.ListAsync(spec, cancellationToken);
        var hasMore = submissionIds.Count > batchSize;
        if (hasMore)
        {
            submissionIds = submissionIds.Take(batchSize).ToList();
        }

        var processed = 0;
        var skipped = 0;
        var failed = 0;
        List<long> failedSubmissionIds = [];

        foreach (var submissionId in submissionIds)
        {
            if (await ShouldSkipAsync(tenantId, submissionId, options.Force, cancellationToken))
            {
                skipped++;
                continue;
            }

            try
            {
                await flatteningProcessor.ProcessAsync(tenantId, formId, submissionId, cancellationToken);
                processed++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                failed++;
                failedSubmissionIds.Add(submissionId);
                logger.LogWarning(
                    ex,
                    "Backfill failed for submission {SubmissionId} on form {FormId}",
                    submissionId,
                    formId);
            }
        }

        var nextAfterSubmissionId = submissionIds.Count == 0
            ? options.AfterSubmissionId
            : submissionIds[^1];

        return new SubmissionBackfillResult(
            FormId: formId,
            Scanned: submissionIds.Count,
            Processed: processed,
            Skipped: skipped,
            Failed: failed,
            HasMore: hasMore,
            NextAfterSubmissionId: hasMore ? nextAfterSubmissionId : null,
            FailedSubmissionIds: failedSubmissionIds);
    }

    private async Task<bool> ShouldSkipAsync(
        long tenantId,
        long submissionId,
        bool force,
        CancellationToken cancellationToken)
    {
        if (force)
        {
            return false;
        }

        var existing = await flattenedSubmissionRepository.GetBySubmissionIdAsync(
            tenantId,
            submissionId,
            cancellationToken);

        return existing is not null &&
               existing.Integration.Code == SubmissionIntegrationStatusCodes.Processed &&
               !string.IsNullOrWhiteSpace(existing.DataJson);
    }

    private static int NormalizeBatchSize(int batchSize) =>
        batchSize switch
        {
            <= 0 => DefaultBatchSize,
            > MaxBatchSize => MaxBatchSize,
            _ => batchSize,
        };
}
