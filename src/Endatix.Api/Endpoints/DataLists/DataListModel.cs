namespace Endatix.Api.Endpoints.DataLists;


/// <summary>
/// Data list model.
/// </summary>
public sealed class DataListModel
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
    /// The items of the data list.
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
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
}
