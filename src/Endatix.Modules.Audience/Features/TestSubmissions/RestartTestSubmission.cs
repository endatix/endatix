using System.Security.Cryptography;
using System.Text;
using Ardalis.Specification;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Submissions;

namespace Endatix.Modules.Audience.Features.TestSubmissions;

/// <summary>
/// Start over: discards a test submission opened as a person and opens a new one for them. The token proves the
/// caller holds that test session; another builder's test submission is not theirs to discard.
/// </summary>
public sealed record RestartTestSubmissionCommand(long TenantId, long FormId, long SubmissionId, string Token)
    : ICommand<Result<OpenedTestSubmissionDto>>;

internal sealed class RestartTestSubmissionHandler(
    IRepository<Submission> submissions,
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
    /// The person is checked before anything is deleted; a person removed from the form keeps the old submission.
    /// The opener deletes it and inserts the new one in one save, under the form's lock.
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
        return opening.IsSuccess
            ? await opener.ReplaceAsync(opening.Value, current, cancellationToken)
            : opening.ToErrorResult<OpenedTestSubmissionDto>();
    }

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
