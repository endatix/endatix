using Endatix.Core.Abstractions.Forms;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Forms;

namespace Endatix.Core.UseCases.DataLists.ListFormDependencies;

public sealed class ListFormDependenciesHandler(
    IRepository<DataList> dataListRepository,
    IDataListDependencyChecker dependencyChecker)
    : IQueryHandler<ListFormDependenciesQuery, Result<IReadOnlyCollection<FormDto>>>
{
    public async Task<Result<IReadOnlyCollection<FormDto>>> Handle(ListFormDependenciesQuery request, CancellationToken cancellationToken)
    {
        var spec = new DataListsSpecifications.ByIdWithItemsSpec(request.DataListId);
        var dataList = await dataListRepository.SingleOrDefaultAsync(spec, cancellationToken);
        if (dataList is null)
        {
            return Result.NotFound("Data list not found.");
        }

        var forms = await dependencyChecker.GetDependentFormsAsync(request.DataListId, cancellationToken);

        return Result.Success(forms);
    }
}
