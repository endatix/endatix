using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Modules.Reporting.Contracts;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Domain;
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

        SubmissionBackfillPageSpec spec = new(
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

        var processed = 0;
        var failed = 0;
        List<long> failedSubmissionIds = [];

        foreach (var candidate in candidates)
        {
            if (await ShouldSkipAsync(tenantId, candidate, options.Force, cancellationToken))
            {
                continue;
            }

            try
            {
                var published = await TryBackfillCandidateAsync(new(tenantId, formId, candidate.SubmissionId), options, cancellationToken);
                processed += published ? 1 : 0;
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
            Skipped: candidates.Count - processed - failed,
            Failed: failed,
            HasMore: hasMore,
            NextAfterSubmissionId: hasMore ? nextAfterSubmissionId : null,
            FailedSubmissionIds: failedSubmissionIds);
    }

    private async Task<bool> TryBackfillCandidateAsync(FlattenedSubmissionKey key, SubmissionBackfillOptions options, CancellationToken ct)
    {
        var published = await flatteningProcessor.TryBackfillAsync(key, options.Completion == SubmissionBackfillCompletion.Incomplete, ct);
        if (published || !options.Force)
        {
            return published;
        }

        // A competing claim alone cannot prove the requested rebuild published. Only a confirmed deletion
        // releases the obligation; leave the live or unknown candidate on the operator's retry list.
        var state = await submissionRepository.SingleOrDefaultAsync(new SubmissionDeletionStateSpec(key.TenantId, key.SubmissionId), ct);
        if (state?.IsDeletedIn(key.FormId) == true)
        {
            return false;
        }

        throw new InvalidOperationException("Forced rebuild did not publish the live submission; retry the rebuild.");
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
            existing.SourceModifiedAt is null)
        {
            return false;
        }

        // Compare the submission's stamp with the stamp of the version that was flattened, both
        // written by the core API. The row's own ModifiedAt is set by the worker after it read the
        // submission, so a save during flattening would look older than the row and never refresh.
        var sourceAt = candidate.ModifiedAt ?? candidate.CreatedAt;
        return sourceAt <= existing.SourceModifiedAt.Value;
    }

    private static int NormalizeBatchSize(int batchSize) =>
        batchSize switch
        {
            <= 0 => DefaultBatchSize,
            > MaxBatchSize => MaxBatchSize,
            _ => batchSize,
        };
}
