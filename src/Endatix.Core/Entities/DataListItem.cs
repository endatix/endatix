using Ardalis.GuardClauses;

namespace Endatix.Core.Entities;

/// <summary>
/// Represents a data list item entity.
/// </summary>
public class DataListItem : BaseEntity
{
    /// For EF Core.
    private DataListItem() { }

    /// <summary>
    /// Creates a new data list item.
    /// </summary>
    /// <param name="label">The label.</param>
    /// <param name="value">The value.</param>
    public DataListItem(string label, string value)
    {
        Guard.Against.NullOrWhiteSpace(label);
        Guard.Against.NullOrWhiteSpace(value);

        Label = label;
        Value = value;
    }

    /// <summary>
    /// The data list ID.
    /// </summary>
    public long DataListId { get; private set; }

    /// <summary>
    /// The data list.
    /// </summary>
    public DataList DataList { get; private set; } = null!;

    /// <summary>
    /// The label of the data list item.
    /// </summary>
    public string Label { get; private set; } = null!;

    /// <summary>
    /// The value of the data list item.
    /// </summary>
    public string Value { get; private set; } = null!;

    /// <summary>
    /// Attaches the data list item to a data list.
    /// </summary>
    /// <param name="dataList">The data list.</param>
    internal void AttachToDataList(DataList dataList)
    {
        Guard.Against.Null(dataList);

        if (DataList is not null)
        {
            if (ReferenceEquals(DataList, dataList) || (DataListId > 0 && DataListId == dataList.Id))
            {
                return;
            }

            throw new InvalidOperationException("DataListItem is already attached to a different DataList.");
        }

        DataList = dataList;
        DataListId = dataList.Id;
    }

    /// <summary>
    /// Updates the data list item.
    /// </summary>
    /// <param name="label">The label.</param>
    /// <param name="value">The value.</param>
    public void Update(string label, string value)
    {
        Guard.Against.NullOrWhiteSpace(label);
        Guard.Against.NullOrWhiteSpace(value);

        Label = label;
        Value = value;
    }
}
