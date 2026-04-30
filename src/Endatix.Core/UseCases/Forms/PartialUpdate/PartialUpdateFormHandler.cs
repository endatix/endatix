using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Forms;
using MediatR;

namespace Endatix.Core.UseCases.Forms.PartialUpdate;

public class PartialUpdateFormHandler(
    IRepository<Form> repository,
    IRepository<Theme> themeRepository,
    IRepository<Submission> submissionRepository,
    IMediator mediator) : ICommandHandler<PartialUpdateFormCommand, Result<Form>>
{
    private const long DEFAULT_THEME_ID = 0; // ThemeId of 0 means clear the theme (set to default)
    private const string ENABLE_CONFLICT_MESSAGE = "Cannot enable single submission gate because this form already has duplicate submissions.";
    private const string DISABLE_CONFLICT_MESSAGE = "Single submission gate cannot be disabled after it has been enabled.";
    private const string PUBLIC_CONFLICT_MESSAGE = "A single-submission form cannot be made public.";

    public async Task<Result<Form>> Handle(PartialUpdateFormCommand request, CancellationToken cancellationToken)
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

        var requestedIsPublic = request.IsPublic ?? form.IsPublic;
        if (requestedIsPublic && requestedLimitOnePerUser)
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

        form.Name = request.Name ?? form.Name;
        form.Description = request.Description ?? form.Description;
        form.IsEnabled = request.IsEnabled ?? form.IsEnabled;
        form.IsPublic = request.IsPublic ?? form.IsPublic;
        form.Metadata = request.Metadata ?? form.Metadata;
        form.LimitOnePerUser = requestedLimitOnePerUser;

        if (request.ThemeId.HasValue && form.ThemeId != request.ThemeId)
        {
            if (request.ThemeId.Value == DEFAULT_THEME_ID)
            {
                form.SetTheme(null);
            }
            else
            {
                var theme = await themeRepository.GetByIdAsync(request.ThemeId.Value, cancellationToken);
                if (theme == null)
                {
                    return Result.NotFound("Form Theme not found.");
                }
                form.SetTheme(theme);
            }
        }

        if (request.WebHookSettingsJson != null)
        {
            UpdateWebHookSettings(request, form);
        }

        await repository.UpdateAsync(form, cancellationToken);

        await mediator.Publish(new FormUpdatedEvent(form), cancellationToken);

        if (request.IsEnabled.HasValue && oldIsEnabled != request.IsEnabled.Value)
        {
            await mediator.Publish(new FormEnabledStateChangedEvent(form, request.IsEnabled.Value), cancellationToken);
        }

        return Result.Success(form);
    }

    private static void UpdateWebHookSettings(PartialUpdateFormCommand request, Form form)
    {
        WebHookConfiguration? webHookConfig;

        if (request.WebHookSettingsJson!.Trim() == string.Empty)
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
