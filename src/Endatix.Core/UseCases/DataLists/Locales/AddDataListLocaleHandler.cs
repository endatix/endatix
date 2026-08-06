using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using MediatR;

namespace Endatix.Core.UseCases.DataLists.Locales;

/// <summary>
/// Handler for <see cref="AddDataListLocaleCommand"/>.
/// </summary>
public sealed class AddDataListLocaleHandler(
    IRepository<DataList> repository,
    IMediator mediator)
    : ICommandHandler<AddDataListLocaleCommand, Result<DataListDto>>
{
    /// <inheritdoc />
    public async Task<Result<DataListDto>> Handle(AddDataListLocaleCommand request, CancellationToken cancellationToken)
    {
        var spec = new DataListsSpecifications.ByIdWithItemsSpec(request.DataListId);
        var dataList = await repository.SingleOrDefaultAsync(spec, cancellationToken);
        if (dataList is null)
        {
            return Result.NotFound("Data list not found.");
        }

        try
        {
            dataList.AddCulture(request.Locale);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            ValidationError error = new()
            {
                Identifier = nameof(request.Locale),
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
