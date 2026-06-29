using Endatix.Core.Abstractions.Data;
using Endatix.Core.Abstractions.Forms;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using MediatR;

namespace Endatix.Core.UseCases.DataLists.Delete;


/// <summary>
/// Handler to delete a data list.
/// </summary>
public sealed class DeleteDataListHandler(
    IRepository<DataList> repository,
    IDataListDependencyChecker dependencyChecker,
    IUnitOfWork unitOfWork,
    IMediator mediator)
    : ICommandHandler<DeleteDataListCommand, Result<DataList>>
{
    public async Task<Result<DataList>> Handle(DeleteDataListCommand request, CancellationToken cancellationToken)
    {
        var spec = new DataListsSpecifications.ByIdWithItemsSpec(request.DataListId);
        var dataList = await repository.SingleOrDefaultAsync(spec, cancellationToken);
        if (dataList is null)
        {
            return Result.NotFound("Data list not found.");
        }

        var isUsedOnForms = await dependencyChecker.HasFormDependenciesAsync(request.DataListId, cancellationToken);
        if (isUsedOnForms)
        {
            ValidationError hasFormsValidationError = new()
            {
                Identifier = nameof(request.DataListId),
                ErrorMessage = "Data list is used by forms and cannot be deleted."
            };
            return Result.Invalid(hasFormsValidationError);
        }

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            dataList.Delete();
            await repository.UpdateAsync(dataList, cancellationToken);

            await mediator.Publish(new DataListDeletedEvent(dataList), cancellationToken);

            await unitOfWork.CommitTransactionAsync(cancellationToken);

            return Result.Success(dataList);
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
