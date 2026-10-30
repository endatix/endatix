using Endatix.Core.Abstractions.Forms;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Modules.Audience.Features.TestSubmissions;

/// <summary>
/// Saves a prepared test submission under its form's shared <see cref="IFormSubmissionsLock"/>, as every submission
/// create does: the form's deletion waits for it, and one that starts during the deletion is refused at once. Under
/// the lock it reads the form again, which may have been deleted, disabled or given another active definition since
/// <see cref="TestSubjects"/> checked it, and builds the submission on the definition active now.
/// </summary>
/// <remarks>
/// The section reads and writes through the application context alone, so it holds one pooled connection.
/// </remarks>
internal sealed class TestSubmissionWriter(
    IRepository<Submission> submissions,
    IRepository<Form> forms,
    IFormSubmissionsLock formLock)
{
    private const string FormNotFound = "Form not found.";

    /// <returns>
    /// Created; <see cref="ResultStatus.NotFound"/> when the form no longer takes submissions; or
    /// <see cref="ResultStatus.Unavailable"/> while the form is being deleted, or when the save lost a deadlock.
    /// </returns>
    public Task<Result<OpenedTestSubmissionDto>> AddAsync(
        PreparedTestSubmission prepared,
        CancellationToken cancellationToken) =>
        SaveLockedAsync(prepared, replaced: null, cancellationToken);

    /// <summary>
    /// Start over: deletes <paramref name="current"/> in the same save as the insert. <see cref="BaseEntity.IsDeleted"/>
    /// is a concurrency token, so the delete matches only while <paramref name="current"/> is live: of two Start overs
    /// of one submission, only one replaces it, and the other saves nothing and is not found.
    /// </summary>
    /// <returns>As <see cref="AddAsync"/>, and <see cref="ResultStatus.NotFound"/> when <paramref name="current"/> was deleted meanwhile.</returns>
    public Task<Result<OpenedTestSubmissionDto>> ReplaceAsync(
        Submission current,
        PreparedTestSubmission prepared,
        CancellationToken cancellationToken) =>
        SaveLockedAsync(prepared, current, cancellationToken);

    /// <summary>
    /// Saves every submission of <paramref name="batch"/> in one save under one hold of the lock, then runs
    /// <paramref name="finish"/> on them, one per submission and in order, before anything commits. A failed
    /// <paramref name="finish"/> rolls the save back, so nothing is opened.
    /// </summary>
    /// <returns>What <paramref name="finish"/> returned, or an error as <see cref="AddAsync"/> reports it.</returns>
    public Task<Result<TResult>> AddManyAsync<TResult>(
        PreparedTestBatch batch,
        Func<IReadOnlyList<OpenedTestSubmissionDto>, Result<TResult>> finish,
        CancellationToken cancellationToken) =>
        batch.Submissions.Count == 0
            ? Task.FromResult(finish([]))
            : InSectionAsync(batch.Key.FormId, () => SaveManyAsync(batch, finish, cancellationToken), cancellationToken);

    private Task<Result<OpenedTestSubmissionDto>> SaveLockedAsync(
        PreparedTestSubmission prepared,
        Submission? replaced,
        CancellationToken cancellationToken) =>
        InSectionAsync(
            prepared.Subject.FormId,
            () => SaveOnCurrentDefinitionAsync(prepared, replaced, cancellationToken),
            cancellationToken);

    /// <summary>Runs <paramref name="save"/> under the form's lock, and commits only what succeeded.</summary>
    private async Task<Result<T>> InSectionAsync<T>(
        long formId,
        Func<Task<Result<T>>> save,
        CancellationToken cancellationToken)
    {
        var locked = await formLock.BeginAddingAsync(formId, cancellationToken);
        if (!locked.IsSuccess)
        {
            return locked.ToErrorResult<T>();
        }

        await using var section = locked.Value;
        var saved = await save();
        if (saved.IsSuccess)
        {
            await section.CommitAsync(cancellationToken);
        }

        return saved;
    }

    private async Task<Result<TResult>> SaveManyAsync<TResult>(
        PreparedTestBatch batch,
        Func<IReadOnlyList<OpenedTestSubmissionDto>, Result<TResult>> finish,
        CancellationToken cancellationToken)
    {
        if (await OpenDefinitionIdAsync(batch.Key, cancellationToken) is not long definitionId)
        {
            return Result.NotFound(FormNotFound);
        }

        var built = batch.Submissions.Select(item => item.Build(definitionId)).ToList();
        var saved = await submissions.TryAddRangeAsync(built, cancellationToken);
        return saved.IsSuccess ? finish([.. built.Select(Opened)]) : saved.ToErrorResult<TResult>();
    }

    /// <summary>A refused form deletes nothing: <paramref name="replaced"/> is deleted only once the form is read open.</summary>
    private async Task<Result<OpenedTestSubmissionDto>> SaveOnCurrentDefinitionAsync(
        PreparedTestSubmission prepared,
        Submission? replaced,
        CancellationToken cancellationToken)
    {
        var key = new TestSubjectKey(prepared.Subject.TenantId, prepared.Subject.FormId);
        if (await OpenDefinitionIdAsync(key, cancellationToken) is not long definitionId)
        {
            return Result.NotFound(FormNotFound);
        }

        replaced?.Delete();
        var submission = prepared.Build(definitionId);
        var saved = await submissions.TryAddAsync(submission, cancellationToken);
        return saved.IsSuccess
            ? Result<OpenedTestSubmissionDto>.Created(Opened(submission))
            : saved.ToErrorResult<OpenedTestSubmissionDto>();
    }

    private Task<long?> OpenDefinitionIdAsync(TestSubjectKey key, CancellationToken cancellationToken) =>
        forms.FirstOrDefaultAsync(
            new FormSpecifications.OpenFormActiveDefinitionId(key.FormId, key.TenantId),
            cancellationToken);

    private static OpenedTestSubmissionDto Opened(Submission submission) =>
        new(submission.Id, submission.Token!.Value);
}

/// <summary>Prepared test submissions, all on the form <see cref="Key"/> names.</summary>
internal sealed record PreparedTestBatch(TestSubjectKey Key, IReadOnlyList<PreparedTestSubmission> Submissions);
