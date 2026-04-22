using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.DataLists.Search;

public sealed class GetDataListChoiceDisplayValuesHandler(IRepository<DataList> repository)
    : IQueryHandler<GetDataListChoiceDisplayValuesQuery, Result<IReadOnlyCollection<DataListChoiceDisplayValueDto>>>
{
    public async Task<Result<IReadOnlyCollection<DataListChoiceDisplayValueDto>>> Handle(
        GetDataListChoiceDisplayValuesQuery request,
        CancellationToken cancellationToken)
    {
        var requestedValues = request.Values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var spec = new DataListsSpecifications.ByIdWithItemsByValuesSpec(request.DataListId, requestedValues);
        var dataList = await repository.SingleOrDefaultAsync(spec, cancellationToken);
        if (dataList is null)
        {
            return Result.NotFound("Data list not found.");
        }

        IReadOnlyCollection<DataListChoiceDisplayValueDto> items = dataList.Items
            .OrderBy(item => item.Label)
            .ThenBy(item => item.Value)
            .Select(item => new DataListChoiceDisplayValueDto(item.Value, item.Label))
            .ToArray();

        return Result.Success(items);
    }
}
