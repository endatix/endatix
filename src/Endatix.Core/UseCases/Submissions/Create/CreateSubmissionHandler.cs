using MediatR;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.Abstractions.Forms;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Features.ReCaptcha;
using Ardalis.GuardClauses;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Submitters;
using Endatix.Core.Exceptions;

namespace Endatix.Core.UseCases.Submissions.Create;

public class CreateSubmissionHandler(
    IRepository<Submission> submissionRepository,
    IFormsRepository formRepository,
    ISubmissionTokenService tokenService,
    IReCaptchaPolicyService recaptchaService,
    IMediator mediator,
    ICurrentUserAuthorizationService authorizationService,
    ISubmitterResolver submitterResolver,
    IFormSubmissionsLock formSubmissionsLock
    ) : ICommandHandler<CreateSubmissionCommand, Result<Submission>>
{
    private const bool DEFAULT_IS_COMPLETE = false;
    private const int DEFAULT_CURRENT_PAGE = 0;
    private const string DEFAULT_METADATA = "{}";

    private const string DEFAULT_JSON_DATA = "{}";
    private const string FORM_NOT_FOUND_MESSAGE = "Form not found. Cannot create a submission";

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
            return Result.NotFound(FORM_NOT_FOUND_MESSAGE);
        }

        var accessResult = await AuthorizeAsync(formWithActiveDefinition, request.RequiredPermission, cancellationToken);
        if (!accessResult.IsSuccess)
        {
            return accessResult;
        }

        var submitterResolution = await submitterResolver.EnsureSubmitterAsync(
            new SubmitterResolveContext(
                activeDefinition!.TenantId,
                request.SubmitterPrincipal,
                request.Submitter),
            cancellationToken);

        var submitterId = submitterResolution.SubmitterId;
        var hasTrustedSubmitterPayload = request.Submitter is not null;
        var canBypassSingleSubmissionLimit = false;
        if (formWithActiveDefinition.LimitOnePerUser && submitterId is not null && !hasTrustedSubmitterPayload)
        {
            var hasTestPermissionResult = await HasTestPermissionAsync(cancellationToken);
            if (!hasTestPermissionResult.IsSuccess)
            {
                return hasTestPermissionResult.ToErrorResult<Submission>();
            }

            canBypassSingleSubmissionLimit = hasTestPermissionResult.Value;
        }

        var screenOut = CollectionOutcomes.IsScreenOut(request.CollectionOutcome);

        // A screen-out ends the interview like a complete, so it gets the same bot check.
        var validationContext = new SubmissionVerificationContext(
            formWithActiveDefinition,
            screenOut || (request.IsComplete ?? DEFAULT_IS_COMPLETE),
            request.JsonData,
            request.ReCaptchaToken
        );
        var recaptchaResult = await recaptchaService.ValidateReCaptchaAsync(validationContext, cancellationToken);
        if (!recaptchaResult.IsSuccess)
        {
            return Result.Invalid(ReCaptchaErrors.ValidationErrors.ReCaptchaVerificationFailed);
        }

        var shouldEnforceSingleSubmissionGate =
            formWithActiveDefinition.LimitOnePerUser &&
            submitterId is not null &&
            !canBypassSingleSubmissionLimit;

        var submission = Submission.Create(new SubmissionCreateArgs(
            TenantId: activeDefinition!.TenantId,
            FormId: request.FormId,
            FormDefinitionId: activeDefinition.Id,
            JsonData: request.JsonData ?? DEFAULT_JSON_DATA,
            IsComplete: !screenOut && (request.IsComplete ?? DEFAULT_IS_COMPLETE),
            CurrentPage: request.CurrentPage ?? DEFAULT_CURRENT_PAGE,
            Metadata: request.Metadata ?? DEFAULT_METADATA,
            SubmitterId: submitterId,
            SubmitterDisplayId: submitterResolution.DisplayId,
            SubmitterProfileSnapshot: submitterResolution.ProfileSnapshot,
            IsTestSubmission: canBypassSingleSubmissionLimit,
            EnforceSingleSubmissionGate: shouldEnforceSingleSubmissionGate,
            // Public/authenticated Create = respondent engagement; CreateOnBehalf = prefill.
            StartSubmission: string.Equals(
                request.RequiredPermission,
                Actions.Submissions.Create,
                StringComparison.Ordinal)));

        ApplySnapshot(submission, request.PersonalizationSnapshot);
        submission.UpdateToken(await tokenService.NewTokenAsync(formWithActiveDefinition, cancellationToken));
        if (screenOut)
        {
            submission.ScreenOut();
        }

        Result<Submission>? refused = await PersistAsync(
            new PersistArgs(submission, shouldEnforceSingleSubmissionGate, submitterId, request.FormId),
            cancellationToken);
        if (refused is not null)
        {
            return refused;
        }

        if (submission.IsComplete)
        {
            await mediator.Publish(new SubmissionCompletedEvent(submission), cancellationToken);
        }

        return Result<Submission>.Created(submission);
    }

    /// <summary>A public form takes submissions from anyone; any other needs <paramref name="requiredPermission"/>.</summary>
    private async Task<Result> AuthorizeAsync(Form form, string requiredPermission, CancellationToken cancellationToken) =>
        form.IsPublic
            ? Result.Success()
            : await authorizationService.ValidateAccessAsync(requiredPermission, cancellationToken);

    /// <summary>Whether the caller may test the form, which lets them past its one-response limit.</summary>
    private async Task<Result<bool>> HasTestPermissionAsync(CancellationToken cancellationToken)
    {
        var result = await authorizationService.HasPermissionAsync(Actions.Forms.Test, cancellationToken);
        if (result.IsSuccess)
        {
            return result;
        }

        return result.Status switch
        {
            ResultStatus.Unauthorized => Result.Unauthorized($"Permission '{Actions.Forms.Test}' access check failed."),
            ResultStatus.Forbidden => Result.Forbidden($"Permission '{Actions.Forms.Test}' required to access this resource."),
            _ => Result.Error($"Permission '{Actions.Forms.Test}' access check failed.")
        };
    }

    private static void ApplySnapshot(Submission submission, string? snapshot)
    {
        if (!string.IsNullOrEmpty(snapshot))
        {
            submission.FreezeOnBehalf(snapshot);
        }
    }

    /// <summary>Stores the submission; <see langword="null"/> when it is stored, else why it was refused.</summary>
    private async Task<Result<Submission>?> PersistAsync(PersistArgs args, CancellationToken cancellationToken)
    {
        Result<Submission>? conflict = await ExistingConflictAsync(args, cancellationToken);
        if (conflict is not null)
        {
            return conflict;
        }

        try
        {
            return await InsertLockedAsync(args, cancellationToken);
        }
        catch (DuplicateSubmissionException)
        {
            return await DuplicateConflictAsync(Spec(args), cancellationToken);
        }
    }

    /// <summary>
    /// Inserts under the form's lock, after reading the form again: the check in <see cref="Handle"/> ran before
    /// the lock, and the form's deletion may have committed in between.
    /// </summary>
    private async Task<Result<Submission>?> InsertLockedAsync(PersistArgs args, CancellationToken cancellationToken)
    {
        var locked = await formSubmissionsLock.BeginAddingAsync(args.FormId, cancellationToken);
        if (!locked.IsSuccess)
        {
            return locked.ToErrorResult<Submission>();
        }

        await using var section = locked.Value;
        var formSpec = new FormSpecifications.ByIdForTenant(args.FormId, args.Submission.TenantId);
        if (!await formRepository.AnyAsync(formSpec, cancellationToken))
        {
            return Result.NotFound(FORM_NOT_FOUND_MESSAGE);
        }

        await submissionRepository.AddAsync(args.Submission, cancellationToken);
        await section.CommitAsync(cancellationToken);
        return null;
    }

    private async Task<Result<Submission>?> ExistingConflictAsync(
        PersistArgs args,
        CancellationToken cancellationToken)
    {
        if (!args.EnforceGate)
        {
            return null;
        }

        // Fast path only; the UX_Submissions_RestrictionKey unique index is the concurrency authority.
        SubmissionByFormIdAndSubmitterIdSpec spec = Spec(args);
        bool exists = await submissionRepository.AnyAsync(spec, cancellationToken);
        return exists ? await DuplicateConflictAsync(spec, cancellationToken) : null;
    }

    private static SubmissionByFormIdAndSubmitterIdSpec Spec(PersistArgs args) =>
        new(args.FormId, args.SubmitterId ?? throw new InvalidOperationException(
            "A single-submission conflict requires a submitter."));

    private sealed record PersistArgs(
        Submission Submission,
        bool EnforceGate,
        long? SubmitterId,
        long FormId);

    private Task<Result<Submission>> DuplicateConflictAsync(
        SubmissionByFormIdAndSubmitterIdSpec spec,
        CancellationToken cancellationToken) =>
        SingleSubmissionConflict.ResultAsync<Submission>(submissionRepository, spec, cancellationToken);
}
