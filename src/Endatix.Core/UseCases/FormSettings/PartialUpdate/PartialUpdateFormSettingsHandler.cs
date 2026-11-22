using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.FormSettings.PartialUpdate;

/// <summary>
/// Replaces the named sections of a form's settings. Not found when the form is missing, deleted or another tenant's; a
/// conflict when the version is stale or another write won the race. A change that matches what is stored saves nothing.
/// </summary>
internal sealed class PartialUpdateFormSettingsHandler(
    IRepository<Form> forms,
    IFormSettingsRepository repository,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<PartialUpdateFormSettingsCommand, Result<FormSettingsDto>>
{
    /// <inheritdoc/>
    public async Task<Result<FormSettingsDto>> Handle(PartialUpdateFormSettingsCommand request, CancellationToken cancellationToken)
    {
        var form = await forms.GetByIdAsync(request.FormId, cancellationToken);
        if (form is null)
        {
            return Result.NotFound("Form not found.");
        }

        var settings = await repository.FindAsync(form.Id, cancellationToken)
            ?? new Entities.FormSettings(form.TenantId, form.Id);
        var applied = settings.Apply(request.Change, request.ExpectedVersion, dateTimeProvider.Now.UtcDateTime);
        if (!applied.IsSuccess)
        {
            return applied.ToErrorResult<FormSettingsDto>();
        }

        if (applied.Value && !await repository.SaveOrConcedeAsync(settings, cancellationToken))
        {
            return Result.Conflict("The form settings changed while they were being saved; read them again and retry.");
        }

        return FormSettingsDto.From(settings);
    }
}
