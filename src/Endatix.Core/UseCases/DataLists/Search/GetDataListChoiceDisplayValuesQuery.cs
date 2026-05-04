using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.DataLists.Search;

/// <summary>
/// Query to get data list choice display values.
/// </summary>
public sealed record GetDataListChoiceDisplayValuesQuery : IQuery<Result<IReadOnlyCollection<DataListChoiceDisplayValueDto>>>
{
    /// <summary>
    /// The ID of the data list.
    /// </summary>
    public long DataListId { get; init; }

    /// <summary>
    /// The values to get display values for.
    /// </summary>
    public IReadOnlyCollection<string> Values { get; init; }

    public GetDataListChoiceDisplayValuesQuery(long dataListId, IReadOnlyCollection<string> values)
    {
        Guard.Against.NegativeOrZero(dataListId);
        Guard.Against.Null(values);

        DataListId = dataListId;
        Values = values;
    }
}
