using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Specifications.Common;
using Endatix.Core.Specifications.Parameters;
using Endatix.Core.UseCases.FormDefinitions.List;

namespace Endatix.Core.Specifications;

public sealed class FormDefinitionsByFormIdSpec : Specification<FormDefinition>
{
    public FormDefinitionsByFormIdSpec(
        long formId,
        PagingParameters? pagingParams = null,
        FormDefinitionListSortBy sortBy = FormDefinitionListSortBy.CreatedAt,
        bool sortDescending = true,
        DateTime? createdFrom = null,
        DateTime? createdTo = null,
        DateTime? modifiedFrom = null,
        DateTime? modifiedTo = null)
    {
        Query
            .Where(fd => fd.FormId == formId)
            .WhereCreatedRange(createdFrom, createdTo)
            .WhereModifiedRange(modifiedFrom, modifiedTo);

        ApplyOrdering(Query, sortBy, sortDescending);

        Query
            .Paginate(pagingParams!)
            .AsNoTracking();
    }

    private static void ApplyOrdering(
        ISpecificationBuilder<FormDefinition> query,
        FormDefinitionListSortBy sortBy,
        bool sortDescending)
    {
        switch (sortBy)
        {
            case FormDefinitionListSortBy.ModifiedAt:
                query.OrderByWithIdTiebreaker(x => x.ModifiedAt, sortDescending);
                break;
            default:
                query.OrderByWithIdTiebreaker(x => x.CreatedAt, sortDescending);
                break;
        }
    }
}
