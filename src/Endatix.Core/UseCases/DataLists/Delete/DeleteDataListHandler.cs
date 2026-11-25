using Endatix.Core.Abstractions;
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
[DirectCallOnly]
public sealed class DeleteDataListHandler : ICommandHandler<DeleteDataListCommand, Result<DataList>>
{
    private readonly DataListRows _lists;
    private readonly IDataListDependencyChecker _dependencyChecker;
    private readonly DataListDeletion _deletion;

    /// <summary>Uses the supplied repositories for direct calls.</summary>
    public DeleteDataListHandler(IRepository<DataList> repository,
        IDataListDependencyChecker dependencyChecker,
        IUnitOfWork unitOfWork,
        IMediator mediator)
    {
        _lists = new DataListRows(repository, ContentQueryScope.Repository);
        _dependencyChecker = dependencyChecker;
        _deletion = new DataListDeletion(_lists, unitOfWork, mediator);
    }

    private DeleteDataListHandler(DataListRows lists, IDataListDependencyChecker dependencyChecker, DataListDeletion deletion)
    {
        _lists = lists;
        _dependencyChecker = dependencyChecker;
        _deletion = deletion;
    }

    public async Task<Result<DataList>> Handle(DeleteDataListCommand request, CancellationToken cancellationToken)
    {
        var dataList = await _lists.WithItemsAsync(request.DataListId, cancellationToken);
        if (dataList is null)
        {
            return Result.NotFound("Data list not found.");
        }

        var isUsedOnForms = await _dependencyChecker.HasFormDependenciesAsync(request.DataListId, cancellationToken);
        if (isUsedOnForms)
        {
            ValidationError hasFormsValidationError = new()
            {
                Identifier = nameof(request.DataListId),
                ErrorMessage = "Data list is used by forms and cannot be deleted."
            };
            return Result.Invalid(hasFormsValidationError);
        }

        return await _deletion.DeleteAsync(dataList, cancellationToken);
    }

    internal sealed class Registered(DataListRows lists, IDataListDependencyChecker dependencyChecker, DataListDeletion deletion)
        : RegisteredContentHandler<DeleteDataListCommand, Result<DataList>>(
            new DeleteDataListHandler(lists, dependencyChecker, deletion));
}
