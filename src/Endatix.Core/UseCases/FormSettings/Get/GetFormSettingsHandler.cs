using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.FormSettings.Get;

/// <summary>Reads a form's settings. Not found when the form is missing, deleted or another tenant's.</summary>
internal sealed class GetFormSettingsHandler(IRepository<Form> forms, IFormSettingsRepository settings, ITenantContext tenant)
    : IQueryHandler<GetFormSettingsQuery, Result<FormSettingsDto>>
{
    /// <inheritdoc/>
    public async Task<Result<FormSettingsDto>> Handle(GetFormSettingsQuery request, CancellationToken cancellationToken)
    {
        var form = await forms.FindAsync(request.FormId, tenant, cancellationToken);
        if (form is null)
        {
            return Result.NotFound("Form not found.");
        }

        return FormSettingsDto.From(await settings.ReadAsync(form, cancellationToken));
    }
}
