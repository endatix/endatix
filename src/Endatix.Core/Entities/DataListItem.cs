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
    /// <param name="dataListId">The data list ID.</param>
    /// <param name="label">The label.</param>
    /// <param name="value">The value.</param>
    public DataListItem(long dataListId, string label, string value)
    {
        Guard.Against.NegativeOrZero(dataListId, nameof(dataListId));
        Guard.Against.NullOrWhiteSpace(label, nameof(label));
        Guard.Against.NullOrWhiteSpace(value, nameof(value));

        DataListId = dataListId;
        Label = label;
        Value = value;
    }

    public long DataListId { get; private set; }
    public DataList DataList { get; private set; } = null!;
    public string Label { get; private set; } = null!;
    public string Value { get; private set; } = null!;

    public void Update(string label, string value)
    {
        Guard.Against.NullOrWhiteSpace(label, nameof(label));
        Guard.Against.NullOrWhiteSpace(value, nameof(value));

        Label = label;
        Value = value;
    }
}
