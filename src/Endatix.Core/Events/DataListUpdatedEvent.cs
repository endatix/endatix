using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Events;

/// <summary>
/// Indicates what changed in a data list update operation.
/// </summary>
[Flags]
public enum DataListUpdateReasons
{
    None = 0,
    MetadataUpdated = 1 << 0,
    ItemsUpdated = 1 << 1,
    ItemsReplaced = 1 << 2
}

/// <summary>
/// Event dispatched when a data list is updated.
/// </summary>
public sealed class DataListUpdatedEvent(DataList dataList, DataListUpdateReasons reason = DataListUpdateReasons.None) : DomainEventBase
{
    public DataList DataList { get; init; } = dataList;
    public DataListUpdateReasons Reason { get; init; } = reason;
}