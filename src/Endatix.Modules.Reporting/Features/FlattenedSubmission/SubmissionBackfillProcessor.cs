using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Modules.Reporting.Contracts;
using Endatix.Modules.Reporting.Data;
using Microsoft.Extensions.Logging;
using FlattenedSubmissionRow = Endatix.Modules.Reporting.Domain.FlattenedSubmission;

namespace Endatix.Modules.Reporting.Features.FlattenedSubmission;

/// <summary>
/// Backfills flattened submission rows for historical submissions in the requested completion scope.
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
            fetchSize,
            options.Completion);

        var candidates = await submissionRepository.ListAsync(spec, cancellationToken);
        var hasMore = candidates.Count > batchSize;
        if (hasMore)
        {
            candidates = candidates.Take(batchSize).ToList();
        }

        var includeIncomplete = options.Completion == SubmissionBackfillCompletion.Incomplete;
        var processed = 0;
        var skipped = 0;
        var failed = 0;
        List<long> failedSubmissionIds = [];

        foreach (var candidate in candidates)
        {
            if (await ShouldSkipAsync(tenantId, candidate, options.Force, cancellationToken))
            {
                skipped++;
                continue;
            }

            try
            {
                await flatteningProcessor.ProcessAsync(
                    tenantId,
                    formId,
                    candidate.SubmissionId,
                    cancellationToken,
                    includeIncomplete);
                processed++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                failed++;
                failedSubmissionIds.Add(candidate.SubmissionId);
                logger.LogWarning(
                    ex,
                    "Backfill failed for submission {SubmissionId} on form {FormId}",
                    candidate.SubmissionId,
                    formId);
            }
        }

        var nextAfterSubmissionId = candidates.Count == 0
            ? options.AfterSubmissionId
            : candidates[^1].SubmissionId;

        return new SubmissionBackfillResult(
            FormId: formId,
            Scanned: candidates.Count,
            Processed: processed,
            Skipped: skipped,
            Failed: failed,
            HasMore: hasMore,
            NextAfterSubmissionId: hasMore ? nextAfterSubmissionId : null,
            FailedSubmissionIds: failedSubmissionIds);
    }

    private async Task<bool> ShouldSkipAsync(
        long tenantId,
        SubmissionBackfillCandidate candidate,
        bool force,
        CancellationToken cancellationToken)
    {
        if (force)
        {
            return false;
        }

        var existing = await flattenedSubmissionRepository.GetBySubmissionIdAsync(
            tenantId,
            candidate.SubmissionId,
            cancellationToken);

        if (existing is null ||
            existing.IsDeleted ||
            existing.Integration.Code != SubmissionIntegrationStatusCodes.Processed ||
            string.IsNullOrWhiteSpace(existing.DataJson) ||
            existing.ModifiedAt is null)
        {
            return false;
        }

        var sourceAt = candidate.ModifiedAt ?? candidate.CreatedAt;
        return sourceAt <= existing.ModifiedAt.Value;
    }

    private static int NormalizeBatchSize(int batchSize) =>
        batchSize switch
        {
            <= 0 => DefaultBatchSize,
            > MaxBatchSize => MaxBatchSize,
            _ => batchSize,
        };
}
