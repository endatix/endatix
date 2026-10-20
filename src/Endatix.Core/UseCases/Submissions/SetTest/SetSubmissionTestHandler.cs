using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Exceptions;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Submissions.SetTest;

/// <summary>
/// Marking as test lifts the one-per-person limit (the Audience module then frees the person's link). Marking as
/// real restores it, and is refused when the person already has a real submission on the form: by Submitter on a
/// one-response form, and through the personalised link that opened this one on any form.
/// </summary>
/// <remarks>
/// Saves the tracked submission, so only the columns the mark changes are written: the test flag, the one-per-person
/// key and <see cref="Submission.Revision"/>. An answer the respondent saves at the same moment is not overwritten.
/// <see cref="Submission.Revision"/> is not a concurrency token, though, so that save and this one, both from the
/// same read, write the same revision. A mark that changes nothing is not saved. The link check has no index behind
/// it: a link open that commits between the check and the save is not refused.
/// </remarks>
public sealed class SetSubmissionTestHandler(
    IRepository<Submission> submissions,
    IRepository<Form> forms) : ICommandHandler<SetSubmissionTestCommand, Result<SubmissionDto>>
{
    /// <inheritdoc />
    public async Task<Result<SubmissionDto>> Handle(SetSubmissionTestCommand request, CancellationToken cancellationToken)
    {
        var form = await forms.GetByIdAsync(request.FormId, cancellationToken);
        if (form is null)
        {
            return Result.NotFound("Form not found");
        }

        var submission = await submissions.GetByIdAsync(request.SubmissionId, cancellationToken);
        if (submission is null || submission.FormId != request.FormId)
        {
            return Result.NotFound("Submission not found");
        }

        if (submission.IsTestSubmission == request.IsTest)
        {
            return Result.Success(SubmissionDto.FromSubmission(submission));
        }

        return request.IsTest
            ? await MarkAsTestAsync(submission, cancellationToken)
            : await MarkAsRealAsync(submission, form.LimitOnePerUser && submission.SubmitterId is not null, cancellationToken);
    }

    private async Task<Result<SubmissionDto>> MarkAsTestAsync(Submission submission, CancellationToken cancellationToken)
    {
        submission.MarkAsTest();
        await submissions.SaveChangesAsync(cancellationToken);
        return Result.Success(SubmissionDto.FromSubmission(submission));
    }

    private async Task<Result<SubmissionDto>> MarkAsRealAsync(
        Submission submission,
        bool enforceGate,
        CancellationToken cancellationToken)
    {
        // Fast path only; the UX_Submissions_RestrictionKey unique index is the concurrency authority.
        if (await ExistingRealAsync(submission, enforceGate, cancellationToken) is { } existing)
        {
            return SingleSubmissionConflict.For<SubmissionDto>(existing);
        }

        try
        {
            submission.MarkAsReal(enforceGate);
            await submissions.SaveChangesAsync(cancellationToken);
            return Result.Success(SubmissionDto.FromSubmission(submission));
        }
        catch (DuplicateSubmissionException)
        {
            return await SingleSubmissionConflict.ResultAsync<SubmissionDto>(submissions, Spec(submission), cancellationToken);
        }
    }

    /// <summary>
    /// The person's real submission, if they have one: by Submitter when the form allows one response, and by the
    /// link that opened <paramref name="submission"/> on any form, since a personalised link answers for one person.
    /// </summary>
    private async Task<Submission?> ExistingRealAsync(
        Submission submission,
        bool enforceGate,
        CancellationToken cancellationToken)
    {
        if (enforceGate && await submissions.FirstOrDefaultAsync(Spec(submission), cancellationToken) is { } bySubmitter)
        {
            return bySubmitter;
        }

        return submission.AudienceLinkId is long linkId
            ? await submissions.FirstOrDefaultAsync(new OtherRealSubmissionOfLinkSpec(submission, linkId), cancellationToken)
            : null;
    }

    private static SubmissionByFormIdAndSubmitterIdSpec Spec(Submission submission) =>
        new(submission.FormId, submission.SubmitterId!.Value);

    /// <summary>A real submission the same link opened after this one was marked as test and the link freed.</summary>
    private sealed class OtherRealSubmissionOfLinkSpec : SingleResultSpecification<Submission>
    {
        public OtherRealSubmissionOfLinkSpec(Submission submission, long audienceLinkId) =>
            Query.Where(other => other.FormId == submission.FormId
                    && other.AudienceLinkId == audienceLinkId
                    && other.Id != submission.Id
                    && !other.IsTestSubmission)
                .AsNoTracking();
    }
}
