using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.DataLists.List;

/// <summary>
/// Handler for <see cref="ListDistinctDataListLocalesQuery"/>.
/// </summary>
public sealed class ListDistinctDataListLocalesHandler(IDataListRepository repository, ITenantContext? tenant)
    : IQueryHandler<ListDistinctDataListLocalesQuery, Result<IReadOnlyList<string>>>
{
    /// <summary>Uses the repository's tenant scope when constructed directly.</summary>
    public ListDistinctDataListLocalesHandler(IDataListRepository repository) : this(repository, null) { }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<string>>> Handle(
        ListDistinctDataListLocalesQuery request,
        CancellationToken cancellationToken)
    {
        if (tenant?.TenantId <= 0)
        {
            return Result.Unauthorized("Tenant context is required.");
        }
        var locales = await repository.ListDistinctLocalesAsync(cancellationToken);
        return Result.Success(locales);
    }
}
