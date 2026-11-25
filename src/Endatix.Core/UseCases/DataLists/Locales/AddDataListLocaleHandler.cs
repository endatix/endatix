using Endatix.Core.Abstractions;
using Endatix.Core.Common.Translations;
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
[DirectCallOnly]
public sealed class AddDataListLocaleHandler : ICommandHandler<AddDataListLocaleCommand, Result<DataListDto>>
{
    private readonly DataListRows _lists;
    private readonly IMediator _mediator;

    /// <summary>Uses the supplied repositories for direct calls.</summary>
    public AddDataListLocaleHandler(IRepository<DataList> repository,
        IMediator mediator)
        : this(new DataListRows(repository, ContentQueryScope.Repository), mediator) { }

    private AddDataListLocaleHandler(DataListRows lists, IMediator mediator)
    {
        _lists = lists;
        _mediator = mediator;
    }

    /// <inheritdoc />
    public async Task<Result<DataListDto>> Handle(AddDataListLocaleCommand request, CancellationToken cancellationToken)
    {
        var dataList = await _lists.WithItemsAsync(request.DataListId, cancellationToken);
        if (dataList is null)
        {
            return Result.NotFound("Data list not found.");
        }

        try
        {
            dataList.AddCulture(CultureCode.Parse(request.Locale));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            ValidationError error = new()
            {
                Identifier = nameof(request.Locale),
                ErrorMessage = "Invalid locale."
            };
            return Result.Invalid(error);
        }

        await _lists.UpdateAsync(dataList, cancellationToken);
        await _mediator.Publish(
            new DataListUpdatedEvent(dataList, DataListUpdateReasons.LocalesUpdated),
            cancellationToken);

        return Result.Success(DataListDtoMapper.FromEntity(dataList));
    }

    internal sealed class Registered(DataListRows lists, IMediator mediator)
        : RegisteredContentHandler<AddDataListLocaleCommand, Result<DataListDto>>(
            new AddDataListLocaleHandler(lists, mediator));
}
