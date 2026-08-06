using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using MediatR;

namespace Endatix.Core.UseCases.DataLists.Locales;

/// <summary>
/// Handler for <see cref="SetDataListDefaultLocaleCommand"/>.
/// </summary>
public sealed class SetDataListDefaultLocaleHandler(
    IRepository<DataList> repository,
    IMediator mediator)
    : ICommandHandler<SetDataListDefaultLocaleCommand, Result<DataListDto>>
{
    /// <inheritdoc />
    public async Task<Result<DataListDto>> Handle(SetDataListDefaultLocaleCommand request, CancellationToken cancellationToken)
    {
        var spec = new DataListsSpecifications.ByIdWithItemsSpec(request.DataListId);
        var dataList = await repository.SingleOrDefaultAsync(spec, cancellationToken);
        if (dataList is null)
        {
            return Result.NotFound("Data list not found.");
        }

        try
        {
            dataList.SetDefaultCulture(request.DefaultLocale);
        }
        catch (ArgumentException ex)
        {
            ValidationError error = new()
            {
                Identifier = nameof(request.DefaultLocale),
                ErrorMessage = ex.Message
            };
            return Result.Invalid(error);
        }

        await repository.UpdateAsync(dataList, cancellationToken);
        await mediator.Publish(
            new DataListUpdatedEvent(dataList, DataListUpdateReasons.LocalesUpdated),
            cancellationToken);

        return Result.Success(DataListDtoMapper.FromEntity(dataList));
    }
}
