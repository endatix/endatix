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
    private const string BUSY_MESSAGE = "The form is busy; try again shortly.";

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

        var draft = new SubmissionDraft(
            request,
            submitterResolution,
            await tokenService.NewTokenAsync(formWithActiveDefinition, cancellationToken),
            new SubmissionGate(canBypassSingleSubmissionLimit, shouldEnforceSingleSubmissionGate),
            activeDefinition.TenantId);
        var persisted = await PersistAsync(draft, cancellationToken);
        if (persisted.IsSuccess && persisted.Value.IsComplete)
        {
            await mediator.Publish(new SubmissionCompletedEvent(persisted.Value), cancellationToken);
        }

        return persisted;
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

    /// <summary>Stores a new submission, created, or says why it was refused.</summary>
    private async Task<Result<Submission>> PersistAsync(SubmissionDraft draft, CancellationToken cancellationToken)
    {
        Result<Submission>? conflict = await ExistingConflictAsync(draft, cancellationToken);
        return conflict ?? await InsertAsync(draft, cancellationToken);
    }

    /// <summary>Inserts, or answers the refusal of a concurrent writer: a duplicate, or a deadlock it lost.</summary>
    private async Task<Result<Submission>> InsertAsync(SubmissionDraft draft, CancellationToken cancellationToken)
    {
        try
        {
            return await InsertLockedAsync(draft, cancellationToken);
        }
        catch (DuplicateSubmissionException)
        {
            return await DuplicateConflictAsync(Spec(draft), cancellationToken);
        }
        catch (DeadlockVictimException)
        {
            return Result<Submission>.Unavailable(BUSY_MESSAGE);
        }
    }

    /// <summary>
    /// Inserts under the form's lock, after reading the form again, as the check in <see cref="Handle"/> does: that
    /// check ran before the lock, and the form may have been deleted, disabled or given another active definition
    /// since. The submission is built against the definition read here.
    /// </summary>
    private async Task<Result<Submission>> InsertLockedAsync(SubmissionDraft draft, CancellationToken cancellationToken)
    {
        var locked = await formSubmissionsLock.BeginAddingAsync(draft.FormId, cancellationToken);
        if (!locked.IsSuccess)
        {
            return locked.ToErrorResult<Submission>();
        }

        await using var section = locked.Value;
        var openForm = new FormSpecifications.OpenFormActiveDefinitionId(draft.FormId, draft.TenantId);
        if (await formRepository.FirstOrDefaultAsync(openForm, cancellationToken) is not long definitionId)
        {
            return Result.NotFound(FORM_NOT_FOUND_MESSAGE);
        }

        var submission = draft.Build(definitionId);
        await submissionRepository.AddAsync(submission, cancellationToken);
        await section.CommitAsync(cancellationToken);
        return Result<Submission>.Created(submission);
    }

    private async Task<Result<Submission>?> ExistingConflictAsync(
        SubmissionDraft draft,
        CancellationToken cancellationToken)
    {
        if (!draft.EnforceGate)
        {
            return null;
        }

        // Fast path only; the UX_Submissions_RestrictionKey unique index is the concurrency authority.
        SubmissionByFormIdAndSubmitterIdSpec spec = Spec(draft);
        bool exists = await submissionRepository.AnyAsync(spec, cancellationToken);
        return exists ? await DuplicateConflictAsync(spec, cancellationToken) : null;
    }

    private static SubmissionByFormIdAndSubmitterIdSpec Spec(SubmissionDraft draft) =>
        new(draft.FormId, draft.SubmitterId ?? throw new InvalidOperationException(
            "A single-submission conflict requires a submitter."));

    /// <summary>Whether the submission is a test one, and whether it carries the one-response restriction key.</summary>
    private sealed record SubmissionGate(bool IsTest, bool Enforce);

    /// <summary>
    /// Everything the new submission is made of but its definition, which is read once the form's lock is held.
    /// </summary>
    private sealed record SubmissionDraft(
        CreateSubmissionCommand Request,
        SubmitterResolution Submitter,
        Token Token,
        SubmissionGate Gate,
        long TenantId)
    {
        public long FormId => Request.FormId;

        public long? SubmitterId => Submitter.SubmitterId;

        public bool EnforceGate => Gate.Enforce;

        public Submission Build(long definitionId)
        {
            var screenOut = CollectionOutcomes.IsScreenOut(Request.CollectionOutcome);
            var submission = Submission.Create(CreateArgs(definitionId, screenOut));
            ApplySnapshot(submission, Request.PersonalizationSnapshot);
            submission.UpdateToken(Token);
            if (screenOut)
            {
                submission.ScreenOut();
            }

            return submission;
        }

        private static void ApplySnapshot(Submission submission, string? snapshot)
        {
            if (!string.IsNullOrEmpty(snapshot))
            {
                submission.FreezeOnBehalf(snapshot);
            }
        }

        private SubmissionCreateArgs CreateArgs(long definitionId, bool screenOut) =>
            new(
                TenantId: TenantId,
                FormId: Request.FormId,
                FormDefinitionId: definitionId,
                JsonData: Request.JsonData ?? DEFAULT_JSON_DATA,
                IsComplete: !screenOut && (Request.IsComplete ?? DEFAULT_IS_COMPLETE),
                CurrentPage: Request.CurrentPage ?? DEFAULT_CURRENT_PAGE,
                Metadata: Request.Metadata ?? DEFAULT_METADATA,
                SubmitterId: Submitter.SubmitterId,
                SubmitterDisplayId: Submitter.DisplayId,
                SubmitterProfileSnapshot: Submitter.ProfileSnapshot,
                IsTestSubmission: Gate.IsTest,
                EnforceSingleSubmissionGate: Gate.Enforce,
                // Public/authenticated Create = respondent engagement; CreateOnBehalf = prefill.
                StartSubmission: string.Equals(
                    Request.RequiredPermission,
                    Actions.Submissions.Create,
                    StringComparison.Ordinal));
    }

    private Task<Result<Submission>> DuplicateConflictAsync(
        SubmissionByFormIdAndSubmitterIdSpec spec,
        CancellationToken cancellationToken) =>
        SingleSubmissionConflict.ResultAsync<Submission>(submissionRepository, spec, cancellationToken);
}
