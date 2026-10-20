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
        var openForm = new FormSpecifications.OpenFormActiveDefinitionId(prepared.Subject.FormId, prepared.Subject.TenantId);
        if (await forms.FirstOrDefaultAsync(openForm, cancellationToken) is not long definitionId)
        {
            return Result.NotFound(FormNotFound);
        }

        replaced?.Delete();
        var submission = prepared.Build(definitionId);
        var saved = await submissions.TryAddAsync(submission, cancellationToken);
        return saved.IsSuccess
            ? Result<OpenedTestSubmissionDto>.Created(new OpenedTestSubmissionDto(submission.Id, submission.Token!.Value))
            : saved.ToErrorResult<OpenedTestSubmissionDto>();
    }
}
