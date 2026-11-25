using Endatix.Core.Abstractions.Data;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Result;
using MediatR;

namespace Endatix.Core.UseCases.DataLists.Delete;

internal sealed class DataListDeletion(DataListRows lists, IUnitOfWork unitOfWork, IMediator mediator)
{
    internal async Task<Result<DataList>> DeleteAsync(DataList dataList, CancellationToken cancellationToken)
    {
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            dataList.Delete();
            await lists.UpdateAsync(dataList, cancellationToken);

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
