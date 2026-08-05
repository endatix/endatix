using Ardalis.GuardClauses;
using Endatix.Core.Entities;
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
/// Prefer <see cref="Labels"/>; <see cref="Label"/> is legacy shorthand for <c>{ "default": label }</c>.
/// </summary>
public sealed record ReplaceDataListItemInput(
    string Value,
    IReadOnlyDictionary<string, string>? Labels = null,
    string? Label = null)
{
    /// <summary>
    /// Resolves the labels dictionary from either <see cref="Labels"/> or legacy <see cref="Label"/>.
    /// </summary>
    public IReadOnlyDictionary<string, string>? ResolveLabels()
    {
        if (Labels is {Count: > 0 })
        {
            return Labels;
        }

        if (!string.IsNullOrWhiteSpace(Label))
        {
            return new(StringComparer.Ordinal)
            {
                [DataListItem.DefaultLabelKey] = Label
            };
        }

        return null;
    }
}
