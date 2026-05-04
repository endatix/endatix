using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.DataLists.ReplaceItems;

/// <summary>
/// Command to replace items in a data list.
/// </summary>
public sealed record ReplaceDataListItemsCommand : ICommand<Result<DataListDto>>
{
    /// <summary>
    /// The ID of the data list to replace items for.
    /// </summary>
    public long DataListId { get; init; }

    /// <summary>
    /// The items to replace in the data list.
    /// </summary>
    public IReadOnlyCollection<ReplaceDataListItemInput> Items { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReplaceDataListItemsCommand"/> class.
    /// </summary>
    /// <param name="dataListId">The ID of the data list to replace items for.</param>
    /// <param name="items">The items to replace in the data list.</param>
    public ReplaceDataListItemsCommand(long dataListId, IReadOnlyCollection<ReplaceDataListItemInput> items)
    {
        Guard.Against.NegativeOrZero(dataListId);
        Guard.Against.Null(items);

        DataListId = dataListId;
        Items = items;
    }
}

/// <summary>
/// Input for replacing a single item in a data list.
/// </summary>
public sealed record ReplaceDataListItemInput(string Label, string Value);
