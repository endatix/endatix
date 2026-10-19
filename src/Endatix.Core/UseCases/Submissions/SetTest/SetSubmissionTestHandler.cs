using Endatix.Core.Entities;
using Endatix.Core.Exceptions;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Submissions.SetTest;

/// <summary>
/// Marking as test lifts the one-per-person limit (the Audience module then frees the person's link). Marking as
/// real restores it, and is refused when the submitter already has a real submission on the form.
/// </summary>
/// <remarks>
/// Saves the tracked submission, so only the flag and key columns are written: an answer the respondent saves at the
/// same moment is not overwritten. A mark that changes nothing is not saved.
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
        if (enforceGate && await submissions.AnyAsync(Spec(submission), cancellationToken))
        {
            return await SingleSubmissionConflict.ResultAsync<SubmissionDto>(submissions, Spec(submission), cancellationToken);
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

    private static SubmissionByFormIdAndSubmitterIdSpec Spec(Submission submission) =>
        new(submission.FormId, submission.SubmitterId!.Value);
}
