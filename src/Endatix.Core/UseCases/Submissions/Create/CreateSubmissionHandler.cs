using MediatR;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Abstractions.Data;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Features.ReCaptcha;
using Ardalis.GuardClauses;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Exceptions;
using System.Data;

namespace Endatix.Core.UseCases.Submissions.Create;

public class CreateSubmissionHandler(
    IRepository<Submission> submissionRepository,
    IFormsRepository formRepository,
    ISubmissionTokenService tokenService,
    IReCaptchaPolicyService recaptchaService,
    IMediator mediator,
    ICurrentUserAuthorizationService authorizationService,
    IUnitOfWork unitOfWork
    ) : ICommandHandler<CreateSubmissionCommand, Result<Submission>>
{
    private const bool DEFAULT_IS_COMPLETE = false;
    private const int DEFAULT_CURRENT_PAGE = 0;
    private const string DEFAULT_METADATA = "{}";

    private const string DEFAULT_JSON_DATA = "{}";
    private const string DUPLICATE_CONFLICT_MESSAGE = "A submission already exists for this user and form.";

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

        var shouldEnforceSingleSubmissionGate =
            formWithActiveDefinition.LimitOnePerUser &&
            !string.IsNullOrWhiteSpace(request.SubmittedBy) &&
            !canBypassSingleSubmissionLimit;

        if (shouldEnforceSingleSubmissionGate)
        {
            await unitOfWork.BeginTransactionAsync(cancellationToken, IsolationLevel.Serializable);
            try
            {
                var duplicateSpec = new SubmissionByFormIdAndSubmittedBySpec(request.FormId, request.SubmittedBy!);
                var hasExistingSubmission = await submissionRepository.AnyAsync(
                    duplicateSpec,
                    cancellationToken);

                if (hasExistingSubmission)
                {
                    await SafeRollbackAsync(unitOfWork, cancellationToken);
                    return Result<Submission>.Conflict(DUPLICATE_CONFLICT_MESSAGE);
                }

                await submissionRepository.AddAsync(submission, cancellationToken);
                try
                {
                    await unitOfWork.CommitTransactionAsync(cancellationToken);
                }
                catch (DuplicateSubmissionException)
                {
                    await SafeRollbackAsync(unitOfWork, cancellationToken);
                    return Result<Submission>.Conflict(DUPLICATE_CONFLICT_MESSAGE);
                }
                catch (Exception commitException) when (IsSerializationOrDeadlockLikeException(commitException))
                {
                    await SafeRollbackAsync(unitOfWork, cancellationToken);
                    var hasExistingSubmissionAfterFailure = await submissionRepository.AnyAsync(duplicateSpec, cancellationToken);
                    if (hasExistingSubmissionAfterFailure)
                    {
                        return Result<Submission>.Conflict(DUPLICATE_CONFLICT_MESSAGE);
                    }

                    throw;
                }
            }
            catch (DuplicateSubmissionException)
            {
                await SafeRollbackAsync(unitOfWork, cancellationToken);
                return Result<Submission>.Conflict(DUPLICATE_CONFLICT_MESSAGE);
            }
            catch
            {
                await SafeRollbackAsync(unitOfWork, cancellationToken);
                throw;
            }
        }
        else
        {
            try
            {
                await submissionRepository.AddAsync(submission, cancellationToken);
            }
            catch (DuplicateSubmissionException)
            {
                return Result<Submission>.Conflict(DUPLICATE_CONFLICT_MESSAGE);
            }
        }

        await tokenService.ObtainTokenAsync(submission.Id, cancellationToken);

        if (submission.IsComplete)
        {
            await mediator.Publish(new SubmissionCompletedEvent(submission), cancellationToken);
        }

        return Result<Submission>.Created(submission);
    }

    private static async Task SafeRollbackAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
        }
        catch (InvalidOperationException rollbackException) when (
            rollbackException.Message.Contains("Transaction not started", StringComparison.Ordinal))
        {
            // Commit can dispose/null the transaction; avoid masking the original failure.
        }
    }

    private static bool IsSerializationOrDeadlockLikeException(Exception exception)
    {
        var current = exception;
        while (current is not null)
        {
            if (IsPostgreSqlSerializationOrDeadlock(current) || IsSqlServerSerializationOrDeadlock(current))
            {
                return true;
            }

            if (current.Message.Contains("deadlock", StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("serialize", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            current = current.InnerException;
        }

        return false;
    }

    private static bool IsPostgreSqlSerializationOrDeadlock(Exception exception)
    {
        if (!string.Equals(exception.GetType().Name, "PostgresException", StringComparison.Ordinal))
        {
            return false;
        }

        var sqlState = exception.GetType().GetProperty("SqlState")?.GetValue(exception) as string;
        return sqlState is "40001" or "40P01";
    }

    private static bool IsSqlServerSerializationOrDeadlock(Exception exception)
    {
        if (!string.Equals(exception.GetType().Name, "SqlException", StringComparison.Ordinal))
        {
            return false;
        }

        var numberValue = exception.GetType().GetProperty("Number")?.GetValue(exception);
        if (numberValue is not int number)
        {
            return false;
        }

        return number is 1205 or 3960;
    }
}
