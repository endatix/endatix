using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.DataLists.ReplaceItems;

public sealed record ReplaceDataListItemsCommand : ICommand<Result<DataListDto>>
{
    public long DataListId { get; init; }
    public IReadOnlyCollection<ReplaceDataListItemInput> Items { get; init; }

    public ReplaceDataListItemsCommand(long dataListId, IReadOnlyCollection<ReplaceDataListItemInput> items)
    {
        Guard.Against.NegativeOrZero(dataListId, nameof(dataListId));
        Guard.Against.Null(items);
        DataListId = dataListId;
        Items = items;
    }
}

public sealed record ReplaceDataListItemInput(string Label, string Value);
