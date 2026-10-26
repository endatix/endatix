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
    /// Saves every one of <paramref name="prepared"/>, all on one form, in one save under one hold of the lock, then
    /// runs <paramref name="finish"/> on them, in order, before anything commits. A failed
    /// <paramref name="finish"/> rolls the save back, so nothing is opened.
    /// </summary>
    /// <returns>What <paramref name="finish"/> returned, or an error as <see cref="AddAsync"/> reports it.</returns>
    public async Task<Result<TResult>> AddManyAsync<TResult>(
        IReadOnlyList<PreparedTestSubmission> prepared,
        Func<IReadOnlyList<OpenedTestSubmissionDto>, Result<TResult>> finish,
        CancellationToken cancellationToken)
    {
        var subject = prepared[0].Subject;
        var locked = await formLock.BeginAddingAsync(subject.FormId, cancellationToken);
        if (!locked.IsSuccess)
        {
            return locked.ToErrorResult<TResult>();
        }

        await using var section = locked.Value;
        if (await OpenDefinitionIdAsync(subject, cancellationToken) is not long definitionId)
        {
            return Result.NotFound(FormNotFound);
        }

        var built = prepared.Select(item => item.Build(definitionId)).ToList();
        var saved = await submissions.TryAddRangeAsync(built, cancellationToken);
        if (!saved.IsSuccess)
        {
            return saved.ToErrorResult<TResult>();
        }

        var finished = finish([.. built.Select(Opened)]);
        if (finished.IsSuccess)
        {
            await section.CommitAsync(cancellationToken);
        }

        return finished;
    }

    private async Task<Result<OpenedTestSubmissionDto>> SaveLockedAsync(
        PreparedTestSubmission prepared,
        Submission? replaced,
        CancellationToken cancellationToken)
    {
        var locked = await formLock.BeginAddingAsync(prepared.Subject.FormId, cancellationToken);
        if (!locked.IsSuccess)
        {
            return locked.ToErrorResult<OpenedTestSubmissionDto>();
        }

        await using var section = locked.Value;
        var saved = await SaveOnCurrentDefinitionAsync(prepared, replaced, cancellationToken);
        if (saved.IsSuccess)
        {
            await section.CommitAsync(cancellationToken);
        }

        return saved;
    }

    /// <summary>A refused form deletes nothing: <paramref name="replaced"/> is deleted only once the form is read open.</summary>
    private async Task<Result<OpenedTestSubmissionDto>> SaveOnCurrentDefinitionAsync(
        PreparedTestSubmission prepared,
        Submission? replaced,
        CancellationToken cancellationToken)
    {
        if (await OpenDefinitionIdAsync(prepared.Subject, cancellationToken) is not long definitionId)
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

    private Task<long?> OpenDefinitionIdAsync(TestSubject subject, CancellationToken cancellationToken) =>
        forms.FirstOrDefaultAsync(
            new FormSpecifications.OpenFormActiveDefinitionId(subject.FormId, subject.TenantId),
            cancellationToken);

    private static OpenedTestSubmissionDto Opened(Submission submission) =>
        new(submission.Id, submission.Token!.Value);
}
