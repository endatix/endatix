using Ardalis.Specification;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Abstractions.Submissions;
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
/// one-response form, and through the personalised link that opened this one on any form. A test opened as a person
/// never becomes real. Form and submission are read for the caller's tenant, not through the ambient filter.
/// </summary>
/// <remarks>
/// Saves the tracked submission, so only the columns the mark changes are written: the test flag, the one-per-person
/// key and <see cref="Submission.Revision"/>. An answer the respondent saves at the same moment is not overwritten.
/// <see cref="Submission.Revision"/> is not a concurrency token, though, so that save and this one, both from the
/// same read, write the same revision. <see cref="BaseEntity.IsDeleted"/> is one, so a mark that races the
/// submission's deletion, or its form's, is not found. A mark that changes nothing is not saved. The link check has
/// no index behind it: a link open that commits between the check and the save is not refused.
/// </remarks>
public sealed class SetSubmissionTestHandler(
    IRepository<Submission> submissions,
    IRepository<Form> forms) : ICommandHandler<SetSubmissionTestCommand, Result<SubmissionDto>>
{
    /// <inheritdoc />
    public async Task<Result<SubmissionDto>> Handle(SetSubmissionTestCommand request, CancellationToken cancellationToken)
    {
        if (request.TenantId <= 0)
        {
            return Result.Unauthorized("Tenant context is required.");
        }

        var form = await forms.FirstOrDefaultAsync(
            new FormSpecifications.ByIdForTenant(request.FormId, request.TenantId), cancellationToken);
        if (form is null)
        {
            return Result.NotFound("Form not found");
        }

        var submission = await submissions.FirstOrDefaultAsync(
            new SubmissionByIdForTenantSpec(request.TenantId, request.FormId, request.SubmissionId), cancellationToken);
        if (submission is null)
        {
            return Result.NotFound("Submission not found");
        }

        if (submission.IsTestSubmission == request.IsTest)
        {
            return Result.Success(SubmissionDto.FromSubmission(submission));
        }

        if (!request.IsTest && IsOpenedAsAPerson(submission))
        {
            return Result.Invalid(new ValidationError(
                "A test submission opened as a person stays a test: its answers are a survey builder's, not the person's."));
        }

        return request.IsTest
            ? await MarkAsTestAsync(submission, cancellationToken)
            : await MarkAsRealAsync(submission, form.LimitOnePerUser && submission.SubmitterId is not null, cancellationToken);
    }

    /// <summary>
    /// Test as this person: a builder's answers on the person's values, with no Submitter and no link, so as a real
    /// response it would pass every one-per-person check and stand in for the person.
    /// </summary>
    private static bool IsOpenedAsAPerson(Submission submission) =>
        PersonalizationReader.Read(submission.PersonalizationSnapshot, includeValues: false)?.Source
            == PersonalizationSources.Test;

    private async Task<Result<SubmissionDto>> MarkAsTestAsync(Submission submission, CancellationToken cancellationToken)
    {
        submission.MarkAsTest();
        return await SaveAsync(submission, cancellationToken);
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
            return await SaveAsync(submission, cancellationToken);
        }
        catch (DuplicateSubmissionException)
        {
            return await SingleSubmissionConflict.ResultAsync<SubmissionDto>(submissions, Spec(submission), cancellationToken);
        }
    }

    /// <summary>
    /// Not found when the submission was deleted since it was read, on its own or with its form; unavailable when the
    /// save lost a deadlock. Either way nothing is saved.
    /// </summary>
    private async Task<Result<SubmissionDto>> SaveAsync(Submission submission, CancellationToken cancellationToken)
    {
        var saved = await submissions.TrySaveChangesAsync(cancellationToken);
        return saved.IsSuccess
            ? Result.Success(SubmissionDto.FromSubmission(submission))
            : saved.ToErrorResult<SubmissionDto>();
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
