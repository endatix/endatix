using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Specifications.Common;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Specifications;

/// <summary>
/// Specifications for working with CustomQuestion entities
/// </summary>
public static class CustomQuestionSpecifications
{
    /// <summary>
    /// Specification to get a custom question by name (case-insensitive)
    /// </summary>
    public sealed class ByName : Specification<CustomQuestion>, ISingleResultSpecification<CustomQuestion>
    {
        public ByName(string name)
        {
            Query.Where(q => q.Name.ToLower() == name.ToLower());
        }
    }

    public class ByTenantId : Specification<CustomQuestion>
    {
        public ByTenantId(long tenantId)
        {
            Query.Where(q => q.TenantId == tenantId);
        }
    }
}
