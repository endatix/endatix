using Ardalis.GuardClauses;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.DataLists.Delete;

public sealed record DeleteDataListCommand : ICommand<Result<DataList>>
{
    public long DataListId { get; init; }

    public DeleteDataListCommand(long dataListId)
    {
        Guard.Against.NegativeOrZero(dataListId, nameof(dataListId));
        DataListId = dataListId;
    }
}
