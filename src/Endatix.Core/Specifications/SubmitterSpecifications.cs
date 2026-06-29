using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Core.Specifications;

public static class SubmitterSpecifications
{
    public sealed class ByAppUserSpec : SingleResultSpecification<Submitter>
    {
        public ByAppUserSpec(long tenantId, string authProvider, long appUserId)
        {
            Query.Where(submitter =>
                submitter.TenantId == tenantId &&
                submitter.AuthProvider == authProvider &&
                submitter.AppUserId == appUserId);
        }
    }

    public sealed class ByExternalSubjectSpec : SingleResultSpecification<Submitter>
    {
        public ByExternalSubjectSpec(long tenantId, string authProvider, string externalSubjectId)
        {
            Query.Where(submitter =>
                submitter.TenantId == tenantId &&
                submitter.AuthProvider == authProvider &&
                submitter.ExternalSubjectId == externalSubjectId);
        }
    }
}
