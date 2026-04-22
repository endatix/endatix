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
        Guard.Against.NullOrWhiteSpace(label, nameof(label));
        Guard.Against.NullOrWhiteSpace(value, nameof(value));

        Label = label;
        Value = value;
    }

    public long DataListId { get; private set; }
    public DataList DataList { get; private set; } = null!;
    public string Label { get; private set; } = null!;
    public string Value { get; private set; } = null!;

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

    public void Update(string label, string value)
    {
        Guard.Against.NullOrWhiteSpace(label, nameof(label));
        Guard.Against.NullOrWhiteSpace(value, nameof(value));

        Label = label;
        Value = value;
    }
}
