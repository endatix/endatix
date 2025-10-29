using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Core.Specifications;

public class TenantSettingsByTenantIdSpec : Specification<TenantSettings>
{
    public TenantSettingsByTenantIdSpec(long tenantId)
    {
        Query
            .AsNoTracking()
            .Where(ts => ts.TenantId == tenantId);
    }
}
