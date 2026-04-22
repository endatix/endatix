using Endatix.Core.Abstractions.Forms;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.DataLists.Delete;

public sealed class DeleteDataListHandler(
    IRepository<DataList> repository,
    IDataListDependencyChecker dependencyChecker)
    : ICommandHandler<DeleteDataListCommand, Result<DataList>>
{
    public async Task<Result<DataList>> Handle(DeleteDataListCommand request, CancellationToken cancellationToken)
    {
        DataListByIdSpec spec = new(request.DataListId);
        var dataList = await repository.SingleOrDefaultAsync(spec, cancellationToken);
        if (dataList is null)
        {
            return Result.NotFound("Data list not found.");
        }

        var hasDependencies = await dependencyChecker.HasFormDependenciesAsync(request.DataListId, cancellationToken);
        if (hasDependencies)
        {
            return Result.Invalid([new ValidationError
            {
                Identifier = nameof(request.DataListId),
                ErrorMessage = "Data list is used by forms and cannot be deleted."
            }]);
        }

        await repository.DeleteAsync(dataList, cancellationToken);
        return Result.Success(dataList);
    }
}
