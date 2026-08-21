using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.DataLists.List;

/// <summary>
/// Handler for <see cref="ListDistinctDataListLocalesQuery"/>.
/// </summary>
public sealed class ListDistinctDataListLocalesHandler(IDataListRepository repository)
    : IQueryHandler<ListDistinctDataListLocalesQuery, Result<IReadOnlyList<string>>>
{
    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<string>>> Handle(
        ListDistinctDataListLocalesQuery request,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> locales = await repository.ListDistinctLocalesAsync(cancellationToken);
        return Result.Success(locales);
    }
}
