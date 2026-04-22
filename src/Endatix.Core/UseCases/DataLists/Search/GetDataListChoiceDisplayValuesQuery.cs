using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.DataLists.Search;

public sealed record GetDataListChoiceDisplayValuesQuery : IQuery<Result<IReadOnlyCollection<DataListChoiceDisplayValueDto>>>
{
    public long DataListId { get; init; }
    public IReadOnlyCollection<string> Values { get; init; }

    public GetDataListChoiceDisplayValuesQuery(long dataListId, IReadOnlyCollection<string> values)
    {
        Guard.Against.NegativeOrZero(dataListId);
        Guard.Against.Null(values);

        DataListId = dataListId;
        Values = values;
    }
}

public sealed record DataListChoiceDisplayValueDto(
    string Value,
    string Label);
