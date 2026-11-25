using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.DataLists.List;

/// <summary>
/// Handler for <see cref="ListDistinctDataListLocalesQuery"/>.
/// </summary>
public sealed class ListDistinctDataListLocalesHandler(IDataListRepository repository, ITenantContext tenant)
    : IQueryHandler<ListDistinctDataListLocalesQuery, Result<IReadOnlyList<string>>>
{
    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<string>>> Handle(
        ListDistinctDataListLocalesQuery request,
        CancellationToken cancellationToken)
    {
        if (tenant.TenantId <= 0)
        {
            return Result.Unauthorized("Tenant context is required.");
        }
        IReadOnlyList<string> locales = await repository.ListDistinctLocalesAsync(cancellationToken);
        return Result.Success(locales);
    }
}
