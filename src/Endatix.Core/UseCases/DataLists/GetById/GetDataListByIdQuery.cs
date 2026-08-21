using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.DataLists.GetById;

public sealed record GetDataListByIdQuery : IQuery<Result<DataListDto>>
{
    public long DataListId { get; init; }
    public bool IncludeItems { get; init; }

    public GetDataListByIdQuery(long dataListId, bool includeItems = true)
    {
        Guard.Against.NegativeOrZero(dataListId);

        DataListId = dataListId;
        IncludeItems = includeItems;
    }
}
