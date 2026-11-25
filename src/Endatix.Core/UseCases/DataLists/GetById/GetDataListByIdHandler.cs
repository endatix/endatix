using Ardalis.Specification;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.DataLists.GetById;

/// <summary>Reads list metadata or items for the current tenant.</summary>
[DirectCallOnly]
public sealed class GetDataListByIdHandler : IQueryHandler<GetDataListByIdQuery, Result<DataListDto>>
{
    private readonly DataListRows _lists;

    /// <summary>Uses the supplied repositories for direct calls.</summary>
    public GetDataListByIdHandler(IRepository<DataList> repository)
        : this(new DataListRows(repository, ContentQueryScope.Repository)) { }

    private GetDataListByIdHandler(DataListRows lists)
    {
        _lists = lists;
    }

    /// <inheritdoc />
    public async Task<Result<DataListDto>> Handle(GetDataListByIdQuery request, CancellationToken cancellationToken)
    {
        var result = request.IncludeItems ? await ReadItemsAsync(request.DataListId, cancellationToken)
            : await _lists.MetadataAsync(request.DataListId, cancellationToken);
        return result is null ? Result.NotFound("Data list not found.") : Result.Success(result);
    }

    private async Task<DataListDto?> ReadItemsAsync(long id, CancellationToken ct)
    {
        var list = await _lists.ReadItemsAsync(id, ct);
        return list is null ? null : DataListDtoMapper.FromEntity(list);
    }

    internal sealed class Registered(DataListRows lists)
        : RegisteredContentHandler<GetDataListByIdQuery, Result<DataListDto>>(
            new GetDataListByIdHandler(lists));
}
