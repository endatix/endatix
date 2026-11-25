using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.FormDefinitions;

namespace Endatix.Core.UseCases.FormSettings.Get;

/// <summary>Reads a form's settings. Not found when the form is missing, deleted or another tenant's.</summary>
internal sealed class GetFormSettingsHandler(IRepository<Form> forms, IFormSettingsRepository settings,
    ManagedFormContentAccess? access = null)
    : IQueryHandler<GetFormSettingsQuery, Result<FormSettingsDto>>
{
    /// <inheritdoc/>
    public async Task<Result<FormSettingsDto>> Handle(GetFormSettingsQuery request, CancellationToken cancellationToken)
    {
        if (access is not null && await access.FormAsync<FormSettingsDto>(forms, request.FormId, cancellationToken) is { } refusal)
        {
            return refusal;
        }

        return await ExecuteAsync(request, cancellationToken);
    }

    private async Task<Result<FormSettingsDto>> ExecuteAsync(GetFormSettingsQuery request, CancellationToken cancellationToken)
    {
        var form = await forms.GetByIdAsync(request.FormId, cancellationToken);
        if (form is null)
        {
            return Result.NotFound("Form not found.");
        }

        return FormSettingsDto.From(await settings.ReadAsync(form, cancellationToken));
    }
}
