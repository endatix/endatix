using System.Diagnostics.CodeAnalysis;
using Ardalis.Specification;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.FormSettings.PartialUpdate;

/// <summary>Reads the retry metadata for an admitted write that returned a conflict.</summary>
[Experimental("ENDATIX0005")]
public sealed record ReadConflictingFormSettingsVersion(long FormId) : IQuery<long?>;

internal sealed class ReadConflictingFormSettingsVersionHandler(
    ITenantContext tenant, IRepository<Form> forms, IRepository<Entities.FormSettings> settings)
    : IQueryHandler<ReadConflictingFormSettingsVersion, long?>
{
    public async Task<long?> Handle(ReadConflictingFormSettingsVersion request, CancellationToken cancellationToken)
    {
        if (tenant.TenantId <= 0 || !await forms.AnyAsync(new FormSpecifications.ByIdForTenant(request.FormId, tenant.TenantId), cancellationToken))
        {
            return null;
        }
        return await settings.FirstOrDefaultAsync(new Version(request.FormId, tenant.TenantId), cancellationToken)
            ?? Entities.FormSettings.UNSAVED_VERSION;
    }

    private sealed class Version : Specification<Entities.FormSettings, long?>
    {
        internal Version(long formId, long tenantId)
        {
            Query.Where(s => s.FormId == formId && s.TenantId == tenantId).Select(s => s.Version);
        }
    }
}
