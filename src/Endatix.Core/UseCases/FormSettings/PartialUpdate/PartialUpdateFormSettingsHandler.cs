using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.FormDefinitions;

namespace Endatix.Core.UseCases.FormSettings.PartialUpdate;

/// <summary>
/// Replaces the named sections of a form's settings. Not found when the form is missing, deleted or another tenant's; a
/// conflict when the version is stale or another write won the race. A change that matches what is stored saves nothing.
/// </summary>
internal sealed class PartialUpdateFormSettingsHandler(IRepository<Form> forms,
    IFormSettingsRepository repository,
    IDateTimeProvider dateTimeProvider,
    ManagedFormContentAccess? access = null)
    : ICommandHandler<PartialUpdateFormSettingsCommand, Result<FormSettingsDto>>
{
    /// <inheritdoc/>
    public async Task<Result<FormSettingsDto>> Handle(PartialUpdateFormSettingsCommand request, CancellationToken cancellationToken)
    {
        if (access is not null && await access.FormAsync<FormSettingsDto>(request.FormId, cancellationToken) is { } refusal)
        {
            return refusal;
        }

        return await ExecuteAsync(request, cancellationToken);
    }

    private async Task<Result<FormSettingsDto>> ExecuteAsync(PartialUpdateFormSettingsCommand request, CancellationToken cancellationToken)
    {
        var form = await forms.GetByIdAsync(request.FormId, cancellationToken);
        return form is null
            ? Result.NotFound("Form not found.")
            : await ApplyAsync(form, request, cancellationToken);
    }

    private async Task<Result<FormSettingsDto>> ApplyAsync(
        Form form,
        PartialUpdateFormSettingsCommand request,
        CancellationToken cancellationToken)
    {
        var settings = await repository.FindAsync(form.Id, cancellationToken)
            ?? Entities.FormSettings.For(form);
        var applied = Apply(settings, request);
        if (!applied.IsSuccess)
        {
            return applied.ToErrorResult<FormSettingsDto>();
        }

        return !applied.Value || await repository.SaveOrConcedeAsync(settings, cancellationToken)
            ? FormSettingsDto.From(settings)
            : await AfterLostRaceAsync(form, request, cancellationToken);
    }

    // Another write won the race. When it stored this very value (a retried or doubled request), the write is a no-op
    // like any other; otherwise it is a conflict.
    private async Task<Result<FormSettingsDto>> AfterLostRaceAsync(
        Form form,
        PartialUpdateFormSettingsCommand request,
        CancellationToken cancellationToken)
    {
        var winner = await repository.FindAsync(form.Id, cancellationToken);
        return winner is not null && Apply(winner, request) is { IsSuccess: true, Value: false }
            ? FormSettingsDto.From(winner)
            : Result.Conflict("The form settings changed while they were being saved; read them again and retry.");
    }

    private Result<bool> Apply(Entities.FormSettings settings, PartialUpdateFormSettingsCommand request) =>
        settings.Apply(request.Change, request.ExpectedVersion, dateTimeProvider.Now.UtcDateTime);
}
