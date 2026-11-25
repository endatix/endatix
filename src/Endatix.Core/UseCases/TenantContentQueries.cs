using Ardalis.Specification;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.UseCases;

internal static class TenantContentQueries
{
    internal static Task<T?> FindAsync<T>(this IRepository<T> rows, long id, ITenantContext tenant,
        CancellationToken ct) where T : TenantEntity, IAggregateRoot =>
        rows.FirstOrDefaultAsync(new ById<T>(id, tenant.TenantId), ct);

    internal static void InTenant<T>(this Specification<T> spec, ITenantContext? tenant) where T : TenantEntity
    {
        if (tenant is not null)
        {
            var id = tenant.TenantId;
            spec.Query.Where(row => id > 0 && row.TenantId == id);
        }
    }

    internal static void OnLiveForm(this Specification<Submission> spec, ITenantContext tenant)
    {
        var id = tenant.TenantId;
        spec.Query.Where(s => id > 0 && s.TenantId == id && s.Form.TenantId == id && !s.Form.IsDeleted);
    }

    internal sealed class ById<T> : Specification<T> where T : TenantEntity
    {
        internal ById(long id, long tenantId) =>
            Query.Where(row => tenantId > 0 && row.TenantId == tenantId && row.Id == id);
    }
}
