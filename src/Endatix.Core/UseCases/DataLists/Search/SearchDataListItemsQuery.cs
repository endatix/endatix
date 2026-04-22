using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.DataLists.Search;

public sealed record SearchDataListItemsQuery : IQuery<Result<SearchDataListItemsDto>>
{
    /// <summary>
    /// The maximum number of items to take.
    /// </summary>
    public const int MAX_TAKE = 100;

    public long DataListId { get; init; }
    public string? Query { get; init; }
    public int Skip { get; init; }
    public int Take { get; init; }

    public SearchDataListItemsQuery(long dataListId, string? query, int skip, int take)
    {
        Guard.Against.NegativeOrZero(dataListId);
        Guard.Against.Negative(skip);
        Guard.Against.NegativeOrZero(take);

        DataListId = dataListId;
        Query = query?.Trim();
        Skip = skip;
        Take = Math.Min(take, MAX_TAKE);
    }
}

public sealed record SearchDataListItemsDto(
    long DataListId,
    int Total,
    int Skip,
    int Take,
    IReadOnlyCollection<DataListItemDto> Items);
