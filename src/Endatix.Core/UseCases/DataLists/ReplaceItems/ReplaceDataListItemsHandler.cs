using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using MediatR;

namespace Endatix.Core.UseCases.DataLists.ReplaceItems;

public sealed class ReplaceDataListItemsHandler(
    IRepository<DataList> repository,
    IMediator mediator
    )
    : ICommandHandler<ReplaceDataListItemsCommand, Result<DataListDto>>
{
    public async Task<Result<DataListDto>> Handle(ReplaceDataListItemsCommand request, CancellationToken cancellationToken)
    {
        DataListByIdSpec spec = new(request.DataListId);
        var dataList = await repository.SingleOrDefaultAsync(spec, cancellationToken);
        if (dataList is null)
        {
            return Result.NotFound("Data list not found.");
        }

        List<ValidationError> errors = [];
        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Label))
            {
                errors.Add(new ValidationError { Identifier = nameof(item.Label), ErrorMessage = "Label is required." });
            }
            if (string.IsNullOrWhiteSpace(item.Value))
            {
                errors.Add(new ValidationError { Identifier = nameof(item.Value), ErrorMessage = "Value is required." });
            }
        }

        if (errors.Count > 0)
        {
            return Result.Invalid(errors);
        }

        dataList.ReplaceItems(request.Items.Select(x => (x.Label.Trim(), x.Value.Trim())));

        await repository.UpdateAsync(dataList, cancellationToken);

        await mediator.Publish(
            new DataListUpdatedEvent(dataList, DataListUpdateReason.ItemsReplaced),
            cancellationToken);

        return Result.Success(new DataListDto(
            dataList.Id,
            dataList.Name,
            dataList.Description,
            dataList.IsActive,
            dataList.Items.Select(x => new DataListItemDto(x.Id, x.Label, x.Value)).ToArray()));
    }
}
