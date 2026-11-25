using System.Diagnostics.CodeAnalysis;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.FormSettings.PartialUpdate;

/// <summary>Reads the retry metadata for an admitted write that returned a conflict.</summary>
[Experimental("ENDATIX0005")]
public sealed record ReadConflictingFormSettingsVersion(long FormId) : IQuery<long?>;

internal sealed class ReadConflictingFormSettingsVersionHandler(
    ITenantContext tenant, IRepository<Form> forms, IFormSettingsRepository settings)
    : IQueryHandler<ReadConflictingFormSettingsVersion, long?>
{
    public async Task<long?> Handle(ReadConflictingFormSettingsVersion request, CancellationToken cancellationToken)
    {
        if (tenant.TenantId <= 0)
        {
            return null;
        }
        var form = await forms.SingleOrDefaultAsync(new FormSpecifications.ByIdForTenant(request.FormId, tenant.TenantId), cancellationToken);
        return form is null ? null : (await settings.ReadAsync(form, cancellationToken)).Version;
    }
}
