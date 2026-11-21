using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Core.Specifications;

/// <summary>
/// A tenant's branding, tracked: the save checks <see cref="TenantBranding.Revision"/> against the value read here.
/// </summary>
internal sealed class TenantBrandingByTenantIdSpec : SingleResultSpecification<TenantBranding>
{
    /// <summary>Selects the branding row of <paramref name="tenantId"/>.</summary>
    public TenantBrandingByTenantIdSpec(long tenantId)
    {
        Query.Where(branding => branding.TenantId == tenantId);
    }
}
