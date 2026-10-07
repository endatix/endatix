using MediatR;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Submissions.PartialUpdate;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Features.ReCaptcha;
using Endatix.Core.Abstractions.Repositories;

namespace Endatix.Core.UseCases.Submissions.PartialUpdateByToken;

/// <summary>
/// Handler for partially updating a form submission by token.
/// </summary>
public class PartialUpdateSubmissionByTokenHandler(
    ISender sender,
    ISubmissionTokenService tokenService,
    IReCaptchaPolicyService recaptchaService,
    IFormsRepository formsRepository
    ) : ICommandHandler<PartialUpdateSubmissionByTokenCommand, Result<Submission>>
{
    public async Task<Result<Submission>> Handle(PartialUpdateSubmissionByTokenCommand request, CancellationToken cancellationToken)
    {
        var tokenResult = await tokenService.ResolveTokenAsync(request.Token, cancellationToken);
        if (!tokenResult.IsSuccess)
        {
            return Result.Invalid(SubmissonTokenErrors.ValidationErrors.SubmissionTokenInvalid);
        }

        var submissionId = tokenResult.Value;

        var form = await formsRepository.GetByIdAsync(request.FormId, cancellationToken);
        if (form == null || !form.IsEnabled)
        {
            return Result.NotFound("Form not found");
        }

        var validationResult = await ValidateReCaptchaAsync(form, request, cancellationToken);
        if (!validationResult.IsSuccess)
        {
            return validationResult;
        }

        await tokenService.ObtainTokenAsync(submissionId, cancellationToken);


        var partialUpdateSubmissionCommand = new PartialUpdateSubmissionCommand(
            SubmissionId: submissionId,
            FormId: request.FormId,
            IsComplete: request.IsComplete,
            CurrentPage: request.CurrentPage,
            JsonData: request.JsonData,
            Metadata: request.Metadata,
            CollectionOutcome: request.CollectionOutcome);

        return await sender.Send(partialUpdateSubmissionCommand, cancellationToken);
    }

    private async Task<Result> ValidateReCaptchaAsync(Form form, PartialUpdateSubmissionByTokenCommand request, CancellationToken cancellationToken)
    {
        // A screen-out ends the interview like a complete, so it gets the same bot check.
        var endsInterview = (request.IsComplete ?? false) || CollectionOutcomes.IsScreenOut(request.CollectionOutcome);
        var validationContext = new SubmissionVerificationContext(
           form,
           endsInterview,
           request.JsonData,
           request.ReCaptchaToken
       );

        var recaptchaResult = await recaptchaService.ValidateReCaptchaAsync(validationContext, cancellationToken);

        if (!recaptchaResult.IsSuccess)
        {
            return Result.Invalid(ReCaptchaErrors.ValidationErrors.ReCaptchaVerificationFailed);
        }

        return Result.Success();
    }
}
