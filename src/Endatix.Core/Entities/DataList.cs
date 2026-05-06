using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Entities;

/// <summary>
/// Represents a data list entity.
/// </summary>
public class DataList : TenantEntity, IAggregateRoot
{
    public static class UniqueConstraints
    {
        public const string NamePerTenant = "IX_DataLists_TenantId_NormalizedName_Unique";
    }

    private readonly List<DataListItem> _items = [];

    private DataList() { }

    public DataList(long tenantId, string name, string? description = null, string? normalizedName = null)
        : base(tenantId)
    {
        Guard.Against.NullOrWhiteSpace(name);
        normalizedName ??= name;
        Guard.Against.NullOrWhiteSpace(normalizedName);
        Name = name;
        NormalizedName = normalizedName;
        Description = description;
    }

    /// <summary>
    /// The name of the data list.
    /// </summary>
    public string Name { get; private set; } = null!;

    /// <summary>
    /// The normalized name of the data list.
    /// </summary>
    public string NormalizedName { get; private set; } = null!;

    /// <summary>
    /// The description of the data list.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Whether the data list is active.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// The items of the data list.
    /// </summary>
    public IReadOnlyCollection<DataListItem> Items => _items.AsReadOnly();

    /// <summary>
    /// Updates the details of the data list.
    /// </summary>
    /// <param name="name">The name of the data list.</param>
    /// <param name="description">The description of the data list.</param>
    /// <param name="normalizedName">The normalized name of the data list.</param>
    public void UpdateDetails(string name, string? description, string normalizedName)
    {
        Guard.Against.NullOrWhiteSpace(name);
        Guard.Against.NullOrWhiteSpace(normalizedName);
        Name = name;
        NormalizedName = normalizedName;
        Description = description;
    }

    /// <summary>
    /// Adds a new item to the data list.
    /// </summary>
    /// <param name="label">The label of the item.</param>
    /// <param name="value">The value of the item.</param>
    /// <returns>The added item.</returns>
    public DataListItem AddItem(string label, string value)
    {
        DataListItem item = new(label, value);
        item.AttachToDataList(this);
        _items.Add(item);
        return item;
    }

    /// <summary>
    /// Replaces the items of the data list.
    /// </summary>
    /// <param name="items">The items to replace the current items with.</param>
    public void ReplaceItems(IEnumerable<(string Label, string Value)> items)
    {
        Guard.Against.Null(items);
        _items.Clear();
        foreach (var (Label, Value) in items)
        {
            AddItem(Label, Value);
        }
    }

    /// <summary>
    /// Sets the active state of the data list.
    /// </summary>
    /// <param name="isActive">Whether the data list is active.</param>
    public void SetActive(bool isActive) => IsActive = isActive;
}
