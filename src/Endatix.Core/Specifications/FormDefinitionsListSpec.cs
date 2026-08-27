using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Specifications.Common;
using Endatix.Core.Specifications.Parameters;
using Endatix.Core.UseCases.FormDefinitions.List;

namespace Endatix.Core.Specifications;

/// <summary>
/// Paged list of form definitions for a form, with sort and calendar date bounds.
/// </summary>
public sealed class FormDefinitionsListSpec : Specification<FormDefinition>
{
    public FormDefinitionsListSpec(
        long formId,
        PagingParameters pagingParams,
        FormDefinitionListSortBy sortBy = FormDefinitionListSortBy.CreatedAt,
        bool sortDescending = true,
        UtcDateTimeRange created = default,
        UtcDateTimeRange modified = default)
    {
        Query
            .Where(fd => fd.FormId == formId)
            .WhereUtcRange(x => x.CreatedAt, created)
            .WhereUtcRange(x => x.ModifiedAt, modified);

        ApplyOrdering(Query, sortBy, sortDescending);

        Query
            .Paginate(pagingParams)
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
