using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.DataLists.List;

/// <summary>
/// Handler for <see cref="ListDistinctDataListLocalesQuery"/>.
/// </summary>
[DirectCallOnly]
public sealed class ListDistinctDataListLocalesHandler(IDataListRepository repository, ITenantContext? tenant)
    : IQueryHandler<ListDistinctDataListLocalesQuery, Result<IReadOnlyList<string>>>, IExplicitTenantBinding
{
    bool IExplicitTenantBinding.IsTenantBound => tenant is not null;

    /// <summary>Uses the repository's tenant scope when constructed directly.</summary>
    public ListDistinctDataListLocalesHandler(IDataListRepository repository) : this(repository, null) { }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<string>>> Handle(
        ListDistinctDataListLocalesQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenant?.TenantId;
        if (tenantId <= 0)
        {
            return Result.Unauthorized("Tenant context is required.");
        }
        var locales = tenantId is long id && repository is ITenantLocaleCatalog catalog
            ? await catalog.ListForTenantAsync(id, cancellationToken)
            : await repository.ListDistinctLocalesAsync(cancellationToken);
        return Result.Success(locales);
    }

    internal sealed class Registered(IDataListRepository repository, ITenantContext tenant)
        : RegisteredContentHandler<ListDistinctDataListLocalesQuery, Result<IReadOnlyList<string>>>(
            new ListDistinctDataListLocalesHandler(repository, tenant));
}
