using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.DataLists;

/// <summary>Verifies the managed list target before a body is bound or a search repository is called.</summary>
public sealed class DataListVisibility(ITenantContext tenant, IRepository<DataList> lists)
{
    /// <summary>Whether the list belongs to the authenticated tenant.</summary>
    public Task<bool> ContainsAsync(long listId, CancellationToken ct)
    {
        var id = tenant.TenantId;
        return id > 0 ? lists.AnyAsync(new DataListsSpecifications.ByIdsForTenantSpec([listId], id), ct) : Task.FromResult(false);
    }
}
