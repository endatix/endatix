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
    /// The count of items of the data list.
    /// </summary>
    public int ItemsCount { get; init; } = 0;

    /// <summary>
    /// Real locale represented by the SurveyJS <c>default</c> label key.
    /// </summary>
    public string DefaultLocale { get; init; } = "en";

    /// <summary>
    /// Added cultures for this list (culture catalog).
    /// </summary>
    public IReadOnlyList<string> AvailableLocales { get; init; } = [];
}

/// <summary>
/// Data list details model used for the GetById endpoint.
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
    /// Localized labels including the <c>default</c> key.
    /// </summary>
    public IReadOnlyDictionary<string, string> Labels { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Resolved default label (compat / convenience).
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
