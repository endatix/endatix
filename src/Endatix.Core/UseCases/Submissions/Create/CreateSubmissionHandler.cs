using MediatR;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Features.ReCaptcha;
using Ardalis.GuardClauses;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Exceptions;

namespace Endatix.Core.UseCases.Submissions.Create;

public class CreateSubmissionHandler(
    IRepository<Submission> submissionRepository,
    IFormsRepository formRepository,
    ISubmissionTokenService tokenService,
    IReCaptchaPolicyService recaptchaService,
    IMediator mediator,
    ICurrentUserAuthorizationService authorizationService
    ) : ICommandHandler<CreateSubmissionCommand, Result<Submission>>
{
    private const bool DEFAULT_IS_COMPLETE = false;
    private const int DEFAULT_CURRENT_PAGE = 0;
    private const string DEFAULT_METADATA = "{}";

    private const string DEFAULT_JSON_DATA = "{}";

    public async Task<Result<Submission>> Handle(CreateSubmissionCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrEmpty(request.RequiredPermission);

        // Consider moving Domain event logic to a separate service should we decide to move UseCases in separate project. This way the Domain logic will stay in the core project. Will also centralize it in one place
        // This way the code will transform to  return await _submissionService.CreateSubmission(createSubmissionDto);
        var activeFormDefinitionSpec = new ActiveFormDefinitionByFormIdSpec(request.FormId);
        var formWithActiveDefinition = await formRepository.SingleOrDefaultAsync(activeFormDefinitionSpec, cancellationToken);
        var activeDefinition = formWithActiveDefinition?.ActiveDefinition;

        if (formWithActiveDefinition?.ActiveDefinition is null || !formWithActiveDefinition.IsEnabled)
        {
            return Result.NotFound("Form not found. Cannot create a submission");
        }

        if (!formWithActiveDefinition.IsPublic)
        {
            var accessResult = await authorizationService.ValidateAccessAsync(
                request.RequiredPermission,
                cancellationToken);

            if (!accessResult.IsSuccess)
            {
                return accessResult;
            }
        }

        var canBypassSingleSubmissionLimit = false;
        if (formWithActiveDefinition.LimitOnePerUser && !string.IsNullOrWhiteSpace(request.SubmittedBy))
        {
            var hasTestPermissionResult = await authorizationService.HasPermissionAsync(Actions.Forms.Test, cancellationToken);
            if (!hasTestPermissionResult.IsSuccess)
            {
                return hasTestPermissionResult.Status switch
                {
                    ResultStatus.Unauthorized => Result.Unauthorized($"Permission '{Actions.Forms.Test}' access check failed."),
                    ResultStatus.Forbidden => Result.Forbidden($"Permission '{Actions.Forms.Test}' required to access this resource."),
                    _ => Result.Error($"Permission '{Actions.Forms.Test}' access check failed.")
                };
            }

            canBypassSingleSubmissionLimit = hasTestPermissionResult.Value;
            if (!canBypassSingleSubmissionLimit)
            {
                var hasExistingSubmission = await submissionRepository.AnyAsync(
                    new SubmissionByFormIdAndSubmittedBySpec(request.FormId, request.SubmittedBy),
                    cancellationToken);

                if (hasExistingSubmission)
                {
                    return Result<Submission>.Conflict("A submission already exists for this user and form.");
                }
            }
        }

        var validationContext = new SubmissionVerificationContext(
            formWithActiveDefinition,
            request.IsComplete ?? DEFAULT_IS_COMPLETE,
            request.JsonData,
            request.ReCaptchaToken
        );
        var recaptchaResult = await recaptchaService.ValidateReCaptchaAsync(validationContext, cancellationToken);
        if (!recaptchaResult.IsSuccess)
        {
            return Result.Invalid(ReCaptchaErrors.ValidationErrors.ReCaptchaVerificationFailed);
        }

        var submission = Submission.Create(
            activeDefinition!.TenantId,
            jsonData: request.JsonData ?? DEFAULT_JSON_DATA,
            formId: request.FormId,
            formDefinitionId: activeDefinition!.Id,
            options: new SubmissionCreateOptions(
                IsComplete: request.IsComplete ?? DEFAULT_IS_COMPLETE,
                CurrentPage: request.CurrentPage ?? DEFAULT_CURRENT_PAGE,
                Metadata: request.Metadata ?? DEFAULT_METADATA,
                SubmittedBy: request.SubmittedBy,
                IsTestSubmission: canBypassSingleSubmissionLimit)
        );

        try
        {
            await submissionRepository.AddAsync(submission, cancellationToken);
        }
        catch (DuplicateSubmissionException)
        {
            return Result<Submission>.Conflict("A submission already exists for this user and form.");
        }
        await tokenService.ObtainTokenAsync(submission.Id, cancellationToken);

        if (submission.IsComplete)
        {
            await mediator.Publish(new SubmissionCompletedEvent(submission), cancellationToken);
        }

        return Result<Submission>.Created(submission);
    }
}
