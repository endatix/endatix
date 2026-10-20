using System.Security.Cryptography;
using System.Text;
using Ardalis.Specification;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Submissions;
using Endatix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.TestSubmissions;

/// <summary>
/// Start over: discards a test submission opened as a person and opens a new one for them. The token proves the
/// caller holds that test session; another builder's test submission is not theirs to discard.
/// </summary>
public sealed record RestartTestSubmissionCommand(long TenantId, long FormId, long SubmissionId, string Token)
    : ICommand<Result<OpenedTestSubmissionDto>>;

internal sealed class RestartTestSubmissionHandler(
    IRepository<Submission> submissions,
    AppDbContext app,
    TestSubjects subjects,
    TestSubmissionOpener opener)
    : ICommandHandler<RestartTestSubmissionCommand, Result<OpenedTestSubmissionDto>>
{
    public async Task<Result<OpenedTestSubmissionDto>> Handle(
        RestartTestSubmissionCommand request,
        CancellationToken cancellationToken)
    {
        var tenant = TenantFormGate.EnsureTenant(request.TenantId);
        if (!tenant.IsSuccess)
        {
            return tenant.ToErrorResult<OpenedTestSubmissionDto>();
        }

        var current = await submissions.FirstOrDefaultAsync(new FormSubmissionSpec(request), cancellationToken);
        if (current is null || !HoldsToken(current, request.Token))
        {
            return Result.NotFound("Submission not found.");
        }

        return TestMemberId(current) is long memberId
            ? await StartOverAsync(current, memberId, cancellationToken)
            : Result.Invalid(new ValidationError("Only a test submission opened as a person can start over."));
    }

    /// <summary>
    /// The person is checked and the new submission prepared before anything is deleted. The delete is claimed with
    /// a conditional update and saves with the new submission in one transaction, so of two Start overs of the same
    /// submission only one deletes it and opens a replacement; the other finds it gone.
    /// </summary>
    private async Task<Result<OpenedTestSubmissionDto>> StartOverAsync(
        Submission current,
        long memberId,
        CancellationToken cancellationToken)
    {
        var opening = await subjects.ByMemberAsync(
            new TestSubjectKey(current.TenantId, current.FormId),
            memberId,
            cancellationToken);
        if (!opening.IsSuccess)
        {
            return opening.ToErrorResult<OpenedTestSubmissionDto>();
        }

        var prepared = await opener.PrepareAsync(opening.Value, cancellationToken);
        await using var transaction = await app.Database.BeginTransactionAsync(cancellationToken);
        if (!await ClaimDeleteAsync(current, cancellationToken))
        {
            return Result.NotFound("Submission not found.");
        }

        current.Delete();
        var opened = await opener.AddAsync(prepared, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return opened;
    }

    /// <summary>
    /// Deletes the row only while it is live. A parallel Start over waits on the row and then updates nothing. The
    /// tracked <see cref="Submission.Delete"/> that follows raises the deleted event and writes the same state.
    /// </summary>
    private async Task<bool> ClaimDeleteAsync(Submission current, CancellationToken cancellationToken) =>
        await app.Submissions
            .Where(submission => submission.TenantId == current.TenantId
                && submission.Id == current.Id
                && !submission.IsDeleted)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(submission => submission.IsDeleted, true)
                    .SetProperty(submission => submission.DeletedAt, DateTime.UtcNow),
                cancellationToken) == 1;

    private static bool HoldsToken(Submission submission, string token) =>
        submission.Token?.Value is string value
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(value), Encoding.UTF8.GetBytes(token));

    /// <summary>The person a test submission was opened as; <see langword="null"/> for any other submission.</summary>
    private static long? TestMemberId(Submission submission) =>
        submission.IsTestSubmission
        && PersonalizationReader.Read(submission.PersonalizationSnapshot, includeValues: false)
            is { Source: PersonalizationSources.Test, MemberId: long memberId }
            ? memberId
            : null;

    /// <summary>Tracked, so the delete saves with the new submission.</summary>
    private sealed class FormSubmissionSpec : Specification<Submission>
    {
        public FormSubmissionSpec(RestartTestSubmissionCommand request) =>
            Query.Where(submission => submission.TenantId == request.TenantId
                && submission.FormId == request.FormId
                && submission.Id == request.SubmissionId);
    }
}
