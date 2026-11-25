using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.DataLists;

internal sealed class DataListRows(IRepository<DataList> rows, ContentQueryScope scope)
{
    internal Task<DataList?> FindAsync(long id, CancellationToken ct) => scope.FindAsync(rows, id, ct);

    internal Task<DataList?> WithItemsAsync(long id, CancellationToken ct) =>
        rows.SingleOrDefaultAsync(WithItems(id), ct);

    internal Task<DataList?> ReadItemsAsync(long id, CancellationToken ct) =>
        rows.FirstOrDefaultAsync(WithItems(id), ct);

    private DataListsSpecifications.ByIdWithItemsSpec WithItems(long id)
    {
        var spec = new DataListsSpecifications.ByIdWithItemsSpec(id);
        scope.Apply(spec);
        return spec;
    }

    internal Task<DataList?> NamedAsync(string name, CancellationToken ct)
    {
        var spec = new DataListsSpecifications.ByNormalizedNameSpec(name);
        scope.Apply(spec);
        return rows.SingleOrDefaultAsync(spec, ct);
    }

    internal Task<DataListDto?> MetadataAsync(long id, CancellationToken ct)
    {
        var spec = new DataListsSpecifications.ByIdWithoutItemsToDtoSpec(id);
        scope.Apply(spec);
        return rows.FirstOrDefaultAsync(spec, ct);
    }

    internal Task UpdateAsync(DataList list, CancellationToken ct) => rows.UpdateAsync(list, ct);
}
