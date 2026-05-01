using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Forms;
using MediatR;

namespace Endatix.Core.UseCases.Forms.Update;

public class UpdateFormHandler(
    IRepository<Form> repository,
    IRepository<Submission> submissionRepository,
    IMediator mediator) : ICommandHandler<UpdateFormCommand, Result<Form>>
{
    private const string ENABLE_CONFLICT_MESSAGE = "Cannot enable single submission gate because this form already has duplicate submissions.";
    private const string DISABLE_CONFLICT_MESSAGE = "Single submission gate cannot be disabled after it has been enabled.";
    private const string PUBLIC_CONFLICT_MESSAGE = "A single-submission form cannot be made public.";

    public async Task<Result<Form>> Handle(UpdateFormCommand request, CancellationToken cancellationToken)
    {
        var form = await repository.GetByIdAsync(request.FormId, cancellationToken);
        if (form == null)
        {
            return Result.NotFound("Form not found.");
        }

        var oldIsEnabled = form.IsEnabled;
        var requestedLimitOnePerUser = request.LimitOnePerUser ?? form.LimitOnePerUser;

        if (form.LimitOnePerUser && !requestedLimitOnePerUser)
        {
            return Result<Form>.Conflict(DISABLE_CONFLICT_MESSAGE);
        }

        if (form.IsPublic && requestedLimitOnePerUser)
        {
            return Result<Form>.Conflict(PUBLIC_CONFLICT_MESSAGE);
        }

        if (!form.LimitOnePerUser && requestedLimitOnePerUser)
        {
            var hasDuplicateEligibleSubmissions = await HasDuplicateEligibleSubmissionsAsync(
                form.Id,
                submissionRepository,
                cancellationToken);
            if (hasDuplicateEligibleSubmissions)
            {
                return Result<Form>.Conflict(ENABLE_CONFLICT_MESSAGE);
            }
        }

        form.Name = request.Name;
        form.Description = request.Description;
        form.IsEnabled = request.IsEnabled;
        form.LimitOnePerUser = requestedLimitOnePerUser;
        form.Metadata = request.Metadata;

        WebHookConfiguration? webHookConfig;

        if (string.IsNullOrWhiteSpace(request.WebHookSettingsJson))
        {
            webHookConfig = null;
        }
        else
        {
            webHookConfig = System.Text.Json.JsonSerializer.Deserialize<WebHookConfiguration>(request.WebHookSettingsJson);

            if (webHookConfig?.Events == null || webHookConfig.Events.Count == 0)
            {
                webHookConfig = null;
            }
        }

        form.UpdateWebHookSettings(webHookConfig);

        await repository.UpdateAsync(form, cancellationToken);

        await mediator.Publish(new FormUpdatedEvent(form), cancellationToken);

        if (oldIsEnabled != request.IsEnabled)
        {
            await mediator.Publish(new FormEnabledStateChangedEvent(form, request.IsEnabled), cancellationToken);
        }

        return Result.Success(form);
    }

    private static Task<bool> HasDuplicateEligibleSubmissionsAsync(
        long formId,
        IRepository<Submission> submissionRepository,
        CancellationToken cancellationToken) =>
        SingleSubmissionGateDuplicateChecker.HasDuplicateEligibleSubmissionsAsync(
            formId,
            submissionRepository,
            cancellationToken);
}
