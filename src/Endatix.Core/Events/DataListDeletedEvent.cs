using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Events;

/// <summary>
/// Event dispatched when a data list is deleted.
/// </summary>
public sealed class DataListDeletedEvent(DataList dataList) : DomainEventBase
{
    public DataList DataList { get; init; } = dataList;
}