using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Entities;

/// <summary>
/// Represents a data list entity.
/// </summary>
public class DataList : TenantEntity, IAggregateRoot
{
    private readonly List<DataListItem> _items = [];

    private DataList() { }

    public DataList(long tenantId, string name, string? description = null)
        : base(tenantId)
    {
        Guard.Against.NullOrWhiteSpace(name, nameof(name));
        Name = name;
        Description = description;
    }

    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;
    public IReadOnlyCollection<DataListItem> Items => _items.AsReadOnly();

    public void UpdateDetails(string name, string? description)
    {
        Guard.Against.NullOrWhiteSpace(name, nameof(name));
        Name = name;
        Description = description;
    }

    public DataListItem AddItem(string label, string value)
    {
        DataListItem item = new(Id, label, value);
        _items.Add(item);
        return item;
    }

    public void SetActive(bool isActive) => IsActive = isActive;
}
