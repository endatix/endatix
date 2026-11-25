using System.Diagnostics.CodeAnalysis;
using Ardalis.Specification;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.DataLists;

/// <summary>Checks managed list visibility without fetching metadata or items.</summary>
[Experimental("ENDATIX0006")]
public sealed class ManagedDataListAccess(ITenantContext tenant, IRepository<DataList> lists, AccountStanding standing)
{
    /// <summary>The ordinary handler retains its read or write-time checks after admission.</summary>
    public async Task<Result?> RefusalAsync(long id, CancellationToken ct)
    {
        if (!standing.IsEnforced)
        {
            return null;
        }
        var visible = new DataListsSpecifications.ByIdsForTenantSpec([id], tenant.TenantId);
        visible.Query.TagWith("Managed data list visibility");
        return tenant.TenantId > 0 && await lists.AnyAsync(visible, ct)
            ? await standing.RefusalAsync(ct)
            : Result.NotFound("Data list not found.");
    }
}
