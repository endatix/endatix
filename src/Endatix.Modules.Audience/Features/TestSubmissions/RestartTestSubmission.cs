using Ardalis.Specification;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Submissions;

namespace Endatix.Modules.Audience.Features.TestSubmissions;

/// <summary>Start over: discards a test submission opened as a person and opens a new one for them.</summary>
public sealed record RestartTestSubmissionCommand(long TenantId, long FormId, long SubmissionId)
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
        if (request.TenantId <= 0)
        {
            return Result.Unauthorized("Tenant context is required.");
        }

        var current = await submissions.FirstOrDefaultAsync(new FormSubmissionSpec(request), cancellationToken);
        if (current is null)
        {
            return Result.NotFound("Submission not found.");
        }

        return TestMemberId(current) is long memberId
            ? await StartOverAsync(current, memberId, cancellationToken)
            : Result.Invalid(new ValidationError("Only a test submission opened as a person can start over."));
    }

    /// <summary>The person is checked before anything is deleted; the delete saves with the new submission.</summary>
    private async Task<Result<OpenedTestSubmissionDto>> StartOverAsync(
        Submission current,
        long memberId,
        CancellationToken cancellationToken)
    {
        var opening = await subjects.ByMemberAsync(current.FormId, memberId, cancellationToken);
        if (!opening.IsSuccess)
        {
            return opening.ToErrorResult<OpenedTestSubmissionDto>();
        }

        current.Delete();
        return await opener.OpenAsync(opening.Value, cancellationToken);
    }

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
            Query.Where(submission => submission.FormId == request.FormId && submission.Id == request.SubmissionId);
    }
}
