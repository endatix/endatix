using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Forms;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Forms;

namespace Endatix.Core.UseCases.DataLists.ListFormDependencies;

/// <summary>Lists the forms that depend on a data list.</summary>
[DirectCallOnly]
public sealed class ListFormDependenciesHandler : IQueryHandler<ListFormDependenciesQuery, Result<IReadOnlyCollection<FormDto>>>
{
    private readonly DataListRows _lists;
    private readonly IDataListDependencyChecker _dependencyChecker;

    /// <summary>Uses the supplied repositories for direct calls.</summary>
    public ListFormDependenciesHandler(IRepository<DataList> dataListRepository,
        IDataListDependencyChecker dependencyChecker)
        : this(new DataListRows(dataListRepository, ContentQueryScope.Repository), dependencyChecker) { }

    private ListFormDependenciesHandler(DataListRows lists, IDataListDependencyChecker dependencyChecker)
    {
        _lists = lists;
        _dependencyChecker = dependencyChecker;
    }

    public async Task<Result<IReadOnlyCollection<FormDto>>> Handle(ListFormDependenciesQuery request, CancellationToken cancellationToken)
    {
        var dataList = await _lists.WithItemsAsync(request.DataListId, cancellationToken);
        if (dataList is null)
        {
            return Result.NotFound("Data list not found.");
        }

        var forms = await _dependencyChecker.GetDependentFormsAsync(request.DataListId, cancellationToken);

        return Result.Success(forms);
    }

    internal sealed class Registered(DataListRows lists, IDataListDependencyChecker dependencyChecker)
        : RegisteredContentHandler<ListFormDependenciesQuery, Result<IReadOnlyCollection<FormDto>>>(
            new ListFormDependenciesHandler(lists, dependencyChecker));
}
