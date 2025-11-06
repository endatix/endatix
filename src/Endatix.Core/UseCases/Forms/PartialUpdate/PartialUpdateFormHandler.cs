using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using MediatR;

namespace Endatix.Core.UseCases.Forms.PartialUpdate;

public class PartialUpdateFormHandler(
    IRepository<Form> repository,
    IRepository<Theme> themeRepository,
    IMediator mediator) : ICommandHandler<PartialUpdateFormCommand, Result<Form>>
{
    public async Task<Result<Form>> Handle(PartialUpdateFormCommand request, CancellationToken cancellationToken)
    {
        var form = await repository.GetByIdAsync(request.FormId, cancellationToken);
        if (form == null)
        {
            return Result.NotFound("Form not found.");
        }

        var oldIsEnabled = form.IsEnabled;
        form.Name = request.Name ?? form.Name;
        form.Description = request.Description ?? form.Description;
        form.IsEnabled = request.IsEnabled ?? form.IsEnabled;

        if (request.ThemeId.HasValue && form.ThemeId != request.ThemeId)
        {
            var theme = await themeRepository.GetByIdAsync(request.ThemeId.Value, cancellationToken);
            if (theme == null)
            {
                return Result.NotFound("Form Theme not found.");
            }
            form.SetTheme(theme);
        }

        // Update webhook settings:
        // - null or empty string will clear the webhook settings
        // - valid JSON string will set the webhook settings
        var webHookConfig = string.IsNullOrEmpty(request.WebHookSettingsJson)
            ? null
            : System.Text.Json.JsonSerializer.Deserialize<WebHookConfiguration>(request.WebHookSettingsJson);
        form.UpdateWebHookSettings(webHookConfig);

        await repository.UpdateAsync(form, cancellationToken);

        await mediator.Publish(new FormUpdatedEvent(form), cancellationToken);

        if (request.IsEnabled.HasValue && oldIsEnabled != request.IsEnabled.Value)
        {
            await mediator.Publish(new FormEnabledStateChangedEvent(form, request.IsEnabled.Value), cancellationToken);
        }

        return Result.Success(form);
    }
}
