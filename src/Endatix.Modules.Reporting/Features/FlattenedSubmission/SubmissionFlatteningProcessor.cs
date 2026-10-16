using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Submissions;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Features.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.Personalization;
using Endatix.Modules.Reporting.Features.FormSchema.SubmitterProfile;
using Microsoft.Extensions.Logging;

namespace Endatix.Modules.Reporting.Features.FlattenedSubmission;

/// <summary>
/// Processes a submission into the reporting flattened read model.
/// </summary>
/// <remarks>
/// <para>
/// Flattens run as independent jobs, so they can overlap, retry, and finish in any order relative to each other and
/// to the deletion syncs. Every write carries the submission's revision and lands only when the row was not written
/// from a newer one, so an older flatten finishing last changes nothing.
/// </para>
/// <para>
/// A submission that cannot be read never gets a row. When it or its form was soft-deleted in the requested tenant and
/// form, an existing row is removed and the flatten succeeds; any other miss throws, so it is retried and then
/// reported as failed. A submission or form deleted after the submission was read loses the row this flatten wrote,
/// as its deletion sync would have removed it: whether the row ended processed or skipped, lost to a newer flatten's
/// write, or the flatten failed or was cancelled after marking it processing, its schema gone with the form included.
/// Such a failure then ends the flatten as a success, since there is nothing left to retry; a cancellation still
/// surfaces as one.
/// </para>
/// </remarks>
internal sealed class SubmissionFlatteningProcessor(
    IRepository<Submission> submissionRepository,
    IFlattenedSubmissionRepository flattenedSubmissionRepository,
    IFormSchemaProvider schemaProvider,
    IFormSchemaPersonalization schemaPersonalization,
    SubmitterProfileFields submitterProfileFields,
    ILogger<SubmissionFlatteningProcessor> logger) : ISubmissionFlatteningProcessor
{
    private const string SubmissionMismatchMessage = "Submission tenant or form does not match the flatten request.";

    // A cancelled flatten still owes its row the deletion check: an operator's cancel is final, and a run stopped at
    // its maximum run time may be its last attempt, so no later run would check. The check gets this long on a token
    // of its own, short enough not to hold up a host that is stopping.
    private static readonly TimeSpan CancelledCheckTimeout = TimeSpan.FromSeconds(5);

    public async Task ProcessAsync(
        long tenantId,
        long formId,
        long submissionId,
        CancellationToken cancellationToken,
        bool includeIncomplete = false)
    {
        var key = new FlattenedSubmissionKey(tenantId, formId, submissionId);
        var submission = await ReadSubmissionAsync(key, cancellationToken);
        if (submission is null)
        {
            await HandleMissingSubmissionAsync(key, cancellationToken);
            return;
        }

        await flattenedSubmissionRepository.EnsureExistsAsync(key, cancellationToken);
        await FlattenAsync(new FlattenRun(key, submission, includeIncomplete), cancellationToken);
    }

    // Read before any write, so a submission that cannot be read never gets a row, and a row never stays in
    // Processing for it. The query filters hide a soft-deleted submission; a failure to read throws and is retried.
    private Task<Submission?> ReadSubmissionAsync(FlattenedSubmissionKey key, CancellationToken cancellationToken) =>
        submissionRepository.SingleOrDefaultAsync(
            new SubmissionWithDefinitionAndFormSpec(key.FormId, key.SubmissionId),
            cancellationToken);

    private async Task FlattenAsync(FlattenRun run, CancellationToken cancellationToken)
    {
        if (run.Mismatched)
        {
            LogUnlessLanded(
                await flattenedSubmissionRepository.TryMarkFailedAsync(run.Write, SubmissionMismatchMessage, cancellationToken),
                run.Write);
            return;
        }

        var processing = await flattenedSubmissionRepository.TryMarkProcessingAsync(run.Write, cancellationToken);
        if (LogUnlessLanded(processing, run.Write))
        {
            await FinishProcessingAsync(run, cancellationToken);
        }
    }

    // Once the processing write landed, the row's cleanup is this flatten's: a deletion whose sync already ran does
    // not come back for it, whether storing the data succeeds, fails, or is cancelled. A cancelled flatten still
    // surfaces as cancelled after its check, so a job's cancel or shutdown, and a backfill's stop, end as they would.
    private async Task FinishProcessingAsync(FlattenRun run, CancellationToken cancellationToken)
    {
        try
        {
            await StoreFlattenedDataAsync(run, cancellationToken);
        }
        catch (Exception failure)
        {
            var removedAsDeleted = await RemoveRowIfDeletedAfterFailureAsync(run.Key, cancellationToken);
            if (!removedAsDeleted || cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            logger.LogDebug(failure, "Flattening submission {SubmissionId} failed after it was deleted", run.Key.SubmissionId);
            return;
        }

        await RemoveRowIfDeletedMeanwhileAsync(run.Key, cancellationToken);
    }

    private async Task StoreFlattenedDataAsync(FlattenRun run, CancellationToken cancellationToken)
    {
        if (run.IsSkipped)
        {
            LogUnlessLanded(await flattenedSubmissionRepository.TryMarkSkippedAsync(run.Write, cancellationToken), run.Write);
            return;
        }

        var schema = await SchemaOfAsync(run, cancellationToken);
        var dataJson = await FlattenedDataAsync(run.Submission, schema, cancellationToken);
        LogUnlessLanded(
            await flattenedSubmissionRepository.TryMarkProcessedAsync(run.Write, dataJson, cancellationToken),
            run.Write);
    }

    // A form deleted after its submission was read, with its cleanup already run, leaves no schema to flatten with;
    // the failure's deletion check then removes the row this flatten created, as that cleanup removed the others.
    // Any other missing schema fails the flatten.
    private async Task<Domain.FormSchema> SchemaOfAsync(FlattenRun run, CancellationToken cancellationToken) =>
        await schemaProvider.GetOrCompileAsync(
            run.Key.TenantId,
            run.Key.FormId,
            run.Submission.FormDefinitionId,
            cancellationToken)
        ?? throw new InvalidOperationException(
            $"Form export schema is not available for form {run.Key.FormId}, definition {run.Submission.FormDefinitionId}.");

    private async Task<string> FlattenedDataAsync(
        Submission submission,
        Domain.FormSchema schema,
        CancellationToken cancellationToken)
    {
        var personalization = PersonalizationReader.Read(submission.PersonalizationSnapshot);
        var mergedSchema = await schemaPersonalization.IncludeAsync(schema, personalization, cancellationToken);
        ResponseSnapshots snapshots = new(personalization, submitterProfileFields.Read(submission.SubmitterProfileSnapshot));
        using var submissionDocument = JsonDocument.Parse(submission.JsonData);
        var flattened = FlattenedSubmissionFlattener.Flatten(submissionDocument.RootElement, mergedSchema, snapshots);
        return FlattenedSubmissionFlattener.ToJson(mergedSchema, flattened);
    }

    // A soft-delete of the submission or its form after the submission was queued or paged removes its row, as the
    // deletion syncs do. Anything else — never existed, or another tenant's or form's — throws, so a job retries and
    // then dead-letters, and a backfill counts it as failed, instead of hiding it.
    private async Task HandleMissingSubmissionAsync(FlattenedSubmissionKey key, CancellationToken cancellationToken)
    {
        if (!await RemoveRowIfDeletedHereAsync(key, cancellationToken))
        {
            throw new InvalidOperationException(
                $"Submission {key.SubmissionId} for form {key.FormId} was not found while flattening.");
        }
    }

    /// <summary>
    /// Removes the row when the submission or its form was soft-deleted in the key's tenant and form; returns whether
    /// it was.
    /// </summary>
    private async Task<bool> RemoveRowIfDeletedHereAsync(FlattenedSubmissionKey key, CancellationToken cancellationToken)
    {
        var state = await submissionRepository.SingleOrDefaultAsync(
            new SubmissionDeletionStateSpec(key.TenantId, key.SubmissionId),
            cancellationToken);
        if (state is null || !state.IsDeletedIn(key.FormId))
        {
            return false;
        }

        await RemoveRowOfDeletedSubmissionAsync(key, cancellationToken);
        return true;
    }

    // A deletion of the submission or its form whose sync ran between this flatten's read and its write would
    // otherwise leave the row behind, whether the flatten marked it processed or skipped or lost it to a newer
    // revision's write. The deletion is committed before its sync runs, so reading again after the write sees it,
    // and the row is removed as the sync would have removed it. The same lookup as for a missing submission, because
    // a criteria-only re-read of the submission would not see the form's own deletion.
    private async Task RemoveRowIfDeletedMeanwhileAsync(FlattenedSubmissionKey key, CancellationToken cancellationToken)
    {
        if (!await RemoveRowIfDeletedAfterWriteAsync(key, cancellationToken))
        {
            logger.LogInformation(
                "Finished flattening submission {SubmissionId} for form {FormId}",
                key.SubmissionId,
                key.FormId);
        }
    }

    // A failure after the processing write would otherwise leave the row in Processing under a deleted submission or
    // form, and nothing removes it once the retries run out; when the row goes, the flatten has nothing left to retry.
    // A failure of the check itself is only logged, so the flatten's own failure is the one that is reported.
    private async Task<bool> RemoveRowIfDeletedAfterFailureAsync(
        FlattenedSubmissionKey key,
        CancellationToken cancellationToken)
    {
        try
        {
            return await RemoveRowIfDeletedAfterWriteAsync(key, cancellationToken);
        }
        catch (Exception checkFailure)
        {
            logger.Log(
                LevelOfCheckFailure(checkFailure),
                checkFailure,
                "Could not check whether submission {SubmissionId} of form {FormId} was deleted after its flatten failed",
                key.SubmissionId,
                key.FormId);
            return false;
        }
    }

    // A check cut off by the cancellation of its own token is expected while a host stops; anything else is not.
    private static LogLevel LevelOfCheckFailure(Exception checkFailure) =>
        checkFailure is OperationCanceledException { CancellationToken.IsCancellationRequested: true }
            ? LogLevel.Debug
            : LogLevel.Warning;

    private async Task<bool> RemoveRowIfDeletedAfterWriteAsync(FlattenedSubmissionKey key, CancellationToken cancellationToken)
    {
        if (!cancellationToken.IsCancellationRequested)
        {
            return await RemoveRowIfDeletedHereAsync(key, cancellationToken);
        }

        using var cancelledCheck = new CancellationTokenSource(CancelledCheckTimeout);
        return await RemoveRowIfDeletedHereAsync(key, cancelledCheck.Token);
    }

    private async Task RemoveRowOfDeletedSubmissionAsync(FlattenedSubmissionKey key, CancellationToken cancellationToken)
    {
        var removed = await flattenedSubmissionRepository.DeleteBySubmissionAsync(key, cancellationToken);
        logger.LogInformation(
            "Submission {SubmissionId} of form {FormId}, or the form, was deleted; removed {Removed} flattened row(s)",
            key.SubmissionId,
            key.FormId,
            removed);
    }

    // A write that did not land lost to a flatten of a newer revision, which is what the row should keep.
    private bool LogUnlessLanded(bool landed, FlattenedRevision write)
    {
        if (!landed)
        {
            logger.LogDebug(
                "Flattened submission {SubmissionId} was already written from a revision newer than {Revision}; left as it is",
                write.SubmissionId,
                write.Revision);
        }

        return landed;
    }

    /// <summary>One flatten of a submission, as it was read for its row.</summary>
    private sealed record FlattenRun(FlattenedSubmissionKey Key, Submission Submission, bool IncludeIncomplete)
    {
        public FlattenedRevision Write =>
            new(Key.TenantId, Key.SubmissionId, Submission.Revision, Submission.ModifiedAt ?? Submission.CreatedAt);

        public bool Mismatched => Submission.TenantId != Key.TenantId || Submission.FormId != Key.FormId;

        public bool IsSkipped => !Submission.IsComplete && !IncludeIncomplete;
    }
}
