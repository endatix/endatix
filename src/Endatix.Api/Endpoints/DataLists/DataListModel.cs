namespace Endatix.Api.Endpoints.DataLists;


/// <summary>
/// Data list model.
/// </summary>
public class DataListModel
{
    /// <summary>
    /// The id of the data list.
    /// </summary>
    public long Id { get; init; }
    /// <summary>
    /// The name of the data list.
    /// </summary>
    public string Name { get; init; } = string.Empty;
    /// <summary>
    /// The description of the data list.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Whether the data list is active.
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// The created at date of the data list.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// The modified at date of the data list.
    /// </summary>
    public DateTime? ModifiedAt { get; init; }

    /// <summary>
    /// The optional count of items in the data list. 
    /// This can be conditionally populated based on request parameters.
    /// </summary>
    public int ItemsCount { get; init; }
}

/// <summary>
/// Data list details model used for the GetById endpoint.
/// Inherits the base properties and includes the full Items collection.
/// </summary>
public sealed class DataListDetailsModel : DataListModel
{
    /// <summary>
    /// The full items of the data list.
    /// </summary>
    public IReadOnlyCollection<DataListItemModel> Items { get; init; } = [];
}

/// <summary>
/// Data list item model.
/// </summary>
public sealed class DataListItemModel
{
    /// <summary>
    /// The id of the data list item.
    /// </summary>  
    public long Id { get; init; }
    /// <summary>
    /// The label of the data list item.
    /// </summary>
    public string Label { get; init; } = string.Empty;
    /// <summary>
    /// The value of the data list item.
    /// </summary>
    public string Value { get; init; } = string.Empty;
}


/// <summary>
/// Data list public choice model.
/// </summary>
public sealed class DataListPublicChoiceModel
{
    /// <summary>
    /// The label of the data list public choice.
    /// </summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// The value of the data list public choice.
    /// </summary>
    public string Value { get; init; } = string.Empty;
}
