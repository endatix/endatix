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
        var key = new FlattenedSubmissionKey(tenantId, formId, submissionId);
        var submission = await ReadSubmissionAsync(key, cancellationToken);
        if (submission is null)
        {
            await RemoveRowOfGoneSubmissionAsync(key, cancellationToken);
            return;
        }

        await flattenedSubmissionRepository.EnsureExistsAsync(key, cancellationToken);
        if (await FlattenAsync(new FlattenRun(key, submission, includeIncomplete), cancellationToken))
        {
            await RemoveRowIfDeletedMeanwhileAsync(key, cancellationToken);
        }
    }

    // Read before any write, so a submission that is already gone never gets a row. The query filters hide a
    // soft-deleted submission, and its required form and definition joins hide one whose form or definition was
    // deleted; a failure to read throws and is retried rather than taken for gone.
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

        var dataJson = await FlattenedDataAsync(run, cancellationToken);
        return LogUnlessLanded(
            await flattenedSubmissionRepository.TryMarkProcessedAsync(run.Write, dataJson, cancellationToken),
            run.Write);
    }

    private async Task<string> FlattenedDataAsync(FlattenRun run, CancellationToken cancellationToken)
    {
        var submission = run.Submission;
        var schema = await schemaProvider.GetOrCompileAsync(
            run.Key.TenantId,
            run.Key.FormId,
            submission.FormDefinitionId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                $"Form export schema is not available for form {run.Key.FormId}, definition {submission.FormDefinitionId}.");

        var mergedSchema = FormSchemaFlatteningMap.FromJson(schema.FlatteningMap);
        using var submissionDocument = JsonDocument.Parse(submission.JsonData);
        var flattened = FlattenedSubmissionFlattener.Flatten(submissionDocument.RootElement, mergedSchema);
        return FlattenedSubmissionFlattener.ToJson(mergedSchema, flattened);
    }

    // A deletion whose cleanup ran between this flatten's read and its write would otherwise leave the row behind.
    // The deletion is committed before its cleanup runs, so reading again after the write sees it.
    private async Task RemoveRowIfDeletedMeanwhileAsync(FlattenedSubmissionKey key, CancellationToken cancellationToken)
    {
        if (!await submissionRepository.AnyAsync(
                new SubmissionWithDefinitionAndFormSpec(key.FormId, key.SubmissionId),
                cancellationToken))
        {
            await RemoveRowOfGoneSubmissionAsync(key, cancellationToken);
            return;
        }

        logger.LogInformation("Flattened submission {SubmissionId} for form {FormId}", key.SubmissionId, key.FormId);
    }

    private async Task RemoveRowOfGoneSubmissionAsync(FlattenedSubmissionKey key, CancellationToken cancellationToken)
    {
        var removed = await flattenedSubmissionRepository.DeleteBySubmissionAsync(key, cancellationToken);
        logger.LogInformation(
            "Submission {SubmissionId} of form {FormId} is gone; removed {Removed} flattened row(s) instead of flattening it",
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
        public FlattenedRevision Write => new(Key.TenantId, Key.SubmissionId, Submission.Revision);

        public bool Mismatched => Submission.TenantId != Key.TenantId || Submission.FormId != Key.FormId;
    }
}
