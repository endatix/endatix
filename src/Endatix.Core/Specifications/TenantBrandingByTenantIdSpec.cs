using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Core.Specifications;

/// <summary>
/// A tenant's branding, tracked: the save checks <see cref="TenantBranding.Revision"/> against the value read here.
/// </summary>
public sealed class TenantBrandingByTenantIdSpec : SingleResultSpecification<TenantBranding>
{
    public TenantBrandingByTenantIdSpec(long tenantId)
    {
        Query.Where(branding => branding.TenantId == tenantId);
    }
}
