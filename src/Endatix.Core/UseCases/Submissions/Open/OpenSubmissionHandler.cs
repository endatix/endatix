using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Forms;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Submissions.Create;
using MediatR;

namespace Endatix.Core.UseCases.Submissions.Open;

/// <summary>
/// Records the respondent's open (<see cref="Submission.Open"/>). A new submission goes through
/// <see cref="CreateSubmissionHandler"/>, so the open passes the same gate as a first save: an enabled form, its
/// access rule, one response per person and the form's lock. A resume names its submission by the respondent's
/// submission token, or by a share link's access token, which must carry the <c>submit</c> permission.
/// </summary>
public sealed class OpenSubmissionHandler(
    IRepository<Submission> submissions,
    IFormsRepository forms,
    ISubmissionTokenService submissionTokens,
    ISubmissionAccessTokenService accessTokens,
    ISender sender) : ICommandHandler<OpenSubmissionCommand, Result<Submission>>
{
    private const string FORM_NOT_FOUND_MESSAGE = "Form not found";
    private const string SUBMISSION_NOT_FOUND_MESSAGE = "Submission not found";
    private const string SUBMIT_PERMISSION_MESSAGE = "Token does not have submit permission";

    public async Task<Result<Submission>> Handle(OpenSubmissionCommand request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(request.SubmissionToken))
        {
            return await ResumeAsync(request.FormId, await BySubmissionTokenAsync(request.SubmissionToken, cancellationToken), cancellationToken);
        }

        if (!string.IsNullOrEmpty(request.AccessToken))
        {
            return await ResumeAsync(request.FormId, await ByAccessTokenAsync(request.AccessToken, cancellationToken), cancellationToken);
        }

        return await sender.Send(CreateCommand(request), cancellationToken);
    }

    private static CreateSubmissionCommand CreateCommand(OpenSubmissionCommand request) =>
        new(
            FormId: request.FormId,
            JsonData: null,
            Metadata: null,
            CurrentPage: null,
            IsComplete: false,
            ReCaptchaToken: null,
            RequiredPermission: Actions.Submissions.Create,
            SubmitterPrincipal: request.SubmitterPrincipal,
            OpenSubmission: true);

    private async Task<Result<long>> BySubmissionTokenAsync(string token, CancellationToken cancellationToken)
    {
        var resolved = await submissionTokens.ResolveTokenAsync(token, cancellationToken);
        return resolved.IsSuccess
            ? resolved
            : Result.Invalid(SubmissonTokenErrors.ValidationErrors.SubmissionTokenInvalid);
    }

    private async Task<Result<long>> ByAccessTokenAsync(string token, CancellationToken cancellationToken)
    {
        var validated = await accessTokens.ValidateAccessTokenAsync(token, cancellationToken);
        if (!validated.IsSuccess)
        {
            return Result.Invalid(validated.ValidationErrors);
        }

        return validated.Value.Permissions.Contains(SubmissionAccessTokenPermissions.Submit.Name)
            ? Result.Success(validated.Value.SubmissionId)
            : Result.Forbidden(SUBMIT_PERMISSION_MESSAGE);
    }

    private async Task<Result<Submission>> ResumeAsync(long formId, Result<long> submissionId, CancellationToken cancellationToken)
    {
        if (!submissionId.IsSuccess)
        {
            return submissionId.ToErrorResult<Submission>();
        }

        var form = await forms.GetByIdAsync(formId, cancellationToken);
        return form is { IsEnabled: true }
            ? await OpenExistingAsync(formId, submissionId.Value, cancellationToken)
            : Result.NotFound(FORM_NOT_FOUND_MESSAGE);
    }

    private async Task<Result<Submission>> OpenExistingAsync(long formId, long submissionId, CancellationToken cancellationToken)
    {
        var submission = await submissions.SingleOrDefaultAsync(
            new SubmissionByFormIdAndSubmissionIdSpec(formId, submissionId),
            cancellationToken);
        if (submission is null)
        {
            return Result.NotFound(SUBMISSION_NOT_FOUND_MESSAGE);
        }

        submission.Open();
        var saved = await submissions.TrySaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? Result.Success(submission) : saved.ToErrorResult<Submission>();
    }
}
