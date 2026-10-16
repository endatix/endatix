using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;

namespace Endatix.Modules.Reporting.Data;

internal static class TenantFormExtensions
{
    /// <summary>
    /// Whether the form exists in the tenant. Read with the tenant compared explicitly, so another tenant's form reads
    /// as missing whatever the ambient tenant is.
    /// </summary>
    public static async Task<bool> IsTenantFormAsync(
        this IRepository<Form> formsRepository,
        long tenantId,
        long formId,
        CancellationToken cancellationToken)
    {
        var form = await formsRepository.SingleOrDefaultAsync(new FormSpecifications.ByIdReadOnly(formId), cancellationToken);
        return form is not null && form.TenantId == tenantId;
    }
}
