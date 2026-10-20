using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Modules.Audience.Features.Links;

/// <summary>
/// The submission already written for this link, including one whose claim never committed. The form narrows
/// it to <c>IX_Submissions_FormId</c>: <c>AudienceLinkId</c> has no index. A submission marked as test is not the
/// link's any more: marking it freed the link, which then opens a fresh one.
/// </summary>
internal sealed class SubmissionForLinkSpec : Specification<Submission>
{
    public SubmissionForLinkSpec(long formId, long audienceLinkId) =>
        Query.Where(submission => submission.FormId == formId
                && submission.AudienceLinkId == audienceLinkId
                && !submission.IsTestSubmission)
            .OrderBy(submission => submission.Id);
}

/// <summary>Whether the form can be opened: enabled, its definition, and whether it allows one response.</summary>
internal sealed record OpenForm(bool IsEnabled, long? DefinitionId, bool LimitOnePerUser);

/// <summary>Those three columns only, untracked, so an open does not load the form aggregate.</summary>
internal sealed class OpenFormSpec : Specification<Form, OpenForm>
{
    public OpenFormSpec(long formId) =>
        Query.Where(form => form.Id == formId)
            .AsNoTracking()
            .Select(form => new OpenForm(form.IsEnabled, form.ActiveDefinitionId, form.LimitOnePerUser));
}

/// <summary>A Submitter's label alone, untracked; <see langword="null"/> when it has none.</summary>
internal sealed class SubmitterLabelSpec : SingleResultSpecification<Submitter, string?>
{
    public SubmitterLabelSpec(long submitterId) =>
        Query.Where(submitter => submitter.Id == submitterId)
            .AsNoTracking()
            .Select(submitter => submitter.DisplayId);
}

/// <summary>
/// What a resume decides on: whether the submission has its token, whether its form is enabled, and whether it was
/// marked as test.
/// </summary>
internal sealed record ResumeState(bool HasToken, bool FormIsEnabled, bool IsTest);

/// <summary>
/// One untracked read for both. <see langword="null"/> when the submission is gone, or its form: the form's
/// query filter drops the row.
/// </summary>
internal sealed class SubmissionResumeSpec : Specification<Submission, ResumeState>
{
    public SubmissionResumeSpec(long submissionId) =>
        Query.Where(submission => submission.Id == submissionId)
            .AsNoTracking()
            .Select(submission => new ResumeState(
                submission.Token!.Value != null,
                submission.Form.IsEnabled,
                submission.IsTestSubmission));
}
