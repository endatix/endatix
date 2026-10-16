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
using Endatix.Modules.Reporting.Features.SearchCopy;
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
/// as its deletion sync would have removed it; so does one whose schema the form's deletion already removed.
/// </para>
/// </remarks>
internal sealed class SubmissionFlatteningProcessor(
    IRepository<Submission> submissionRepository,
    IFlattenedSubmissionRepository flattenedSubmissionRepository,
    IFormSchemaProvider schemaProvider,
    IFormSchemaPersonalization schemaPersonalization,
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
        var key = new FlattenedSubmissionKey(tenantId, formId, submissionId);
        var submission = await ReadSubmissionAsync(key, cancellationToken);
        if (submission is null)
        {
            await HandleMissingSubmissionAsync(key, cancellationToken);
            return;
        }

        await flattenedSubmissionRepository.EnsureExistsAsync(key, cancellationToken);
        if (await FlattenAsync(new FlattenRun(key, submission, includeIncomplete), cancellationToken))
        {
            await RemoveRowIfDeletedMeanwhileAsync(key, cancellationToken);
        }
    }

    // Read before any write, so a submission that cannot be read never gets a row, and a row never stays in
    // Processing for it. The query filters hide a soft-deleted submission; a failure to read throws and is retried.
    private Task<Submission?> ReadSubmissionAsync(FlattenedSubmissionKey key, CancellationToken cancellationToken) =>
        submissionRepository.SingleOrDefaultAsync(
            new SubmissionWithDefinitionAndFormSpec(key.FormId, key.SubmissionId),
            cancellationToken);

    /// <summary>Returns whether the flattened data landed on the row.</summary>
    private async Task<bool> FlattenAsync(FlattenRun run, CancellationToken cancellationToken)
    {
        if (run.Mismatched)
        {
            LogUnlessLanded(
                await flattenedSubmissionRepository.TryMarkFailedAsync(run.Write, SubmissionMismatchMessage, cancellationToken),
                run.Write);
            return false;
        }

        return LogUnlessLanded(await flattenedSubmissionRepository.TryMarkProcessingAsync(run.Write, cancellationToken), run.Write)
            && await StoreFlattenedDataAsync(run, cancellationToken);
    }

    private async Task<bool> StoreFlattenedDataAsync(FlattenRun run, CancellationToken cancellationToken)
    {
        if (!run.Submission.IsComplete && !run.IncludeIncomplete)
        {
            LogUnlessLanded(await flattenedSubmissionRepository.TryMarkSkippedAsync(run.Write, cancellationToken), run.Write);
            return false;
        }

        var schema = await SchemaOfAsync(run, cancellationToken);
        if (schema is null)
        {
            await HandleMissingSchemaAsync(run, cancellationToken);
            return false;
        }

        var data = await FlattenedDataAsync(run.Submission, schema, cancellationToken);
        return LogUnlessLanded(
            await flattenedSubmissionRepository.TryMarkProcessedAsync(run.Write, data, cancellationToken),
            run.Write);
    }

    private Task<Domain.FormSchema?> SchemaOfAsync(FlattenRun run, CancellationToken cancellationToken) =>
        schemaProvider.GetOrCompileAsync(
            run.Key.TenantId,
            run.Key.FormId,
            run.Submission.FormDefinitionId,
            cancellationToken);

    private async Task<FlattenedRowData> FlattenedDataAsync(
        Submission submission,
        Domain.FormSchema schema,
        CancellationToken cancellationToken)
    {
        var personalization = PersonalizationReader.Read(submission.PersonalizationSnapshot);
        var mergedSchema = await schemaPersonalization.IncludeAsync(schema, personalization, cancellationToken);
        using var submissionDocument = JsonDocument.Parse(submission.JsonData);
        var flattened = FlattenedSubmissionFlattener.Flatten(submissionDocument.RootElement, mergedSchema, personalization);
        return new FlattenedRowData(
            FlattenedSubmissionFlattener.ToJson(mergedSchema, flattened),
            SearchCopyDocument.Build(flattened, FormSearchKeys.FromJson(schema.SearchKeys)));
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

    // A form deleted after its submission was read, with its cleanup already run, leaves no schema to flatten with.
    // The row this flatten created is removed as that cleanup removed the others. Any other missing schema throws.
    private async Task HandleMissingSchemaAsync(FlattenRun run, CancellationToken cancellationToken)
    {
        if (!await RemoveRowIfDeletedHereAsync(run.Key, cancellationToken))
        {
            throw new InvalidOperationException(
                $"Form export schema is not available for form {run.Key.FormId}, definition {run.Submission.FormDefinitionId}.");
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
    // otherwise leave the row behind. The deletion is committed before its sync runs, so reading again after the
    // write sees it, and the row is removed as the sync would have removed it. The same lookup as for a missing
    // submission, because a criteria-only re-read of the submission would not see the form's own deletion.
    private async Task RemoveRowIfDeletedMeanwhileAsync(FlattenedSubmissionKey key, CancellationToken cancellationToken)
    {
        if (!await RemoveRowIfDeletedHereAsync(key, cancellationToken))
        {
            logger.LogInformation("Flattened submission {SubmissionId} for form {FormId}", key.SubmissionId, key.FormId);
        }
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
    }
}
